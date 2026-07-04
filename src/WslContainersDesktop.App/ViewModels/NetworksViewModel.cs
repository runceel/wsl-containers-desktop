// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop_App.ViewModels;

/// <summary>
/// コンテナーネットワーク一覧の表示・操作を管理するViewModel。
/// </summary>
public sealed partial class NetworksViewModel(INetworkManagementService networkManagementService) : ObservableObject
{
    private const string CreatingStatusMessage = "Creating network...";
    private const string CreateCompletedStatusMessage = "Network created.";
    private const string EmptyNetworkNameMessage = "Network name is required.";

    /// <summary>
    /// 現在表示中のコンテナーネットワーク一覧。
    /// </summary>
    public ObservableCollection<NetworkRowViewModel> Networks { get; } = [];

    /// <summary>
    /// 作成する新しいネットワーク名。
    /// </summary>
    [ObservableProperty]
    public partial string NewNetworkName { get; set; } = string.Empty;

    /// <summary>
    /// 直近の操作で発生したエラーメッセージ。エラーがない場合は <see langword="null"/>。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsErrorMessageVisible))]
    public partial string? ErrorMessage { get; private set; }

    /// <summary>
    /// 直近の成功操作を表すステータスメッセージ。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStatusMessageVisible))]
    public partial string? StatusMessage { get; private set; }

    /// <summary>
    /// エラーメッセージを表示するかどうか。
    /// </summary>
    public bool IsErrorMessageVisible => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>
    /// 成功または進行中ステータスメッセージを表示するかどうか。
    /// </summary>
    public bool IsStatusMessageVisible => !string.IsNullOrEmpty(StatusMessage);

    /// <summary>
    /// ネットワーク作成操作が進行中かどうか。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCreate))]
    public partial bool IsCreating { get; private set; }

    /// <summary>
    /// ネットワーク作成操作を開始できるかどうか。
    /// </summary>
    public bool CanCreate => !IsCreating;

    /// <summary>
    /// 表示対象のネットワークが存在するかどうか。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNoUserNetworksInfoVisible))]
    public partial bool HasNetworks { get; private set; }

    /// <summary>
    /// ユーザー作成ネットワークが存在しないかどうか。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNoUserNetworksInfoVisible))]
    public partial bool HasNoUserNetworks { get; private set; } = true;

    /// <summary>
    /// ユーザー作成ネットワークが存在しないことを示す情報バーを表示するかどうか。
    /// </summary>
    public bool IsNoUserNetworksInfoVisible => HasNetworks && HasNoUserNetworks;

    /// <summary>
    /// コンテナーネットワーク一覧を手動で再取得する。
    /// </summary>
    [RelayCommand]
    public async Task RefreshAsync()
    {
        StatusMessage = null;
        try
        {
            var networks = await networkManagementService.GetNetworksAsync();
            ReplaceNetworks(networks);
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = null;
        }
    }

    /// <summary>
    /// 入力されたネットワーク名で新しいネットワークを作成し、成功後に一覧を再取得する。
    /// </summary>
    [RelayCommand]
    public async Task CreateAsync()
    {
        var networkName = NewNetworkName.Trim();
        StatusMessage = null;
        ErrorMessage = null;
        if (networkName.Length == 0)
        {
            ErrorMessage = EmptyNetworkNameMessage;
            return;
        }

        StatusMessage = CreatingStatusMessage;
        IsCreating = true;
        try
        {
            var created = await networkManagementService.CreateAsync(networkName);
            NewNetworkName = string.Empty;
            StatusMessage = CreateCompletedStatusMessage;
            ErrorMessage = null;
            await RefreshNetworksAfterCreateAsync(created);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = null;
        }
        finally
        {
            IsCreating = false;
        }
    }

    /// <summary>
    /// 指定したコンテナーネットワークを削除する。
    /// </summary>
    /// <param name="row">対象の行。</param>
    [RelayCommand]
    public async Task DeleteAsync(NetworkRowViewModel row)
    {
        StatusMessage = null;
        if (row.IsSystem)
        {
            ErrorMessage = FormatSystemNetworkMessage(row.Name);
            return;
        }

        if (row.IsInUse)
        {
            ErrorMessage = FormatInUseMessage(row.Name, row.ConnectedContainerNames);
            return;
        }

        row.IsBusy = true;
        try
        {
            await networkManagementService.DeleteAsync(row.Name);
            var liveRow = Networks.FirstOrDefault(network => network.Name == row.Name);
            if (liveRow is not null)
            {
                liveRow.IsBusy = false;
                Networks.Remove(liveRow);
            }
            else
            {
                row.IsBusy = false;
            }

            UpdateNetworkFlags();
            ErrorMessage = null;
        }
        catch (NetworkInUseException ex)
        {
            row.IsBusy = false;
            ErrorMessage = FormatInUseMessage(ex.NetworkName, ex.ConnectedContainerNames);
        }
        catch (SystemNetworkDeletionException ex)
        {
            row.IsBusy = false;
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            row.IsBusy = false;
            ErrorMessage = ex.Message;
        }
    }

    private async Task RefreshNetworksAfterCreateAsync(ContainerNetworkResource created)
    {
        try
        {
            var networks = await networkManagementService.GetNetworksAsync();
            ReplaceNetworks(networks.Count == 0 ? [created] : networks);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = null;
        }
    }

    private void ReplaceNetworks(IReadOnlyList<ContainerNetworkResource> networks)
    {
        Networks.Clear();
        foreach (var network in networks)
        {
            Networks.Add(new NetworkRowViewModel(network));
        }

        UpdateNetworkFlags();
    }

    private void UpdateNetworkFlags()
    {
        HasNetworks = Networks.Count > 0;
        HasNoUserNetworks = !Networks.Any(network => !network.IsSystem);
    }

    private static string FormatInUseMessage(string networkName, IReadOnlyList<string> connectedContainerNames)
    {
        return $"Network '{networkName}' is in use by: {string.Join(", ", connectedContainerNames)}";
    }

    private static string FormatSystemNetworkMessage(string networkName)
    {
        return $"Network '{networkName}' is a system network and cannot be deleted.";
    }
}
