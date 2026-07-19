using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.FskRatings.Web;

/// <summary>
/// Injects the FSK overlay script tag into the web client's index.html at request
/// time via ASP.NET middleware. This works regardless of whether the web directory
/// on disk is writable (package installs like /usr/share/jellyfin/web are typically
/// read-only for the jellyfin user).
/// The middleware is deliberately defensive: it only touches GET requests for the
/// web index document, no-ops when the tag is already present (e.g. added on disk),
/// and on any error serves the original response unchanged.
/// </summary>
public class OverlayInjectionStartupFilter : IStartupFilter
{
    private readonly ILogger<OverlayInjectionStartupFilter> _logger;
    private int _loggedOnce;

    /// <summary>
    /// Initializes a new instance of the <see cref="OverlayInjectionStartupFilter"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public OverlayInjectionStartupFilter(ILogger<OverlayInjectionStartupFilter> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            // Registered before the rest of the pipeline so this runs outermost;
            // stripping Accept-Encoding below then reliably yields an uncompressed
            // response that can be read and rewritten.
            app.Use(InvokeAsync);
            next(app);
        };
    }

    private static bool IsIndexRequest(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        return path.EndsWith("/web/index.html", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/web/", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/web", StringComparison.OrdinalIgnoreCase);
    }

    private async Task InvokeAsync(HttpContext context, Func<Task> next)
    {
        // Only GET produces a body that can be rewritten; other methods pass through.
        if (!IsIndexRequest(context.Request.Path.Value) || !HttpMethods.IsGet(context.Request.Method))
        {
            await next().ConfigureAwait(false);
            return;
        }

        // Normalize the request so the static handler returns a complete plain-text
        // 200: no compression, no partial (206) responses.
        context.Request.Headers.Remove("Accept-Encoding");
        context.Request.Headers.Remove("Range");
        context.Request.Headers.Remove("If-Range");

        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await next().ConfigureAwait(false);
        }
        catch
        {
            // Downstream failure: discard the partial buffer and rethrow so the
            // host's error handling still produces a clean response.
            context.Response.Body = originalBody;
            throw;
        }

        context.Response.Body = originalBody;
        buffer.Seek(0, SeekOrigin.Begin);

        var isHtml = context.Response.StatusCode == 200
            && (context.Response.ContentType?.Contains("text/html", StringComparison.OrdinalIgnoreCase) ?? false);

        if (!isHtml)
        {
            // 304, redirects, non-HTML — pass through unchanged.
            await buffer.CopyToAsync(originalBody, context.RequestAborted).ConfigureAwait(false);
            return;
        }

        string html;
        using (var reader = new StreamReader(buffer, System.Text.Encoding.UTF8, true, 1024, leaveOpen: true))
        {
            html = await reader.ReadToEndAsync(context.RequestAborted).ConfigureAwait(false);
        }

        try
        {
            var injected = OverlayScriptInjector.AddScriptTag(html);
            if (injected is not null)
            {
                html = injected;
                if (Interlocked.Exchange(ref _loggedOnce, 1) == 0)
                {
                    _logger.LogInformation("FSK playback overlay script injected into index.html via request-time middleware.");
                }
            }
        }
        catch (Exception ex)
        {
            // Never break index.html — serve whatever we have.
            _logger.LogWarning(ex, "FSK overlay injection middleware error; serving original HTML.");
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(html);
        context.Response.ContentType = "text/html;charset=utf-8";
        context.Response.ContentLength = bytes.Length;
        // The body changed, so validators from the static-file handler no longer hold.
        context.Response.Headers.Remove("ETag");
        context.Response.Headers.Remove("Last-Modified");
        context.Response.Headers.Remove("Accept-Ranges");
        await originalBody.WriteAsync(bytes, context.RequestAborted).ConfigureAwait(false);
    }
}
