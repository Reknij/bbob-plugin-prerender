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
    PrerenderStatusManager PSM = new PrerenderStatusManager();
    public Class1()
    {
        themeInfo = PluginHelper.getThemeInfo<ThemeInfo>() ?? new ThemeInfo();
        if (themeInfo.articleBaseUrl != null)
        {
            if (themeInfo.articleBaseUrl.Contains('?'))
            {
                themeInfo.prerender.enable = false;
                PluginHelper.printConsole("articleBaseUrlShort is query url! Prerender is not support query.");
            }
        }
        else
        {
            PluginHelper.printConsole("Theme is not support prerender because it don't have `articleBaseUrl` data.");
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
        bool needClear = false;
        if (!themeInfo.prerender.fixedVersion)
        {
            PluginHelper.printConsole($"Theme has updated. {WCAC}");
            needClear = true;

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
        if (PSM.Main.pluginsHash != PluginHelper.HashPluginsLoaded)
        {
            if (!needClear)
            {
                needClear = true;
                PluginHelper.printConsole($"Has plugin modified. {WCAC}");
            }
            PSM.Main.pluginsHash = PluginHelper.HashPluginsLoaded;
            PSM.Save();
        }
        if (needClear) ClearCache();
    }

    private void ClearCache()
    {
        string pp = Path.Combine(PluginHelper.CurrentDirectory, "prerender");
        if (Directory.Exists(pp))
        {
            var dirs = Directory.GetDirectories(pp);
            var files = Directory.GetFiles(pp);
            foreach (var dir in dirs)
            {
                Directory.Delete(dir, true);
            }
            foreach (var file in files)
            {
                if (file == PrerenderStatusManager.FilePath) continue;
                File.Delete(file);
            }
        }
    }
    private MyConfig GetMyConfig()
    {
        PluginHelper.getPluginJsonConfig<MyConfig>(out MyConfig? tar);
        return tar ?? new MyConfig();
    }
    public class ThemeInfo
    {
        public string? articleBaseUrl { get; set; }
        public class Prerender
        {
            public bool enable { get; set; } = false;
            public bool fixedVersion { get; set; } = false;
            public string[] otherUrls { get; set; } = Array.Empty<string>();
        }
        public Prerender prerender { get; set; } = new Prerender();
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
            if (themeInfo.articleBaseUrl == null) return null;

            PluginHelper.registerMeta("prerenderNow", false);

            return () =>
            {
                PluginHelper.printConsole("Initialize generate static page server...");
                string url = $"http://localhost:{MyHelper.GetAvailablePort(1024)}";
                Server server = new Server(url, PluginHelper.ConfigBbob.baseUrl, PluginHelper.DistributionDirectory);
                server.Start();

                GenerateStatic generateStatic = new GenerateStatic(PluginHelper.DistributionDirectory, myConfig);
                PluginHelper.Events.ProgramExited += (sender, e) => generateStatic.Dispose();
                List<Task> tasks = new List<Task>();
                PluginHelper.getRegisteredObject<List<dynamic>>("links", out List<dynamic>? links);
                if (links == null) return;
                PluginHelper.printConsole("Generate static pages...");
                bool modified = false;
                foreach (var link in links)
                {
                    string real = $"{url}{themeInfo.articleBaseUrl}{link.id}";
                    bool regenerate = isModifed(link.id, link.contentHash);
                    modified = regenerate ? true : modified;
                    tasks.Add(generateStatic.GenerateHtml(real, regenerate));
                }
                foreach (var otherUrl in themeInfo.prerender.otherUrls)
                {
                    tasks.Add(generateStatic.GenerateHtml($"{url}{otherUrl}", true, false));
                }
                Task.WaitAll(tasks.ToArray());
                generateStatic.Stop();

                PluginHelper.printConsole($"Done! {generateStatic.RegenerateCount} Generated, {generateStatic.UseCacheCount} Use cache.");
                if (modified) PSM.Save();
                PSM.Close();
            };
        }
        return null;
    }

    /// <summary>
    /// True if modified. If modified will update to prerenderStatus.json.
    /// </summary>
    /// <param name="address"></param>
    /// <param name="contentHash"></param>
    /// <returns></returns>
    private bool isModifed(string identifier, string hash)
    {
        if (PSM.Main != null)
        {
            if (!PSM.Main.lastModified.ContainsKey(identifier))
            {
                PSM.Main.lastModified.Add(identifier, hash);
                return true;
            }
            else if (PSM.Main.lastModified[identifier] != hash)
            {
                PSM.Main.lastModified[identifier] = hash;
                return true;
            }
        }
        return false;
    }
}
