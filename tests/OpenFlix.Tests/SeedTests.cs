using OpenFlix.Catalog;
using Xunit;

namespace OpenFlix.Tests;

public sealed class SeedTests
{
    [Fact]
    public void EveryMediaItemIncludesRightsAndSourceMetadata()
    {
        Assert.NotEmpty(Seed.Items);
        Assert.All(Seed.Items, item =>
        {
            Assert.StartsWith("https://archive.org/details/", item.SourceUrl);
            Assert.StartsWith("https://archive.org/embed/", item.StreamUrl);
            Assert.False(string.IsNullOrWhiteSpace(item.License));
            Assert.False(string.IsNullOrWhiteSpace(item.Attribution));
        });
    }

    [Fact]
    public void HomeCatalogContainsMoviesAndSeries()
    {
        Assert.Contains(Seed.Items, item => item.Kind == MediaKind.Movie);
        Assert.Contains(Seed.Items, item => item.Kind == MediaKind.Series);
    }
}
