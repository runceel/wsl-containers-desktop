using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Infrastructure.Cli;
using WslContainersDesktop.Infrastructure.Clients;
using WslContainersDesktop.Infrastructure.Tests.Fakes;

namespace WslContainersDesktop.Infrastructure.Tests.Clients;

[TestClass]
public sealed class VolumeManagementInfrastructureTests
{
    [TestMethod]
    public async Task ListVolumesAsync_CliReturnsVolumesAndInspectReturnsCreatedAt_MapsJsonToContainerVolumes()
    {
        // Arrange
        const string listJson = "[{\"Driver\":\"guest\",\"Name\":\"vol-demo\"}]";
        const string inspectJson = "[{\"CreatedAt\":\"2026-07-02T09:00:00Z\",\"Driver\":\"guest\",\"Name\":\"vol-demo\"}]";
        var runner = new FakeWslcCliRunner();
        runner.RunAsyncFunc = (arguments, cancellationToken) =>
        {
            if (arguments.SequenceEqual(new[] { "volume", "list", "--format", "json" }))
            {
                return Task.FromResult(new CliResult(0, listJson, string.Empty));
            }

            if (arguments.SequenceEqual(new[] { "volume", "inspect", "vol-demo" }))
            {
                return Task.FromResult(new CliResult(0, inspectJson, string.Empty));
            }

            return Task.FromResult(new CliResult(0, string.Empty, string.Empty));
        };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        var volumes = await sut.ListVolumesAsync();

        // Assert
        Assert.HasCount(1, volumes);
        Assert.AreEqual("vol-demo", volumes[0].Name);
        Assert.AreEqual("guest", volumes[0].Driver);
        Assert.AreEqual(DateTimeOffset.Parse("2026-07-02T09:00:00Z"), volumes[0].CreatedAt);
    }

    [TestMethod]
    public async Task ListVolumesAsync_CliArguments_AreVolumeListJsonThenVolumeInspectForEachName()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, "[]", string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        await sut.ListVolumesAsync();

        // Assert
        CollectionAssert.AreEqual(new[] { "volume", "list", "--format", "json" }, runner.Calls[0].ToList());
    }

    [TestMethod]
    public async Task ListVolumesAsync_InspectReturnsEmptyArray_UsesMinValueCreatedAtAndKeepsVolume()
    {
        // Arrange
        var runner = new FakeWslcCliRunner();
        runner.RunAsyncFunc = (arguments, cancellationToken) =>
        {
            if (arguments.SequenceEqual(new[] { "volume", "list", "--format", "json" }))
            {
                return Task.FromResult(new CliResult(0, "[{\"Driver\":\"guest\",\"Name\":\"vol-demo\"}]", string.Empty));
            }

            return Task.FromResult(new CliResult(0, "[]", string.Empty));
        };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        var volumes = await sut.ListVolumesAsync();

        // Assert
        Assert.HasCount(1, volumes);
        Assert.AreEqual(DateTimeOffset.MinValue, volumes[0].CreatedAt);
        Assert.AreEqual("vol-demo", volumes[0].Name);
    }

    [TestMethod]
    public async Task ListVolumesAsync_ListMalformedJson_ThrowsContainerRuntimeExceptionWithInnerException()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, "not-json", string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.ListVolumesAsync());
        Assert.IsNotNull(ex.InnerException);
    }

    [TestMethod]
    public async Task ListVolumesAsync_InspectMalformedJson_ThrowsContainerRuntimeExceptionWithInnerException()
    {
        // Arrange
        var runner = new FakeWslcCliRunner();
        runner.RunAsyncFunc = (arguments, cancellationToken) =>
        {
            if (arguments.SequenceEqual(new[] { "volume", "list", "--format", "json" }))
            {
                return Task.FromResult(new CliResult(0, "[{\"Driver\":\"guest\",\"Name\":\"vol-demo\"}]", string.Empty));
            }

            return Task.FromResult(new CliResult(0, "not-json", string.Empty));
        };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.ListVolumesAsync());
        Assert.IsNotNull(ex.InnerException);
    }

    [TestMethod]
    public async Task ListVolumesAsync_InspectCreatedAtIsEmpty_UsesMinValueCreatedAt()
    {
        // Arrange
        var runner = new FakeWslcCliRunner();
        runner.RunAsyncFunc = (arguments, cancellationToken) =>
        {
            if (arguments.SequenceEqual(new[] { "volume", "list", "--format", "json" }))
            {
                return Task.FromResult(new CliResult(0, "[{\"Driver\":\"guest\",\"Name\":\"vol-demo\"}]", string.Empty));
            }

            return Task.FromResult(new CliResult(0, "[{\"CreatedAt\":\"\",\"Driver\":\"guest\",\"Name\":\"vol-demo\"}]", string.Empty));
        };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        var volumes = await sut.ListVolumesAsync();

        // Assert
        Assert.AreEqual(DateTimeOffset.MinValue, volumes[0].CreatedAt);
    }

    [TestMethod]
    public async Task CreateVolumeAsync_CliArguments_AreVolumeCreateWithName()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, string.Empty, string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        await sut.CreateVolumeAsync("vol-demo");

        // Assert
        CollectionAssert.AreEqual(new[] { "volume", "create", "vol-demo" }, runner.Calls[0].ToList());
    }

    [TestMethod]
    public async Task DeleteVolumeAsync_CliArguments_AreVolumeRemoveWithoutForce()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, string.Empty, string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        await sut.DeleteVolumeAsync("vol-demo");

        // Assert
        var arguments = runner.Calls[0].ToList();
        CollectionAssert.AreEqual(new[] { "volume", "remove", "vol-demo" }, arguments);
        CollectionAssert.DoesNotContain(arguments, "--force");
    }
}
