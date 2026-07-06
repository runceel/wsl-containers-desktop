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
    private readonly IContainerManagementService _containerManagementService;
    private readonly IImageManagementService _imageManagementService;
    private readonly IVolumeManagementService _volumeManagementService;
    private readonly INetworkManagementService _networkManagementService;
    private readonly NavigationViewModel _navigation;
    private readonly ContainersViewModel _containers;

    public DashboardViewModel(
        IContainerManagementService containerManagementService,
        IImageManagementService imageManagementService,
        IVolumeManagementService volumeManagementService,
        INetworkManagementService networkManagementService,
        NavigationViewModel navigation,
        ContainersViewModel containers)
    {
        _containerManagementService = containerManagementService;
        _imageManagementService = imageManagementService;
        _volumeManagementService = volumeManagementService;
        _networkManagementService = networkManagementService;
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
            try
            {
                var containers = await _containerManagementService.GetContainersAsync();
                RunningContainerCount = containers.Count(c => c.State == ContainerState.Running);
                StoppedContainerCount = containers.Count(c => c.State == ContainerState.Stopped);
            }
            catch (Exception ex)
            {
                ContainerCountErrorMessage = ex.Message;
            }

            ImageCountErrorMessage = null;
            ImageCount = null;
            try
            {
                var images = await _imageManagementService.GetImagesAsync();
                ImageCount = images.Count;
            }
            catch (Exception ex)
            {
                ImageCountErrorMessage = ex.Message;
            }

            VolumeCountErrorMessage = null;
            VolumeCount = null;
            try
            {
                var volumes = await _volumeManagementService.GetVolumesAsync();
                VolumeCount = volumes.Count;
            }
            catch (Exception ex)
            {
                VolumeCountErrorMessage = ex.Message;
            }

            NetworkCountErrorMessage = null;
            NetworkCount = null;
            try
            {
                // システムネットワークも含めた総数を表示する。
                var networks = await _networkManagementService.GetNetworksAsync();
                NetworkCount = networks.Count;
            }
            catch (Exception ex)
            {
                NetworkCountErrorMessage = ex.Message;
            }

            StatsErrorMessage = null;
            ContainerStats.Clear();
            OnPropertyChanged(nameof(IsStatsEmpty));
            try
            {
                var stats = await _containerManagementService.GetStatsAsync();
                foreach (var usage in stats)
                {
                    ContainerStats.Add(new DashboardContainerStatsRowViewModel(usage));
                }
            }
            catch (Exception ex)
            {
                StatsErrorMessage = ex.Message;
            }
            finally
            {
                IsStatsLoading = false;
                OnPropertyChanged(nameof(IsStatsEmpty));
            }
        }
        finally
        {
            IsRefreshing = false;
        }
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
