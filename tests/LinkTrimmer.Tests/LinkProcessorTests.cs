using LinkTrimmer.Core;
using Xunit;

namespace LinkTrimmer.Tests;

public class LinkProcessorTests
{
    private readonly LinkProcessor _p = new();

    [Theory]
    [InlineData("https://x.com/user/status/123?s=20&t=abc", "https://fxtwitter.com/user/status/123")]
    [InlineData("https://twitter.com/user/status/123?s=46", "https://fxtwitter.com/user/status/123")]
    [InlineData("https://www.instagram.com/p/ABC/?igsh=xyz", "https://kkinstagram.com/p/ABC/")]
    [InlineData("https://www.youtube.com/watch?v=abc&si=track&t=10", "https://www.youtube.com/watch?v=abc&t=10")]
    [InlineData("https://youtu.be/abc?si=track", "https://youtu.be/abc")]
    [InlineData("https://example.com/page?id=5&utm_source=x&fbclid=y", "https://example.com/page?id=5")]
    public void Rewrites(string input, string expected) => Assert.Equal(expected, _p.Process(input));

    [Theory]
    [InlineData("zwykły tekst")]
    [InlineData("zobacz https://x.com/a/status/1?s=20 super")]
    [InlineData("https://example.com/page?id=5")]
    [InlineData("C:\\Users\\a\\obraz.png")]
    [InlineData("")]
    [InlineData(null)]
    public void LeavesAlone(string? input) => Assert.Null(_p.Process(input));

    // --- bezpieczeństwo ---

    [Theory]
    [InlineData("https://evilx.com/status/1?s=20")]              // domena kończąca się na "x.com", ale nie subdomena
    [InlineData("https://x.com.evil.com/status/1?s=20")]         // x.com jako przedrostek cudzej domeny
    [InlineData("https://x.com@evil.com/status/1?s=20")]         // x.com tylko jako "użytkownik" w adresie
    [InlineData("https://evil.com/?u=https://x.com/a?s=1")]      // x.com tylko w parametrze
    public void LookalikeHostsDoNotMatchRules(string input)
    {
        var result = _p.Process(input);
        Assert.True(result == null || !result.Contains("fxtwitter.com"), $"Reguła X nie powinna pasować: {result}");
    }

    [Theory]
    [InlineData("evil.com/path")]
    [InlineData("evil.com@other.com")]
    [InlineData("a b.com")]
    [InlineData("evil.com:8080")]
    public void InvalidRewriteHostFromConfigNeverBreaksOrRedirects(string badHost)
    {
        var cfg = new TrimmerConfig
        {
            Rules = { new SiteRule { Name = "bad", Hosts = { "example.com" }, RewriteHost = badHost } }
        };
        cfg.Rules.RemoveAll(r => r.Name != "bad");
        Assert.Null(new LinkProcessor(cfg).Process("https://example.com/p?id=1"));
    }

    [Fact]
    public void RewriteDropsUserInfo()
    {
        var result = _p.Process("https://user:pass@x.com/u/status/1?s=20");
        Assert.Equal("https://fxtwitter.com/u/status/1", result);
    }

    [Theory]
    [InlineData("https://example.com/%ZZ?a=%")]
    [InlineData("http://[::1")]
    [InlineData("https://")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/Windows/system32/config")]
    [InlineData("ftp://example.com/x?utm_source=a")]
    public void MalformedOrNonHttpInputIsIgnoredWithoutThrowing(string input) =>
        Assert.Null(_p.Process(input));

    [Fact]
    public void Idempotent()
    {
        var once = _p.Process("https://x.com/u/status/1?s=20")!;
        Assert.Null(_p.Process(once));
    }
}
