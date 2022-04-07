using Bbob.Plugin;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace bbob_plugin_prerender;

[PluginCondition("*", PluginOrder = PluginOrder.BeforeMe)]
public class Class1 : IPlugin
{
    ThemeInfo themeInfo;
    MyConfig myConfig;
    public Class1()
    {
        themeInfo = PluginHelper.getThemeInfo<ThemeInfo>() ?? new ThemeInfo();
        if (themeInfo.articleBaseUrlShort != null)
        {
            if (themeInfo.articleBaseUrlShort.Contains('?'))
            {
                themeInfo.prerender.enable = false;
                PluginHelper.printConsole("articleBaseUrlShort is query url! Prerender is not support query.");
            }
        }
        else
        {
            PluginHelper.printConsole("articleBaseUrlShort from theme info is not support!");
        }
        PluginHelper.getPluginJsonConfig<BWAJ>("BuildWebArticleJson", out BWAJ? bwaj);
        bool isShortAddress = bwaj?.shortAddress ?? false;
        if (!isShortAddress)
        {
            PluginHelper.printConsole("BuildWebArticleJson config 'shortAddress' is disable. Prerender only support short address.");
            themeInfo.prerender.enable = false;
        }
        if (themeInfo.prerender.enable) CheckGitignore();
        checkCache();

        myConfig = GetMyConfig();
        PluginHelper.registerCustomCommand("clear", (args) =>
        {
            string pp = Path.Combine(PluginHelper.CurrentDirectory, "prerender");
            if (Directory.Exists(pp))
            {
                ClearCache();
                PluginHelper.printConsole("Clear cache success!");
            }
            else
            {
                PluginHelper.printConsole("No found cache.");
            }
        });
    }

    private void checkCache()
    {
        string pp = Path.Combine(PluginHelper.CurrentDirectory, "prerender");
        if (!Directory.Exists(pp)) return;
        
        const string WCAC = "Will clear all cache.";
        if (!themeInfo.prerender.fixedVersion)
        {
            PluginHelper.printConsole($"Theme has updated. {WCAC}");
            ClearCache();

            string file = Path.Combine(PluginHelper.ThemePath, "theme.json");
            if (File.Exists(file))
            {
                using (FileStream fs = File.Open(file, FileMode.Open))
                {
                    var root = JsonNode.Parse(fs);
                    if (root != null)
                    {
                        JsonNode? prerender = root["prerender"];
                        if (prerender != null)
                        {
                            prerender.AsObject().Remove("fixedVersion");
                            prerender.AsObject().Add("fixedVersion", true);
                            fs.SetLength(0);
                            fs.Position = 0;
                            JsonSerializer.Serialize(fs, root, new JsonSerializerOptions() { WriteIndented = true });
                        }
                    }
                }
            }
        }
        else
        {
            string ps = Path.Combine(pp, "prerenderStatus.json");
            PrerenderStatus? prerenderStatus = null;
            if (File.Exists(ps) && (prerenderStatus = JsonSerializer.Deserialize<PrerenderStatus>(File.ReadAllText(ps))) != null)
            {
                if (prerenderStatus.pluginsHash != PluginHelper.PluginsLoaded.GetHashCode().ToString())
                {
                    ClearCache();
                    PluginHelper.printConsole($"Has plugin modified. {WCAC}");
                }
            }
        }
    }

    private void ClearCache()
    {
        string pp = Path.Combine(PluginHelper.CurrentDirectory, "prerender");
        if (Directory.Exists(pp)) Directory.Delete(pp, true);
    }
    private MyConfig GetMyConfig()
    {
        PluginHelper.getPluginJsonConfig<MyConfig>(out MyConfig? tar);
        return tar ?? new MyConfig();
    }
    public class ThemeInfo
    {
        public string? articleBaseUrlShort { get; set; }
        public class Prerender
        {
            public bool enable { get; set; } = false;
            public bool fixedVersion { get; set; } = false;
            public string[] otherUrls { get; set; } = Array.Empty<string>();
        }
        public Prerender prerender { get; set; } = new Prerender();
    }
    public class BWAJ
    {
        public bool shortAddress { get; set; } = false;
    }

