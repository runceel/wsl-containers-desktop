using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Application.Services;

/// <summary>
/// <see cref="IImageManagementService"/> の実装。
/// </summary>
public sealed class ImageManagementService(IContainerRuntimeClient runtimeClient) : IImageManagementService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<ContainerImage>> GetImagesAsync(CancellationToken cancellationToken = default)
    {
        return runtimeClient.ListImagesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task PullAsync(string imageReference, CancellationToken cancellationToken = default)
    {
        var trimmed = ValidateNotWhiteSpace(imageReference, nameof(imageReference));
        return runtimeClient.PullImageAsync(trimmed, cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteAsync(string imageId, CancellationToken cancellationToken = default)
    {
        var trimmed = ValidateNotWhiteSpace(imageId, nameof(imageId));
        return runtimeClient.DeleteImageAsync(trimmed, cancellationToken);
    }

    private static string ValidateNotWhiteSpace(string value, string parameterName)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("値を空白だけにすることはできません。", parameterName);
        }

        return trimmed;
    }
}
