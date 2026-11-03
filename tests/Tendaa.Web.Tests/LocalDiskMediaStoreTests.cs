using Microsoft.Extensions.Options;
using Tendaa.Core.Configurations.ConfigModels;
using Tendaa.Infrastructure.WhatsApp;

namespace Tendaa.Web.Tests;

public sealed class LocalDiskMediaStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"tendaa-media-test-{Guid.NewGuid():N}");

    private LocalDiskMediaStore CreateStore() =>
        new(Options.Create(new MediaStorageOptions { RootPath = _root }));

    [Fact]
    public async Task GetAsync_AfterSave_ReturnsSameBytesAndMimeType()
    {
        var store = CreateStore();
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        await store.SaveAsync("media-1", bytes, "image/jpeg");

        var result = await store.GetAsync("media-1");

        Assert.True(result.HasValue);
        Assert.Equal("image/jpeg", result!.Value.MimeType);
        using var stream = result.Value.Stream;
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        Assert.Equal(bytes, buffer.ToArray());
    }

    [Fact]
    public async Task GetAsync_UnknownId_ReturnsNull()
    {
        var store = CreateStore();
        await store.SaveAsync("media-1", [1, 2, 3], "image/jpeg");

        Assert.False((await store.GetAsync("media-2")).HasValue);
    }

    [Fact]
    public async Task GetAsync_MissingRoot_ReturnsNull()
    {
        var result = await CreateStore().GetAsync("media-1");

        Assert.False(result.HasValue);
    }

    [Theory]
    [InlineData("../evil")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("a*b")]
    public async Task GetAsync_PathTraversalOrGlob_ReturnsNull(string mediaId)
    {
        var result = await CreateStore().GetAsync(mediaId);

        Assert.False(result.HasValue);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
