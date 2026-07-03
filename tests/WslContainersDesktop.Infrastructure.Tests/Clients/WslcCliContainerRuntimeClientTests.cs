using System.Runtime.CompilerServices;
using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Domain;
using WslContainersDesktop.Infrastructure.Cli;
using WslContainersDesktop.Infrastructure.Clients;
using WslContainersDesktop.Infrastructure.Tests.Fakes;

namespace WslContainersDesktop.Infrastructure.Tests.Clients;

[TestClass]
public sealed class WslcCliContainerRuntimeClientTests
{
    [TestMethod]
    public async Task ListContainersAsync_CliReturnsContainers_MapsJsonToContainers()
    {
        // Arrange
        // 実機の `wslc list -a --format json` で確認したスキーマに基づく。
        const string json = """
            [
              {
                "CreatedAt": 1751446800,
                "Id": "sha256:aaa",
                "Image": "nginx:latest",
                "Name": "web",
                "Ports": [],
                "State": 2,
                "StateChangedAt": 1751446801
              },
              {
                "CreatedAt": 1751443200,
                "Id": "sha256:bbb",
                "Image": "alpine:latest",
                "Name": "wcd-test",
                "Ports": [],
                "State": 3,
                "StateChangedAt": 1751443260
              }
            ]
            """;
        var runner = new FakeWslcCliRunner { Result = new(0, json, string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        var containers = await sut.ListContainersAsync();

        // Assert
        Assert.HasCount(2, containers);
        Assert.AreEqual("sha256:aaa", containers[0].Id);
        Assert.AreEqual("web", containers[0].Name);
        Assert.AreEqual("nginx:latest", containers[0].Image);
        Assert.AreEqual(ContainerState.Running, containers[0].State);
        Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(1751446800), containers[0].CreatedAt);

        Assert.AreEqual("sha256:bbb", containers[1].Id);
        Assert.AreEqual(ContainerState.Stopped, containers[1].State);
    }

    [TestMethod]
    public async Task ListContainersAsync_CliReturnsEmptyArray_ReturnsEmptyList()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, "[]", string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        var containers = await sut.ListContainersAsync();

        // Assert
        Assert.IsEmpty(containers);
    }

    [TestMethod]
    public async Task ListContainersAsync_CliArguments_AreListAllFormatJson()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, "[]", string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        await sut.ListContainersAsync();

