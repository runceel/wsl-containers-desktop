// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop_App.ViewModels;

/// <summary>
/// コンテナ詳細のフォーカス対象となるViewModel。
/// </summary>
public sealed partial class ContainerDetailsViewModel : ObservableObject
{
    private readonly IContainerManagementService _containerManagementService;

    /// <summary>
    /// <see cref="ContainerDetailsViewModel"/> の新しいインスタンスを初期化する。
    /// </summary>
    /// <param name="containerManagementService">コンテナ操作を行うApplication層のサービス。</param>
    public ContainerDetailsViewModel(IContainerManagementService containerManagementService)
    {
        _containerManagementService = containerManagementService;
    }

    /// <summary>
    /// 現在表示中のコンテナ詳細行。
    /// </summary>
    public ObservableCollection<string> DetailLines { get; } = [];

    /// <summary>
    /// 現在表示中のコンテナ詳細。
    /// </summary>
    [ObservableProperty]
    public partial ContainerDetail? SelectedContainerDetail { get; private set; }

    /// <summary>
    /// 詳細取得で発生したエラーメッセージ。
    /// </summary>
    [ObservableProperty]
    public partial string? DetailErrorMessage { get; private set; }

    /// <summary>
    /// コンテナ詳細パネルが表示されているかどうか。
    /// </summary>
    [ObservableProperty]
    public partial bool IsDetailPanelVisible { get; private set; }

    /// <summary>
    /// 指定したコンテナの詳細情報を開く。
    /// </summary>
    /// <param name="containerId">対象コンテナID。</param>
    public async Task OpenAsync(string containerId)
    {
        IsDetailPanelVisible = true;
        DetailErrorMessage = null;
        DetailLines.Clear();

        try
        {
            SelectedContainerDetail = await _containerManagementService.GetContainerDetailAsync(containerId);
            PopulateDetailLines(SelectedContainerDetail!);
        }
        catch (Exception ex)
        {
            DetailErrorMessage = ex.Message;
        }
    }

    /// <summary>
    /// 詳細パネルを閉じる。
    /// </summary>
    public Task CloseAsync()
    {
        IsDetailPanelVisible = false;
        return Task.CompletedTask;
    }

    private void PopulateDetailLines(ContainerDetail detail)
    {
        DetailLines.Add($"ID: {detail.Id}");
        DetailLines.Add($"Name: {detail.Name}");
        DetailLines.Add($"Image: {detail.Image}");
        DetailLines.Add($"State: {detail.State}");
        DetailLines.Add($"Created: {detail.CreatedAt:u}");
        DetailLines.Add($"Command: {detail.Command ?? "(none)"}");
        DetailLines.Add($"Entrypoint: {detail.Entrypoint ?? "(none)"}");
        DetailLines.Add($"Exit code: {detail.RunState.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "(none)"}");
        DetailLines.Add($"Started: {FormatOptionalDateTime(detail.RunState.StartedAt)}");
        DetailLines.Add($"Finished: {FormatOptionalDateTime(detail.RunState.FinishedAt)}");
        DetailLines.Add("Ports:");
        AddIndentedLines(detail.Ports.Select(port =>
        {
            var host = port.HostPort is null ? "(not published)" : $"{port.HostAddress ?? "0.0.0.0"}:{port.HostPort}";
            return $"{host} -> {port.ContainerPort}/{port.Protocol}";
        }));
        DetailLines.Add("Environment:");
        AddIndentedLines(detail.Environment.Select(variable => $"{variable.Name}={variable.Value}"));
        DetailLines.Add("Mounts:");
        AddIndentedLines(detail.Mounts.Select(mount => $"{mount.Type}: {mount.Source} -> {mount.Target} ({(mount.IsReadOnly ? "ro" : "rw")})"));
        DetailLines.Add("Networks:");
        AddIndentedLines(detail.Networks.Select(network => $"{network.Name}: {network.IpAddress ?? "(no IP)"}"));
    }

    private void AddIndentedLines(IEnumerable<string> lines)
    {
        var added = false;
        foreach (var line in lines)
        {
            DetailLines.Add($"  {line}");
            added = true;
        }

        if (!added)
        {
            DetailLines.Add("  (none)");
        }
    }

    private static string FormatOptionalDateTime(DateTimeOffset? value)
    {
        return value?.ToString("u", CultureInfo.InvariantCulture) ?? "(none)";
    }
}
