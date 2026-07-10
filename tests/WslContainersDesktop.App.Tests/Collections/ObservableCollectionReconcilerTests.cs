using System.Collections.ObjectModel;
using System.Collections.Specialized;
using WslContainersDesktop_App.Collections;

namespace WslContainersDesktop_App_Tests.Collections;

[TestClass]
public sealed class ObservableCollectionReconcilerTests
{
    private sealed class Item(string key, int value)
    {
        public string Key { get; } = key;

        public int Value { get; set; } = value;
    }

    private static void Reconcile(
        ObservableCollection<Item> target,
        IReadOnlyList<Item> source,
        Func<Item, string> targetKeySelector,
        Func<Item, Item> create,
        Action<Item, Item>? update = null) =>
        ObservableCollectionReconciler.Reconcile(
            target,
            source,
            targetKeySelector: targetKeySelector,
            sourceKeySelector: static item => item.Key,
            create: create,
            update: update);

    private static void Reconcile(ObservableCollection<Item> target, IReadOnlyList<Item> source, bool applyUpdate = true) =>
        Reconcile(
            target,
            source,
            static item => item.Key,
            static item => new Item(item.Key, item.Value),
            applyUpdate ? static (item, source) => item.Value = source.Value : null);

    private static List<NotifyCollectionChangedAction> RecordActions(ObservableCollection<Item> target)
    {
        var actions = new List<NotifyCollectionChangedAction>();
        target.CollectionChanged += (_, e) => actions.Add(e.Action);
        return actions;
    }

