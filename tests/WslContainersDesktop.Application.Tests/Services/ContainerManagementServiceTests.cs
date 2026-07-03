using System.Runtime.CompilerServices;
using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Application.Services;
using WslContainersDesktop.Application.Tests.Fakes;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Application.Tests.Services;

[TestClass]
public sealed class ContainerManagementServiceTests
{
    private static Container CreateContainer(string id, ContainerState state) => new(
        Id: id,
        Name: $"name-{id}",
        Image: "nginx:latest",
        State: state,
        CreatedAt: new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.Zero));

    [TestMethod]
    public async Task GetContainersAsync_ClientReturnsContainers_ReturnsSameContainers()
    {
        // Arrange
        var expected = new[] { CreateContainer("c1", ContainerState.Running), CreateContainer("c2", ContainerState.Stopped) };
        var client = new FakeContainerRuntimeClient { Containers = expected };
        var sut = new ContainerManagementService(client);

        // Act
        var actual = await sut.GetContainersAsync();

        // Assert
        CollectionAssert.AreEqual(expected, actual.ToList());
    }

    [TestMethod]
    public async Task StartAsync_ContainerIsStopped_CallsClientStartAndReturnsRunningContainer()
    {
        // Arrange
        var client = new FakeContainerRuntimeClient { Containers = [CreateContainer("c1", ContainerState.Stopped)] };
        var sut = new ContainerManagementService(client);

        // Act
        var result = await sut.StartAsync("c1");

        // Assert
        CollectionAssert.AreEqual(new[] { "c1" }, client.StartCalls);
        Assert.AreEqual(ContainerState.Running, result.State);
        Assert.AreEqual("c1", result.Id);
    }

    [TestMethod]
    public async Task StartAsync_ContainerIsAlreadyRunning_ThrowsInvalidContainerOperationExceptionAndDoesNotCallClient()
    {
        // Arrange
        var client = new FakeContainerRuntimeClient { Containers = [CreateContainer("c1", ContainerState.Running)] };
        var sut = new ContainerManagementService(client);

        // Act & Assert
        await Assert.ThrowsExactlyAsync<InvalidContainerOperationException>(() => sut.StartAsync("c1"));
        Assert.IsEmpty(client.StartCalls);
    }

    [TestMethod]
    public async Task StartAsync_ContainerIdNotFound_ThrowsContainerNotFoundException()
    {
        // Arrange
        var client = new FakeContainerRuntimeClient { Containers = [] };
        var sut = new ContainerManagementService(client);

        // Act & Assert
        await Assert.ThrowsExactlyAsync<ContainerNotFoundException>(() => sut.StartAsync("missing"));
    }

    [TestMethod]
    public async Task StopAsync_ContainerIsRunning_CallsClientStopAndReturnsStoppedContainer()
    {
        // Arrange
        var client = new FakeContainerRuntimeClient { Containers = [CreateContainer("c1", ContainerState.Running)] };
        var sut = new ContainerManagementService(client);

        // Act
        var result = await sut.StopAsync("c1");

        // Assert
        CollectionAssert.AreEqual(new[] { "c1" }, client.StopCalls);
        Assert.AreEqual(ContainerState.Stopped, result.State);
    }

    [TestMethod]
    public async Task StopAsync_ContainerIsAlreadyStopped_ThrowsInvalidContainerOperationExceptionAndDoesNotCallClient()
    {
        // Arrange
        var client = new FakeContainerRuntimeClient { Containers = [CreateContainer("c1", ContainerState.Stopped)] };
        var sut = new ContainerManagementService(client);

        // Act & Assert
        await Assert.ThrowsExactlyAsync<InvalidContainerOperationException>(() => sut.StopAsync("c1"));
        Assert.IsEmpty(client.StopCalls);
    }

    [TestMethod]
    public async Task StopAsync_ContainerIdNotFound_ThrowsContainerNotFoundException()
    {
        // Arrange
        var client = new FakeContainerRuntimeClient { Containers = [] };
        var sut = new ContainerManagementService(client);

        // Act & Assert
        await Assert.ThrowsExactlyAsync<ContainerNotFoundException>(() => sut.StopAsync("missing"));
    }

    [TestMethod]
    public async Task RestartAsync_ContainerIsRunning_CallsClientStopThenStartAndReturnsRunningContainer()
    {
        // Arrange
        var client = new FakeContainerRuntimeClient { Containers = [CreateContainer("c1", ContainerState.Running)] };
        var sut = new ContainerManagementService(client);

        // Act
        var result = await sut.RestartAsync("c1");

        // Assert
        CollectionAssert.AreEqual(new[] { "c1" }, client.StopCalls);
        CollectionAssert.AreEqual(new[] { "c1" }, client.StartCalls);
        Assert.AreEqual(ContainerState.Running, result.State);
    }

    [TestMethod]
    public async Task RestartAsync_ContainerIsStopped_ThrowsInvalidContainerOperationExceptionAndDoesNotCallClient()
    {
        // Arrange
        // 既に外部から停止済みのコンテナに再起動を要求した場合、Stop→Startへすり替わって
        // 「起動」として成功してしまうことを防ぐため、事前検証で必ず失敗させる。
        var client = new FakeContainerRuntimeClient { Containers = [CreateContainer("c1", ContainerState.Stopped)] };
        var sut = new ContainerManagementService(client);

        // Act & Assert
        await Assert.ThrowsExactlyAsync<InvalidContainerOperationException>(() => sut.RestartAsync("c1"));
        Assert.IsEmpty(client.StopCalls);
        Assert.IsEmpty(client.StartCalls);
    }

    [TestMethod]
    public async Task RestartAsync_ContainerIdNotFound_ThrowsContainerNotFoundException()
    {
        // Arrange
        var client = new FakeContainerRuntimeClient { Containers = [] };
        var sut = new ContainerManagementService(client);

        // Act & Assert
        await Assert.ThrowsExactlyAsync<ContainerNotFoundException>(() => sut.RestartAsync("missing"));
    }

    [TestMethod]
    public async Task DeleteAsync_ContainerIsStopped_CallsClientDelete()
    {
        // Arrange
        var client = new FakeContainerRuntimeClient { Containers = [CreateContainer("c1", ContainerState.Stopped)] };
        var sut = new ContainerManagementService(client);

        // Act
        await sut.DeleteAsync("c1");

        // Assert
        CollectionAssert.AreEqual(new[] { "c1" }, client.DeleteCalls);
    }

    [TestMethod]
    public async Task DeleteAsync_ContainerIsRunning_ThrowsInvalidContainerOperationExceptionAndDoesNotCallClient()
    {
        // Arrange
        var client = new FakeContainerRuntimeClient { Containers = [CreateContainer("c1", ContainerState.Running)] };
        var sut = new ContainerManagementService(client);

        // Act & Assert
        await Assert.ThrowsExactlyAsync<InvalidContainerOperationException>(() => sut.DeleteAsync("c1"));
        Assert.IsEmpty(client.DeleteCalls);
    }

    [TestMethod]
    public async Task DeleteAsync_ContainerIdNotFound_ThrowsContainerNotFoundException()
    {
        // Arrange
        var client = new FakeContainerRuntimeClient { Containers = [] };
        var sut = new ContainerManagementService(client);

        // Act & Assert
        await Assert.ThrowsExactlyAsync<ContainerNotFoundException>(() => sut.DeleteAsync("missing"));
    }

    [TestMethod]
    public async Task StartAsync_ClientThrowsContainerRuntimeException_ExceptionPropagatesUnchanged()
    {
        // Arrange
        var runtimeException = new ContainerRuntimeException("container start c1", 1, "起動に失敗しました。")
        ;
        var client = new FakeContainerRuntimeClient
        {
            Containers = [CreateContainer("c1", ContainerState.Stopped)],
            ExceptionToThrow = runtimeException,
        };
        var sut = new ContainerManagementService(client);

        // Act & Assert
        var actual = await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.StartAsync("c1"));
        Assert.AreSame(runtimeException, actual);
    }

    [TestMethod]
    public async Task GetContainerLogsAsync_ContainerExists_ReturnsRuntimeLogs()
    {
        // Arrange
        var expectedLogs = new[] { "line-1", "line-2" };
        var client = new FakeContainerRuntimeClient
        {
            Containers = [CreateContainer("c1", ContainerState.Running)],
            ContainerLogs = expectedLogs,
        };
        var sut = new ContainerManagementService(client);

        // Act
        var actual = await sut.GetContainerLogsAsync("c1");

        // Assert
        CollectionAssert.AreEqual(expectedLogs, actual.ToList());
        CollectionAssert.AreEqual(new[] { "c1" }, client.GetContainerLogsCalls);
    }

    [TestMethod]
    public async Task GetContainerLogsAsync_ContainerMissing_ThrowsContainerNotFoundExceptionAndDoesNotCallRuntimeLogs()
    {
        // Arrange
        var client = new FakeContainerRuntimeClient { Containers = [] };
        var sut = new ContainerManagementService(client);

        // Act & Assert
        await Assert.ThrowsExactlyAsync<ContainerNotFoundException>(() => sut.GetContainerLogsAsync("missing"));
        Assert.IsEmpty(client.GetContainerLogsCalls);
    }

    [TestMethod]
    public async Task FollowContainerLogsAsync_ContainerExists_StreamsRuntimeLines()
    {
        // Arrange
        var client = new FakeContainerRuntimeClient
        {
            Containers = [CreateContainer("c1", ContainerState.Running)],
            FollowContainerLogsAsyncFunc = (containerId, cancellationToken) => CreateLinesAsync("line-1", "line-2"),
        };
        var sut = new ContainerManagementService(client);
        var actual = new List<string>();

        // Act
        await foreach (var line in sut.FollowContainerLogsAsync("c1"))
        {
            actual.Add(line);
        }

        // Assert
        CollectionAssert.AreEqual(new[] { "line-1", "line-2" }, actual);
        CollectionAssert.AreEqual(new[] { "c1" }, client.FollowContainerLogsCalls);
    }

    [TestMethod]
    public async Task FollowContainerLogsAsync_ContainerMissing_ThrowsContainerNotFoundExceptionWhenEnumeratedAndDoesNotCallRuntimeFollow()
    {
        // Arrange
        var client = new FakeContainerRuntimeClient { Containers = [] };
        var sut = new ContainerManagementService(client);

        // Act & Assert
        await Assert.ThrowsExactlyAsync<ContainerNotFoundException>(async () =>
        {
            await foreach (var _ in sut.FollowContainerLogsAsync("missing"))
            {
            }
        });
        Assert.IsEmpty(client.FollowContainerLogsCalls);
    }

    private static async IAsyncEnumerable<string> CreateLinesAsync(params string[] lines)
    {
        foreach (var line in lines)
        {
            yield return line;
        }
    }
}