    public void CheckGitignore()
    {
        string ggFile = Path.Combine(PluginHelper.CurrentDirectory, ".gitignore");
        string[] ignores =
        {
            ".local-chromium/",
            "prerender/"
        };
        if (File.Exists(ggFile))
        {
            HashSet<string> gitignore = new HashSet<string>(File.ReadAllLines(ggFile));
            foreach (string ignore in ignores)
            {
                gitignore.Add(ignore);
            }
            File.WriteAllLines(ggFile, gitignore);
        }
    }

    public void InitCommand()
    {
        if (!PluginHelper.isPluginJsonConfigExists())
        {
            myConfig = new MyConfig();
            PluginHelper.savePluginJsonConfig<MyConfig>(myConfig);
            PluginHelper.printConsole("Initialize config done.");
        }
        else
        {
            PluginHelper.printConsole("Already exists config.");
        }
    }

    public Action? CommandComplete(Commands cmd)
    {
        if (cmd == Commands.GenerateCommand)
        {
            if (!themeInfo.prerender.enable || themeInfo.articleBaseUrlShort == null) return null;

            PluginHelper.registerMeta("prerenderNow", false);

            return () =>
            {
                PluginHelper.printConsole("Generate static pages...");
                string url = $"http://localhost:{MyHelper.GetAvailablePort(1024)}";
                Server server = new Server(url, PluginHelper.ConfigBbob.baseUrl, PluginHelper.DistributionDirectory);
                server.Start();

                GenerateStatic generateStatic = new GenerateStatic(PluginHelper.DistributionDirectory, myConfig);
                List<Task> tasks = new List<Task>();
                PluginHelper.getRegisteredObject<List<dynamic>>("links", out List<dynamic>? links);
                if (links == null) return;
                foreach (var link in links)
                {
                    string address = link.address;
                    string real = $"{url}{themeInfo.articleBaseUrlShort}{address}";
                    tasks.Add(generateStatic.GenerateHtml(real, isModifed(link.address, link.contentHash)));
                }
                foreach (var otherUrl in themeInfo.prerender.otherUrls)
                {
                    tasks.Add(generateStatic.GenerateHtml($"{url}{otherUrl}", true, false));
                }
                Task.WaitAll(tasks.ToArray());
                PluginHelper.printConsole($"Done! {generateStatic.RegenerateCount} Generated, {generateStatic.UseCacheCount} Use cache.");
                generateStatic.Stop();
            };
        }
        return null;
    }

    public class PrerenderStatus
    {
        public Dictionary<string, string> lastModified { get; set; } = new();
        public string pluginsHash { get; set; } = "";
    }

    /// <summary>
    /// True if modified. If modified will update to prerenderStatus.json.
    /// </summary>
    /// <param name="address"></param>
    /// <param name="contentHash"></param>
    /// <returns></returns>
    private bool isModifed(string identifier, string hash)
    {
        string psPath = Path.Combine(MyHelper.prerenderDirectory, "prerenderStatus.json");
        if (!File.Exists(psPath))
        {
            Directory.CreateDirectory(MyHelper.prerenderDirectory);
            using FileStream fs = File.OpenWrite(psPath);
            var p = new PrerenderStatus();
            p.lastModified.Add(identifier, hash);
            JsonSerializer.Serialize(fs, p);
            return true;
        }
        using (FileStream fs = File.Open(psPath, FileMode.Open))
        {
            PrerenderStatus prerenderStatus = JsonSerializer.Deserialize<PrerenderStatus>(fs) ?? new PrerenderStatus();
            bool modified = false;
            if (!prerenderStatus.lastModified.ContainsKey(identifier))
            {
                prerenderStatus.lastModified.Add(identifier, hash);
                modified = true;
            }
            else if (prerenderStatus.lastModified[identifier] != hash)
            {
                prerenderStatus.lastModified[identifier] = hash;
                modified = true;
            }
            if (modified)
            {
                fs.Flush();
                fs.Position = 0;
                JsonSerializer.Serialize(fs, prerenderStatus);
                return true;
            }
        }
        return false;
    }
}
