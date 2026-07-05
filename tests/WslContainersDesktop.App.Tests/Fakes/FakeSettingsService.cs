using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop_App_Tests.Fakes;

internal sealed class FakeSettingsService : ISettingsService
{
    public WslIntegrationStatus Status { get; set; } = new(null, false, false);

    public WslResourceLimits Limits { get; set; } = WslResourceLimits.Defaults;

    public Exception? GetStatusException { get; set; }

    public Exception? GetLimitsException { get; set; }

    public Exception? SaveException { get; set; }

    public Exception? ResetException { get; set; }

    public TaskCompletionSource<bool>? SaveGate { get; set; }

    public List<WslResourceLimits> SaveCalls { get; } = [];

    public int ResetCalls { get; private set; }

    public int GetStatusCalls { get; private set; }

    public int GetLimitsCalls { get; private set; }

    public Task<WslIntegrationStatus> GetIntegrationStatusAsync(CancellationToken cancellationToken = default)
    {
        GetStatusCalls++;
        if (GetStatusException is not null)
        {
            throw GetStatusException;
        }

        return Task.FromResult(Status);
    }

    public Task<WslResourceLimits> GetResourceLimitsAsync(CancellationToken cancellationToken = default)
    {
        GetLimitsCalls++;
        if (GetLimitsException is not null)
        {
            throw GetLimitsException;
        }

        return Task.FromResult(Limits);
    }

    public async Task SaveResourceLimitsAsync(WslResourceLimits limits, CancellationToken cancellationToken = default)
    {
        SaveCalls.Add(limits);
        if (SaveGate is not null)
        {
            await SaveGate.Task;
        }

        if (SaveException is not null)
        {
            throw SaveException;
        }

        Limits = limits;
    }

    public Task ResetResourceLimitsAsync(CancellationToken cancellationToken = default)
    {
        ResetCalls++;
        if (ResetException is not null)
        {
            throw ResetException;
        }

        Limits = WslResourceLimits.Defaults;
        return Task.CompletedTask;
    }
}
