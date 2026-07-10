using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using WslContainersDesktop.Infrastructure.Cli;

namespace WslContainersDesktop.Infrastructure.Tests.Cli;

[TestClass]
public sealed class CliProcessExecutorTests
{
    [TestMethod]
    public async Task WaitForExitAsync_WhenProcessCompletesOnStart_CompletesFirstWaitImmediatelyAndOnlyMarksPostKillFlagForSecondWait()
    {
        // Arrange
        var process = new FakeCliProcess(completeWaitForExitOnStart: true);

        // Act
        process.Start();

        var firstWaitTask = process.WaitForExitAsync(CancellationToken.None);
        await Task.WhenAny(firstWaitTask, Task.Delay(TimeSpan.FromMilliseconds(200)));

        // Assert
        Assert.IsTrue(firstWaitTask.IsCompleted);
        Assert.IsFalse(process.PostKillWaitForExitCompleted);

        process.KillEntireProcessTree();

        var secondWaitTask = process.WaitForExitAsync(CancellationToken.None);
        await Task.WhenAny(secondWaitTask, Task.Delay(TimeSpan.FromMilliseconds(200)));

        Assert.IsTrue(secondWaitTask.IsCompleted);
        Assert.IsTrue(process.PostKillWaitForExitCompleted);
    }

    [TestMethod]
    public async Task ExecuteAsync_CancellationAfterRootExitWhileReadersPending_KillsProcessWaitsForReadersAndRethrowsOperationCanceledException()
    {
        // Arrange
        var process = new FakeCliProcess(completeWaitForExitOnStart: true);
        var executor = new CliProcessExecutor(_ => process);
        var startInfo = new ProcessStartInfo("cmd.exe") { Arguments = "/c timeout /t 30" };
        using var cts = new CancellationTokenSource();
        var executeTask = executor.ExecuteAsync(startInfo, cts.Token);

        await process.WaitForExitObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await process.ReadersStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Act
        cts.Cancel();

        // Assert
        var exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await executeTask);
        Assert.AreEqual(cts.Token, exception.CancellationToken);
        Assert.IsTrue(process.KillEntireProcessTreeCalled);
        Assert.IsTrue(process.PostKillWaitForExitCompleted);
        Assert.IsTrue(process.WaitForExitCancellationObserved);
        Assert.IsTrue(process.StandardOutputReadCancelled);
        Assert.IsTrue(process.StandardErrorReadCancelled);
        Assert.IsTrue(process.DisposeCalled);

