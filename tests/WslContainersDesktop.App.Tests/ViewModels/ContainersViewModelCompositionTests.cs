// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;
using WslContainersDesktop_App.ViewModels;
using WslContainersDesktop_App_Tests.Fakes;

namespace WslContainersDesktop_App_Tests.ViewModels;

// Architecture fitness test for ADR-0017: the ContainersViewModel composition surface should expose focused component view-model properties as read-only instance members.
[TestClass]
public sealed class ContainersViewModelCompositionTests
{
    [TestMethod]
    public async Task ContainerListViewModel_RefreshAsync_ServiceReturnsContainer_ExposesMatchingRow()
    {
        // Arrange
        var expectedContainer = new Container(
            "c1",
            "alpha",
            "demo:v1",
            ContainerState.Stopped,
            new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.Zero));
        var service = new FakeContainerManagementService
        {
            DefaultContainers = [expectedContainer]
        };
        var facade = new ContainersViewModel(service);
        object component = facade.List;
        var componentType = component.GetType();

        // Act
        var refreshAsyncMethod = componentType.GetMethod(
            "RefreshAsync",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
        var containersProperty = componentType.GetProperty(
            "Containers",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
        var isEmptyProperty = componentType.GetProperty(
            "IsEmpty",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
        var errorMessageProperty = componentType.GetProperty(
            "ErrorMessage",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);

        Assert.IsNotNull(refreshAsyncMethod, "Expected public instance RefreshAsync method to exist on ContainerListViewModel.");
        Assert.AreEqual(typeof(Task), refreshAsyncMethod!.ReturnType, "Expected RefreshAsync to return Task.");
        await (Task)refreshAsyncMethod.Invoke(component, null)!;

        Assert.IsNotNull(containersProperty, "Expected public Containers property to exist on ContainerListViewModel.");
        Assert.IsNotNull(isEmptyProperty, "Expected public IsEmpty property to exist on ContainerListViewModel.");
        Assert.IsNotNull(errorMessageProperty, "Expected public ErrorMessage property to exist on ContainerListViewModel.");

        Assert.IsTrue(containersProperty!.CanRead, "Expected Containers property to be readable.");
        Assert.IsTrue(isEmptyProperty!.CanRead, "Expected IsEmpty property to be readable.");
        Assert.IsTrue(errorMessageProperty!.CanRead, "Expected ErrorMessage property to be readable.");

        var containersValue = containersProperty.GetValue(component);
        var rows = ((IEnumerable<ContainerRowViewModel>)containersValue!).ToList();

        // Assert
        Assert.AreEqual(1, rows.Count, "Expected exactly one container row to be exposed.");
        var row = rows[0];
        Assert.AreEqual(expectedContainer.Id, row.Id, "Expected row Id to match the refreshed container.");
        Assert.AreEqual(expectedContainer.Name, row.Name, "Expected row Name to match the refreshed container.");
        Assert.AreEqual(expectedContainer.Image, row.Image, "Expected row Image to match the refreshed container.");
        Assert.AreEqual(expectedContainer.State, row.State, "Expected row State to match the refreshed container.");
        Assert.AreEqual(expectedContainer.CreatedAt, row.CreatedAt, "Expected row CreatedAt to match the refreshed container.");

        Assert.IsFalse((bool)isEmptyProperty.GetValue(component)!, "Expected IsEmpty to be false after refresh.");
        Assert.IsNull(errorMessageProperty.GetValue(component), "Expected ErrorMessage to be null after successful refresh.");
    }

    [TestMethod]
    public async Task ContainerDetailsViewModel_OpenAsync_ServiceReturnsDetail_ExposesFormattedDetail()
    {
        // Arrange
        var expectedDetail = new ContainerDetail(
            "c1",
            "alpha",
            "demo:v1",
            ContainerState.Stopped,
            new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.Zero),
            null,
            null,
            [],
            [],
            [],
            [],
            new ContainerRunState(null, null, null, null));
        var service = new FakeContainerManagementService
        {
            ContainerDetail = expectedDetail
        };
        var facade = new ContainersViewModel(service);
        object component = facade.Details;
        var componentType = component.GetType();

        // Act
        var openAsyncMethod = componentType.GetMethod(
            "OpenAsync",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy,
            null,
            [typeof(string)],
            null);
        Assert.IsNotNull(openAsyncMethod, "Expected public instance OpenAsync(string) method to exist on ContainerDetailsViewModel.");
        Assert.AreEqual(typeof(Task), openAsyncMethod!.ReturnType, "Expected OpenAsync to return Task.");
        await (Task)openAsyncMethod.Invoke(component, new object?[] { "c1" })!;

        var selectedContainerDetailProperty = componentType.GetProperty(
            "SelectedContainerDetail",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
        var detailLinesProperty = componentType.GetProperty(
            "DetailLines",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
        var detailErrorMessageProperty = componentType.GetProperty(
            "DetailErrorMessage",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
        var isDetailPanelVisibleProperty = componentType.GetProperty(
            "IsDetailPanelVisible",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);

        Assert.IsNotNull(selectedContainerDetailProperty, "Expected public SelectedContainerDetail property to exist on ContainerDetailsViewModel.");
        Assert.IsNotNull(detailLinesProperty, "Expected public DetailLines property to exist on ContainerDetailsViewModel.");
        Assert.IsNotNull(detailErrorMessageProperty, "Expected public DetailErrorMessage property to exist on ContainerDetailsViewModel.");
        Assert.IsNotNull(isDetailPanelVisibleProperty, "Expected public IsDetailPanelVisible property to exist on ContainerDetailsViewModel.");

        var selectedContainerDetailValue = selectedContainerDetailProperty!.GetValue(component);
        var detailLinesValue = detailLinesProperty!.GetValue(component);
        var detailErrorMessageValue = detailErrorMessageProperty!.GetValue(component);
        var isDetailPanelVisibleValue = isDetailPanelVisibleProperty!.GetValue(component);

        // Assert
        Assert.AreSame(expectedDetail, selectedContainerDetailValue, "Expected SelectedContainerDetail to expose the exact same detail instance.");
        Assert.AreEqual(true, isDetailPanelVisibleValue, "Expected IsDetailPanelVisible to be true after opening a detail.");
        Assert.IsNull(detailErrorMessageValue, "Expected DetailErrorMessage to be null after successful detail open.");

        var detailLines = ((IEnumerable<string>)detailLinesValue!).ToList();
        var expectedLines = new[]
        {
            "ID: c1",
            "Name: alpha",
            "Image: demo:v1",
            "State: Stopped",
            "Created: 2026-07-10 00:00:00Z",
            "Command: (none)",
            "Entrypoint: (none)",
            "Exit code: (none)",
            "Started: (none)",
            "Finished: (none)",
            "Ports:",
            "  (none)",
            "Environment:",
            "  (none)",
            "Mounts:",
            "  (none)",
            "Networks:",
            "  (none)"
        };
        CollectionAssert.AreEqual(expectedLines, detailLines, "Expected detail lines to be formatted in the exact order.");
    }

    [TestMethod]
    public async Task ContainerLogsViewModel_OpenAsync_RunningContainer_DisplaysSnapshotAndCancelsFollowOnClose()
    {
        // Arrange
        var expectedContainer = new Container(
            "c1",
            "alpha",
            "demo:v1",
            ContainerState.Running,
            new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.Zero));
        var service = new FakeContainerManagementService
        {
            DefaultContainers = [expectedContainer],
            DefaultLogs = ["snapshot-1", "snapshot-2"],
            FollowContainerLogsStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
        };
        var facade = new ContainersViewModel(service);
        object component = facade.Logs;
        var componentType = component.GetType();

        // Act
        var openAsyncMethod = componentType.GetMethod(
            "OpenAsync",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy,
            null,
            [typeof(string)],
            null);
        var closeAsyncMethod = componentType.GetMethod(
            "CloseAsync",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
        var logLinesProperty = componentType.GetProperty(
            "LogLines",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
        var isLogPanelVisibleProperty = componentType.GetProperty(
            "IsLogPanelVisible",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
        var isLogEmptyProperty = componentType.GetProperty(
            "IsLogEmpty",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);

        Assert.IsNotNull(openAsyncMethod, "Expected public instance OpenAsync(string) method to exist on ContainerLogsViewModel.");
        Assert.AreEqual(typeof(Task), openAsyncMethod!.ReturnType, "Expected OpenAsync to return Task.");
        Assert.IsNotNull(closeAsyncMethod, "Expected public instance CloseAsync() method to exist on ContainerLogsViewModel.");
        Assert.AreEqual(typeof(Task), closeAsyncMethod!.ReturnType, "Expected CloseAsync to return Task.");
        Assert.IsNotNull(logLinesProperty, "Expected public LogLines property to exist on ContainerLogsViewModel.");
        Assert.IsTrue(logLinesProperty!.CanRead, "Expected LogLines property to be readable.");
        Assert.IsNotNull(isLogPanelVisibleProperty, "Expected public IsLogPanelVisible property to exist on ContainerLogsViewModel.");
        Assert.IsTrue(isLogPanelVisibleProperty!.CanRead, "Expected IsLogPanelVisible property to be readable.");
        Assert.IsNotNull(isLogEmptyProperty, "Expected public IsLogEmpty property to exist on ContainerLogsViewModel.");
        Assert.IsTrue(isLogEmptyProperty!.CanRead, "Expected IsLogEmpty property to be readable.");

        await (Task)openAsyncMethod.Invoke(component, new object?[] { "c1" })!;
        await service.FollowContainerLogsStarted!.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var logLinesValue = logLinesProperty.GetValue(component);
        var isLogPanelVisibleValue = isLogPanelVisibleProperty.GetValue(component);
        var isLogEmptyValue = isLogEmptyProperty.GetValue(component);

        // Assert
        var logLines = ((IEnumerable<string>)logLinesValue!).ToList();
        CollectionAssert.AreEqual(new[] { "snapshot-1", "snapshot-2" }, logLines, "Expected snapshot log lines to be displayed in order.");
        Assert.IsTrue((bool)isLogPanelVisibleValue!, "Expected IsLogPanelVisible to be true after opening the logs.");
        Assert.IsFalse((bool)isLogEmptyValue!, "Expected IsLogEmpty to be false after displaying snapshot logs.");
        CollectionAssert.AreEqual(new[] { "c1" }, service.FollowContainerLogsCalls, "Expected follow logs to be started for the opened container.");

        await (Task)closeAsyncMethod.Invoke(component, null)!;

        var visibleAfterClose = isLogPanelVisibleProperty.GetValue(component);
        Assert.IsFalse((bool)visibleAfterClose!, "Expected IsLogPanelVisible to be false after closing the logs.");
        Assert.AreEqual(1, service.FollowCancellationCount, "Expected follow to be cancelled once when closing the logs.");
    }

    [TestMethod]
    public async Task ContainerShellViewModel_OpenAsync_LiveSessionDisplaysOutputAndReusesSession()
    {
        // Arrange
        var fakeSession = new FakeContainerExecSession();
        var service = new FakeContainerManagementService { ExecSession = fakeSession };
        var facade = new ContainersViewModel(service);
        object component = facade.Shell;
        var componentType = component.GetType();

        // Act
        var openAsyncMethod = componentType.GetMethod(
            "OpenAsync",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy,
            null,
            [typeof(string), typeof(ContainerDetail)],
            null);
        var closeAsyncMethod = componentType.GetMethod(
            "CloseAsync",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
        var shellOutputProperty = componentType.GetProperty(
            "ShellOutput",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
        var isShellConnectedProperty = componentType.GetProperty(
            "IsShellConnected",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);

        Assert.IsNotNull(openAsyncMethod, "Expected public instance OpenAsync(string, ContainerDetail) method to exist on ContainerShellViewModel.");
        Assert.AreEqual(typeof(Task), openAsyncMethod!.ReturnType, "Expected OpenAsync to return Task.");
        Assert.IsNotNull(closeAsyncMethod, "Expected public instance CloseAsync() method to exist on ContainerShellViewModel.");
        Assert.AreEqual(typeof(Task), closeAsyncMethod!.ReturnType, "Expected CloseAsync to return Task.");
        Assert.IsNotNull(shellOutputProperty, "Expected public ShellOutput property to exist on ContainerShellViewModel.");
        Assert.IsNotNull(isShellConnectedProperty, "Expected public IsShellConnected property to exist on ContainerShellViewModel.");
        Assert.IsTrue(shellOutputProperty!.CanRead, "Expected ShellOutput property to be readable.");
        Assert.IsTrue(isShellConnectedProperty!.CanRead, "Expected IsShellConnected property to be readable.");

        await (Task)openAsyncMethod.Invoke(component, new object?[] { "c1", null })!;
        await WaitForConditionAsync(() =>
        {
            var shellOutputValue = shellOutputProperty.GetValue(component);
            return shellOutputValue is IEnumerable<string> shellOutput && shellOutput.Count() == 1;
        }, TimeSpan.FromSeconds(2));

        // Assert
        var shellOutputValue = shellOutputProperty.GetValue(component);
        var shellOutput = ((IEnumerable<string>)shellOutputValue!).ToList();
        var isShellConnectedValue = (bool)isShellConnectedProperty.GetValue(component)!;
        Assert.IsTrue(isShellConnectedValue, "Expected IsShellConnected to be true after opening the shell.");
        CollectionAssert.AreEqual(new[] { "prompt> " }, shellOutput, "Expected shell output to display the initial prompt once.");
        CollectionAssert.AreEqual(new[] { "c1" }, service.OpenExecSessionCalls, "Expected the shell to open an exec session for the selected container.");

        await (Task)openAsyncMethod.Invoke(component, new object?[] { "c1", null })!;
        CollectionAssert.AreEqual(new[] { "c1" }, service.OpenExecSessionCalls, "Expected the shell to reuse the existing shell session on the second open.");

        await (Task)closeAsyncMethod.Invoke(component, null)!;
        Assert.IsTrue(fakeSession.IsClosed, "Expected the exec session to be closed when the shell is closed.");
    }

    [TestMethod]
    public void ContainersViewModel_DeclaredFields_ContainOnlyFocusedComponentsAndGeneratedCommands()
    {
        // Arrange
        var sutType = typeof(ContainersViewModel);
        var expectedFieldTypes = new[]
        {
            typeof(ContainerListViewModel),
            typeof(ContainerDetailsViewModel),
            typeof(ContainerLogsViewModel),
            typeof(ContainerShellViewModel)
        };

        // Act
        var declaredFields = sutType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        var fieldDetails = declaredFields.Select(field => $"{field.Name}: {field.FieldType.FullName}").ToList();
        var componentFieldTypes = declaredFields
            .Where(field => expectedFieldTypes.Contains(field.FieldType))
            .Select(field => field.FieldType)
            .ToList();

        // Assert
        Assert.AreEqual(0, declaredFields.Count(field =>
            field.FieldType != typeof(ContainerListViewModel)
            && field.FieldType != typeof(ContainerDetailsViewModel)
            && field.FieldType != typeof(ContainerLogsViewModel)
            && field.FieldType != typeof(ContainerShellViewModel)
            && field.FieldType.Namespace != "CommunityToolkit.Mvvm.Input"),
            $"Unexpected declared field type(s): {string.Join(", ", fieldDetails)}");
        CollectionAssert.AreEquivalent(expectedFieldTypes, componentFieldTypes, "Expected all four focused component field types to be present.");
    }

    [TestMethod]
    public void ContainersViewModel_PublicCommands_PreserveExpectedContract()
    {
        // Arrange
        var sutType = typeof(ContainersViewModel);
        var expectedCommandNames = new[]
        {
            "ClearLogsCommand",
            "CloseDetailsCommand",
            "CloseLogsCommand",
            "CloseShellCommand",
            "DeleteCommand",
            "OpenDetailsCommand",
            "OpenLogsCommand",
            "OpenShellCommand",
            "PauseLogsCommand",
            "RefreshCommand",
            "RestartCommand",
            "ResumeLogsCommand",
            "SendShellCommandCommand",
            "StartCommand",
            "StopCommand"
        };

        // Act
        var commandProperties = sutType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => typeof(ICommand).IsAssignableFrom(property.PropertyType))
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        // Assert
        CollectionAssert.AreEqual(expectedCommandNames, commandProperties, "Expected the public command contract to remain stable.");
    }

    [TestMethod]
    public void ShellCommandText_SetOnShellComponent_FacadeRelaysValueAndChangingEvents()
    {
        // Arrange
        var service = new FakeContainerManagementService();
        var sut = new ContainersViewModel(service);
        var changingEvents = new List<string>();
        var changedEvents = new List<string>();
        sut.PropertyChanging += (_, e) =>
        {
            if (e.PropertyName == "ShellCommandText")
            {
                changingEvents.Add(e.PropertyName!);
            }
        };
        sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == "ShellCommandText")
            {
                changedEvents.Add(e.PropertyName!);
            }
        };

        // Act
        sut.Shell.ShellCommandText = "echo test";

        // Assert
        Assert.AreEqual("echo test", sut.ShellCommandText, "Expected the facade to expose the same shell command text value.");
        CollectionAssert.AreEqual(new[] { "ShellCommandText" }, changingEvents, "Expected exactly one ShellCommandText changing event.");
        CollectionAssert.AreEqual(new[] { "ShellCommandText" }, changedEvents, "Expected exactly one ShellCommandText changed event.");
    }

    [TestMethod]
    public void ShellCommandText_SetOnFacade_ShellComponentReceivesValueAndFacadeRaisesEvents()
    {
        // Arrange
        var service = new FakeContainerManagementService();
        var sut = new ContainersViewModel(service);
        var changingEvents = new List<string>();
        var changedEvents = new List<string>();
        sut.PropertyChanging += (_, e) =>
        {
            if (e.PropertyName == "ShellCommandText")
            {
                changingEvents.Add(e.PropertyName!);
            }
        };
        sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == "ShellCommandText")
            {
                changedEvents.Add(e.PropertyName!);
            }
        };

        // Act
        sut.ShellCommandText = "pwd";

        // Assert
        Assert.AreEqual("pwd", sut.Shell.ShellCommandText, "Expected the shell component to receive the facade shell command text value.");
        CollectionAssert.AreEqual(new[] { "ShellCommandText" }, changingEvents, "Expected exactly one ShellCommandText changing event.");
        CollectionAssert.AreEqual(new[] { "ShellCommandText" }, changedEvents, "Expected exactly one ShellCommandText changed event.");
    }

    [TestMethod]
    public async Task DetailsPanel_OpenAndClose_FacadeRelaysVisibilityAndSidePanel()
    {
        // Arrange
        var detail = new ContainerDetail(
            "c1",
            "alpha",
            "demo:v1",
            ContainerState.Stopped,
            new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.Zero),
            null,
            null,
            [],
            [],
            [],
            [],
            new ContainerRunState(null, null, null, null));
        var service = new FakeContainerManagementService { ContainerDetail = detail };
        var sut = new ContainersViewModel(service);
        var changingEvents = new List<string>();
        var changedEvents = new List<string>();
        sut.PropertyChanging += (_, e) =>
        {
            if (e.PropertyName is "IsDetailPanelVisible" or "IsSidePanelVisible")
            {
                changingEvents.Add(e.PropertyName!);
            }
        };
        sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is "IsDetailPanelVisible" or "IsSidePanelVisible")
            {
                changedEvents.Add(e.PropertyName!);
            }
        };

        // Act
        await sut.Details.OpenAsync("c1");

        // Assert
        Assert.AreEqual(true, sut.IsDetailPanelVisible, "Expected the facade to reflect detail panel visibility after opening.");
        Assert.AreEqual(true, sut.IsSidePanelVisible, "Expected the facade to reflect side panel visibility after opening.");
        CollectionAssert.AreEqual(new[] { "IsDetailPanelVisible", "IsSidePanelVisible" }, changingEvents, "Expected the facade to relay the visibility-changing sequence in order.");
        CollectionAssert.AreEqual(new[] { "IsDetailPanelVisible", "IsSidePanelVisible" }, changedEvents, "Expected the facade to relay the visibility-changed sequence in order.");

        // Act
        changingEvents.Clear();
        changedEvents.Clear();
        await sut.Details.CloseAsync();

        // Assert
        Assert.AreEqual(false, sut.IsDetailPanelVisible, "Expected the facade to hide the detail panel after closing.");
        Assert.AreEqual(false, sut.IsSidePanelVisible, "Expected the facade to hide the side panel after closing.");
        CollectionAssert.AreEqual(new[] { "IsDetailPanelVisible", "IsSidePanelVisible" }, changingEvents, "Expected the facade to relay the visibility-changing sequence after close.");
        CollectionAssert.AreEqual(new[] { "IsDetailPanelVisible", "IsSidePanelVisible" }, changedEvents, "Expected the facade to relay the visibility-changed sequence after close.");
    }

