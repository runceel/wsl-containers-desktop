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
    public async Task GetStatsAsync_RuntimeClientReturnsUsages_ReturnsSameUsages()
    {
        // Arrange
        var u1 = new ContainerResourceUsage("c1", "n1", 12.5, 1000, 2000);
        var u2 = new ContainerResourceUsage("c2", "n2", 0, 500, 0);
        var client = new FakeContainerRuntimeClient { Stats = [u1, u2] };
        var sut = new ContainerManagementService(client);

        // Act
        var actual = await sut.GetStatsAsync();

        // Assert
        Assert.HasCount(2, actual);
        Assert.AreEqual(u1, actual[0]);
        Assert.AreEqual(u2, actual[1]);
        Assert.HasCount(1, client.GetContainerStatsCalls);
    }

    [TestMethod]
    public async Task GetStatsAsync_RuntimeClientThrows_PropagatesException()
    {
        // Arrange
        var client = new FakeContainerRuntimeClient { GetContainerStatsException = new ContainerRuntimeException("stats", 1, "boom") };
        var sut = new ContainerManagementService(client);

        // Act & Assert
        await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.GetStatsAsync());
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

    [TestMethod]
    public async Task RunAsync_RequestHasImageReference_CallsRuntimeRunContainerWithNormalizedRequestAndDoesNotListContainers()
    {
        // Arrange
        var client = new FakeContainerRuntimeClient();
        var sut = new ContainerManagementService(client);
        var request = new ContainerRunRequest
        {
            ImageReference = " ubuntu:latest ",
            ContainerName = " web ",
            RemoveWhenStopped = true,
            PortMappings = [" 8080:80 ", " ", " 8443:443"],
            EnvironmentVariables = [" FOO=bar ", " "],
            Command = " echo hi ",
        };

        // Act
        await sut.RunAsync(request);

        // Assert
        Assert.HasCount(1, client.RunContainerCalls);
        var actual = client.RunContainerCalls[0];
        Assert.AreEqual("ubuntu:latest", actual.ImageReference);
        Assert.AreEqual("web", actual.ContainerName);
        Assert.IsTrue(actual.RemoveWhenStopped);
        CollectionAssert.AreEqual(new[] { "8080:80", "8443:443" }, actual.PortMappings.ToList());
        CollectionAssert.AreEqual(new[] { "FOO=bar" }, actual.EnvironmentVariables.ToList());
        Assert.AreEqual("echo hi", actual.Command);
        Assert.AreEqual(0, client.ListContainersCallCount);
    }

    [TestMethod]
    public async Task RunAsync_ImageReferenceIsWhiteSpace_ThrowsArgumentExceptionAndDoesNotCallRuntime()
    {
        // Arrange
        var client = new FakeContainerRuntimeClient();
        var sut = new ContainerManagementService(client);
        var request = new ContainerRunRequest { ImageReference = "   " };

        // Act & Assert
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => sut.RunAsync(request));
        Assert.IsEmpty(client.RunContainerCalls);
    }

    [TestMethod]
    public async Task RunAsync_CommandIsWhiteSpace_NormalizesCommandToEmpty()
    {
        // Arrange
        var client = new FakeContainerRuntimeClient();
        var sut = new ContainerManagementService(client);
        var request = new ContainerRunRequest { ImageReference = "hello-world", Command = "   " };

        // Act
        await sut.RunAsync(request);

        // Assert
        Assert.AreEqual(string.Empty, client.RunContainerCalls[0].Command);
    }

    [TestMethod]
    public async Task RunAsync_RuntimeThrows_PropagatesException()
    {
        // Arrange
        var runtimeException = new InvalidOperationException("boom");
        var client = new FakeContainerRuntimeClient { RunContainerException = runtimeException };
        var sut = new ContainerManagementService(client);
        var request = new ContainerRunRequest { ImageReference = "hello-world" };

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => sut.RunAsync(request));
        Assert.AreSame(runtimeException, ex);
    }

    [TestMethod]
    public async Task GetContainerDetailAsync_ContainerExists_ReturnsRuntimeDetailAndCallsRuntimeDetail()
    {
        // Arrange
        var expected = CreateDetail("c1", ContainerState.Running);
        var client = new FakeContainerRuntimeClient
        {
            Containers = [CreateContainer("c1", ContainerState.Running)],
            ContainerDetail = expected,
        };
        var sut = new ContainerManagementService(client);

        // Act
        var actual = await sut.GetContainerDetailAsync("c1");

        // Assert
        Assert.AreSame(expected, actual);
        CollectionAssert.AreEqual(new[] { "c1" }, client.GetContainerDetailCalls);
    }

    [TestMethod]
    public async Task GetContainerDetailAsync_ContainerMissing_ThrowsContainerNotFoundExceptionAndDoesNotCallRuntimeDetail()
    {
        // Arrange
        var client = new FakeContainerRuntimeClient { Containers = [] };
        var sut = new ContainerManagementService(client);

        // Act & Assert
        await Assert.ThrowsExactlyAsync<ContainerNotFoundException>(() => sut.GetContainerDetailAsync("missing"));
        Assert.IsEmpty(client.GetContainerDetailCalls);
    }

    [TestMethod]
    public async Task OpenExecSessionAsync_RunningContainer_ReturnsRuntimeSessionAndCallsRuntimeExec()
    {
        // Arrange
        var expected = new FakeContainerExecSession();
        var client = new FakeContainerRuntimeClient
        {
            Containers = [CreateContainer("c1", ContainerState.Running)],
            ExecSession = expected,
        };
        var sut = new ContainerManagementService(client);

        // Act
        var actual = await sut.OpenExecSessionAsync("c1");

        // Assert
        Assert.AreSame(expected, actual);
        CollectionAssert.AreEqual(new[] { "c1" }, client.OpenExecSessionCalls);
    }

    [TestMethod]
    public async Task OpenExecSessionAsync_StoppedContainer_ThrowsInvalidContainerOperationExceptionAndDoesNotCallRuntimeExec()
    {
        // Arrange
        var client = new FakeContainerRuntimeClient { Containers = [CreateContainer("c1", ContainerState.Stopped)] };
        var sut = new ContainerManagementService(client);

        // Act & Assert
        await Assert.ThrowsExactlyAsync<InvalidContainerOperationException>(() => sut.OpenExecSessionAsync("c1"));
        Assert.IsEmpty(client.OpenExecSessionCalls);
    }

    private static async IAsyncEnumerable<string> CreateLinesAsync(params string[] lines)
    {
        foreach (var line in lines)
        {
            yield return line;
        }
    }

    private static ContainerDetail CreateDetail(string id, ContainerState state) => new(
        Id: id,
        Name: $"name-{id}",
        Image: "nginx:latest",
        State: state,
        CreatedAt: new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.Zero),
        Command: "sleep infinity",
        Entrypoint: null,
        Ports: [],
        Environment: [],
        Mounts: [],
        Networks: [],
        RunState: new ContainerRunState(null, null, null, null));

    private sealed class FakeContainerExecSession : IContainerExecSession
    {
        public bool IsClosed { get; private set; }

        public async IAsyncEnumerable<string> ReadOutputAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task SendCommandAsync(string command, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            IsClosed = true;
            return Task.CompletedTask;
        }
    }
}
