using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Domain.Tests;

[TestClass]
public sealed class ContainerTests
{
    private static Container CreateContainer(ContainerState state) => new(
        Id: "c1",
        Name: "web",
        Image: "nginx:latest",
        State: state,
        CreatedAt: new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.Zero));

    [TestMethod]
    public void Constructor_ValidValues_PropertiesAreSetAsGiven()
    {
        // Arrange
        var createdAt = new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.Zero);

        // Act
        var sut = new Container(
            Id: "c1",
            Name: "web",
            Image: "nginx:latest",
            State: ContainerState.Running,
            CreatedAt: createdAt);

        // Assert
        Assert.AreEqual("c1", sut.Id);
        Assert.AreEqual("web", sut.Name);
        Assert.AreEqual("nginx:latest", sut.Image);
        Assert.AreEqual(ContainerState.Running, sut.State);
        Assert.AreEqual(createdAt, sut.CreatedAt);
    }

    [TestMethod]
    public void CanStart_StateIsStopped_IsTrue()
    {
        // Arrange
        var sut = CreateContainer(ContainerState.Stopped);

        // Act & Assert
        Assert.IsTrue(sut.CanStart);
    }

    [TestMethod]
    public void CanStart_StateIsRunning_IsFalse()
    {
        // Arrange
        var sut = CreateContainer(ContainerState.Running);

        // Act & Assert
        Assert.IsFalse(sut.CanStart);
    }

    [TestMethod]
    public void CanStop_StateIsRunning_IsTrue()
    {
        // Arrange
        var sut = CreateContainer(ContainerState.Running);

        // Act & Assert
        Assert.IsTrue(sut.CanStop);
    }

    [TestMethod]
    public void CanStop_StateIsStopped_IsFalse()
    {
        // Arrange
        var sut = CreateContainer(ContainerState.Stopped);

        // Act & Assert
        Assert.IsFalse(sut.CanStop);
    }

    [TestMethod]
    public void CanRestart_StateIsRunning_IsTrue()
    {
        // Arrange
        var sut = CreateContainer(ContainerState.Running);

        // Act & Assert
        Assert.IsTrue(sut.CanRestart);
    }

    [TestMethod]
    public void CanRestart_StateIsStopped_IsFalse()
    {
        // Arrange
        var sut = CreateContainer(ContainerState.Stopped);

        // Act & Assert
        Assert.IsFalse(sut.CanRestart);
    }

    [TestMethod]
    public void CanDelete_StateIsStopped_IsTrue()
    {
        // Arrange
        var sut = CreateContainer(ContainerState.Stopped);

        // Act & Assert
        Assert.IsTrue(sut.CanDelete);
    }

    [TestMethod]
    public void CanDelete_StateIsRunning_IsFalse()
    {
        // Arrange
        var sut = CreateContainer(ContainerState.Running);

        // Act & Assert
        Assert.IsFalse(sut.CanDelete);
    }

    [TestMethod]
    public void With_StateChangedToRunning_OtherPropertiesAreUnchanged()
    {
        // Arrange
        var sut = CreateContainer(ContainerState.Stopped);

        // Act
        var updated = sut with { State = ContainerState.Running };

        // Assert
        Assert.AreEqual(ContainerState.Running, updated.State);
        Assert.AreEqual(sut.Id, updated.Id);
        Assert.AreEqual(sut.Name, updated.Name);
        Assert.AreEqual(sut.Image, updated.Image);
        Assert.AreEqual(sut.CreatedAt, updated.CreatedAt);
    }
}
