namespace WslContainersDesktop.Infrastructure.Wsl;

/// <summary>
/// 環境変数 <c>PATH</c> 上に <c>wslc.exe</c> が存在するかどうかで可用性を判定する
/// <see cref="IWslcExecutableProbe"/> の実装。ロジックを持たない薄いアダプターであり、
/// 単体テストの対象外とする。
/// </summary>
public sealed class WslcExecutableProbe : IWslcExecutableProbe
{
    private const string ExecutableName = "wslc.exe";

    /// <inheritdoc />
    public bool IsAvailable()
    {
        var pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVariable))
        {
            return false;
        }

        var directories = pathVariable.Split(
            Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var directory in directories)
        {
            try
            {
                if (File.Exists(Path.Combine(directory, ExecutableName)))
                {
                    return true;
                }
            }
            catch (ArgumentException)
            {
                // PATH に不正な文字を含むエントリがあっても他のエントリの探索を継続する。
            }
        }

        return false;
    }
}
