using System.Runtime.CompilerServices;
using WslContainersDesktop.Infrastructure.Cli;

namespace WslContainersDesktop.Infrastructure.Tests.Cli;

[TestClass]
public sealed class WslcCliRunnerTests
{
    [TestMethod]
    public async Task RunAsync_CommandWritesToStandardOutput_ReturnsExitCodeZeroAndStandardOutput()
    {
        // Arrange
        // 実在のwslc.exeに依存せず、Windows標準のcmd.exeで代替してプロセス起動の
        // 仕組みそのものを検証する。
        var sut = new WslcCliRunner(executablePath: "cmd.exe");

        // Act
        var result = await sut.RunAsync(["/c", "echo hello-stdout"]);

        // Assert
        Assert.AreEqual(0, result.ExitCode);
        StringAssert.Contains(result.StandardOutput, "hello-stdout");
    }

    [TestMethod]
    public async Task RunAsync_CommandWritesToStandardError_StandardErrorIsCapturedSeparatelyFromStandardOutput()
    {
        // Arrange
        var sut = new WslcCliRunner(executablePath: "cmd.exe");

        // Act
        var result = await sut.RunAsync(["/c", "echo hello-stderr 1>&2"]);

        // Assert
        StringAssert.Contains(result.StandardError, "hello-stderr");
        StringAssert.DoesNotMatch(result.StandardOutput, new System.Text.RegularExpressions.Regex("hello-stderr"));
    }

    [TestMethod]
    public async Task RunAsync_CommandExitsWithNonZeroCode_ExitCodeIsReturned()
    {
        // Arrange
        var sut = new WslcCliRunner(executablePath: "cmd.exe");

        // Act
        var result = await sut.RunAsync(["/c", "exit 3"]);

        // Assert
        Assert.AreEqual(3, result.ExitCode);
    }

    [TestMethod]
    public void Constructor_NoExecutablePathSpecified_DefaultsToWslc()
    {
        // Arrange & Act
        var sut = new WslcCliRunner();

        // Assert
        Assert.AreEqual("wslc", sut.ExecutablePath);
    }

    [TestMethod]
    public async Task StreamLinesAsync_ProcessWritesStdOutAndStdErr_YieldsBothStreams()
    {
        // Arrange
        var process = new FakeWslcProcess(
            stdOutLines: ["hello-stdout", "world"],
            stdErrLines: ["hello-stderr"]);
        var sut = new WslcCliRunner(new FakeWslcProcessFactory(process));
        var lines = new List<string>();

        // Act
        await foreach (var line in sut.StreamLinesAsync(["container", "logs", "c1"]))
        {
            lines.Add(line);
        }

        // Assert
        CollectionAssert.AreEqual(new[] { "hello-stdout", "world", "hello-stderr" }, lines);
    }

    [TestMethod]
    public async Task StreamLinesAsync_EnumerationIsCanceled_KillsProcessAndDisposesIt()
    {
        // Arrange
        var process = new FakeWslcProcess(stdOutLines: ["hello"]);
        var sut = new WslcCliRunner(new FakeWslcProcessFactory(process));
        using var cts = new CancellationTokenSource();
        await using var enumerator = sut.StreamLinesAsync(["container", "logs", "c1"], cts.Token).GetAsyncEnumerator(cts.Token);

        // Act
        await enumerator.MoveNextAsync();
        cts.Cancel();

        // Assert
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await enumerator.MoveNextAsync());
        Assert.IsTrue(process.KillCalled);
        Assert.IsTrue(process.DisposeCalled);
    }

    [TestMethod]
    public async Task StreamLinesAsync_ProcessExitsNormally_CompletesEnumeration()
    {
        // Arrange
        var process = new FakeWslcProcess(stdOutLines: ["done"]);
        var sut = new WslcCliRunner(new FakeWslcProcessFactory(process));
        var lines = new List<string>();

        // Act
        await foreach (var line in sut.StreamLinesAsync(["container", "logs", "c1"]))
        {
            lines.Add(line);
        }

        // Assert
        CollectionAssert.AreEqual(new[] { "done" }, lines);
        Assert.IsTrue(process.DisposeCalled);
    }

    private sealed class FakeWslcProcessFactory(IWslcProcess process) : IWslcProcessFactory
    {
        public IWslcProcess Create(string executablePath, IReadOnlyList<string> arguments) => process;
    }

    private sealed class FakeWslcProcess(IReadOnlyList<string>? stdOutLines = null, IReadOnlyList<string>? stdErrLines = null) : IWslcProcess
    {
        public bool KillCalled { get; private set; }

        public bool DisposeCalled { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public async IAsyncEnumerable<string> ReadLinesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var line in stdOutLines ?? [])
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return line;
            }

            foreach (var line in stdErrLines ?? [])
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return line;
            }

            cancellationToken.ThrowIfCancellationRequested();
            await Task.CompletedTask;
        }

        public void Kill() => KillCalled = true;

        public void Dispose() => DisposeCalled = true;
    }
}
