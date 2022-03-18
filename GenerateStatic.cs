

using Bbob.Plugin;
using PuppeteerSharp;

namespace bbob_plugin_prerender;

public class GenerateStatic : IDisposable
{
    string distribution { get; set; }
    BrowserFetcher browserFetcher = new BrowserFetcher();
    Browser browser;

    public GenerateStatic(string distribution)
    {
        this.distribution = distribution;
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
        Uri uri = new Uri(url);
        string dist = Path.Combine(distribution, uri.LocalPath.Remove(0, 1));
        string distIndex = Path.Combine(dist, "index.html");
        string prerender = saveCache ? Path.Combine(MyHelper.prerenderDirectory, uri.LocalPath.Remove(0, 1)) : dist;
        string prerenderIndex = saveCache ? Path.Combine(prerender, "index.html") : distIndex;
        if (regenerate || !File.Exists(prerenderIndex))
        {
            await using var page = await browser.NewPageAsync();
            await page.GoToAsync(url);
            var element = await page.WaitForExpressionAsync($"Bbob.meta.extra.prerenderNow", new WaitForFunctionOptions()
            {
                Timeout = 5000
            });
            string htmlContent = await page.GetContentAsync();
            Directory.CreateDirectory(prerender);
            File.WriteAllText(prerenderIndex, htmlContent);
        }

        Directory.CreateDirectory(dist);
        if (prerenderIndex != distIndex) File.Copy(prerenderIndex, distIndex, true);
    }
}