    [TestMethod]
    public void Reconcile_EmptyTargetNonEmptySource_AddsAllInSourceOrder()
    {
        // Arrange
        var target = new ObservableCollection<Item>();
        var source = new[] { new Item("a", 1), new Item("b", 2), new Item("c", 3) };

        // Act
        Reconcile(target, source);

        // Assert
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, target.Select(i => i.Key).ToArray());
    }

    [TestMethod]
    public void Reconcile_IdenticalKeysSameOrder_PreservesAllInstances()
    {
        // Arrange
        var target = new ObservableCollection<Item> { new("a", 1), new("b", 2), new("c", 3) };
        var originals = target.ToArray();
        var source = new[] { new Item("a", 1), new Item("b", 2), new Item("c", 3) };

        // Act
        Reconcile(target, source);

        // Assert
        Assert.AreSame(originals[0], target[0]);
        Assert.AreSame(originals[1], target[1]);
        Assert.AreSame(originals[2], target[2]);
    }

    [TestMethod]
    public void Reconcile_IdenticalContent_RaisesNoCollectionChanged()
    {
        // Arrange
        var target = new ObservableCollection<Item> { new("a", 1), new("b", 2), new("c", 3) };
        var actions = RecordActions(target);
        var source = new[] { new Item("a", 1), new Item("b", 2), new Item("c", 3) };

        // Act
        Reconcile(target, source);

        // Assert
        Assert.IsEmpty(actions);
    }

    [TestMethod]
    public void Reconcile_MatchingKeyDifferentValue_UpdateAppliedInPlaceSameInstance()
    {
        // Arrange
        var target = new ObservableCollection<Item> { new("a", 1) };
        var original = target[0];
        var actions = RecordActions(target);
        var source = new[] { new Item("a", 42) };

        // Act
        Reconcile(target, source);

        // Assert
        Assert.AreSame(original, target[0]);
        Assert.AreEqual(42, target[0].Value);
        Assert.IsEmpty(actions);
    }

    [TestMethod]
    public void Reconcile_NewKeyInMiddle_InsertsAtCorrectPositionPreservingOthers()
    {
        // Arrange
        var target = new ObservableCollection<Item> { new("a", 1), new("c", 3) };
        var originalA = target[0];
        var originalC = target[1];
        var actions = RecordActions(target);
        var source = new[] { new Item("a", 1), new Item("b", 2), new Item("c", 3) };

        // Act
        Reconcile(target, source);

        // Assert
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, target.Select(i => i.Key).ToArray());
        Assert.AreSame(originalA, target[0]);
        Assert.AreSame(originalC, target[2]);
        Assert.AreEqual(1, actions.Count(a => a == NotifyCollectionChangedAction.Add));
    }

    [TestMethod]
    public void Reconcile_KeyRemovedFromMiddle_RemovesItAndPreservesOthers()
    {
        // Arrange
        var target = new ObservableCollection<Item> { new("a", 1), new("b", 2), new("c", 3) };
        var originalA = target[0];
        var originalC = target[2];
        var actions = RecordActions(target);
        var source = new[] { new Item("a", 1), new Item("c", 3) };

        // Act
        Reconcile(target, source);

        // Assert
        CollectionAssert.AreEqual(new[] { "a", "c" }, target.Select(i => i.Key).ToArray());
        Assert.AreSame(originalA, target[0]);
        Assert.AreSame(originalC, target[1]);
        Assert.AreEqual(1, actions.Count(a => a == NotifyCollectionChangedAction.Remove));
    }

    [TestMethod]
    public void Reconcile_SourceReordered_ReflectsNewOrderPreservingInstances()
    {
        // Arrange
        var target = new ObservableCollection<Item> { new("a", 1), new("b", 2), new("c", 3) };
        var originalA = target[0];
        var originalB = target[1];
        var originalC = target[2];
        var actions = RecordActions(target);
        var source = new[] { new Item("c", 3), new Item("a", 1), new Item("b", 2) };

        // Act
        Reconcile(target, source);

        // Assert
        CollectionAssert.AreEqual(new[] { "c", "a", "b" }, target.Select(i => i.Key).ToArray());
        Assert.AreSame(originalC, target[0]);
        Assert.AreSame(originalA, target[1]);
        Assert.AreSame(originalB, target[2]);
        CollectionAssert.DoesNotContain(actions, NotifyCollectionChangedAction.Reset, "リセットを発生させてはならない。");
        CollectionAssert.DoesNotContain(actions, NotifyCollectionChangedAction.Add, "並び替えで追加を発生させてはならない。");
        CollectionAssert.DoesNotContain(actions, NotifyCollectionChangedAction.Remove, "並び替えで削除を発生させてはならない。");
        CollectionAssert.Contains(actions, NotifyCollectionChangedAction.Move, "並び替えはMoveで反映されるべき。");
    }

    [TestMethod]
    public void Reconcile_SourceEmpty_ClearsAllRows()
    {
        // Arrange
        var target = new ObservableCollection<Item> { new("a", 1), new("b", 2) };
        var actions = RecordActions(target);

        // Act
        Reconcile(target, []);

        // Assert
        Assert.IsEmpty(target);
        CollectionAssert.DoesNotContain(actions, NotifyCollectionChangedAction.Reset, "Clearによるリセットを使ってはならない。");
    }

    [TestMethod]
    public void Reconcile_AddedAtEnd_RaisesSingleAdd()
    {
        // Arrange
        var target = new ObservableCollection<Item> { new("a", 1) };
        var originalA = target[0];
        var actions = RecordActions(target);
        var source = new[] { new Item("a", 1), new Item("b", 2) };

        // Act
        Reconcile(target, source);

        // Assert
        CollectionAssert.AreEqual(new[] { "a", "b" }, target.Select(i => i.Key).ToArray());
        Assert.AreSame(originalA, target[0]);
        Assert.AreEqual(1, actions.Count(a => a == NotifyCollectionChangedAction.Add));
    }

    [TestMethod]
    public void Reconcile_NullUpdate_MatchingKeysNotMutated()
    {
        // Arrange
        var target = new ObservableCollection<Item> { new("a", 1) };
        var original = target[0];
        var source = new[] { new Item("a", 99) };

        // Act
        Reconcile(target, source, applyUpdate: false);

        // Assert
        Assert.AreSame(original, target[0]);
        Assert.AreEqual(1, target[0].Value);
    }

    [TestMethod]
    public void Reconcile_RemoveInsertAndReorderCombined_ProducesSourceStateAndPreservesSurvivors()
    {
        // Arrange
        var target = new ObservableCollection<Item> { new("a", 1), new("b", 2), new("c", 3), new("d", 4) };
        var originalB = target[1];
        var originalD = target[3];
        var source = new[] { new Item("d", 4), new Item("b", 2), new Item("e", 5) };

        // Act
        Reconcile(target, source);

        // Assert
        CollectionAssert.AreEqual(new[] { "d", "b", "e" }, target.Select(i => i.Key).ToArray());
        Assert.AreSame(originalD, target[0]);
        Assert.AreSame(originalB, target[1]);
    }

    [TestMethod]
    public void Reconcile_InsertedAtBeginning_FollowingMatchesRemainInSourceOrder()
    {
        // Arrange
        var target = new ObservableCollection<Item> { new("a", 1), new("x", 2), new("y", 3) };
        var originalA = target[0];
        var originalX = target[1];
        var originalY = target[2];
        var actions = RecordActions(target);
        var source = new[] { new Item("z", 4), new Item("a", 10), new Item("x", 20), new Item("y", 30) };

        // Act
        Reconcile(target, source);

        // Assert
        CollectionAssert.AreEqual(new[] { "z", "a", "x", "y" }, target.Select(i => i.Key).ToArray());
        Assert.AreSame(originalA, target[1]);
        Assert.AreSame(originalX, target[2]);
        Assert.AreSame(originalY, target[3]);
        Assert.AreEqual(10, target[1].Value);
        Assert.AreEqual(20, target[2].Value);
        Assert.AreEqual(30, target[3].Value);
        Assert.AreEqual(1, actions.Count(a => a == NotifyCollectionChangedAction.Add));
        CollectionAssert.DoesNotContain(actions, NotifyCollectionChangedAction.Move, "挿入時はMoveを発生させてはならない。" );
    }

    [TestMethod]
    public void Reconcile_LargeLeadingInsertBlock_DoesNotRescanTarget()
    {
        // Arrange
        const int size = 500;
        var target = new ObservableCollection<Item>();
        for (var i = 0; i < size; i++)
        {
            target.Add(new Item($"old-{i}", i));
        }

        for (var i = 0; i < size; i++)
        {
            target.Add(new Item($"obsolete-{i}", i));
        }

        var originalOld0 = target[0];
        var source = new List<Item>();
        for (var i = 0; i < size; i++)
        {
            source.Add(new Item($"new-{i}", i));
        }

        for (var i = 0; i < size; i++)
        {
            source.Add(new Item($"old-{i}", i + size));
        }

        var targetInitialCount = target.Count;
        var sourceCount = source.Count;
        var targetKeySelectionCount = 0;
        Func<Item, string> targetKeySelector = item =>
        {
            targetKeySelectionCount++;
            return item.Key;
        };

        // Act
        Reconcile(
            target,
            source,
            targetKeySelector,
            static item => new Item(item.Key, item.Value),
            static (item, source) => item.Value = source.Value);

        // Assert
        var expectedKeys = Enumerable.Range(0, size)
            .Select(i => $"new-{i}")
            .Concat(Enumerable.Range(0, size).Select(i => $"old-{i}"))
            .ToArray();
        CollectionAssert.AreEqual(expectedKeys, target.Select(i => i.Key).ToArray());
        Assert.AreSame(originalOld0, target[size]);
        Assert.IsLessThanOrEqualTo(
            5 * (targetInitialCount + sourceCount),
            targetKeySelectionCount,
            $"target key selection count {targetKeySelectionCount} exceeded threshold.");
    }

    [TestMethod]
    public void Reconcile_LargeReorder_DoesNotRescanTarget()
    {
        // Arrange
        const int size = 500;
        var target = new ObservableCollection<Item>();
        for (var i = 0; i < size; i++)
        {
            target.Add(new Item($"old-{i}", i));
        }

        var originalOld0 = target[0];
        var source = new List<Item>();
        for (var i = size - 1; i >= 0; i--)
        {
            source.Add(new Item($"old-{i}", i));
        }

        var targetInitialCount = target.Count;
        var sourceCount = source.Count;
        var targetKeySelectionCount = 0;
        Func<Item, string> targetKeySelector = item =>
        {
            targetKeySelectionCount++;
            return item.Key;
        };

        // Act
        Reconcile(
            target,
            source,
            targetKeySelector,
            static item => new Item(item.Key, item.Value),
            static (item, source) => item.Value = source.Value);

        // Assert
        var expectedKeys = Enumerable.Range(0, size).Select(i => $"old-{size - 1 - i}").ToArray();
        CollectionAssert.AreEqual(expectedKeys, target.Select(i => i.Key).ToArray());
        Assert.AreSame(originalOld0, target[size - 1]);
        Assert.IsLessThanOrEqualTo(
            5 * (targetInitialCount + sourceCount),
            targetKeySelectionCount,
            $"target key selection count {targetKeySelectionCount} exceeded threshold.");
    }

    [TestMethod]
    public void Reconcile_InsertThenMoveExistingItem_ProducesSourceOrder()
    {
        // Arrange
        var target = new ObservableCollection<Item> { new("a", 1), new("b", 2), new("c", 3), new("d", 4), new("e", 5) };
        var originalB = target[1];
        var originalC = target[2];
        var originalD = target[3];
        var originalE = target[4];
        var actions = RecordActions(target);
        var source = new[] { new Item("z", 6), new Item("e", 7), new Item("b", 8), new Item("c", 9), new Item("d", 10) };

        // Act
        Reconcile(target, source);

        // Assert
        CollectionAssert.AreEqual(new[] { "z", "e", "b", "c", "d" }, target.Select(i => i.Key).ToArray());
        Assert.AreSame(originalE, target[1]);
        Assert.AreSame(originalB, target[2]);
        Assert.AreSame(originalC, target[3]);
        Assert.AreSame(originalD, target[4]);
        CollectionAssert.Contains(actions, NotifyCollectionChangedAction.Add, "挿入はAddとして反映されるべき。" );
        CollectionAssert.Contains(actions, NotifyCollectionChangedAction.Move, "既存項目の並び替えはMoveとして反映されるべき。" );
        CollectionAssert.DoesNotContain(actions, NotifyCollectionChangedAction.Reset, "挿入と移動ではResetを発生させてはならない。" );
    }

    [TestMethod]
    public void Reconcile_DuplicateSourceKeys_ThrowsArgumentException()
    {
        // Arrange
        var target = new ObservableCollection<Item>();
        var source = new[] { new Item("a", 1), new Item("a", 2) };

        // Act & Assert
        Assert.ThrowsExactly<ArgumentException>(() => Reconcile(target, source));
    }
}
