using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Domain.Tests;

[TestClass]
public sealed class ContainerImageTests
{
    [TestMethod]
    public void DisplayName_TaggedImage_ReturnsRepositoryColonTag()
    {
        // Arrange
        var createdAt = new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.Zero);
        var sut = new ContainerImage(
            Id: "sha256:abc",
            Repository: "ubuntu",
            Tag: "latest",
            SizeBytes: 120L,
            CreatedAt: createdAt);

        // Act
        var displayName = sut.DisplayName;

        // Assert
        Assert.AreEqual("ubuntu:latest", displayName);
    }

    [TestMethod]
    public void DisplayName_UntaggedImage_ReturnsNoneColonNone()
    {
        // Arrange
        var createdAt = new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.Zero);
        var sut = new ContainerImage(
            Id: "sha256:def",
            Repository: "<none>",
            Tag: "<none>",
            SizeBytes: 120L,
            CreatedAt: createdAt);
        var emptyValues = new ContainerImage(
            Id: "sha256:ghi",
            Repository: string.Empty,
            Tag: string.Empty,
            SizeBytes: 120L,
            CreatedAt: createdAt);

        // Act
        var displayName = sut.DisplayName;
        var emptyValuesDisplayName = emptyValues.DisplayName;

        // Assert
        Assert.AreEqual("<none>:<none>", displayName);
        Assert.AreEqual("<none>:<none>", emptyValuesDisplayName);
    }
}
