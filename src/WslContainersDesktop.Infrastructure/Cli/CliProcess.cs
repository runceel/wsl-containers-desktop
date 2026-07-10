using System.Diagnostics;

namespace WslContainersDesktop.Infrastructure.Cli;

internal sealed class CliProcess : ICliProcess
{
    private readonly ProcessJob _job = new();
    private readonly Process _process;

    public CliProcess(ProcessStartInfo startInfo)
    {
        _process = new Process { StartInfo = startInfo };
    }

    public int ExitCode => _process.ExitCode;

    public bool HasExited => _process.HasExited;

    public void Start()
    {
        _process.Start();
        try
        {
            _job.Add(_process);
        }
        catch
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }

            throw;
        }
    }

    public Task<string> ReadStandardOutputAsync(CancellationToken cancellationToken)
    {
        return _process.StandardOutput.ReadToEndAsync(cancellationToken);
    }

    public Task<string> ReadStandardErrorAsync(CancellationToken cancellationToken)
    {
        return _process.StandardError.ReadToEndAsync(cancellationToken);
    }

    public Task WaitForExitAsync(CancellationToken cancellationToken)
    {
        return _process.WaitForExitAsync(cancellationToken);
    }

    public void KillEntireProcessTree()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        finally
        {
            _job.Dispose();
        }
    }

    public void Dispose()
    {
        _job.Dispose();
        _process.Dispose();
    }
}
