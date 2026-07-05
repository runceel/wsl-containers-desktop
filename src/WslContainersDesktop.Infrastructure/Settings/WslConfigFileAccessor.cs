namespace WslContainersDesktop.Infrastructure.Settings;

/// <summary>
/// <c>%USERPROFILE%\.wslconfig</c> を読み書きする <see cref="IWslConfigFileAccessor"/> の実装。
/// 書き込みは一時ファイルへ書き出してから置換することで原子的に行う。
/// ロジックを持たない薄いファイルアクセスアダプターであり、単体テストの対象外とする。
/// </summary>
public sealed class WslConfigFileAccessor : IWslConfigFileAccessor
{
    private readonly string _filePath;

    /// <summary>
    /// 既定のパス（<c>%USERPROFILE%\.wslconfig</c>）を対象に初期化する。
    /// </summary>
    public WslConfigFileAccessor()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".wslconfig"))
    {
    }

    /// <summary>
    /// 指定したパスを対象に初期化する。
    /// </summary>
    /// <param name="filePath">読み書き対象の <c>.wslconfig</c> パス。</param>
    public WslConfigFileAccessor(string filePath)
    {
        _filePath = filePath;
    }

    /// <inheritdoc />
    public async Task<string?> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        return await File.ReadAllTextAsync(_filePath, cancellationToken);
    }

    /// <inheritdoc />
    public async Task WriteAsync(string content, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(temporaryPath, content, cancellationToken);
        File.Move(temporaryPath, _filePath, overwrite: true);
    }
}
