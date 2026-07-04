using System.Runtime.CompilerServices;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Infrastructure.Cli;

namespace WslContainersDesktop.Infrastructure.Tests.Fakes;

/// <summary>
/// <see cref="IWslcCliRunner"/> のテスト用フェイク実装。
/// 呼び出された引数を記録し、あらかじめ設定した <see cref="CliResult"/> を返す。
/// </summary>
internal sealed class FakeWslcCliRunner : IWslcCliRunner
{
    public CliResult Result { get; set; } = new(0, string.Empty, string.Empty);

    public Func<IReadOnlyList<string>, CancellationToken, Task<CliResult>>? RunAsyncFunc { get; set; }

    public Func<IReadOnlyList<string>, CancellationToken, IAsyncEnumerable<string>>? StreamLinesAsyncFunc { get; set; }

    public Exception? StreamLinesException { get; set; }

    public IContainerExecSession? ExecSession { get; set; }

    public List<IReadOnlyList<string>> Calls { get; } = [];

    public List<IReadOnlyList<string>> StreamCalls { get; } = [];

    public List<IReadOnlyList<string>> OpenInteractiveCalls { get; } = [];

    public Task<CliResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        Calls.Add(arguments);
        if (RunAsyncFunc is not null)
        {
            return RunAsyncFunc(arguments, cancellationToken);
        }

        return Task.FromResult(Result);
    }

    public async IAsyncEnumerable<string> StreamLinesAsync(IReadOnlyList<string> arguments, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        StreamCalls.Add(arguments);
        if (StreamLinesException is not null)
        {
            throw StreamLinesException;
        }

        if (StreamLinesAsyncFunc is not null)
        {
            await foreach (var line in StreamLinesAsyncFunc(arguments, cancellationToken))
            {
                yield return line;
            }

            yield break;
        }

        foreach (var line in Result.StandardOutput.Split(Environment.NewLine, StringSplitOptions.None))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            if (line.Length > 0)
            {
                yield return line;
            }
        }
    }

    public Task<IContainerExecSession> OpenInteractiveAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        OpenInteractiveCalls.Add(arguments);
        return Task.FromResult(ExecSession!);
    }
}
