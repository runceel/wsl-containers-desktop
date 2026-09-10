using System.Globalization;
using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Infrastructure.Clients;
using WslContainersDesktop.Infrastructure.Tests.Fakes;

namespace WslContainersDesktop.Infrastructure.Tests.Clients;

[TestClass]
public sealed class WslcCliImageRuntimeClientTests
{
    [TestMethod]
    public async Task ListImagesAsync_CliReturnsImages_MapsJsonToContainerImages()
    {
        // Arrange
        const string json = """
            [{
              "Created": 1782533899,
              "Id": "sha256:fb3bcc37a9d41b510f9bdb8ec8e66884578aa44b9703f77cef905db46c6557e5",
              "Repository": "ubuntu",
              "Size": 120033654,
              "Tag": "latest"
            }]
            """;
        var runner = new FakeWslcCliRunner { Result = new(0, json, string.Empty) };
        var sut = new WslcCliImageRuntimeClient(runner);

        // Act
        var images = await sut.ListImagesAsync();

        // Assert
        Assert.HasCount(1, images);
        Assert.AreEqual("sha256:fb3bcc37a9d41b510f9bdb8ec8e66884578aa44b9703f77cef905db46c6557e5", images[0].Id);
        Assert.AreEqual("ubuntu", images[0].Repository);
        Assert.AreEqual("latest", images[0].Tag);
        Assert.AreEqual(120033654L, images[0].SizeBytes);
        Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(1782533899), images[0].CreatedAt);
    }

    [TestMethod]
    public async Task ListImagesAsync_CliReturnsJsonLinesWithCurrentFields_MapsAllRecords()
    {
        // Arrange
        const string jsonLines = "{\"ID\":\"sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"Repository\":\"ubuntu\",\"Tag\":\"latest\",\"Size\":\"69.6MB\",\"CreatedAt\":\"2026-07-10 15:02:32 +0900 JST\"}\r\n\r\n{\"ID\":\"sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\",\"Repository\":\"alpine\",\"Tag\":\"3.22\",\"Size\":\"1.5kB\",\"CreatedAt\":\"2026-07-10 06:02:32 +0000 UTC\"}\r\n";
        var runner = new FakeWslcCliRunner { Result = new(0, jsonLines, string.Empty) };
        var sut = new WslcCliImageRuntimeClient(runner);

        // Act
        var images = await sut.ListImagesAsync();

        // Assert
        Assert.HasCount(2, images);
        Assert.AreEqual("sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", images[0].Id);
        Assert.AreEqual(new DateTimeOffset(2026, 7, 10, 6, 2, 32, TimeSpan.Zero), images[0].CreatedAt);
        Assert.AreEqual(69_600_000L, images[0].SizeBytes);
        Assert.AreEqual("sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", images[1].Id);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" \r\n \r\n")]
    public async Task ListImagesAsync_CliReturnsEmptyOrWhitespaceOutput_ReturnsEmptyList(string output)
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, output, string.Empty) };
        var sut = new WslcCliImageRuntimeClient(runner);

        // Act
        var images = await sut.ListImagesAsync();

        // Assert
        Assert.IsEmpty(images);
    }

    [TestMethod]
    public async Task ListImagesAsync_CliReturnsDecimalSize_UsesInvariantCultureAndRoundsDeterministically()
    {
        // Arrange
        const string json = "{\"ID\":\"sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"Repository\":\"ubuntu\",\"Tag\":\"latest\",\"Size\":\"1.2345kB\",\"CreatedAt\":\"2026-07-10 15:02:32 +0900 JST\"}";
        var runner = new FakeWslcCliRunner { Result = new(0, json, string.Empty) };
        var sut = new WslcCliImageRuntimeClient(runner);
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

            // Act
            var images = await sut.ListImagesAsync();

            // Assert
            Assert.AreEqual(1_235L, images[0].SizeBytes);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [TestMethod]
    public async Task ListImagesAsync_CliReturnsInvalidHumanSizeUnit_ThrowsContainerRuntimeExceptionWithJsonException()
    {
        // Arrange
        const string json = "{\"ID\":\"sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"Repository\":\"ubuntu\",\"Tag\":\"latest\",\"Size\":\"69.6XB\",\"CreatedAt\":\"2026-07-10 15:02:32 +0900 JST\"}";
        var runner = new FakeWslcCliRunner { Result = new(0, json, string.Empty) };
        var sut = new WslcCliImageRuntimeClient(runner);

        // Act
        var ex = await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.ListImagesAsync());

        // Assert
        Assert.AreEqual(typeof(System.Text.Json.JsonException), ex.InnerException?.GetType());
    }

    [TestMethod]
    [DataRow("\"not-a-date\"")]
    [DataRow("{}")]
    public async Task ListImagesAsync_CliReturnsInvalidCreatedAt_ThrowsContainerRuntimeExceptionWithJsonException(string createdAt)
    {
        // Arrange
        var json = $"{{\"ID\":\"sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"Repository\":\"ubuntu\",\"Tag\":\"latest\",\"Size\":\"69.6MB\",\"CreatedAt\":{createdAt}}}";
        var runner = new FakeWslcCliRunner { Result = new(0, json, string.Empty) };
        var sut = new WslcCliImageRuntimeClient(runner);

        // Act
        var ex = await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.ListImagesAsync());

        // Assert
        Assert.AreEqual(typeof(System.Text.Json.JsonException), ex.InnerException?.GetType());
    }

    [TestMethod]
    public async Task ListImagesAsync_CliArguments_AreImageListJsonNoTrunc()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, "[]", string.Empty) };
        var sut = new WslcCliImageRuntimeClient(runner);

        // Act
        await sut.ListImagesAsync();

        // Assert
        CollectionAssert.AreEqual(new[] { "image", "list", "--format", "json", "--no-trunc" }, runner.Calls[0].ToList());
    }

    [TestMethod]
    public async Task ListImagesAsync_CliReturnsMalformedJson_ThrowsContainerRuntimeExceptionWithInnerException()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, "not-json", string.Empty) };
        var sut = new WslcCliImageRuntimeClient(runner);

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.ListImagesAsync());
        Assert.IsNotNull(ex.InnerException);
    }

    [TestMethod]
    public async Task ListImagesAsync_CliReturnsNullElementJson_ThrowsContainerRuntimeExceptionWithJsonException()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, "[null]", string.Empty) };
        var sut = new WslcCliImageRuntimeClient(runner);

        // Act
        var ex = await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.ListImagesAsync());

        // Assert
        Assert.AreEqual(typeof(System.Text.Json.JsonException), ex.InnerException?.GetType());
    }

    [TestMethod]
    public async Task PullImageAsync_CliArguments_AreTopLevelPullWithImageReference()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, string.Empty, string.Empty) };
        var sut = new WslcCliImageRuntimeClient(runner);

        // Act
        await sut.PullImageAsync("ubuntu:latest");

        // Assert
        CollectionAssert.AreEqual(new[] { "pull", "ubuntu:latest" }, runner.Calls[0].ToList());
    }

    [TestMethod]
    public async Task DeleteImageAsync_CliArguments_AreImageRemoveWithoutForce()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, string.Empty, string.Empty) };
        var sut = new WslcCliImageRuntimeClient(runner);

        // Act
        await sut.DeleteImageAsync("sha256:abc");

        // Assert
        var arguments = runner.Calls[0].ToList();
        CollectionAssert.AreEqual(new[] { "image", "remove", "sha256:abc" }, arguments);
        CollectionAssert.DoesNotContain(arguments, "--force");
    }
}
