using System.Collections.Specialized;
using System.Threading.Channels;
using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;
using WslContainersDesktop_App.ViewModels;
using WslContainersDesktop_App_Tests.Fakes;

namespace WslContainersDesktop_App_Tests.ViewModels;

[TestClass]
public sealed class ContainersViewModelTests
{
    private static Container CreateContainer(string id, ContainerState state) => new(
        Id: id,
        Name: $"name-{id}",
        Image: "nginx:latest",
        State: state,
        CreatedAt: new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.Zero));

    [TestMethod]
    public async Task RefreshAsync_ServiceReturnsContainers_PopulatesRowsAndClearsErrorAndIsEmptyIsFalse()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            DefaultContainers = [CreateContainer("c1", ContainerState.Running), CreateContainer("c2", ContainerState.Stopped)],
        };
        var sut = new ContainersViewModel(service);

        // Act
        await sut.RefreshCommand.ExecuteAsync(null);

        // Assert
        Assert.HasCount(2, sut.Containers);
        Assert.AreEqual("c1", sut.Containers[0].Id);
        Assert.AreEqual(ContainerState.Running, sut.Containers[0].State);
        Assert.AreEqual("c2", sut.Containers[1].Id);
        Assert.IsFalse(sut.IsEmpty);
        Assert.IsNull(sut.ErrorMessage);
    }

    [TestMethod]
    public async Task RefreshAsync_ServiceReturnsEmptyList_ContainersIsEmptyAndIsEmptyIsTrue()
    {
        // Arrange
        var service = new FakeContainerManagementService { DefaultContainers = [] };
        var sut = new ContainersViewModel(service);

        // Act
        await sut.RefreshCommand.ExecuteAsync(null);

        // Assert
        Assert.IsEmpty(sut.Containers);
        Assert.IsTrue(sut.IsEmpty);
    }

    [TestMethod]
    public async Task RefreshAsync_ServiceThrowsOnSecondCall_ErrorMessageIsSetAndExistingContainersArePreserved()
    {
        // Arrange
        var service = new FakeContainerManagementService();
        service.GetContainersResults.Enqueue(() => Task.FromResult<IReadOnlyList<Container>>([CreateContainer("c1", ContainerState.Running)]));
        service.GetContainersResults.Enqueue(() => Task.FromException<IReadOnlyList<Container>>(new ContainerRuntimeException("list", 1, "一覧の取得に失敗しました。")));
        var sut = new ContainersViewModel(service);
        await sut.RefreshCommand.ExecuteAsync(null);

        // Act
        await sut.RefreshCommand.ExecuteAsync(null);

        // Assert
        Assert.AreEqual("一覧の取得に失敗しました。", sut.ErrorMessage);
        Assert.HasCount(1, sut.Containers);
        Assert.AreEqual("c1", sut.Containers[0].Id);
    }

    [TestMethod]
    public async Task StartAsync_ContainerIsStopped_RowStateBecomesRunningAndErrorIsCleared()
    {
        // Arrange
        var service = new FakeContainerManagementService { DefaultContainers = [CreateContainer("c1", ContainerState.Stopped)] };
        var sut = new ContainersViewModel(service);
        await sut.RefreshCommand.ExecuteAsync(null);
        var row = sut.Containers[0];
        service.StartResult = CreateContainer("c1", ContainerState.Running);
        // 操作成功後のベストエフォート再同期では、サーバーが更新後の状態（Running）を返す。
        service.GetContainersResults.Enqueue(() => Task.FromResult<IReadOnlyList<Container>>([CreateContainer("c1", ContainerState.Running)]));

        // Act
        await sut.StartCommand.ExecuteAsync(row);

        // Assert
        Assert.AreEqual(ContainerState.Running, row.State);
        Assert.IsNull(sut.ErrorMessage);
    }

    [TestMethod]
    public async Task StartAsync_ServiceThrowsInvalidContainerOperationException_ErrorMessageIsSetAndRowStateUnchanged()
    {
        // Arrange
        var service = new FakeContainerManagementService { DefaultContainers = [CreateContainer("c1", ContainerState.Running)] };
        var sut = new ContainersViewModel(service);
        await sut.RefreshCommand.ExecuteAsync(null);
        var row = sut.Containers[0];
        service.StartException = new InvalidContainerOperationException("c1", "StartAsync");

        // Act
        await sut.StartCommand.ExecuteAsync(row);

        // Assert
        Assert.AreEqual(service.StartException.Message, sut.ErrorMessage);
        Assert.AreEqual(ContainerState.Running, row.State);
    }

    [TestMethod]
    public async Task StartAsync_OperationIsStillRunning_RowIsBusyBecomesTrueAndThenFalse()
    {
        // Arrange
        var service = new FakeContainerManagementService { DefaultContainers = [CreateContainer("c1", ContainerState.Stopped)] };
        var gate = new TaskCompletionSource<Container>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.StartAsyncGate = gate;
        service.StartResult = CreateContainer("c1", ContainerState.Running);
        var sut = new ContainersViewModel(service);
        await sut.RefreshCommand.ExecuteAsync(null);
        var row = sut.Containers[0];

        // Act
        var operationTask = sut.StartCommand.ExecuteAsync(row);

        // Assert
        Assert.IsTrue(row.IsBusy);
        gate.SetResult(service.StartResult!);
        await operationTask;
        Assert.IsFalse(row.IsBusy);
    }

    [TestMethod]
    public async Task StartAsync_OperationSucceeds_RowInLiveCollectionIsNotBusyAfterCompletion()
    {
        // Arrange
        var service = new FakeContainerManagementService { DefaultContainers = [CreateContainer("c1", ContainerState.Stopped)] };
        service.StartResult = CreateContainer("c1", ContainerState.Running);
        var sut = new ContainersViewModel(service);
        await sut.RefreshCommand.ExecuteAsync(null);
        var row = sut.Containers[0];

        // Act
        await sut.StartCommand.ExecuteAsync(row);

        // Assert
        var refreshedRow = sut.Containers.Single(c => c.Id == "c1");
        Assert.IsFalse(refreshedRow.IsBusy);
    }

    [TestMethod]
    public async Task StartAsync_OperationSucceedsAfterBackgroundRefresh_SilentRefreshFailureAppliesOptimisticUpdateToLiveRow()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            DefaultContainers = [CreateContainer("c1", ContainerState.Stopped), CreateContainer("c2", ContainerState.Stopped)],
        };
        var gate = new TaskCompletionSource<Container>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.StartAsyncGate = gate;
        service.StartResult = CreateContainer("c1", ContainerState.Running);
        var sut = new ContainersViewModel(service);
        await sut.RefreshCommand.ExecuteAsync(null);
        service.GetContainersResults.Enqueue(() => Task.FromResult<IReadOnlyList<Container>>([CreateContainer("c1", ContainerState.Stopped), CreateContainer("c2", ContainerState.Stopped)]));
        service.GetContainersResults.Enqueue(() => Task.FromException<IReadOnlyList<Container>>(new ContainerRuntimeException("list", 1, "一覧の取得に失敗しました。")));
        var row1 = sut.Containers.Single(c => c.Id == "c1");

        // Act
        var operationTask = sut.StartCommand.ExecuteAsync(row1);
        await sut.RefreshCommand.ExecuteAsync(null);
        var liveRow = sut.Containers.Single(c => c.Id == "c1");
        Assert.AreSame(row1, liveRow);
        gate.SetResult(service.StartResult!);
        await operationTask;

        // Assert
        Assert.AreEqual(ContainerState.Running, liveRow.State);
        Assert.IsFalse(liveRow.IsBusy);
    }

    [TestMethod]
    public async Task StartAsync_OperationFailsAfterBackgroundRefresh_LiveRowIsNotBusyAfterFailure()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            DefaultContainers = [CreateContainer("c1", ContainerState.Stopped), CreateContainer("c2", ContainerState.Stopped)],
        };
        var gate = new TaskCompletionSource<Container>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.StartAsyncGate = gate;
        var exception = new InvalidContainerOperationException("c1", "StartAsync");
        service.StartException = exception;
        var sut = new ContainersViewModel(service);
        await sut.RefreshCommand.ExecuteAsync(null);
        service.GetContainersResults.Enqueue(() => Task.FromResult<IReadOnlyList<Container>>([CreateContainer("c1", ContainerState.Stopped), CreateContainer("c2", ContainerState.Stopped)]));
        service.GetContainersResults.Enqueue(() => Task.FromException<IReadOnlyList<Container>>(new ContainerRuntimeException("list", 1, "一覧の取得に失敗しました。")));
        var row1 = sut.Containers.Single(c => c.Id == "c1");

        // Act
        var operationTask = sut.StartCommand.ExecuteAsync(row1);
        await sut.RefreshCommand.ExecuteAsync(null);
        var liveRow = sut.Containers.Single(c => c.Id == "c1");
        Assert.AreSame(row1, liveRow);
        gate.SetException(exception);
        await operationTask;

        // Assert
        Assert.IsFalse(liveRow.IsBusy);
        Assert.AreEqual(ContainerRowOperation.None, liveRow.PendingOperation);
    }

    [TestMethod]
    public async Task StartAsync_OperationThrows_RowIsBusyBecomesFalseAfterFailure()
    {
        // Arrange
        var service = new FakeContainerManagementService { DefaultContainers = [CreateContainer("c1", ContainerState.Stopped)] };
        var gate = new TaskCompletionSource<Container>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.StartAsyncGate = gate;
        var exception = new InvalidContainerOperationException("c1", "StartAsync");
        service.StartException = exception;
        var sut = new ContainersViewModel(service);
        await sut.RefreshCommand.ExecuteAsync(null);
        var row = sut.Containers[0];

        // Act
        var operationTask = sut.StartCommand.ExecuteAsync(row);

        // Assert
        Assert.IsTrue(row.IsBusy);
        gate.SetException(exception);
        await operationTask;
        Assert.IsFalse(row.IsBusy);
    }

    [TestMethod]
    public async Task StartAsync_OperationThrows_RowInLiveCollectionIsNotBusyAfterFailure()
    {
        // Arrange
        var service = new FakeContainerManagementService { DefaultContainers = [CreateContainer("c1", ContainerState.Stopped)] };
        var exception = new InvalidContainerOperationException("c1", "StartAsync");
        service.StartException = exception;
        var sut = new ContainersViewModel(service);
        await sut.RefreshCommand.ExecuteAsync(null);
        var row = sut.Containers[0];

        // Act
        await sut.StartCommand.ExecuteAsync(row);

        // Assert
        var refreshedRow = sut.Containers.Single(c => c.Id == "c1");
        Assert.IsFalse(refreshedRow.IsBusy);
    }

    [TestMethod]
    public async Task StartAsync_OperationIsStillRunning_RowPendingOperationIsStartingThenNone()
    {
        // Arrange
        var service = new FakeContainerManagementService { DefaultContainers = [CreateContainer("c1", ContainerState.Stopped)] };
        var gate = new TaskCompletionSource<Container>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.StartAsyncGate = gate;
        service.StartResult = CreateContainer("c1", ContainerState.Running);
        var sut = new ContainersViewModel(service);
        await sut.RefreshCommand.ExecuteAsync(null);
        var row = sut.Containers[0];

        // Act
        var operationTask = sut.StartCommand.ExecuteAsync(row);

        // Assert
        Assert.AreEqual(ContainerRowOperation.Starting, row.PendingOperation);
        gate.SetResult(service.StartResult!);
        await operationTask;
        Assert.AreEqual(ContainerRowOperation.None, row.PendingOperation);
    }

    [TestMethod]
    public async Task StopAsync_OperationIsStillRunning_RowPendingOperationIsStoppingThenNone()
    {
        // Arrange
        var service = new FakeContainerManagementService { DefaultContainers = [CreateContainer("c1", ContainerState.Running)] };
        var gate = new TaskCompletionSource<Container>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.StopAsyncGate = gate;
        service.StopResult = CreateContainer("c1", ContainerState.Stopped);
        var sut = new ContainersViewModel(service);
        await sut.RefreshCommand.ExecuteAsync(null);
        var row = sut.Containers[0];

        // Act
        var operationTask = sut.StopCommand.ExecuteAsync(row);

        // Assert
        Assert.AreEqual(ContainerRowOperation.Stopping, row.PendingOperation);
        gate.SetResult(service.StopResult!);
        await operationTask;
        Assert.AreEqual(ContainerRowOperation.None, row.PendingOperation);
    }

    [TestMethod]
    public async Task RestartAsync_OperationIsStillRunning_RowPendingOperationIsRestartingThenNone()
    {
        // Arrange
        var service = new FakeContainerManagementService { DefaultContainers = [CreateContainer("c1", ContainerState.Running)] };
        var gate = new TaskCompletionSource<Container>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.RestartAsyncGate = gate;
        service.RestartResult = CreateContainer("c1", ContainerState.Running);
        var sut = new ContainersViewModel(service);
        await sut.RefreshCommand.ExecuteAsync(null);
        var row = sut.Containers[0];

        // Act
        var operationTask = sut.RestartCommand.ExecuteAsync(row);

        // Assert
        Assert.AreEqual(ContainerRowOperation.Restarting, row.PendingOperation);
        gate.SetResult(service.RestartResult!);
        await operationTask;
        Assert.AreEqual(ContainerRowOperation.None, row.PendingOperation);
    }

    [TestMethod]
    public async Task DeleteAsync_OperationIsStillRunning_RowPendingOperationIsDeleting()
    {
        // Arrange
        var service = new FakeContainerManagementService { DefaultContainers = [CreateContainer("c1", ContainerState.Stopped)] };
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.DeleteAsyncGate = gate;
        var sut = new ContainersViewModel(service);
        await sut.RefreshCommand.ExecuteAsync(null);
        var row = sut.Containers[0];

        // Act
        var operationTask = sut.DeleteCommand.ExecuteAsync(row);

        // Assert
        Assert.AreEqual(ContainerRowOperation.Deleting, row.PendingOperation);
        gate.SetResult(true);
        await operationTask;
    }

    [TestMethod]
    public async Task StopAsync_OperationIsStillRunningAcrossBackgroundRefresh_PendingOperationIsRestoredThenClearedOnLiveRow()
    {
        // Arrange
        var service = new FakeContainerManagementService { DefaultContainers = [CreateContainer("c1", ContainerState.Running)] };
        var gate = new TaskCompletionSource<Container>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.StopAsyncGate = gate;
        service.StopResult = CreateContainer("c1", ContainerState.Stopped);
        var sut = new ContainersViewModel(service);
        await sut.RefreshCommand.ExecuteAsync(null);
        var row = sut.Containers[0];
        service.GetContainersResults.Enqueue(() => Task.FromResult<IReadOnlyList<Container>>([CreateContainer("c1", ContainerState.Running)]));

        // Act
        var operationTask = sut.StopCommand.ExecuteAsync(row);
        await sut.RefreshCommand.ExecuteAsync(null);
        var rebuiltRow = sut.Containers.Single(c => c.Id == "c1");

        // Assert: in-flight中は差分更新で行インスタンスが維持され、保留中操作も保持される。
        Assert.AreSame(row, rebuiltRow);
        Assert.AreEqual(ContainerRowOperation.Stopping, rebuiltRow.PendingOperation);
        Assert.AreEqual(ContainerRowOperation.Stopping, rebuiltRow.DisplayState.PendingOperation);

        // 操作完了後のベストエフォート再同期で最新状態（Stopped）を返すようにする。
        service.GetContainersResults.Enqueue(() => Task.FromResult<IReadOnlyList<Container>>([CreateContainer("c1", ContainerState.Stopped)]));
        gate.SetResult(service.StopResult!);
        await operationTask;

        // Assert: 完了後は最新のライブ行の保留中操作がNoneに戻り、実際の状態が反映される。
        var finalRow = sut.Containers.Single(c => c.Id == "c1");
        Assert.AreEqual(ContainerRowOperation.None, finalRow.PendingOperation);
        Assert.AreEqual(ContainerState.Stopped, finalRow.State);
    }

    [TestMethod]
    public async Task RefreshAsync_TwoDifferentOperationsInFlight_EachLiveRowPreservesItsOwnPendingOperation()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            DefaultContainers = [CreateContainer("c1", ContainerState.Running), CreateContainer("c2", ContainerState.Stopped)],
        };
        var stopGate = new TaskCompletionSource<Container>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.StopAsyncGate = stopGate;
        var sut = new ContainersViewModel(service);
        await sut.RefreshCommand.ExecuteAsync(null);
        var c1Row = sut.Containers[0];
        var c2Row = sut.Containers[1];

        // Act
        var stopTask = sut.StopCommand.ExecuteAsync(c1Row);
        var startGate = new TaskCompletionSource<Container>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.StartAsyncGate = startGate;
        var startTask = sut.StartCommand.ExecuteAsync(c2Row);

        try
        {
            await sut.RefreshCommand.ExecuteAsync(null);
            var liveC1 = sut.Containers.Single(c => c.Id == "c1");
            var liveC2 = sut.Containers.Single(c => c.Id == "c2");

            // Assert
            Assert.AreEqual(ContainerRowOperation.Stopping, liveC1.PendingOperation);
            Assert.AreEqual(ContainerRowOperation.Starting, liveC2.PendingOperation);
        }
        finally
        {
            stopGate.SetResult(CreateContainer("c1", ContainerState.Stopped));
            startGate.SetResult(CreateContainer("c2", ContainerState.Running));
            await stopTask;
            await startTask;
        }
    }

    [TestMethod]
    public async Task DeleteAsync_OperationFailsAfterBackgroundRefreshAndRecoveryRefreshFails_RestoredRowPendingOperationIsNone()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            DefaultContainers = [CreateContainer("c1", ContainerState.Stopped), CreateContainer("c2", ContainerState.Stopped)],
        };
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.DeleteAsyncGate = gate;
        var exception = new InvalidContainerOperationException("c1", "DeleteAsync");
        service.DeleteException = exception;
        var sut = new ContainersViewModel(service);
        await sut.RefreshCommand.ExecuteAsync(null);
        service.GetContainersResults.Enqueue(() => Task.FromResult<IReadOnlyList<Container>>([CreateContainer("c1", ContainerState.Stopped), CreateContainer("c2", ContainerState.Stopped)]));
        service.GetContainersResults.Enqueue(() => Task.FromException<IReadOnlyList<Container>>(new ContainerRuntimeException("list", 1, "一覧の取得に失敗しました。")));
        var row1 = sut.Containers.Single(c => c.Id == "c1");

        // Act
        var operationTask = sut.DeleteCommand.ExecuteAsync(row1);
        await sut.RefreshCommand.ExecuteAsync(null);
        gate.SetException(exception);
        await operationTask;

        // Assert
        var restoredRow = sut.Containers.SingleOrDefault(c => c.Id == "c1");
        Assert.IsNotNull(restoredRow);
        Assert.AreEqual(ContainerRowOperation.None, restoredRow!.PendingOperation);
    }

    [TestMethod]
    public async Task RefreshAsync_OperationIsStillRunning_BusyStateIsPreservedAfterRefresh()
    {
        // Arrange
        var service = new FakeContainerManagementService { DefaultContainers = [CreateContainer("c1", ContainerState.Stopped)] };
        var gate = new TaskCompletionSource<Container>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.StartAsyncGate = gate;
        service.StartResult = CreateContainer("c1", ContainerState.Running);
        var sut = new ContainersViewModel(service);
        await sut.RefreshCommand.ExecuteAsync(null);
        var row = sut.Containers[0];

        // Act
        var operationTask = sut.StartCommand.ExecuteAsync(row);

        try
        {
            await sut.RefreshCommand.ExecuteAsync(null);

            // Assert
            ContainerRowViewModel? refreshedRow = null;
            foreach (var candidate in sut.Containers)
            {
                if (candidate.Id == "c1")
                {
                    refreshedRow = candidate;
                    break;
                }
            }

            Assert.IsNotNull(refreshedRow);
            Assert.IsTrue(refreshedRow!.IsBusy);
        }
        finally
        {
            gate.SetResult(service.StartResult!);
            await operationTask;
        }
    }

    [TestMethod]
    public async Task RefreshAsync_SecondOperationIsStillRunning_BusyStateForFirstOperationIsPreservedAfterRefresh()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            DefaultContainers = [CreateContainer("c1", ContainerState.Stopped), CreateContainer("c2", ContainerState.Stopped)],
        };
        var gate = new TaskCompletionSource<Container>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.StartAsyncGate = gate;
        service.StartResult = CreateContainer("c2", ContainerState.Running);
        var sut = new ContainersViewModel(service);
        await sut.RefreshCommand.ExecuteAsync(null);
        var c1Row = sut.Containers[0];
        var c2Row = sut.Containers[1];

        // Act
        var c1OperationTask = sut.StartCommand.ExecuteAsync(c1Row);
        service.StartAsyncGate = null;
        var c2OperationTask = sut.StartCommand.ExecuteAsync(c2Row);

        try
        {
            await c2OperationTask;

            // Assert
            ContainerRowViewModel? refreshedC1Row = null;
            foreach (var candidate in sut.Containers)
            {
                if (candidate.Id == "c1")
                {
                    refreshedC1Row = candidate;
                    break;
                }
            }

            Assert.IsNotNull(refreshedC1Row);
            Assert.IsTrue(refreshedC1Row!.IsBusy);
        }
        finally
        {
            gate.SetResult(service.StartResult!);
            await c1OperationTask;
        }
    }

    [TestMethod]
    public async Task StopAsync_ContainerIsRunning_RowStateBecomesStopped()
    {
        // Arrange
        var service = new FakeContainerManagementService { DefaultContainers = [CreateContainer("c1", ContainerState.Running)] };
        var sut = new ContainersViewModel(service);
        await sut.RefreshCommand.ExecuteAsync(null);
        var row = sut.Containers[0];
        service.StopResult = CreateContainer("c1", ContainerState.Stopped);
        // 操作成功後のベストエフォート再同期では、サーバーが更新後の状態（Stopped）を返す。
        service.GetContainersResults.Enqueue(() => Task.FromResult<IReadOnlyList<Container>>([CreateContainer("c1", ContainerState.Stopped)]));

        // Act
        await sut.StopCommand.ExecuteAsync(row);

        // Assert
        Assert.AreEqual(ContainerState.Stopped, row.State);
        Assert.IsNull(sut.ErrorMessage);
    }

    [TestMethod]
    public async Task RestartAsync_ContainerIsRunning_RowStateRemainsRunningAndErrorIsCleared()
    {
        // Arrange
        var service = new FakeContainerManagementService { DefaultContainers = [CreateContainer("c1", ContainerState.Running)] };
        var sut = new ContainersViewModel(service);
        await sut.RefreshCommand.ExecuteAsync(null);
        var row = sut.Containers[0];
        service.RestartResult = CreateContainer("c1", ContainerState.Running);

        // Act
        await sut.RestartCommand.ExecuteAsync(row);

        // Assert
        Assert.AreEqual(ContainerState.Running, row.State);
        Assert.IsNull(sut.ErrorMessage);
    }

    [TestMethod]
    public async Task DeleteAsync_ContainerIsStopped_RowIsRemovedAndIsEmptyBecomesTrue()
    {
        // Arrange
        var service = new FakeContainerManagementService { DefaultContainers = [CreateContainer("c1", ContainerState.Stopped)] };
        var sut = new ContainersViewModel(service);
        await sut.RefreshCommand.ExecuteAsync(null);
        var row = sut.Containers[0];

        // Act
        await sut.DeleteCommand.ExecuteAsync(row);

        // Assert
        Assert.IsEmpty(sut.Containers);
        Assert.IsTrue(sut.IsEmpty);
        CollectionAssert.Contains(service.DeleteCalls, "c1");
    }

    [TestMethod]
    public async Task DeleteAsync_OperationSucceedsAfterBackgroundRefresh_RemovesLiveRowFromCollection()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            DefaultContainers = [CreateContainer("c1", ContainerState.Stopped), CreateContainer("c2", ContainerState.Stopped)],
        };
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.DeleteAsyncGate = gate;
        var sut = new ContainersViewModel(service);
        await sut.RefreshCommand.ExecuteAsync(null);
        var row1 = sut.Containers.Single(c => c.Id == "c1");
        var row2 = sut.Containers.Single(c => c.Id == "c2");

        // Act
        var operationTask = sut.DeleteCommand.ExecuteAsync(row1);
        await sut.RefreshCommand.ExecuteAsync(null);
        var liveRow = sut.Containers.SingleOrDefault(c => c.Id == "c1");
        gate.SetResult(true);
        await operationTask;

        // Assert
        Assert.IsNull(liveRow);
        Assert.IsFalse(sut.Containers.Any(c => c.Id == "c1"));
        Assert.AreSame(row2, sut.Containers.Single(c => c.Id == "c2"));
    }

    [TestMethod]
    public async Task DeleteAsync_OperationFailsAfterBackgroundRefreshAndRecoveryRefreshFails_RestoresRowToCollection()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            DefaultContainers = [CreateContainer("c1", ContainerState.Stopped), CreateContainer("c2", ContainerState.Stopped)],
        };
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.DeleteAsyncGate = gate;
        var exception = new InvalidContainerOperationException("c1", "DeleteAsync");
        service.DeleteException = exception;
        var sut = new ContainersViewModel(service);
        await sut.RefreshCommand.ExecuteAsync(null);
        service.GetContainersResults.Enqueue(() => Task.FromResult<IReadOnlyList<Container>>([CreateContainer("c1", ContainerState.Stopped), CreateContainer("c2", ContainerState.Stopped)]));
        service.GetContainersResults.Enqueue(() => Task.FromException<IReadOnlyList<Container>>(new ContainerRuntimeException("list", 1, "一覧の取得に失敗しました。")));
        var row1 = sut.Containers.Single(c => c.Id == "c1");

        // Act
        var operationTask = sut.DeleteCommand.ExecuteAsync(row1);
        await sut.RefreshCommand.ExecuteAsync(null);
        Assert.IsFalse(sut.Containers.Any(c => c.Id == "c1"));
        gate.SetException(exception);
        await operationTask;

        // Assert
        var restoredRow = sut.Containers.SingleOrDefault(c => c.Id == "c1");
        Assert.IsNotNull(restoredRow);
        Assert.IsFalse(restoredRow!.IsBusy);
        Assert.AreEqual(service.DeleteException!.Message, sut.ErrorMessage);
    }

    [TestMethod]
    public async Task DeleteAsync_ServiceThrowsInvalidContainerOperationException_ErrorMessageIsSetAndRowRemains()
    {
        // Arrange
        var service = new FakeContainerManagementService { DefaultContainers = [CreateContainer("c1", ContainerState.Running)] };
        var sut = new ContainersViewModel(service);
        await sut.RefreshCommand.ExecuteAsync(null);
        var row = sut.Containers[0];
        service.DeleteException = new InvalidContainerOperationException("c1", "DeleteAsync");

        // Act
        await sut.DeleteCommand.ExecuteAsync(row);

        // Assert
        Assert.AreEqual(service.DeleteException.Message, sut.ErrorMessage);
        Assert.HasCount(1, sut.Containers);
    }

    [TestMethod]
    public async Task RestartAsync_ServiceThrowsAfterRefresh_ErrorMessageIsSetAndRowStateIsSynchronized()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            DefaultContainers = [CreateContainer("c1", ContainerState.Running)],
        };
        service.RestartException = new InvalidContainerOperationException("c1", "RestartAsync");
        var sut = new ContainersViewModel(service);
        await sut.RefreshCommand.ExecuteAsync(null);
        service.GetContainersResults.Enqueue(() => Task.FromResult<IReadOnlyList<Container>>([CreateContainer("c1", ContainerState.Stopped)]));

        // Act
        await sut.RestartCommand.ExecuteAsync(sut.Containers[0]);

        // Assert
        Assert.AreEqual(service.RestartException.Message, sut.ErrorMessage);
        Assert.AreEqual(ContainerState.Stopped, sut.Containers[0].State);
    }

    [TestMethod]
    public async Task DeleteAsync_ServiceThrowsAfterRefresh_ContainersAreSynchronizedAndIsEmptyBecomesTrue()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            DefaultContainers = [CreateContainer("c1", ContainerState.Stopped)],
        };
        service.DeleteException = new InvalidContainerOperationException("c1", "DeleteAsync");
        var sut = new ContainersViewModel(service);
        await sut.RefreshCommand.ExecuteAsync(null);
        service.GetContainersResults.Enqueue(() => Task.FromResult<IReadOnlyList<Container>>([]));

        // Act
        await sut.DeleteCommand.ExecuteAsync(sut.Containers[0]);

        // Assert
        Assert.AreEqual(service.DeleteException.Message, sut.ErrorMessage);
        Assert.IsTrue(sut.IsEmpty);
        Assert.IsEmpty(sut.Containers);
    }

    [TestMethod]
    public async Task StartAsync_SucceedsButBackgroundRefreshFails_OptimisticStateIsPreservedAndErrorMessageIsNotSet()
    {
        // Arrange
        // 直前の操作が成功した後にバックグラウンドの全件更新が失敗しても、
        // 楽観的更新済みの状態を古い状態へ巻き戻してはならない（詳細設計フェーズの
        // ラバーダックレビューで指摘された論点）。
        var service = new FakeContainerManagementService();
        service.GetContainersResults.Enqueue(() => Task.FromResult<IReadOnlyList<Container>>([CreateContainer("c1", ContainerState.Stopped)]));
        service.GetContainersResults.Enqueue(() => Task.FromException<IReadOnlyList<Container>>(new ContainerRuntimeException("list", 1, "一覧の取得に失敗しました。")));
        var sut = new ContainersViewModel(service);
        await sut.RefreshCommand.ExecuteAsync(null);
        var row = sut.Containers[0];
        service.StartResult = CreateContainer("c1", ContainerState.Running);

        // Act
        await sut.StartCommand.ExecuteAsync(row);

        // Assert
        Assert.AreEqual(ContainerState.Running, row.State);
        Assert.HasCount(1, sut.Containers);
        Assert.IsNull(sut.ErrorMessage);
    }

    [TestMethod]
    public async Task OpenLogsAsync_RunningContainerWithExistingLogs_DisplaysSnapshotAndStartsFollowWithoutDuplicatingSnapshot()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            DefaultContainers = [CreateContainer("c1", ContainerState.Running)],
            DefaultLogs = ["old"],
        };
        var dispatcher = new RecordingDispatcher();
        var sut = new ContainersViewModel(service, dispatcher);
        var followChannel = Channel.CreateUnbounded<string>();
        service.FollowLogsChannel = followChannel;
        service.FollowContainerLogsStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        await sut.OpenLogsAsync("c1");
        await service.FollowContainerLogsStarted.Task;
        await followChannel.Writer.WriteAsync("new");
        await WaitForConditionAsync(() => sut.LogLines.Count == 2, TimeSpan.FromSeconds(1));

        // Assert
        CollectionAssert.AreEqual(new[] { "old", "new" }, sut.LogLines.ToList());
        Assert.AreEqual(1, service.FollowContainerLogsCalls.Count(c => c == "c1"));
        Assert.IsGreaterThan(0, dispatcher.InvokeCount);
    }

    [TestMethod]
    public async Task OpenLogsAsync_StoppedContainerWithExistingLogs_DisplaysSnapshotAndDoesNotStartFollowAndStoppedStatusIsNonEmpty()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            DefaultContainers = [CreateContainer("c1", ContainerState.Stopped)],
            DefaultLogs = ["old"],
        };
        var sut = new ContainersViewModel(service, new RecordingDispatcher());

        // Act
        await sut.OpenLogsAsync("c1");

        // Assert
        CollectionAssert.AreEqual(new[] { "old" }, sut.LogLines.ToList());
        Assert.IsEmpty(service.FollowContainerLogsCalls);
        Assert.IsFalse(string.IsNullOrWhiteSpace(sut.LogStatusMessage));
    }

    [TestMethod]
    public async Task OpenLogsAsync_ContainerHasNoLogs_SetsLogEmptyStateWithoutLogError()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            DefaultContainers = [CreateContainer("c1", ContainerState.Running)],
            DefaultLogs = [],
        };
        var sut = new ContainersViewModel(service, new RecordingDispatcher());

        // Act
        await sut.OpenLogsAsync("c1");

        // Assert
        Assert.IsTrue(sut.IsLogEmpty);
        Assert.IsFalse(sut.IsLogError);
    }

    [TestMethod]
    public async Task OpenLogsAsync_GetLogsThrowsContainerNotFound_ShowsDeletedOrMissingStatusWithoutThrowing()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            DefaultContainers = [CreateContainer("c1", ContainerState.Running)],
            GetContainerLogsException = new ContainerNotFoundException("c1"),
        };
        var sut = new ContainersViewModel(service, new RecordingDispatcher());

        // Act
        await sut.OpenLogsAsync("c1");

        // Assert
        Assert.IsFalse(sut.IsLogError);
        Assert.IsFalse(string.IsNullOrWhiteSpace(sut.LogStatusMessage));
    }

    [TestMethod]
    public async Task OpenLogsAsync_GetLogsThrowsRuntimeException_SetsLogErrorMessageDistinctFromEmpty()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            DefaultContainers = [CreateContainer("c1", ContainerState.Running)],
            GetContainerLogsException = new ContainerRuntimeException("logs", 1, "log error"),
        };
        var sut = new ContainersViewModel(service, new RecordingDispatcher());

        // Act
        await sut.OpenLogsAsync("c1");

        // Assert
        Assert.IsTrue(sut.IsLogError);
        Assert.AreEqual("log error", sut.LogStatusMessage);
    }

    [TestMethod]
    public async Task FollowLiveLine_UsesDispatcherForCollectionMutation()
    {
        // Arrange
        var service = new FakeContainerManagementService();
        var dispatcher = new RecordingDispatcher();
        var sut = new ContainersViewModel(service, dispatcher);

        // Act
        await sut.FollowLiveLine("new");

        // Assert
        CollectionAssert.AreEqual(new[] { "new" }, sut.LogLines.ToList());
        Assert.AreEqual(1, dispatcher.InvokeCount);
    }

    [TestMethod]
    public async Task PauseLogsAsync_LiveLineArrivesWhilePaused_BuffersWithoutAppending()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            DefaultContainers = [CreateContainer("c1", ContainerState.Running)],
            DefaultLogs = ["old"],
        };
        var sut = new ContainersViewModel(service, new RecordingDispatcher());
        var followChannel = Channel.CreateUnbounded<string>();
        service.FollowLogsChannel = followChannel;
        await sut.OpenLogsAsync("c1");
        await sut.PauseLogsAsync();

        // Act
        await followChannel.Writer.WriteAsync("new");
        await Task.Delay(100);

        // Assert
        CollectionAssert.AreEqual(new[] { "old" }, sut.LogLines.ToList());
        Assert.IsTrue(sut.IsLogsPaused);
    }

    [TestMethod]
    public async Task ResumeLogsAsync_FlushesBufferedLinesInOrder()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            DefaultContainers = [CreateContainer("c1", ContainerState.Running)],
            DefaultLogs = ["old"],
        };
        var sut = new ContainersViewModel(service, new RecordingDispatcher());
        var followChannel = Channel.CreateUnbounded<string>();
        service.FollowLogsChannel = followChannel;
        await sut.OpenLogsAsync("c1");
        await sut.PauseLogsAsync();
        await followChannel.Writer.WriteAsync("new");
        await Task.Delay(100);

        // Act
        await sut.ResumeLogsAsync();

        // Assert
        CollectionAssert.AreEqual(new[] { "old", "new" }, sut.LogLines.ToList());
    }

    [TestMethod]
    public async Task ClearLogsAsync_WhileFollowing_ClearsDisplayedLinesAndFutureLiveLineStillAppears()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            DefaultContainers = [CreateContainer("c1", ContainerState.Running)],
            DefaultLogs = ["old"],
        };
        var sut = new ContainersViewModel(service, new RecordingDispatcher());
        var followChannel = Channel.CreateUnbounded<string>();
        service.FollowLogsChannel = followChannel;
        await sut.OpenLogsAsync("c1");

        // Act
        await sut.ClearLogsAsync();
        await followChannel.Writer.WriteAsync("new");
        await Task.Delay(100);

        // Assert
        CollectionAssert.AreEqual(new[] { "new" }, sut.LogLines.ToList());
    }

    [TestMethod]
    public async Task ClearLogsAsync_WhilePaused_DiscardsBufferedLinesAndFutureLiveLineStillAppears()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            DefaultContainers = [CreateContainer("c1", ContainerState.Running)],
            DefaultLogs = ["old"],
        };
        var sut = new ContainersViewModel(service, new RecordingDispatcher());
        var followChannel = Channel.CreateUnbounded<string>();
        service.FollowLogsChannel = followChannel;
        await sut.OpenLogsAsync("c1");
        await sut.PauseLogsAsync();
        await followChannel.Writer.WriteAsync("new");
        await Task.Delay(100);

        // Act
        await sut.ClearLogsAsync();
        await sut.ResumeLogsAsync();

        // Assert
        Assert.IsEmpty(sut.LogLines);
    }

    [TestMethod]
    public async Task CloseLogsAsync_CancelsFollowHidesPanelAndIgnoresPostCloseLines()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            DefaultContainers = [CreateContainer("c1", ContainerState.Running)],
            DefaultLogs = ["old"],
        };
        var sut = new ContainersViewModel(service, new RecordingDispatcher());
        var followChannel = Channel.CreateUnbounded<string>();
        service.FollowLogsChannel = followChannel;
        await sut.OpenLogsAsync("c1");

        // Act
        await sut.CloseLogsAsync();
        await followChannel.Writer.WriteAsync("new");
        await Task.Delay(100);

        // Assert
        Assert.IsFalse(sut.IsLogPanelVisible);
        CollectionAssert.AreEqual(new[] { "old" }, sut.LogLines.ToList());
    }

    [TestMethod]
    public async Task FollowStreamThrowsRuntimeException_SetsLogErrorMessageAndKeepsExistingLines()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            DefaultContainers = [CreateContainer("c1", ContainerState.Running)],
            DefaultLogs = ["old"],
            FollowContainerLogsException = new ContainerRuntimeException("logs", 1, "follow error"),
        };
        var sut = new ContainersViewModel(service, new RecordingDispatcher());

        // Act
        await sut.OpenLogsAsync("c1");

        // Assert
        CollectionAssert.AreEqual(new[] { "old" }, sut.LogLines.ToList());
        Assert.IsTrue(sut.IsLogError);
        Assert.AreEqual("follow error", sut.LogStatusMessage);
    }

    [TestMethod]
    public async Task FollowStreamThrowsContainerNotFoundException_ShowsMissingStatusDistinctFromGenericLogError()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            DefaultContainers = [CreateContainer("c1", ContainerState.Running)],
            DefaultLogs = ["old"],
            FollowContainerLogsException = new ContainerNotFoundException("c1"),
        };
        var sut = new ContainersViewModel(service, new RecordingDispatcher());

        // Act
        await sut.OpenLogsAsync("c1");

        // Assert
        CollectionAssert.AreEqual(new[] { "old" }, sut.LogLines.ToList());
        Assert.IsFalse(sut.IsLogError);
        Assert.IsFalse(string.IsNullOrWhiteSpace(sut.LogStatusMessage));
    }

    [TestMethod]
    public async Task OpenLogsAsync_WhileAlreadyFollowing_CancelsPreviousFollowBeforeStartingNew()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            DefaultContainers = [CreateContainer("c1", ContainerState.Running)],
            DefaultLogs = ["old"],
        };
        var sut = new ContainersViewModel(service, new RecordingDispatcher());
        await sut.OpenLogsAsync("c1");

        // Act
        await sut.OpenLogsAsync("c1");

        // Assert
        Assert.IsGreaterThan(0, service.FollowCancellationCount);
    }

    [TestMethod]
    public async Task OpenDetailsAsync_ServiceReturnsDetail_ShowsDetailPanelAndPopulatesDetail()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            ContainerDetail = CreateDetail("c1", ContainerState.Running),
        };
        var sut = new ContainersViewModel(service);

        // Act
        await sut.OpenDetailsAsync("c1");

        // Assert
        Assert.IsTrue(sut.IsDetailPanelVisible);
        Assert.IsTrue(sut.IsSidePanelVisible);
        Assert.IsNull(sut.DetailErrorMessage);
        Assert.AreSame(service.ContainerDetail, sut.SelectedContainerDetail);
        Assert.AreEqual("c1", service.GetContainerDetailCalls.Single());
    }

    [TestMethod]
    public async Task CloseDetailsAsync_DetailPanelIsOpen_HidesDetailPanelButKeepsOtherPanelsVisible()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            ContainerDetail = CreateDetail("c1", ContainerState.Running),
            ExecSession = new FakeContainerExecSession(),
        };
        var sut = new ContainersViewModel(service);
        await sut.OpenDetailsAsync("c1");
        await sut.OpenShellAsync("c1");

        // Act
        await sut.CloseDetailsAsync();

        // Assert
        Assert.IsFalse(sut.IsDetailPanelVisible);
        Assert.IsTrue(sut.IsShellPanelVisible);
        Assert.IsTrue(sut.IsSidePanelVisible);
    }

    [TestMethod]
    public async Task OpenDetailsAsync_ServiceThrowsRuntimeException_SetsDetailErrorAndKeepsPanelVisible()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            GetContainerDetailException = new ContainerRuntimeException("container inspect c1", 1, "inspect failed"),
        };
        var sut = new ContainersViewModel(service);

        // Act
        await sut.OpenDetailsAsync("c1");

        // Assert
        Assert.IsTrue(sut.IsDetailPanelVisible);
        Assert.AreEqual("inspect failed", sut.DetailErrorMessage);
    }

    [TestMethod]
    public async Task OpenShellAsync_StoppedContainer_ShowsShellErrorAndDoesNotCallServiceOpenExec()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            ContainerDetail = CreateDetail("c1", ContainerState.Stopped),
        };
        var sut = new ContainersViewModel(service);
        await sut.OpenDetailsAsync("c1");

        // Act
        await sut.OpenShellAsync("c1");

        // Assert
        Assert.IsTrue(sut.IsShellError);
        Assert.IsFalse(sut.IsShellStatusVisible);
        Assert.IsFalse(string.IsNullOrWhiteSpace(sut.ShellStatusMessage));
        Assert.IsEmpty(service.OpenExecSessionCalls);
    }

    [TestMethod]
    public async Task CloseShellAsync_StoppedContainerShellErrorIsOpen_HidesShellPanel()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            ContainerDetail = CreateDetail("c1", ContainerState.Stopped),
        };
        var sut = new ContainersViewModel(service);
        await sut.OpenDetailsAsync("c1");
        await sut.OpenShellAsync("c1");

        // Act
        await sut.CloseShellAsync();

        // Assert
        Assert.IsFalse(sut.IsShellPanelVisible);
        Assert.IsFalse(sut.IsShellConnected);
        Assert.IsTrue(sut.IsDetailPanelVisible);
        Assert.IsTrue(sut.IsSidePanelVisible);
    }

    [TestMethod]
    public async Task OpenShellAsync_RunningContainer_StartsSessionAndAppendsOutputThroughDispatcher()
    {
        // Arrange
        var session = new FakeContainerExecSession(outputChunks: ["connected"]);
        var service = new FakeContainerManagementService { ExecSession = session };
        var dispatcher = new RecordingDispatcher();
        var sut = new ContainersViewModel(service, dispatcher);

        // Act
        await sut.OpenShellAsync("c1");
        await WaitForConditionAsync(() => sut.ShellOutput.Count == 1, TimeSpan.FromSeconds(1));

        // Assert
        Assert.IsTrue(sut.IsShellPanelVisible);
        Assert.IsTrue(sut.IsSidePanelVisible);
        Assert.IsTrue(sut.IsShellConnected);
        Assert.IsTrue(sut.IsShellStatusVisible);
        CollectionAssert.AreEqual(new[] { "connected" }, sut.ShellOutput.ToList());
        Assert.IsGreaterThan(0, dispatcher.InvokeCount);
    }

    [TestMethod]
    public async Task OpenShellAsync_ExistingLiveSession_ReusesSessionAndDoesNotCallServiceTwice()
    {
        // Arrange
        var session = new FakeContainerExecSession();
        var service = new FakeContainerManagementService { ExecSession = session };
        var sut = new ContainersViewModel(service);

        // Act
        await sut.OpenShellAsync("c1");
        await sut.OpenShellAsync("c1");

        // Assert
        CollectionAssert.AreEqual(new[] { "c1" }, service.OpenExecSessionCalls);
    }

    [TestMethod]
    public async Task OpenShellAsync_CachedSessionIsClosed_StartsNewSession()
    {
        // Arrange
        var first = new FakeContainerExecSession { IsClosed = true };
        var second = new FakeContainerExecSession();
        var service = new FakeContainerManagementService();
        service.OpenExecSessionResults.Enqueue(first);
        service.OpenExecSessionResults.Enqueue(second);
        var sut = new ContainersViewModel(service);

        // Act
        await sut.OpenShellAsync("c1");
        await sut.OpenShellAsync("c1");

        // Assert
        CollectionAssert.AreEqual(new[] { "c1", "c1" }, service.OpenExecSessionCalls);
    }

    [TestMethod]
    public async Task OpenShellAsync_DifferentContainerShellIsAlreadyOpen_ClosesPreviousSession()
    {
        // Arrange
        var first = new FakeContainerExecSession();
        var second = new FakeContainerExecSession();
        var service = new FakeContainerManagementService();
        service.OpenExecSessionResults.Enqueue(first);
        service.OpenExecSessionResults.Enqueue(second);
        var sut = new ContainersViewModel(service);
        await sut.OpenShellAsync("c1");

        // Act
        await sut.OpenShellAsync("c2");

        // Assert
        Assert.IsTrue(first.CloseCalled);
        Assert.IsTrue(first.IsClosed);
        Assert.IsFalse(second.CloseCalled);
        CollectionAssert.AreEqual(new[] { "c1", "c2" }, service.OpenExecSessionCalls);
    }

    [TestMethod]
    public async Task OpenShellAsync_ServiceThrowsRuntimeException_SetsShellErrorWithoutStartingSession()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            OpenExecSessionException = new ContainerRuntimeException("container exec -i c1 /bin/sh", 1, "exec failed"),
            RecordFailedOpenExecSessionCalls = true,
        };
        var sut = new ContainersViewModel(service);

        // Act
        await sut.OpenShellAsync("c1");

        // Assert
        Assert.IsTrue(sut.IsShellError);
        Assert.IsFalse(sut.IsShellStatusVisible);
        Assert.AreEqual("exec failed", sut.ShellStatusMessage);
        Assert.IsFalse(sut.IsShellConnected);
    }

    [TestMethod]
    public async Task SendShellCommandAsync_ConnectedSession_SendsTextAndClearsShellCommandText()
    {
        // Arrange
        var session = new FakeContainerExecSession();
        var service = new FakeContainerManagementService { ExecSession = session };
        var sut = new ContainersViewModel(service);
        await sut.OpenShellAsync("c1");
        sut.ShellCommandText = "pwd";

        // Act
        await sut.SendShellCommandAsync();

        // Assert
        CollectionAssert.AreEqual(new[] { "pwd" }, session.Commands);
        Assert.AreEqual(string.Empty, sut.ShellCommandText);
    }

    [TestMethod]
    public async Task SendShellCommandAsync_CommandTextEndsWithCarriageReturn_SendsCommandWithoutCarriageReturn()
    {
        // Arrange
        var session = new FakeContainerExecSession();
        var service = new FakeContainerManagementService { ExecSession = session };
        var sut = new ContainersViewModel(service);
        await sut.OpenShellAsync("c1");
        sut.ShellCommandText = "ls\r";

        // Act
        await sut.SendShellCommandAsync();

        // Assert
        CollectionAssert.AreEqual(new[] { "ls" }, session.Commands);
        Assert.AreEqual(string.Empty, sut.ShellCommandText);
    }

    [TestMethod]
    public async Task SendShellCommandAsync_SendFails_ShowsShellErrorAndKeepsCommandText()
    {
        // Arrange
        var session = new FakeContainerExecSession
        {
            SendException = new InvalidOperationException("stdin closed"),
        };
        var service = new FakeContainerManagementService { ExecSession = session };
        var sut = new ContainersViewModel(service);
        await sut.OpenShellAsync("c1");
        sut.ShellCommandText = "pwd";

        // Act
        await sut.SendShellCommandAsync();

        // Assert
        Assert.IsTrue(sut.IsShellError);
        Assert.IsFalse(sut.IsShellConnected);
        Assert.AreEqual("stdin closed", sut.ShellStatusMessage);
        Assert.AreEqual("pwd", sut.ShellCommandText);
    }

    [TestMethod]
    public async Task CloseShellAsync_ConnectedSession_ClosesSessionAndShowsDisconnectedStatus()
    {
        // Arrange
        var session = new FakeContainerExecSession();
        var service = new FakeContainerManagementService { ExecSession = session };
        var sut = new ContainersViewModel(service);
        await sut.OpenShellAsync("c1");

        // Act
        await sut.CloseShellAsync();

        // Assert
        Assert.IsTrue(session.CloseCalled);
        Assert.IsFalse(sut.IsShellPanelVisible);
        Assert.IsFalse(sut.IsShellConnected);
        Assert.IsFalse(sut.IsSidePanelVisible);
    }

    [TestMethod]
    public async Task RefreshAsync_RefetchesSameContainers_PreservesRowInstances()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            DefaultContainers = [CreateContainer("c1", ContainerState.Running), CreateContainer("c2", ContainerState.Stopped)],
        };
        var sut = new ContainersViewModel(service);
        await sut.RefreshCommand.ExecuteAsync(null);
        var row1 = sut.Containers.Single(c => c.Id == "c1");
        var row2 = sut.Containers.Single(c => c.Id == "c2");

        // Act
        await sut.RefreshCommand.ExecuteAsync(null);

        // Assert
        Assert.AreSame(row1, sut.Containers.Single(c => c.Id == "c1"));
        Assert.AreSame(row2, sut.Containers.Single(c => c.Id == "c2"));
    }

    [TestMethod]
    public async Task RefreshAsync_RefetchesIdenticalContainers_RaisesNoCollectionChanged()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            DefaultContainers = [CreateContainer("c1", ContainerState.Running), CreateContainer("c2", ContainerState.Stopped)],
        };
        var sut = new ContainersViewModel(service);
        await sut.RefreshCommand.ExecuteAsync(null);
        var actions = new List<NotifyCollectionChangedAction>();
        sut.Containers.CollectionChanged += (_, e) => actions.Add(e.Action);

        // Act
        await sut.RefreshCommand.ExecuteAsync(null);

        // Assert
        Assert.IsEmpty(actions);
    }

    [TestMethod]
    public async Task RefreshAsync_ContainerStateChangedOnServer_SameInstanceReflectsNewState()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            DefaultContainers = [CreateContainer("c1", ContainerState.Running)],
        };
        var sut = new ContainersViewModel(service);
        await sut.RefreshCommand.ExecuteAsync(null);
        var row1 = sut.Containers.Single(c => c.Id == "c1");
        service.GetContainersResults.Enqueue(() => Task.FromResult<IReadOnlyList<Container>>([CreateContainer("c1", ContainerState.Stopped)]));

        // Act
        await sut.RefreshCommand.ExecuteAsync(null);

        // Assert
        Assert.AreSame(row1, sut.Containers.Single(c => c.Id == "c1"));
        Assert.AreEqual(ContainerState.Stopped, row1.State);
    }

    [TestMethod]
    public async Task RefreshAsync_ContainerRenamedOnServerSameId_RowReflectsNewNameAndInstanceIsRecreated()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            DefaultContainers = [new Container("c1", "old-name", "nginx:latest", ContainerState.Running, new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.Zero))],
        };
        var sut = new ContainersViewModel(service);
        await sut.RefreshCommand.ExecuteAsync(null);
        var originalRow = sut.Containers.Single(c => c.Id == "c1");
        service.GetContainersResults.Enqueue(() => Task.FromResult<IReadOnlyList<Container>>(
            [new Container("c1", "new-name", "nginx:latest", ContainerState.Running, new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.Zero))]));

        // Act
        await sut.RefreshCommand.ExecuteAsync(null);

        // Assert
        var currentRow = sut.Containers.Single(c => c.Id == "c1");
        Assert.AreEqual("new-name", currentRow.Name);
        Assert.AreNotSame(originalRow, currentRow);
    }

    [TestMethod]
    public async Task RefreshAsync_ContainerRemovedOnServer_RowRemovedAndOthersPreserved()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            DefaultContainers = [CreateContainer("c1", ContainerState.Running), CreateContainer("c2", ContainerState.Stopped)],
        };
        var sut = new ContainersViewModel(service);
        await sut.RefreshCommand.ExecuteAsync(null);
        var row2 = sut.Containers.Single(c => c.Id == "c2");
        service.GetContainersResults.Enqueue(() => Task.FromResult<IReadOnlyList<Container>>([CreateContainer("c2", ContainerState.Stopped)]));

        // Act
        await sut.RefreshCommand.ExecuteAsync(null);

        // Assert
        Assert.IsFalse(sut.Containers.Any(c => c.Id == "c1"));
        Assert.AreSame(row2, sut.Containers.Single(c => c.Id == "c2"));
    }

    [TestMethod]
    public async Task RefreshAsync_NewContainerOnServer_RowAddedAndExistingPreserved()
    {
        // Arrange
        var service = new FakeContainerManagementService
        {
            DefaultContainers = [CreateContainer("c1", ContainerState.Running)],
        };
        var sut = new ContainersViewModel(service);
        await sut.RefreshCommand.ExecuteAsync(null);
        var row1 = sut.Containers.Single(c => c.Id == "c1");
        service.GetContainersResults.Enqueue(() => Task.FromResult<IReadOnlyList<Container>>([CreateContainer("c1", ContainerState.Running), CreateContainer("c2", ContainerState.Stopped)]));

        // Act
        await sut.RefreshCommand.ExecuteAsync(null);

        // Assert
        Assert.AreSame(row1, sut.Containers.Single(c => c.Id == "c1"));
        Assert.IsTrue(sut.Containers.Any(c => c.Id == "c2"));
    }

    [TestMethod]
    public async Task StopAsync_ContainerDisappearsThenReappearsWhileInFlight_RecreatedRowShowsStopping()
    {
        // Arrange
        var service = new FakeContainerManagementService { DefaultContainers = [CreateContainer("c1", ContainerState.Running)] };
        var gate = new TaskCompletionSource<Container>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.StopAsyncGate = gate;
        service.StopResult = CreateContainer("c1", ContainerState.Stopped);
        var sut = new ContainersViewModel(service);
        await sut.RefreshCommand.ExecuteAsync(null);
        var row = sut.Containers[0];

        // 停止操作の実行中に、サーバー一覧から c1 が一旦消え、その後まだ完了前に再出現するシナリオ。
        service.GetContainersResults.Enqueue(() => Task.FromResult<IReadOnlyList<Container>>([]));
        service.GetContainersResults.Enqueue(() => Task.FromResult<IReadOnlyList<Container>>([CreateContainer("c1", ContainerState.Running)]));

        // Act
        var operationTask = sut.StopCommand.ExecuteAsync(row);
        await sut.RefreshCommand.ExecuteAsync(null); // c1 が消える → 行が削除される
        Assert.IsFalse(sut.Containers.Any(c => c.Id == "c1"));
        await sut.RefreshCommand.ExecuteAsync(null); // c1 が再出現 → 新しい行が作られる

        // Assert: 再生成された行にも進行中の Stopping が復元される。
        var recreatedRow = sut.Containers.Single(c => c.Id == "c1");
        Assert.IsTrue(recreatedRow.IsBusy);
        Assert.AreEqual(ContainerRowOperation.Stopping, recreatedRow.PendingOperation);
        Assert.AreEqual(ContainerRowOperation.Stopping, recreatedRow.DisplayState.PendingOperation);

        // クリーンアップ: 操作を完了させる。
        service.GetContainersResults.Enqueue(() => Task.FromResult<IReadOnlyList<Container>>([CreateContainer("c1", ContainerState.Stopped)]));
        gate.SetResult(service.StopResult!);
        await operationTask;
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, TimeSpan timeout)
    {
        var started = DateTime.UtcNow;
        while (!condition())
        {
            if (DateTime.UtcNow - started > timeout)
            {
                Assert.Fail("Condition was not met in time.");
            }

            await Task.Delay(10);
        }
    }

    private static ContainerDetail CreateDetail(string id, ContainerState state) => new(
        Id: id,
        Name: $"name-{id}",
        Image: "nginx:latest",
        State: state,
        CreatedAt: new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.Zero),
        Command: "sleep infinity",
        Entrypoint: "/entrypoint.sh",
        Ports: [new ContainerPortMapping("127.0.0.1", 8080, 80, "tcp")],
        Environment: [new ContainerEnvironmentVariable("A", "1")],
        Mounts: [new ContainerMount("bind", "C:\\data", "/data", true)],
        Networks: [new ContainerNetwork("bridge", "172.17.0.2")],
        RunState: new ContainerRunState(null, null, 0, null));

    private sealed class FakeContainerExecSession(IReadOnlyList<string>? outputChunks = null) : IContainerExecSession
    {
        public bool IsClosed { get; set; }

        public bool CloseCalled { get; private set; }

        public Exception? SendException { get; set; }

        public List<string> Commands { get; } = [];

        public async IAsyncEnumerable<string> ReadOutputAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var chunk in outputChunks ?? [])
            {
                yield return chunk;
            }

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
        }

        public Task SendCommandAsync(string command, CancellationToken cancellationToken = default)
        {
            if (SendException is not null)
            {
                throw SendException;
            }

            Commands.Add(command);
            return Task.CompletedTask;
        }

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            CloseCalled = true;
            IsClosed = true;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingDispatcher : IUiDispatcher
    {
        public int InvokeCount { get; private set; }

        public void Invoke(Action action)
        {
            InvokeCount++;
            action();
        }
    }
}
