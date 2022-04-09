using System.Text.Json;
using Bbob.Plugin;

namespace bbob_plugin_prerender;

public class PrerenderStatusManager
{
    public static readonly string FilePath = Path.Combine(MyHelper.prerenderDirectory, "prerenderStatus.json");
    public PrerenderStatus Main { get; private set; }
    private FileStream RWStream;
    public bool Closed { get; private set; }
    public PrerenderStatusManager()
    {
        if (!File.Exists(FilePath))
        {
            Directory.CreateDirectory(MyHelper.prerenderDirectory);
            RWStream = File.Open(FilePath, FileMode.Create, FileAccess.ReadWrite);
            JsonSerializer.Serialize(RWStream, new PrerenderStatus()
            {
                pluginsHash = PluginHelper.HashPluginsLoaded
            });
            Main = new PrerenderStatus();
        }
        else
        {
            RWStream = File.Open(FilePath, FileMode.Open, FileAccess.ReadWrite);
            PrerenderStatus? val = null;
            try
            {
                val = JsonSerializer.Deserialize<PrerenderStatus>(RWStream);
            }
            catch (System.Exception)
            {
            }
            Main = val ?? new PrerenderStatus();
        }
    }

    public void Save()
    {
        if (!Closed)
        {
            RWStream.SetLength(0);
            JsonSerializer.Serialize(RWStream, Main);
        }
    }

    public void Close()
    {
        if (!Closed)
        {
            RWStream.Close();
            Closed = true;
        }
    }
}

public class PrerenderStatus
{
    public Dictionary<string, string> lastModified { get; set; } = new();
    public string pluginsHash { get; set; } = "";
}