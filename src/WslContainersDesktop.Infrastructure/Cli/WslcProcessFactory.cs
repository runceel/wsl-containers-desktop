namespace WslContainersDesktop.Infrastructure.Cli;

/// <summary>
/// 実際のOSプロセスを起動する <see cref="IWslcProcessFactory"/>。
/// </summary>
public sealed class WslcProcessFactory : IWslcProcessFactory
{
    public IWslcProcess Create(string executablePath, IReadOnlyList<string> arguments)
    {
        return new WslcProcess(executablePath, arguments);
    }

    public IWslcInteractiveProcess CreateInteractive(string executablePath, IReadOnlyList<string> arguments)
    {
        return new WslcInteractiveProcess(executablePath, arguments);
    }
}
