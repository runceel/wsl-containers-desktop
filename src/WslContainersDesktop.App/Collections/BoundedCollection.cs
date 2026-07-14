// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace WslContainersDesktop_App.Collections;

/// <summary>
/// 表示・一時停止バッファ・ディスパッチ待ちキューに共通する、要素数上限（capacity）を
/// 超えないよう最も古い要素を取り除いてから追加する操作を提供する。
/// </summary>
/// <remarks>
/// <see cref="WslContainersDesktop_App.ViewModels.ContainerLogsViewModel"/>（表示行・一時停止バッファ）と
/// <see cref="WslContainersDesktop_App.ViewModels.ContainerShellViewModel"/>（シェル出力・シェルの
/// ディスパッチ待ちキュー）の両方が同一のeviction規則を必要とするため、ここに共通実装を置く。
/// </remarks>
public static class BoundedCollection
{
    /// <summary>
    /// <paramref name="collection"/> の末尾に <paramref name="item"/> を追加する。
    /// 追加前に要素数が <paramref name="capacity"/> に達している場合は先頭（最も古い要素）を
    /// 取り除いてから追加することで、常に最新 <paramref name="capacity"/> 件を保つ。
    /// </summary>
    /// <typeparam name="T">要素の型。</typeparam>
    /// <param name="collection">追加先のコレクション。</param>
    /// <param name="item">追加する要素。</param>
    /// <param name="capacity">保持する要素数の上限。</param>
    public static void AppendBounded<T>(IList<T> collection, T item, int capacity)
    {
        if (collection.Count >= capacity)
        {
            collection.RemoveAt(0);
        }

        collection.Add(item);
    }

    /// <summary>
    /// <paramref name="queue"/> に <paramref name="item"/> を追加する。
    /// 追加前に要素数が <paramref name="capacity"/> に達している場合は先頭（最も古い要素）を
    /// 取り除いてから追加することで、常に最新 <paramref name="capacity"/> 件を保つ。
    /// </summary>
    /// <typeparam name="T">要素の型。</typeparam>
    /// <param name="queue">追加先のキュー。</param>
    /// <param name="item">追加する要素。</param>
    /// <param name="capacity">保持する要素数の上限。</param>
    public static void EnqueueBounded<T>(Queue<T> queue, T item, int capacity)
    {
        if (queue.Count >= capacity)
        {
            queue.Dequeue();
        }

        queue.Enqueue(item);
    }
}
