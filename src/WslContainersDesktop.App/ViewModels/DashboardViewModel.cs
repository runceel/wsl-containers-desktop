// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;
using WslContainersDesktop_App.Navigation;

namespace WslContainersDesktop_App.ViewModels;

/// <summary>
/// ダッシュボード（概要画面）のViewModel。各リソースのサマリ件数と稼働中コンテナのリソース使用量を集約する。
/// </summary>
public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly IDashboardService _dashboardService;
    private readonly NavigationViewModel _navigation;
    private readonly ContainersViewModel _containers;

    public DashboardViewModel(IDashboardService dashboardService, NavigationViewModel navigation, ContainersViewModel containers)
    {
        _dashboardService = dashboardService;
        _navigation = navigation;
        _containers = containers;
    }

    [ObservableProperty]
    public partial int? RunningContainerCount { get; private set; }

    [ObservableProperty]
    public partial int? StoppedContainerCount { get; private set; }

    [ObservableProperty]
    public partial int? ImageCount { get; private set; }

    [ObservableProperty]
    public partial int? VolumeCount { get; private set; }

    [ObservableProperty]
    public partial int? NetworkCount { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsContainerCountErrorVisible))]
    public partial string? ContainerCountErrorMessage { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsImageCountErrorVisible))]
    public partial string? ImageCountErrorMessage { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVolumeCountErrorVisible))]
    public partial string? VolumeCountErrorMessage { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNetworkCountErrorVisible))]
    public partial string? NetworkCountErrorMessage { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStatsErrorVisible))]
    [NotifyPropertyChangedFor(nameof(IsStatsEmpty))]
    public partial string? StatsErrorMessage { get; private set; }

    [ObservableProperty]
    public partial bool IsRefreshing { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStatsEmpty))]
    public partial bool IsStatsLoading { get; private set; }

    public bool IsContainerCountErrorVisible => ContainerCountErrorMessage is not null;

    public bool IsImageCountErrorVisible => ImageCountErrorMessage is not null;

    public bool IsVolumeCountErrorVisible => VolumeCountErrorMessage is not null;

    public bool IsNetworkCountErrorVisible => NetworkCountErrorMessage is not null;

    public ObservableCollection<DashboardContainerStatsRowViewModel> ContainerStats { get; } = [];

    public bool IsStatsErrorVisible => StatsErrorMessage is not null;

    public bool IsStatsEmpty => !IsStatsLoading && ContainerStats.Count == 0 && StatsErrorMessage is null;

    /// <summary>
    /// すべてのサマリ件数と稼働中コンテナのリソース使用量を再取得する。
    /// 各取得は独立して行い、一部が失敗しても他の情報は更新する（部分失敗の許容）。
    /// 実行中に再度呼び出された場合は二重取得を避けるため無視する。
    /// </summary>
    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (IsRefreshing)
        {
            return;
        }

        IsRefreshing = true;
        try
        {
            // Stats セクションは Refresh 中に一旦クリアするため、読み込み中であることを示す。
            // これにより一覧が空になった瞬間に「対象なし」が点滅するのを防ぐ。
            IsStatsLoading = true;

            ContainerCountErrorMessage = null;
            RunningContainerCount = null;
            StoppedContainerCount = null;
            ImageCountErrorMessage = null;
            ImageCount = null;
            VolumeCountErrorMessage = null;
            VolumeCount = null;
            NetworkCountErrorMessage = null;
            NetworkCount = null;
            StatsErrorMessage = null;
            ContainerStats.Clear();
            OnPropertyChanged(nameof(IsStatsEmpty));

            var snapshot = await _dashboardService.GetSnapshotAsync();
            ApplySnapshot(snapshot);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        finally
        {
            IsStatsLoading = false;
            OnPropertyChanged(nameof(IsStatsEmpty));
            IsRefreshing = false;
        }
    }

    private void ApplySnapshot(DashboardSnapshot? snapshot)
    {
        var resolvedSnapshot = snapshot ?? new DashboardSnapshot();
        ApplyContainers(resolvedSnapshot.Containers);
        ApplyImages(resolvedSnapshot.Images);
        ApplyVolumes(resolvedSnapshot.Volumes);
        ApplyNetworks(resolvedSnapshot.Networks);
        ApplyStats(resolvedSnapshot.Stats);
    }

    private void ApplyContainers(DashboardSection<IReadOnlyList<Container>>? section)
    {
        var resolvedSection = section ?? new DashboardSection<IReadOnlyList<Container>> { Value = [] };
        if (resolvedSection.Exception is not null)
        {
            ContainerCountErrorMessage = resolvedSection.Exception.Message;
            RunningContainerCount = null;
            StoppedContainerCount = null;
            return;
        }

        var containers = resolvedSection.Value ?? [];
        RunningContainerCount = containers.Count(container => container.State == ContainerState.Running);
        StoppedContainerCount = containers.Count(container => container.State == ContainerState.Stopped);
    }

    private void ApplyImages(DashboardSection<IReadOnlyList<ContainerImage>>? section)
    {
        var resolvedSection = section ?? new DashboardSection<IReadOnlyList<ContainerImage>> { Value = [] };
        if (resolvedSection.Exception is not null)
        {
            ImageCountErrorMessage = resolvedSection.Exception.Message;
            ImageCount = null;
            return;
        }

        ImageCount = resolvedSection.Value?.Count ?? 0;
    }

    private void ApplyVolumes(DashboardSection<IReadOnlyList<ContainerVolume>>? section)
    {
        var resolvedSection = section ?? new DashboardSection<IReadOnlyList<ContainerVolume>> { Value = [] };
        if (resolvedSection.Exception is not null)
        {
            VolumeCountErrorMessage = resolvedSection.Exception.Message;
            VolumeCount = null;
            return;
        }

        VolumeCount = resolvedSection.Value?.Count ?? 0;
    }

    private void ApplyNetworks(DashboardSection<IReadOnlyList<ContainerNetworkResource>>? section)
    {
        var resolvedSection = section ?? new DashboardSection<IReadOnlyList<ContainerNetworkResource>> { Value = [] };
        if (resolvedSection.Exception is not null)
        {
            NetworkCountErrorMessage = resolvedSection.Exception.Message;
            NetworkCount = null;
            return;
        }

        NetworkCount = resolvedSection.Value?.Count ?? 0;
    }

    private void ApplyStats(DashboardSection<IReadOnlyList<ContainerResourceUsage>>? section)
    {
        var resolvedSection = section ?? new DashboardSection<IReadOnlyList<ContainerResourceUsage>> { Value = [] };
        if (resolvedSection.Exception is not null)
        {
            StatsErrorMessage = resolvedSection.Exception.Message;
            ContainerStats.Clear();
            OnPropertyChanged(nameof(IsStatsEmpty));
            return;
        }

        StatsErrorMessage = null;
        ContainerStats.Clear();
        foreach (var usage in resolvedSection.Value ?? [])
        {
            ContainerStats.Add(new DashboardContainerStatsRowViewModel(usage));
        }

        OnPropertyChanged(nameof(IsStatsEmpty));
    }

    [RelayCommand]
    private void ShowContainers() => _navigation.NavigateToCommand.Execute(NavigationPageKey.Containers);

    [RelayCommand]
    private void ShowImages() => _navigation.NavigateToCommand.Execute(NavigationPageKey.Images);

    [RelayCommand]
    private void ShowVolumes() => _navigation.NavigateToCommand.Execute(NavigationPageKey.Volumes);

    [RelayCommand]
    private void ShowNetworks() => _navigation.NavigateToCommand.Execute(NavigationPageKey.Networks);

    [RelayCommand]
    private async Task OpenContainerDetails(DashboardContainerStatsRowViewModel row)
    {
        _navigation.NavigateToCommand.Execute(NavigationPageKey.Containers);
        await _containers.OpenDetailsCommand.ExecuteAsync(row.ContainerId);
    }

    [RelayCommand]
    private async Task OpenContainerLogs(DashboardContainerStatsRowViewModel row)
    {
        _navigation.NavigateToCommand.Execute(NavigationPageKey.Containers);
        await _containers.OpenLogsCommand.ExecuteAsync(row.ContainerId);
    }
}
