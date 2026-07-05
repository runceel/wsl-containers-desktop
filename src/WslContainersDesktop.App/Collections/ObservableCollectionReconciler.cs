// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;

namespace WslContainersDesktop_App.Collections;

/// <summary>
/// <see cref="ObservableCollection{T}"/> を、キー一致に基づいて差分更新するためのヘルパー。
/// </summary>
/// <remarks>
/// <see cref="ObservableCollection{T}.Clear"/> による全項目リセットを避け、
/// 変更のない行インスタンスを維持したまま追加・削除・並び替え・インプレース更新のみを行う。
/// これにより <c>ListView</c> の全体再構築（ちらつき・選択状態や途中状態の喪失）を防ぐ。
/// キーはソース内で一意であることを前提とする。
/// </remarks>
public static class ObservableCollectionReconciler
{
    /// <summary>
    /// <paramref name="target"/> の内容を <paramref name="source"/> に一致するよう差分更新する。
    /// </summary>
    /// <typeparam name="TItem">対象コレクションの要素型。</typeparam>
    /// <typeparam name="TSource">ソースの要素型。</typeparam>
    /// <typeparam name="TKey">一致判定に用いるキーの型。</typeparam>
    /// <param name="target">差分更新される対象のコレクション。</param>
    /// <param name="source">あるべき状態を表すソース。</param>
    /// <param name="targetKeySelector">対象要素からキーを取り出す関数。</param>
    /// <param name="sourceKeySelector">ソース要素からキーを取り出す関数。</param>
    /// <param name="create">ソース要素から新しい対象要素を生成する関数。</param>
    /// <param name="update">
    /// キーが一致した既存要素をインプレース更新する関数。省略時は何もしない。
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="source"/> に重複するキーが含まれる場合。</exception>
    public static void Reconcile<TItem, TSource, TKey>(
        ObservableCollection<TItem> target,
        IReadOnlyList<TSource> source,
        Func<TItem, TKey> targetKeySelector,
        Func<TSource, TKey> sourceKeySelector,
        Func<TSource, TItem> create,
        Action<TItem, TSource>? update = null)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(targetKeySelector);
        ArgumentNullException.ThrowIfNull(sourceKeySelector);
        ArgumentNullException.ThrowIfNull(create);

        var sourceKeys = new HashSet<TKey>(source.Count);
        foreach (var sourceItem in source)
        {
            if (!sourceKeys.Add(sourceKeySelector(sourceItem)))
            {
                throw new ArgumentException("ソースに重複するキーが含まれています。", nameof(source));
            }
        }

        // Pass 1: ソースに存在しないキーの要素を末尾から削除する。
        for (var i = target.Count - 1; i >= 0; i--)
        {
            if (!sourceKeys.Contains(targetKeySelector(target[i])))
            {
                target.RemoveAt(i);
            }
        }

        // Pass 2: ソース順に走査し、対象コレクションを整列させる。
        for (var i = 0; i < source.Count; i++)
        {
            var sourceItem = source[i];
            var key = sourceKeySelector(sourceItem);

            var matchIndex = -1;
            for (var j = i; j < target.Count; j++)
            {
                if (EqualityComparer<TKey>.Default.Equals(targetKeySelector(target[j]), key))
                {
                    matchIndex = j;
                    break;
                }
            }

            if (matchIndex < 0)
            {
                target.Insert(i, create(sourceItem));
                continue;
            }

            if (matchIndex != i)
            {
                target.Move(matchIndex, i);
            }

            update?.Invoke(target[i], sourceItem);
        }
    }
}
