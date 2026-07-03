using WslContainersDesktop.Infrastructure.Cli;

namespace WslContainersDesktop.Infrastructure.Tests.Fakes;

/// <summary>
/// <see cref="IWslcCliRunner"/> のテスト用フェイク実装。
/// 呼び出された引数を記録し、あらかじめ設定した <see cref="CliResult"/> を返す。
/// </summary>
internal sealed class FakeWslcCliRunner : IWslcCliRunner
{
    public CliResult Result { get; set; } = new(0, string.Empty, string.Empty);

    public List<IReadOnlyList<string>> Calls { get; } = [];

    public Task<CliResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        Calls.Add(arguments);
        return Task.FromResult(Result);
    }
}
