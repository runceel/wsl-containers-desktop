namespace WslContainersDesktop.Infrastructure.Clients;

/// <summary>
/// 項目一覧を境界を設けた並行実行（bounded concurrency）で検査（inspect）するための
/// 共通ヘルパー。ボリューム・ネットワークの検査で、指定した並列数を超えずに
/// 一覧の並び順を保ったまま結果を返す。
/// </summary>
internal static class BoundedConcurrencyInspector
{
    public static async Task<IReadOnlyList<TResult>> InspectAsync<TItem, TResult>(
        IReadOnlyList<TItem> items,
        Func<TItem, CancellationToken, Task<TResult>> inspectAsync,
        int concurrencyLimit,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return [];
        }

        var semaphore = new SemaphoreSlim(concurrencyLimit);
        var results = new TResult[items.Count];
        var tasks = items.Select((item, index) => InspectItemAsync(item, index, results, inspectAsync, semaphore, cancellationToken));
        await Task.WhenAll(tasks);
        return results;
    }

    private static async Task InspectItemAsync<TItem, TResult>(
        TItem item,
        int index,
        TResult[] results,
        Func<TItem, CancellationToken, Task<TResult>> inspectAsync,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            results[index] = await inspectAsync(item, cancellationToken);
        }
        finally
        {
            semaphore.Release();
        }
    }
}
