using Jellyfin.Plugin.FskRatings.Web;
using Xunit;

namespace Jellyfin.Plugin.FskRatings.Tests;

public class OverlayScriptInjectionTests
{
    [Fact]
    public void AddScriptTag_InsertsBeforeHeadEnd()
    {
        var html = "<html><head><title>x</title></head><body></body></html>";
        var result = OverlayScriptInjector.AddScriptTag(html);
        Assert.NotNull(result);
        Assert.Equal(
            "<html><head><title>x</title>" + OverlayScriptInjector.ScriptTag + "</head><body></body></html>",
            result);
    }

    [Fact]
    public void AddScriptTag_IsIdempotent()
    {
        var html = "<html><head></head><body></body></html>";
        var once = OverlayScriptInjector.AddScriptTag(html);
        Assert.NotNull(once);
        Assert.Null(OverlayScriptInjector.AddScriptTag(once!));
    }

    [Fact]
    public void AddScriptTag_HandlesUppercaseHeadTag()
    {
        var result = OverlayScriptInjector.AddScriptTag("<HTML><HEAD></HEAD><BODY></BODY></HTML>");
        Assert.NotNull(result);
        Assert.Contains(OverlayScriptInjector.ScriptTag + "</HEAD>", result, StringComparison.Ordinal);
    }

    [Fact]
    public void AddScriptTag_ReturnsNullWithoutHeadTag()
    {
        Assert.Null(OverlayScriptInjector.AddScriptTag("<html><body></body></html>"));
    }
}
