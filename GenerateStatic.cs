using Bbob.Plugin;
using PuppeteerSharp;

namespace bbob_plugin_prerender;

public class GenerateStatic : IDisposable
{
    string distribution { get; set; }
    BrowserFetcher browserFetcher = new BrowserFetcher();
    Browser browser;
    MyConfig MyConfig { get; set; }
    public int RegenerateCount { get; set; } = 0;
    public int UseCacheCount { get; set; } = 0;

    public GenerateStatic(string distribution, MyConfig myConfig)
    {
        this.distribution = distribution;
        this.MyConfig = myConfig;
        browserFetcher.DownloadAsync().Wait();
        browser = Puppeteer.LaunchAsync(new LaunchOptions { Headless = true }).Result;
    }

    public void Dispose()
    {
        Stop();
        this.browserFetcher.Dispose();
        this.browser.Dispose();
    }

    public void Stop()
    {
        this.browser.CloseAsync().Wait();
    }

    /// <summary>
    /// Generate static html.
    /// </summary>
    /// <param name="url"></param>
    /// <param name="regenerate">Regenerate if true, otherwise will use cache if exists.</param>
    /// <param name="saveCache">Save cache if true, otherwise no save.</param>
    /// <returns></returns>
    public async Task GenerateHtml(string url, bool regenerate = false, bool saveCache = true)
    {
        string prerenderIndex = "";
        try
        {
            Uri uri = new Uri(url);
            string dist = Path.Combine(distribution, uri.LocalPath.Remove(0, 1));
            string distIndex = Path.Combine(dist, "index.html");
            string prerender = saveCache ? Path.Combine(MyHelper.prerenderDirectory, uri.LocalPath.Remove(0, 1)) : dist;
            prerenderIndex = saveCache ? Path.Combine(prerender, "index.html") : distIndex;
            if (regenerate || !File.Exists(prerenderIndex))
            {
                RegenerateCount++;
                await using var page = await browser.NewPageAsync();
                await page.GoToAsync(url);
                try
                {
                    await page.WaitForExpressionAsync($"Bbob.meta.extra.prerenderNow", new WaitForFunctionOptions()
                    {
                        Timeout = MyConfig.timeout
                    });
                }
                catch (WaitTaskTimeoutException)
                {
                    PluginHelper.printConsole($"Wait already {MyConfig.timeout} ms. Get html now.");
                }
                string htmlContent = await page.GetContentAsync();
                Directory.CreateDirectory(prerender);
                await WriteTextAsync(prerenderIndex, htmlContent);
            }
            else UseCacheCount++;

            Directory.CreateDirectory(dist);
            if (prerenderIndex != distIndex) File.Copy(prerenderIndex, distIndex, true);
        }
        catch (System.Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(prerenderIndex) && File.Exists(prerenderIndex)) File.Delete(prerenderIndex);
            PluginHelper.printConsole($"generate html {url} error:\n{ex.Message}");
        }
    }

    public async Task WriteTextAsync(string filePath, string text)
    {
        byte[] encodedText = System.Text.Encoding.Unicode.GetBytes(text);

        using (FileStream sourceStream = new FileStream(filePath,
            FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 4096, useAsync: true))
        {
            await sourceStream.WriteAsync(encodedText, 0, encodedText.Length);
        };
    }
}