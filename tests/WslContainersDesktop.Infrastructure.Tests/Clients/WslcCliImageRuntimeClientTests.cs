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
