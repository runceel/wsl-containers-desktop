using WslContainersDesktop.Infrastructure.Cli;
using WslContainersDesktop.Infrastructure.Wsl;

namespace WslContainersDesktop.Infrastructure.Tests.Fakes;

/// <summary>
/// <see cref="IWslCommandRunner"/> のテスト用フェイク実装。
/// </summary>
internal sealed class FakeWslCommandRunner : IWslCommandRunner
{
    public CliResult Result { get; set; } = new(0, string.Empty, string.Empty);

    public Exception? RunException { get; set; }

    public List<IReadOnlyList<string>> Calls { get; } = [];

    public Task<CliResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        Calls.Add(arguments);
        if (RunException is not null)
        {
            throw RunException;
        }

        return Task.FromResult(Result);
    }
}
