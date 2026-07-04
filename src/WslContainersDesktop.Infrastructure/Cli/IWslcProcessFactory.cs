namespace WslContainersDesktop.Infrastructure.Cli;

/// <summary>
/// <see cref="IWslcProcess"/> を作成するファクトリ。
/// </summary>
public interface IWslcProcessFactory
{
    /// <summary>
    /// 指定された実行ファイルと引数でプロセス抽象を作成する。
    /// </summary>
    /// <param name="executablePath">実行ファイルパス。</param>
    /// <param name="arguments">コマンドライン引数。</param>
    IWslcProcess Create(string executablePath, IReadOnlyList<string> arguments);

    /// <summary>
    /// 指定された実行ファイルと引数で対話プロセス抽象を作成する。
    /// </summary>
    /// <param name="executablePath">実行ファイルパス。</param>
    /// <param name="arguments">コマンドライン引数。</param>
    IWslcInteractiveProcess CreateInteractive(string executablePath, IReadOnlyList<string> arguments);
}
