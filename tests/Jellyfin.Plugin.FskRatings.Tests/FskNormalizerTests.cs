using Jellyfin.Plugin.FskRatings.Normalization;
using Xunit;

namespace Jellyfin.Plugin.FskRatings.Tests;

public class FskNormalizerTests
{
    [Theory]
    [InlineData("FSK-12", "FSK-12")]
    [InlineData("FSK 12", "FSK-12")]
    [InlineData("fsk12", "FSK-12")]
    [InlineData("fsk-16", "FSK-16")]
    [InlineData("FSK: 6", "FSK-6")]
    [InlineData("FSK/18", "FSK-18")]
    [InlineData("DE-12", "FSK-12")]
    [InlineData("DE 16", "FSK-16")]
    [InlineData("de-0", "FSK-0")]
    [InlineData("12", "FSK-12")]
    [InlineData("0", "FSK-0")]
    [InlineData("18", "FSK-18")]
    [InlineData(" 16 ", "FSK-16")]
    [InlineData("12+", "FSK-12")]
    [InlineData("6+", "FSK-6")]
    [InlineData("0+", "FSK-0")]
    [InlineData("18 +", "FSK-18")]
    [InlineData("FSK 16+", "FSK-16")]
    [InlineData("ab 12", "FSK-12")]
    [InlineData("ab 12 Jahren", "FSK-12")]
    [InlineData("Ab 6 Jahren", "FSK-6")]
    [InlineData("o.A.", "FSK-0")]
    [InlineData("ohne Altersbeschränkung", "FSK-0")]
    [InlineData("FSK o.A.", "FSK-0")]
    [InlineData("Keine Jugendfreigabe", "FSK-18")]
    public void TryNormalize_RecognizesGermanVariants(string input, string expected)
    {
        Assert.Equal(expected, FskNormalizer.TryNormalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("PG-13")]
    [InlineData("R")]
    [InlineData("TV-MA")]
    [InlineData("FSK-13")] // not a valid FSK level
    [InlineData("7")] // not a valid FSK level
    [InlineData("7+")] // not a valid FSK level
    [InlineData("13+")] // not a valid FSK level
    [InlineData("FSK 21")]
    [InlineData("Not Rated")]
    [InlineData("ab morgen")]
    public void TryNormalize_RejectsNonGermanOrInvalid(string? input)
    {
        Assert.Null(FskNormalizer.TryNormalize(input));
    }

    [Theory]
    [InlineData("G", "FSK-0")]
    [InlineData("PG", "FSK-6")]
    [InlineData("PG-13", "FSK-12")]
    [InlineData("pg-13", "FSK-12")]
    [InlineData("R", "FSK-16")]
    [InlineData("NC-17", "FSK-18")]
    [InlineData("TV-Y", "FSK-0")]
    [InlineData("TV-G", "FSK-0")]
    [InlineData("TV-PG", "FSK-6")]
    [InlineData("TV-14", "FSK-12")]
    [InlineData("TV-MA", "FSK-16")]
    [InlineData("U", "FSK-0")]
    [InlineData("12A", "FSK-12")]
    [InlineData("15", "FSK-16")]
    [InlineData("R18", "FSK-18")]
    public void TryMapForeign_MapsKnownRatings(string input, string expected)
    {
        Assert.Equal(expected, FskNormalizer.TryMapForeign(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Unrated")]
    [InlineData("Approved")]
    [InlineData("XYZ")]
    public void TryMapForeign_ReturnsNullForUnknown(string? input)
    {
        Assert.Null(FskNormalizer.TryMapForeign(input));
    }
}