        var sequence = process.EventLog.ToArray();
        Assert.IsGreaterThanOrEqualTo(0, Array.IndexOf(sequence, "canceled"));
        Assert.IsGreaterThanOrEqualTo(0, Array.IndexOf(sequence, "kill"));
        Assert.IsGreaterThanOrEqualTo(0, Array.IndexOf(sequence, "output-cancelled"));
        Assert.IsGreaterThanOrEqualTo(0, Array.IndexOf(sequence, "error-cancelled"));
        Assert.IsGreaterThanOrEqualTo(0, Array.IndexOf(sequence, "dispose"));
        Assert.IsGreaterThanOrEqualTo(0, Array.IndexOf(sequence, "wait-for-exit-1"));
        Assert.IsGreaterThanOrEqualTo(0, Array.IndexOf(sequence, "wait-for-exit-2"));
        Assert.IsLessThan(Array.IndexOf(sequence, "wait-for-exit-2"), Array.IndexOf(sequence, "kill"));
        Assert.IsLessThan(Array.IndexOf(sequence, "dispose"), Array.IndexOf(sequence, "output-cancelled"));
        Assert.IsLessThan(Array.IndexOf(sequence, "dispose"), Array.IndexOf(sequence, "error-cancelled"));
    }

    [TestMethod]
    public async Task ExecuteAsync_CancellationWhileProcessRuns_KillsEntireProcessTreeAwaitsExitAndRethrowsOperationCanceledException()
    {
        // Arrange
        var process = new FakeCliProcess();
        var executor = new CliProcessExecutor(_ => process);
        var startInfo = new ProcessStartInfo("cmd.exe") { Arguments = "/c timeout /t 30" };
        using var cts = new CancellationTokenSource();
        var executeTask = executor.ExecuteAsync(startInfo, cts.Token);

        await process.WaitForExitObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Act
        cts.Cancel();

        // Assert
        var exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await executeTask);
        Assert.AreEqual(cts.Token, exception.CancellationToken);
        Assert.IsTrue(process.KillEntireProcessTreeCalled);
        Assert.IsTrue(process.WaitForExitAsyncCalled);
        Assert.AreEqual(2, process.WaitForExitInvocationCount);
        CollectionAssert.AreEqual(new[] { 1, 2 }, process.WaitForExitInvocationOrder.ToArray());
        Assert.IsTrue(process.PostKillWaitForExitCompleted);
        Assert.IsTrue(process.WaitForExitCancellationObserved);
        Assert.IsTrue(process.StandardOutputReadCompleted);
        Assert.IsTrue(process.StandardErrorReadCompleted);
        Assert.IsTrue(process.DisposeCalled);

        var sequence = process.EventLog.ToArray();
        Assert.IsGreaterThanOrEqualTo(0, Array.IndexOf(sequence, "canceled"));
        Assert.IsGreaterThanOrEqualTo(0, Array.IndexOf(sequence, "kill"));
        Assert.IsGreaterThanOrEqualTo(0, Array.IndexOf(sequence, "output-completed"));
        Assert.IsGreaterThanOrEqualTo(0, Array.IndexOf(sequence, "error-completed"));
        Assert.IsGreaterThanOrEqualTo(0, Array.IndexOf(sequence, "dispose"));
        Assert.IsGreaterThanOrEqualTo(0, Array.IndexOf(sequence, "wait-for-exit-1"));
        Assert.IsGreaterThanOrEqualTo(0, Array.IndexOf(sequence, "wait-for-exit-2"));
        Assert.IsLessThan(Array.IndexOf(sequence, "kill"), Array.IndexOf(sequence, "canceled"));
        Assert.IsLessThan(Array.IndexOf(sequence, "kill"), Array.IndexOf(sequence, "wait-for-exit-1"));
        Assert.IsGreaterThan(Array.IndexOf(sequence, "kill"), Array.IndexOf(sequence, "wait-for-exit-2"));
        Assert.IsLessThan(Array.IndexOf(sequence, "dispose"), Array.IndexOf(sequence, "wait-for-exit-2"));
        Assert.IsLessThan(Array.IndexOf(sequence, "dispose"), Array.IndexOf(sequence, "output-completed"));
        Assert.IsLessThan(Array.IndexOf(sequence, "dispose"), Array.IndexOf(sequence, "error-completed"));
    }

    [TestMethod]
    public async Task ExecuteAsync_CancellationWhileProcessRuns_WhenKillEntireProcessTreeThrowsBecauseProcessAlreadyExited_StillAwaitsPostKillExitAndStreamsAndRethrowsOriginalOperationCanceledException()
    {
        // Arrange
        var process = new FakeCliProcess(killEntireProcessTreeException: new InvalidOperationException("process already exited"));
        var executor = new CliProcessExecutor(_ => process);
        var startInfo = new ProcessStartInfo("cmd.exe") { Arguments = "/c timeout /t 30" };
        using var cts = new CancellationTokenSource();
        var executeTask = executor.ExecuteAsync(startInfo, cts.Token);

        await process.WaitForExitObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Act
        cts.Cancel();

        // Assert
        var exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await executeTask);
        Assert.AreEqual(cts.Token, exception.CancellationToken);
        Assert.IsTrue(process.KillEntireProcessTreeCalled);
        Assert.IsTrue(process.HasExited);
        Assert.IsTrue(process.WaitForExitAsyncCalled);
        Assert.AreEqual(2, process.WaitForExitInvocationCount);
        CollectionAssert.AreEqual(new[] { 1, 2 }, process.WaitForExitInvocationOrder.ToArray());
        Assert.IsTrue(process.PostKillWaitForExitCompleted);
        Assert.IsTrue(process.WaitForExitCancellationObserved);
        Assert.IsTrue(process.StandardOutputReadCompleted);
        Assert.IsTrue(process.StandardErrorReadCompleted);
        Assert.IsTrue(process.DisposeCalled);

        var sequence = process.EventLog.ToArray();
        Assert.IsGreaterThanOrEqualTo(0, Array.IndexOf(sequence, "canceled"));
        Assert.IsGreaterThanOrEqualTo(0, Array.IndexOf(sequence, "kill"));
        Assert.IsGreaterThanOrEqualTo(0, Array.IndexOf(sequence, "output-completed"));
        Assert.IsGreaterThanOrEqualTo(0, Array.IndexOf(sequence, "error-completed"));
        Assert.IsGreaterThanOrEqualTo(0, Array.IndexOf(sequence, "dispose"));
        Assert.IsGreaterThanOrEqualTo(0, Array.IndexOf(sequence, "wait-for-exit-1"));
        Assert.IsGreaterThanOrEqualTo(0, Array.IndexOf(sequence, "wait-for-exit-2"));
        Assert.IsLessThan(Array.IndexOf(sequence, "kill"), Array.IndexOf(sequence, "canceled"));
        Assert.IsLessThan(Array.IndexOf(sequence, "kill"), Array.IndexOf(sequence, "wait-for-exit-1"));
        Assert.IsGreaterThan(Array.IndexOf(sequence, "kill"), Array.IndexOf(sequence, "wait-for-exit-2"));
        Assert.IsLessThan(Array.IndexOf(sequence, "dispose"), Array.IndexOf(sequence, "wait-for-exit-2"));
        Assert.IsLessThan(Array.IndexOf(sequence, "dispose"), Array.IndexOf(sequence, "output-completed"));
        Assert.IsLessThan(Array.IndexOf(sequence, "dispose"), Array.IndexOf(sequence, "error-completed"));
    }

    [TestMethod]
    public async Task ExecuteAsync_ProcessExitsNormally_ReturnsCapturedOutputAndExitCode()
    {
        // Arrange
        var process = new FakeCliProcess("hello-stdout", "hello-stderr", completeStreamsImmediately: true, completeWaitForExitOnStart: true)
        {
            ExitCode = 7,
        };
        var executor = new CliProcessExecutor(_ => process);
        var startInfo = new ProcessStartInfo("cmd.exe") { Arguments = "/c echo hello" };

        // Act
        var result = await executor.ExecuteAsync(startInfo);

        // Assert
        Assert.AreEqual(7, result.ExitCode);
        Assert.AreEqual("hello-stdout", result.StandardOutput);
        Assert.AreEqual("hello-stderr", result.StandardError);
        Assert.IsFalse(process.KillEntireProcessTreeCalled);
        Assert.IsTrue(process.DisposeCalled);
    }

    [TestMethod]
    public async Task ExecuteAsync_ProcessStartThrows_DisposesProcessWithoutKillAndPropagatesException()
    {
        // Arrange
        var process = new FakeCliProcess(startException: new InvalidOperationException("boom"));
        var executor = new CliProcessExecutor(_ => process);
        var startInfo = new ProcessStartInfo("cmd.exe") { Arguments = "/c echo hello" };

        // Act
        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await executor.ExecuteAsync(startInfo));

        // Assert
        Assert.AreEqual("boom", exception.Message);
        Assert.IsFalse(process.KillEntireProcessTreeCalled);
        Assert.IsTrue(process.DisposeCalled);
    }

    private sealed class FakeCliProcess : ICliProcess
    {
        private readonly TaskCompletionSource<string> _standardOutputReadTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<string> _standardErrorReadTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<TaskCompletionSource<object?>> _waitForExitTcs = new();

        public FakeCliProcess(string standardOutput = "", string standardError = "", Exception? startException = null, bool completeStreamsImmediately = false, bool completeWaitForExitOnStart = false, Exception? killEntireProcessTreeException = null)
        {
            StandardOutput = standardOutput;
            StandardError = standardError;
            StartException = startException;
            CompleteWaitForExitOnStart = completeWaitForExitOnStart;
            KillEntireProcessTreeException = killEntireProcessTreeException;
            if (completeStreamsImmediately)
            {
                CompleteStreams();
            }
        }

        public string StandardOutput { get; }

        public string StandardError { get; }

        public Exception? StartException { get; }

        public bool CompleteWaitForExitOnStart { get; }

        public Exception? KillEntireProcessTreeException { get; }

        public int ExitCode { get; set; }

        public bool KillEntireProcessTreeCalled { get; private set; }

        public bool ProcessExited { get; private set; }

        public bool HasExited { get; private set; }

        public bool DisposeCalled { get; private set; }

        public bool WaitForExitAsyncCalled { get; private set; }

        public int WaitForExitInvocationCount { get; private set; }

        public List<int> WaitForExitInvocationOrder { get; } = new();

        public bool PostKillWaitForExitCompleted { get; private set; }

        public bool WaitForExitCancellationObserved { get; private set; }

        public bool StandardOutputReadCompleted { get; private set; }

        public bool StandardErrorReadCompleted { get; private set; }

        public bool StandardOutputReadCancelled { get; private set; }

        public bool StandardErrorReadCancelled { get; private set; }

        public TaskCompletionSource<bool> ReadersStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> WaitForExitObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ConcurrentQueue<string> EventLog { get; } = new();

        public void Start()
        {
            EventLog.Enqueue("start");
            if (StartException is not null)
            {
                throw StartException;
            }

            if (CompleteWaitForExitOnStart)
            {
                ProcessExited = true;
                HasExited = true;
                CompleteWaitForExit();
            }
        }

        public Task<string> ReadStandardOutputAsync(CancellationToken cancellationToken)
        {
            EventLog.Enqueue("read-output");
            ReadersStarted.TrySetResult(true);

            cancellationToken.Register(() =>
            {
                StandardOutputReadCompleted = true;
                StandardOutputReadCancelled = true;
                EventLog.Enqueue("output-cancelled");
                _standardOutputReadTcs.TrySetCanceled(cancellationToken);
            });

            return _standardOutputReadTcs.Task;
        }

        public Task<string> ReadStandardErrorAsync(CancellationToken cancellationToken)
        {
            EventLog.Enqueue("read-error");
            ReadersStarted.TrySetResult(true);

            cancellationToken.Register(() =>
            {
                StandardErrorReadCompleted = true;
                StandardErrorReadCancelled = true;
                EventLog.Enqueue("error-cancelled");
                _standardErrorReadTcs.TrySetCanceled(cancellationToken);
            });

            return _standardErrorReadTcs.Task;
        }

        public Task WaitForExitAsync(CancellationToken cancellationToken)
        {
            WaitForExitAsyncCalled = true;
            WaitForExitInvocationCount += 1;
            WaitForExitInvocationOrder.Add(WaitForExitInvocationCount);
            EventLog.Enqueue($"wait-for-exit-{WaitForExitInvocationCount}");
            WaitForExitObserved.TrySetResult(true);

            var waitForExitTcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _waitForExitTcs.Add(waitForExitTcs);

            if (ProcessExited && (WaitForExitInvocationCount >= 2 || CompleteWaitForExitOnStart))
            {
                if (WaitForExitInvocationCount >= 2)
                {
                    PostKillWaitForExitCompleted = true;
                }

                waitForExitTcs.TrySetResult(null);
            }

            cancellationToken.Register(() =>
            {
                WaitForExitCancellationObserved = true;
                EventLog.Enqueue("canceled");
                if (WaitForExitInvocationCount == 1)
                {
                    waitForExitTcs.TrySetException(new OperationCanceledException(cancellationToken));
                }
            });

            return waitForExitTcs.Task;
        }

        public void CompleteWaitForExit()
        {
            EventLog.Enqueue("exit");
            ProcessExited = true;
            HasExited = true;
            if (_waitForExitTcs.Count > 0)
            {
                _waitForExitTcs[0].TrySetResult(null);
                _waitForExitTcs.RemoveAt(0);
            }
        }

        public void KillEntireProcessTree()
        {
            KillEntireProcessTreeCalled = true;
            EventLog.Enqueue("kill");
            ProcessExited = true;
            HasExited = true;
            CompleteStreams();
            if (KillEntireProcessTreeException is not null)
            {
                throw KillEntireProcessTreeException;
            }
        }

        public void Dispose()
        {
            DisposeCalled = true;
            EventLog.Enqueue("dispose");
        }

        private void CompleteStreams()
        {
            _standardOutputReadTcs.TrySetResult(StandardOutput);
            StandardOutputReadCompleted = true;
            EventLog.Enqueue("output-completed");
            _standardErrorReadTcs.TrySetResult(StandardError);
            StandardErrorReadCompleted = true;
            EventLog.Enqueue("error-completed");
        }
    }
}
