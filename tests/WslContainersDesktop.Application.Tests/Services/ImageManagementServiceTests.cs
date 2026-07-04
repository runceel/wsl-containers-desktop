using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Application.Services;
using WslContainersDesktop.Application.Tests.Fakes;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Application.Tests.Services;

[TestClass]
public sealed class ImageManagementServiceTests
{
    private static ContainerImage CreateImage(string id) => new(
        Id: id,
        Repository: "ubuntu",
        Tag: "latest",
        SizeBytes: 120L,
        CreatedAt: new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.Zero));

    [TestMethod]
    public async Task GetImagesAsync_ClientReturnsImages_ReturnsSameImages()
    {
        // Arrange
        var expected = new[] { CreateImage("img-1"), CreateImage("img-2") };
        var client = new FakeContainerRuntimeClient { Images = expected };
        var sut = new ImageManagementService(client);

        // Act
        var actual = await sut.GetImagesAsync();

        // Assert
        CollectionAssert.AreEqual(expected.ToList(), actual.ToList());
    }

    [TestMethod]
    public async Task PullAsync_ImageReferenceHasWhitespace_TrimsAndCallsClientPull()
    {
        // Arrange
        var client = new FakeContainerRuntimeClient();
        var sut = new ImageManagementService(client);

        // Act
        await sut.PullAsync("  ubuntu:latest  ");

        // Assert
        CollectionAssert.AreEqual(new[] { "ubuntu:latest" }, client.PullCalls);
    }

    [TestMethod]
    public async Task PullAsync_ImageReferenceIsWhitespace_ThrowsArgumentExceptionAndDoesNotCallClient()
    {
        // Arrange
        var client = new FakeContainerRuntimeClient();
        var sut = new ImageManagementService(client);

        // Act & Assert
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => sut.PullAsync("   "));
        Assert.IsEmpty(client.PullCalls);
    }

    [TestMethod]
    public async Task PullAsync_ClientThrowsContainerRuntimeException_ExceptionPropagatesUnchanged()
    {
        // Arrange
        var runtimeException = new ContainerRuntimeException("pull", 1, "pull failed");
        var client = new FakeContainerRuntimeClient { PullException = runtimeException };
        var sut = new ImageManagementService(client);

        // Act & Assert
        var actual = await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.PullAsync("ubuntu:latest"));
        Assert.AreSame(runtimeException, actual);
    }

    [TestMethod]
    public async Task DeleteAsync_ImageIdProvided_CallsClientDeleteImage()
    {
        // Arrange
        var client = new FakeContainerRuntimeClient();
        var sut = new ImageManagementService(client);

        // Act
        await sut.DeleteAsync("sha256:abc");

        // Assert
        CollectionAssert.AreEqual(new[] { "sha256:abc" }, client.DeleteImageCalls);
    }

    [TestMethod]
    public async Task DeleteAsync_ClientThrowsContainerRuntimeException_ExceptionPropagatesUnchanged()
    {
        // Arrange
        var runtimeException = new ContainerRuntimeException("delete", 1, "delete failed");
        var client = new FakeContainerRuntimeClient { DeleteImageException = runtimeException };
        var sut = new ImageManagementService(client);

        // Act & Assert
        var actual = await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.DeleteAsync("sha256:abc"));
        Assert.AreSame(runtimeException, actual);
    }
}
