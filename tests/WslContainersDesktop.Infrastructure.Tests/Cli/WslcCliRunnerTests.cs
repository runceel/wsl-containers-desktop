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

    [TestMethod]
    public async Task OpenInteractiveAsync_CreatesStartsAndReturnsSession()
    {
        // Arrange
        var process = new FakeWslcInteractiveProcess();
        var factory = new FakeWslcProcessFactory(new FakeWslcProcess(), process);
        var sut = new WslcCliRunner(factory, executablePath: "wslc-test");

        // Act
        var session = await sut.OpenInteractiveAsync(["container", "exec", "-i", "c1", "/bin/sh"]);

        // Assert
        Assert.IsNotNull(session);
        Assert.IsTrue(process.StartCalled);
        Assert.AreEqual("wslc-test", factory.InteractiveExecutablePath);
        CollectionAssert.AreEqual(new[] { "container", "exec", "-i", "c1", "/bin/sh" }, factory.InteractiveArguments!.ToList());
    }

    [TestMethod]
    public async Task ContainerExecSession_SendCommandAsync_WritesLfTerminatedCommandAndFlushes()
    {
        // Arrange
        var process = new FakeWslcInteractiveProcess();
        var sut = new WslcCliRunner(new FakeWslcProcessFactory(new FakeWslcProcess(), process));
        var session = await sut.OpenInteractiveAsync(["container", "exec", "-i", "c1", "/bin/sh"]);

        // Act
        await session.SendCommandAsync("pwd");

        // Assert
        CollectionAssert.AreEqual(new[] { "pwd\n" }, process.InputWrites);
        Assert.IsTrue(process.FlushCalled);
    }

    [TestMethod]
    public async Task ContainerExecSession_ReadOutputAsync_ProcessOutputsChunks_YieldsChunksIncludingUnterminatedText()
    {
        // Arrange
        var process = new FakeWslcInteractiveProcess(outputChunks: ["partial"]);
        var sut = new WslcCliRunner(new FakeWslcProcessFactory(new FakeWslcProcess(), process));
        var session = await sut.OpenInteractiveAsync(["container", "exec", "-i", "c1", "/bin/sh"]);
        var chunks = new List<string>();

        // Act
        await foreach (var chunk in session.ReadOutputAsync())
        {
            chunks.Add(chunk);
        }

        // Assert
        CollectionAssert.AreEqual(new[] { "partial" }, chunks);
    }

    [TestMethod]
    public async Task ContainerExecSession_ProcessExitsWithNonZero_CompletesWithoutThrowingAndMarksClosed()
    {
        // Arrange
        var process = new FakeWslcInteractiveProcess(exitCode: 42);
        var sut = new WslcCliRunner(new FakeWslcProcessFactory(new FakeWslcProcess(), process));
        var session = await sut.OpenInteractiveAsync(["container", "exec", "-i", "c1", "/bin/sh"]);

        // Act
        await foreach (var _ in session.ReadOutputAsync())
        {
        }

        // Assert
        Assert.IsTrue(session.IsClosed);
    }

    [TestMethod]
    public async Task ContainerExecSession_CloseAsync_KillsAndDisposesInteractiveProcess()
    {
        // Arrange
        var process = new FakeWslcInteractiveProcess();
        var sut = new WslcCliRunner(new FakeWslcProcessFactory(new FakeWslcProcess(), process));
        var session = await sut.OpenInteractiveAsync(["container", "exec", "-i", "c1", "/bin/sh"]);

        // Act
        await session.CloseAsync();

        // Assert
        Assert.IsTrue(process.KillCalled);
        Assert.IsTrue(process.DisposeCalled);
        Assert.IsTrue(session.IsClosed);
    }

    private sealed class FakeWslcProcessFactory(IWslcProcess process, IWslcInteractiveProcess? interactiveProcess = null) : IWslcProcessFactory
    {
        public string? InteractiveExecutablePath { get; private set; }

        public IReadOnlyList<string>? InteractiveArguments { get; private set; }

        public IWslcProcess Create(string executablePath, IReadOnlyList<string> arguments) => process;

        public IWslcInteractiveProcess CreateInteractive(string executablePath, IReadOnlyList<string> arguments)
        {
            InteractiveExecutablePath = executablePath;
            InteractiveArguments = arguments;
            return interactiveProcess ?? new FakeWslcInteractiveProcess();
        }
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

    private sealed class FakeWslcInteractiveProcess(IReadOnlyList<string>? outputChunks = null, int exitCode = 0) : IWslcInteractiveProcess
    {
        public bool StartCalled { get; private set; }

        public bool FlushCalled { get; private set; }

        public bool KillCalled { get; private set; }

        public bool DisposeCalled { get; private set; }

        public bool HasExited { get; private set; }

        public int ExitCode { get; private set; } = exitCode;

        public List<string> InputWrites { get; } = [];

        public Task StartAsync(CancellationToken cancellationToken)
        {
            StartCalled = true;
            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<string> ReadOutputAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var chunk in outputChunks ?? [])
            {
                yield return chunk;
            }

            HasExited = true;
            await Task.CompletedTask;
        }

        public Task WriteInputAsync(string input, CancellationToken cancellationToken)
        {
            InputWrites.Add(input);
            return Task.CompletedTask;
        }

        public Task FlushInputAsync(CancellationToken cancellationToken)
        {
            FlushCalled = true;
            return Task.CompletedTask;
        }

        public void Kill()
        {
            KillCalled = true;
            HasExited = true;
        }

        public void Dispose() => DisposeCalled = true;
    }
}