        // Assert
        CollectionAssert.AreEqual(new[] { "list", "-a", "--format", "json" }, runner.Calls[0].ToList());
    }

    [TestMethod]
    public async Task ListContainersAsync_CliExitsWithNonZeroCode_ThrowsContainerRuntimeExceptionWithStandardError()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(1, string.Empty, "一覧の取得に失敗しました。") };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.ListContainersAsync());
        Assert.AreEqual(1, ex.ExitCode);
        StringAssert.Contains(ex.Message, "一覧の取得に失敗しました。");
    }

    [TestMethod]
    public async Task ListContainersAsync_CliReturnsMalformedJson_ThrowsContainerRuntimeExceptionWithInnerException()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, "not-json", string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.ListContainersAsync());
        Assert.IsNotNull(ex.InnerException);
    }

    [TestMethod]
    public async Task GetContainerLogsAsync_CliReturnsLinesWithTrailingNewline_ReturnsLinesWithoutTrailingPhantomEmpty()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, "one\ntwo\n", string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        var logs = await sut.GetContainerLogsAsync("c1");

        // Assert
        CollectionAssert.AreEqual(new[] { "one", "two" }, logs.ToList());
    }

    [TestMethod]
    public async Task GetContainerLogsAsync_CliOutputWithoutTrailingNewline_KeepsLastLine()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, "one\ntwo", string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        var logs = await sut.GetContainerLogsAsync("c1");

        // Assert
        CollectionAssert.AreEqual(new[] { "one", "two" }, logs.ToList());
    }

    [TestMethod]
    public async Task GetContainerLogsAsync_CliReturnsCrLf_TrimsCarriageReturns()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, "one\r\ntwo\r\n", string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        var logs = await sut.GetContainerLogsAsync("c1");

        // Assert
        CollectionAssert.AreEqual(new[] { "one", "two" }, logs.ToList());
    }

    [TestMethod]
    public async Task GetContainerLogsAsync_CliReturnsEmptyOutput_ReturnsEmptyList()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, string.Empty, string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        var logs = await sut.GetContainerLogsAsync("c1");

        // Assert
        Assert.IsEmpty(logs);
    }

    [TestMethod]
    public async Task GetContainerLogsAsync_CliArguments_AreContainerLogsWithId()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, string.Empty, string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        await sut.GetContainerLogsAsync("c1");

        // Assert
        CollectionAssert.AreEqual(new[] { "container", "logs", "c1" }, runner.Calls[0].ToList());
    }

    [TestMethod]
    public async Task GetContainerLogsAsync_CliReturnsStdErrWithExitZero_IncludesStdErrAsLogContent()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, "one\n", "two\n") };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        var logs = await sut.GetContainerLogsAsync("c1");

        // Assert
        CollectionAssert.AreEqual(new[] { "one", "two" }, logs.ToList());
    }

    [TestMethod]
    public async Task GetContainerLogsAsync_CliExitsWithNonZero_ThrowsContainerRuntimeException()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(1, string.Empty, "失敗しました。") };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.GetContainerLogsAsync("c1"));
        Assert.AreEqual(1, ex.ExitCode);
    }

    [TestMethod]
    public async Task FollowContainerLogsAsync_CliArguments_AreContainerLogsSinceFollowWithId()
    {
        // Arrange
        var runner = new FakeWslcCliRunner();
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        await foreach (var _ in sut.FollowContainerLogsAsync("c1"))
        {
        }

        // Assert
        var arguments = runner.StreamCalls[0].ToList();
        CollectionAssert.AreEqual(new[] { "container", "logs", "--since" }, arguments.Take(3).ToList());
        Assert.IsTrue(long.TryParse(arguments[3], out _));
        CollectionAssert.AreEqual(new[] { "--follow", "c1" }, arguments.Skip(4).ToList());
    }

    [TestMethod]
    public async Task FollowContainerLogsAsync_CliStreamsLines_ReturnsLinesInArrivalOrder()
    {
        // Arrange
        var runner = new FakeWslcCliRunner();
        runner.StreamLinesAsyncFunc = (arguments, cancellationToken) => CreateLinesAsync("one", "two");
        var sut = new WslcCliContainerRuntimeClient(runner);
        var actual = new List<string>();

        // Act
        await foreach (var line in sut.FollowContainerLogsAsync("c1"))
        {
            actual.Add(line);
        }

        // Assert
        CollectionAssert.AreEqual(new[] { "one", "two" }, actual);
    }

    [TestMethod]
    public async Task FollowContainerLogsAsync_CliStreamExitsNonZero_ThrowsContainerRuntimeException()
    {
        // Arrange
        var runner = new FakeWslcCliRunner();
        runner.StreamLinesException = new CliStreamException("container logs --since 1783057407 --follow c1", 1, "追跡に失敗しました。");
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act & Assert
        await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(async () =>
        {
            await foreach (var _ in sut.FollowContainerLogsAsync("c1"))
            {
            }
        });
    }

    [TestMethod]
    public async Task StartAsync_CliSucceeds_CompletesWithoutException()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, string.Empty, string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        await sut.StartAsync("c1");

        // Assert
        CollectionAssert.AreEqual(new[] { "container", "start", "c1" }, runner.Calls[0].ToList());
    }

    [TestMethod]
    public async Task StartAsync_CliExitsWithNonZeroCode_ThrowsContainerRuntimeException()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(1, string.Empty, "起動に失敗しました。") };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.StartAsync("c1"));
        StringAssert.Contains(ex.Message, "起動に失敗しました。");
    }

    [TestMethod]
    public async Task StopAsync_CliSucceeds_CallsContainerStopWithId()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, string.Empty, string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        await sut.StopAsync("c1");

        // Assert
        CollectionAssert.AreEqual(new[] { "container", "stop", "c1" }, runner.Calls[0].ToList());
    }

    [TestMethod]
    public async Task StopAsync_CliExitsWithNonZeroCode_ThrowsContainerRuntimeException()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(1, string.Empty, "停止に失敗しました。") };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act & Assert
        await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.StopAsync("c1"));
    }

    [TestMethod]
    public async Task DeleteAsync_CliSucceeds_CallsContainerRemoveWithIdWithoutForceFlag()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, string.Empty, string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        await sut.DeleteAsync("c1");

        // Assert
        // -f を付けないことで、実行中コンテナの削除をwslc自体に拒否させる
        // （ADR-0009）。
        CollectionAssert.AreEqual(new[] { "container", "remove", "c1" }, runner.Calls[0].ToList());
    }

    [TestMethod]
    public async Task DeleteAsync_CliExitsWithNonZeroCode_ThrowsContainerRuntimeExceptionWithStandardError()
    {
        // Arrange
        var runner = new FakeWslcCliRunner
        {
            Result = new(1, string.Empty, "エラー コード: WSLC_E_CONTAINER_IS_RUNNING"),
        };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.DeleteAsync("c1"));
        StringAssert.Contains(ex.Message, "WSLC_E_CONTAINER_IS_RUNNING");
    }

    private static async IAsyncEnumerable<string> CreateLinesAsync(params string[] lines)
    {
        foreach (var line in lines)
        {
            yield return line;
        }
    }
}
