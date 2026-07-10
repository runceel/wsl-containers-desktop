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
/// キーは対象（<c>target</c>）・ソース（<c>source</c>）ともに一意であることを
/// 契約上の前提とする。ソース側の重複は検出して <see cref="ArgumentException"/> を投げるが、
/// 対象側の重複は検出しない。この前提が破られた場合の動作は未定義（どの要素が
/// 生き残るか、並び順や更新結果がどうなるかは保証されない）であり、呼び出し側が
/// 事前に保証する責務を負う。
/// </remarks>
public static class ObservableCollectionReconciler
{
    /// <summary>
    /// <paramref name="target"/> の内容を <paramref name="source"/> に一致するよう差分更新する。
    /// </summary>
    /// <typeparam name="TItem">対象コレクションの要素型。</typeparam>
    /// <typeparam name="TSource">ソースの要素型。</typeparam>
    /// <typeparam name="TKey">一致判定に用いるキーの型。</typeparam>
    /// <param name="target">
    /// 差分更新される対象のコレクション。キーが一意であることが呼び出し側の責務であり、
    /// 重複がある場合の動作は未定義。
    /// </param>
    /// <param name="source">あるべき状態を表すソース。キーの一意性はこのメソッドが検証する。</param>
    /// <param name="targetKeySelector">対象要素からキーを取り出す関数。</param>
    /// <param name="sourceKeySelector">ソース要素からキーを取り出す関数。</param>
    /// <param name="create">ソース要素から新しい対象要素を生成する関数。</param>
    /// <param name="update">
    /// キーが一致した既存要素をインプレース更新する関数。省略時は何もしない。
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="source"/> に重複するキーが含まれる場合。<paramref name="target"/> 側の
    /// 重複キーはこの検証の対象外であり、検出されない。
    /// </exception>
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

        var survivingNodes = new List<OrderNode<TKey>>(target.Count);
        var orderIndex = new OrderIndex<TKey>();

        // Pass 1: ソースに存在しないキーの要素を末尾から削除する。
        for (var i = target.Count - 1; i >= 0; i--)
        {
            var targetItem = target[i];
            var targetKey = targetKeySelector(targetItem);
            if (!sourceKeys.Contains(targetKey))
            {
                target.RemoveAt(i);
            }
            else
            {
                survivingNodes.Add(orderIndex.CreateNode(targetKey));
            }
        }

        survivingNodes.Reverse();
        foreach (var node in survivingNodes)
        {
            orderIndex.InsertAt(orderIndex.Count, node);
        }

        var nodesByKey = new Dictionary<TKey, OrderNode<TKey>>(survivingNodes.Count);
        foreach (var node in survivingNodes)
        {
            nodesByKey[node.Key] = node;
        }

