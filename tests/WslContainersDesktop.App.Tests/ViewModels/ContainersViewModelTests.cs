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
}