    [TestMethod]
    public void FocusedComponents_ContainersViewModel_ExposesReadOnlyCompositionProperties()
    {
        // Arrange
        var sutType = typeof(ContainersViewModel);
        var expectedProperties = new[]
        {
            new ExpectedCompositionProperty("List", "WslContainersDesktop_App.ViewModels.ContainerListViewModel"),
            new ExpectedCompositionProperty("Details", "WslContainersDesktop_App.ViewModels.ContainerDetailsViewModel"),
            new ExpectedCompositionProperty("Logs", "WslContainersDesktop_App.ViewModels.ContainerLogsViewModel"),
            new ExpectedCompositionProperty("Shell", "WslContainersDesktop_App.ViewModels.ContainerShellViewModel")
        };

        // Act
        foreach (var expectedProperty in expectedProperties)
        {
            var property = sutType.GetProperty(
                expectedProperty.Name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);

            // Assert
            Assert.IsNotNull(property, $"Expected property '{expectedProperty.Name}' to exist on '{sutType.FullName}'.");
            Assert.AreEqual(expectedProperty.ExpectedTypeFullName, property!.PropertyType.FullName, $"Expected property '{expectedProperty.Name}' to have type '{expectedProperty.ExpectedTypeFullName}'.");
            Assert.IsTrue(property.CanRead, $"Expected property '{expectedProperty.Name}' to be readable.");
            Assert.IsFalse(property.CanWrite, $"Expected property '{expectedProperty.Name}' to be read-only.");
            Assert.IsNotNull(property.GetMethod, $"Expected property '{expectedProperty.Name}' to have a public getter.");
            Assert.IsTrue(property.GetMethod!.IsPublic, $"Expected getter for property '{expectedProperty.Name}' to be public.");
        }
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, TimeSpan timeout)
    {
        var startTime = DateTime.UtcNow;
        while (DateTime.UtcNow - startTime < timeout)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.IsTrue(condition(), $"Condition was not satisfied within {timeout.TotalSeconds:0.##} seconds.");
    }

    private sealed class FakeContainerExecSession : IContainerExecSession
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        public bool IsClosed { get; private set; }

        public List<string> SentCommands { get; } = [];

        public async IAsyncEnumerable<string> ReadOutputAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellationTokenSource.Token);
            yield return "prompt> ";

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }

        public Task SendCommandAsync(string command, CancellationToken cancellationToken = default)
        {
            SentCommands.Add(command);
            return Task.CompletedTask;
        }

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            IsClosed = true;
            _cancellationTokenSource.Cancel();
            return Task.CompletedTask;
        }
    }

    private sealed record ExpectedCompositionProperty(string Name, string ExpectedTypeFullName);
}
