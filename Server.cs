using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace bbob_plugin_prerender;

public class Server
{
    string domain;
    string baseUrl;
    string distribution;
    public bool AutoRedirect { get; set; } = true;

    private WebApplication? main;
    public Server(string domain, string baseUrl, string distribution)
    {
        this.domain = domain;
        this.baseUrl = baseUrl;
        this.distribution = distribution;
    }
    public bool Start()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        var app = builder.Build();
        if (AutoRedirect) app.Use(async (context, next) =>
        {
            await next();
            if (context.Response.StatusCode == 404)
            {
                context.Request.Path = "/";
                await next();
            }
        });
        app.UsePathBase(baseUrl);
        app.UseFileServer(new FileServerOptions()
        {
            FileProvider = new PhysicalFileProvider(distribution),

        });
        app.RunAsync(domain);
        main = app;
        return true;
    }

    public bool Stop()
    {
        if (main != null)
        {
            main.StopAsync();
            return true;
        }

        return false;
    }
}