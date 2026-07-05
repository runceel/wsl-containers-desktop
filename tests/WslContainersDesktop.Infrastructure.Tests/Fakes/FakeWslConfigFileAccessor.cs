using WslContainersDesktop.Infrastructure.Settings;

namespace WslContainersDesktop.Infrastructure.Tests.Fakes;

/// <summary>
/// <see cref="IWslConfigFileAccessor"/> のテスト用フェイク実装。
/// 読み取り内容と書き込み内容を記録し、任意の例外をスローできる。
/// </summary>
internal sealed class FakeWslConfigFileAccessor : IWslConfigFileAccessor
{
    public string? Content { get; set; }

    public string? WrittenContent { get; private set; }

    public int WriteCalls { get; private set; }

    public Exception? ReadException { get; set; }

    public Exception? WriteException { get; set; }

    public Task<string?> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (ReadException is not null)
        {
            throw ReadException;
        }

        return Task.FromResult(Content);
    }

    public Task WriteAsync(string content, CancellationToken cancellationToken = default)
    {
        WriteCalls++;
        if (WriteException is not null)
        {
            throw WriteException;
        }

        WrittenContent = content;
        Content = content;
        return Task.CompletedTask;
    }
}