        // Pass 2: ソース順に走査し、対象コレクションを整列させる。
        for (var i = 0; i < source.Count; i++)
        {
            var sourceItem = source[i];
            var key = sourceKeySelector(sourceItem);

            if (nodesByKey.TryGetValue(key, out var existingNode))
            {
                var matchIndex = orderIndex.GetIndex(existingNode);
                if (matchIndex != i)
                {
                    target.Move(matchIndex, i);
                    orderIndex.RemoveAt(matchIndex);
                    orderIndex.InsertAt(i, existingNode);
                }

                update?.Invoke(target[i], sourceItem);
            }
            else
            {
                var createdItem = create(sourceItem);
                target.Insert(i, createdItem);
                var createdNode = orderIndex.CreateNode(key);
                orderIndex.InsertAt(i, createdNode);
                nodesByKey[key] = createdNode;
            }
        }
    }

    /// <summary>
    /// キーの並び順を暗黙キー・トリープ（implicit-key treap）で管理し、
    /// 任意ノードの現在位置（<see cref="GetIndex"/>）の取得と、
    /// 位置指定での挿入・削除を償却 O(log n) で行うための内部インデックス。
    /// </summary>
    /// <remarks>
    /// 各 <see cref="OrderNode{TKey}"/> は対象コレクション内の1要素のキーと
    /// 現在の並び順（Left/Right/Parent と部分木サイズ Size）だけを保持する。
    /// 要素そのもの（<c>TItem</c>）は <c>target</c> 側で直接管理されるため、
    /// このインデックスは関与しない。
    /// </remarks>
    private sealed class OrderIndex<TKey>
        where TKey : notnull
    {
        private OrderNode<TKey>? root;
        private int nextPriority;

        public int Count => root?.Size ?? 0;

        public OrderNode<TKey> CreateNode(TKey key) => new(key, NextPriority());

        /// <summary>
        /// <paramref name="node"/> を単独ノードとして初期化し、現在の並びの
        /// <paramref name="index"/> 番目に挿入する。
        /// </summary>
        public void InsertAt(int index, OrderNode<TKey> node)
        {
            node.Left = null;
            node.Right = null;
            node.Parent = null;
            node.Size = 1;
            var (left, right) = Split(root, index);
            root = Merge(Merge(left, node), right);
        }

        /// <summary>
        /// <paramref name="node"/> の現在の並び順における位置（0始まり）を、
        /// 根までの親ポインタをたどって求める。
        /// </summary>
        public int GetIndex(OrderNode<TKey> node)
        {
            var current = node;
            var index = current.Left?.Size ?? 0;
            while (current.Parent is not null)
            {
                var parent = current.Parent;
                if (current == parent.Right)
                {
                    index += 1 + (parent.Left?.Size ?? 0);
                }

                current = parent;
            }

            return index;
        }

        /// <summary>
        /// <paramref name="index"/> 番目のノードを木から取り除く。
        /// </summary>
        /// <remarks>
        /// 取り除かれたノードの Left/Right/Parent/Size は未初期化のまま残る。
        /// 再利用する場合は <see cref="InsertAt"/> が挿入時に必ずリセットするため、
        /// 呼び出し側で個別にクリアする必要はない。
        /// </remarks>
        public void RemoveAt(int index)
        {
            var (left, right) = Split(root, index);
            var (_, rest) = Split(right, 1);
            root = Merge(left, rest);
        }

        /// <summary>
        /// 単調増加するカウンタに xorshift を適用し、衝突しにくく分布の良い
        /// 優先度を生成する。トリープの優先度に真の乱数を使わずとも
        /// 木の高さを O(log n) に保つのに十分なばらつきが得られる。
        /// </summary>
        private int NextPriority()
        {
            unchecked
            {
                var value = (uint)nextPriority++;
                value ^= value << 13;
                value ^= value >> 7;
                value ^= value << 17;
                return (int)value;
            }
        }

        /// <summary>
        /// <paramref name="left"/> の全要素が <paramref name="right"/> の全要素より
        /// 前に来るという順序を保ったまま2本の部分木を1本に統合する。
        /// </summary>
        private static OrderNode<TKey>? Merge(OrderNode<TKey>? left, OrderNode<TKey>? right)
        {
            if (left is null)
            {
                return right;
            }

            if (right is null)
            {
                return left;
            }

            if (left.Priority > right.Priority)
            {
                left.Right = Merge(left.Right, right);
                if (left.Right is not null)
                {
                    left.Right.Parent = left;
                }

                left.Parent = null;
                RecomputeSize(left);
                return left;
            }

            right.Left = Merge(left, right.Left);
            if (right.Left is not null)
            {
                right.Left.Parent = right;
            }

            right.Parent = null;
            RecomputeSize(right);
            return right;
        }

        /// <summary>
        /// <paramref name="node"/> が根である部分木を、先頭から <paramref name="index"/> 個の
        /// 要素からなる左側と、残りの要素からなる右側に分割する。
        /// 戻り値として返す両部分木の根の Parent は必ず <c>null</c> になる。
        /// </summary>
        private static (OrderNode<TKey>? Left, OrderNode<TKey>? Right) Split(OrderNode<TKey>? node, int index)
        {
            if (node is null)
            {
                return (null, null);
            }

            var leftSize = node.Left?.Size ?? 0;
            if (index <= leftSize)
            {
                var (left, right) = Split(node.Left, index);
                node.Left = right;
                if (node.Left is not null)
                {
                    node.Left.Parent = node;
                }

                node.Parent = null;
                RecomputeSize(node);
                return (left, node);
            }

            var (leftNode, rightNode) = Split(node.Right, index - leftSize - 1);
            node.Right = leftNode;
            if (node.Right is not null)
            {
                node.Right.Parent = node;
            }

            node.Parent = null;
            RecomputeSize(node);
            return (node, rightNode);
        }

        private static void RecomputeSize(OrderNode<TKey> node)
        {
            node.Size = 1 + (node.Left?.Size ?? 0) + (node.Right?.Size ?? 0);
        }
    }

    /// <summary>
    /// <see cref="OrderIndex{TKey}"/> が管理するトリープの1ノード。
    /// 要素の並び順とキーのみを保持し、対象コレクションの要素自体は持たない。
    /// </summary>
    private sealed class OrderNode<TKey>
        where TKey : notnull
    {
        public OrderNode(TKey key, int priority)
        {
            Key = key;
            Priority = priority;
        }

        public TKey Key { get; }

        public int Priority { get; }

        public OrderNode<TKey>? Left { get; set; }

        public OrderNode<TKey>? Right { get; set; }

        public OrderNode<TKey>? Parent { get; set; }

        public int Size { get; set; } = 1;
    }
}
