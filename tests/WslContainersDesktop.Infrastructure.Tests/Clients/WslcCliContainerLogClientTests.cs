using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Infrastructure.Cli;
using WslContainersDesktop.Infrastructure.Clients;
using WslContainersDesktop.Infrastructure.Tests.Fakes;

namespace WslContainersDesktop.Infrastructure.Tests.Clients;

[TestClass]
public sealed class WslcCliContainerLogClientTests
{
    [TestMethod]
    public async Task GetContainerLogsAsync_CliReturnsLinesWithTrailingNewline_ReturnsLinesWithoutTrailingPhantomEmpty()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, "one\ntwo\n", string.Empty) };
        var sut = new WslcCliContainerLogClient(runner);

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
        var sut = new WslcCliContainerLogClient(runner);

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
        var sut = new WslcCliContainerLogClient(runner);

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
        var sut = new WslcCliContainerLogClient(runner);

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
        var sut = new WslcCliContainerLogClient(runner);

        // Act
        await sut.GetContainerLogsAsync("c1");

        // Assert
        // GetContainerLogsAsyncは単一のストリーミング呼び出しのみを行う（RunAsyncは呼ばれない）。
        Assert.IsEmpty(runner.Calls);
        CollectionAssert.AreEqual(new[] { "container", "logs", "c1" }, runner.StreamCalls[0].ToList());
    }

    [TestMethod]
    public async Task GetContainerLogsAsync_CliReturnsStdErrWithExitZero_IncludesStdErrAsLogContent()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, "one\n", "two\n") };
        var sut = new WslcCliContainerLogClient(runner);

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
        var sut = new WslcCliContainerLogClient(runner);

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.GetContainerLogsAsync("c1"));
        Assert.AreEqual(1, ex.ExitCode);
    }

    [TestMethod]
    public async Task FollowContainerLogsAsync_CliArguments_AreContainerLogsSinceFollowWithId()
    {
        // Arrange
        var runner = new FakeWslcCliRunner();
        var sut = new WslcCliContainerLogClient(runner);

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
        var sut = new WslcCliContainerLogClient(runner);
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
        var sut = new WslcCliContainerLogClient(runner);

        // Act & Assert
        await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(async () =>
        {
            await foreach (var _ in sut.FollowContainerLogsAsync("c1"))
            {
            }
        });
    }

    private static async IAsyncEnumerable<string> CreateLinesAsync(params string[] lines)
    {
        foreach (var line in lines)
        {
            yield return line;
        }
    }
}
