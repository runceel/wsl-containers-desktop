using System.Threading.Channels;
using WslContainersDesktop.Application.Exceptions;
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
        Assert.AreNotSame(row1, liveRow);
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
        Assert.AreNotSame(row1, liveRow);
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

        // Assert: in-flight中は再構築後の行にも保留中操作が復元される。
        Assert.AreNotSame(row, rebuiltRow);
        Assert.AreEqual(ContainerRowOperation.Stopping, rebuiltRow.PendingOperation);

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

        // Act
        var operationTask = sut.DeleteCommand.ExecuteAsync(row1);
        await sut.RefreshCommand.ExecuteAsync(null);
        var liveRow = sut.Containers.SingleOrDefault(c => c.Id == "c1");
        Assert.AreNotSame(row1, liveRow);
        gate.SetResult(true);
        await operationTask;

        // Assert
        Assert.IsNull(liveRow);
        Assert.IsFalse(sut.Containers.Any(c => c.Id == "c1"));
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
