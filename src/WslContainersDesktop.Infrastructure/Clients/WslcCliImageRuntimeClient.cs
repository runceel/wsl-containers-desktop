using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;
using WslContainersDesktop.Infrastructure.Cli;

namespace WslContainersDesktop.Infrastructure.Clients;

/// <summary>
/// <c>wslc</c> CLIを利用してイメージランタイムポートを実装する。
/// </summary>
public sealed class WslcCliImageRuntimeClient(IWslcCliRunner cliRunner) : IImageRuntimeClient
{
    private readonly WslcCliCommandExecutor _executor = new(cliRunner);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ContainerImage>> ListImagesAsync(CancellationToken cancellationToken = default)
    {
        var result = await _executor.RunAsync(["image", "list", "--format", "json", "--no-trunc"], cancellationToken);

        var items = WslcCliCommandExecutor.DeserializeJsonList<ImageListItemDto>(
            result,
            command: "image list --format json --no-trunc",
            failureMessage: "コンテナーイメージ一覧の解析に失敗しました。");
        if (items is null)
        {
            return [];
        }

        return items
            .Select(item => new ContainerImage(
                Id: item.Id,
                Repository: item.Repository,
                Tag: item.Tag,
                SizeBytes: item.Size,
                CreatedAt: DateTimeOffset.FromUnixTimeSeconds(item.Created)))
            .ToList();
    }

    /// <inheritdoc/>
    public Task PullImageAsync(string imageReference, CancellationToken cancellationToken = default)
    {
        return _executor.RunAsync(["pull", imageReference], cancellationToken);
    }

    /// <inheritdoc/>
    public Task DeleteImageAsync(string imageId, CancellationToken cancellationToken = default)
    {
        return _executor.RunAsync(["image", "remove", imageId], cancellationToken);
    }
}
