using Bbob.Plugin;
using System.Text.Json;

namespace bbob_plugin_prerender;

[PluginCondition("*", PluginOrder = PluginOrder.BeforeMe)]
public class Class1 : IPlugin
{
    ThemeInfo themeInfo;
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
    }
    public class ThemeInfo
    {
        public string? articleBaseUrlShort { get; set; }
        public class Prerender
        {
            public bool enable { get; set; } = false;
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

                GenerateStatic generateStatic = new GenerateStatic(PluginHelper.DistributionDirectory);
                List<Task> tasks = new List<Task>();
                PluginHelper.getRegisteredObject<List<dynamic>>("links", out List<dynamic>? links);
                if (links == null) return;
                int generateCount = 0;
                int cacheCount = 0;
                foreach (var link in links)
                {
                    string address = link.address;
                    string real = $"{url}{themeInfo.articleBaseUrlShort}{address}";
                    bool modified = isModifed(link.address, link.contentHash);
                    if (modified) generateCount++;
                    else cacheCount++;
                    tasks.Add(generateStatic.GenerateHtml(real, modified));
                }
                foreach (var otherUrl in themeInfo.prerender.otherUrls)
                {
                    tasks.Add(generateStatic.GenerateHtml($"{url}{otherUrl}", true, false));
                }
                Task.WaitAll(tasks.ToArray());
                PluginHelper.printConsole($"Done! {generateCount} Generated, {cacheCount} Use cache.");
                generateStatic.Stop();
            };
        }
        return null;
    }

    public class PrerenderStatus
    {
        public Dictionary<string, string> lastModified { get; set; } = new();
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
