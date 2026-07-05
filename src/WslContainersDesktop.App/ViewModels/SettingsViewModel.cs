// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop_App.ViewModels;

/// <summary>
/// 設定画面（WSL連携状態の確認とリソース制限の編集）を管理するViewModel。
/// </summary>
public sealed partial class SettingsViewModel(ISettingsService settingsService) : ObservableObject
{
    /// <summary>
    /// リソース制限を保存した後に表示するステータスメッセージ。
    /// ADR-0011に基づき自動で <c>wsl --shutdown</c> は行わず、再起動が必要である旨を案内する。
    /// </summary>
    public const string SavedStatusMessage =
        "Resource limits saved. Restart WSL (run 'wsl --shutdown') to apply the changes.";

    /// <summary>
    /// リソース制限をリセットした後に表示するステータスメッセージ。
    /// </summary>
    public const string ResetStatusMessage =
        "Resource limits reset to WSL defaults. Restart WSL (run 'wsl --shutdown') to apply the changes.";

    /// <summary>
    /// WSL Containersの要件を満たしていない状態で変更が要求されたときのエラーメッセージ。
    /// </summary>
    public const string RequirementsNotMetMessage =
        "WSL Containers requirements are not met, so resource limits cannot be changed.";

    /// <summary>
    /// 入力値が正の整数でない場合のエラーメッセージ。
    /// </summary>
    public const string InvalidInputMessage =
        "Enter a positive whole number, or leave the field blank to use the WSL default.";

    /// <summary>
    /// WSLが検出されなかった場合にバージョン欄へ表示するテキスト。
    /// </summary>
    public const string NotDetectedText = "Not detected";

    /// <summary>
    /// 検出したWSLのバージョン表示テキスト。未検出時は <see cref="NotDetectedText"/>。
    /// </summary>
    [ObservableProperty]
    public partial string WslVersionText { get; private set; } = NotDetectedText;

    /// <summary>
    /// WSL Containers（<c>wslc</c>）が利用可能かどうか。
    /// </summary>
    [ObservableProperty]
    public partial bool IsWslContainersAvailable { get; private set; }

    /// <summary>
    /// WSL Containersの要件を満たしているかどうか。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditResourceLimits))]
    [NotifyPropertyChangedFor(nameof(IsRequirementsWarningVisible))]
    public partial bool MeetsRequirements { get; private set; }

    /// <summary>
    /// WSLが検出されているかどうか。
    /// </summary>
    [ObservableProperty]
    public partial bool IsWslDetected { get; private set; }

    /// <summary>
    /// メモリ制限（MB）の入力値。空欄はWSL既定を表す。
    /// </summary>
    [ObservableProperty]
    public partial string MemoryMegabytesInput { get; set; } = string.Empty;

    /// <summary>
    /// 論理プロセッサ数の入力値。空欄はWSL既定を表す。
    /// </summary>
    [ObservableProperty]
    public partial string ProcessorCountInput { get; set; } = string.Empty;

    /// <summary>
    /// 連携状態の読み込みが進行中かどうか。
    /// </summary>
    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    /// <summary>
    /// 保存・リセット操作が進行中かどうか。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditResourceLimits))]
    public partial bool IsSaving { get; private set; }

    /// <summary>
    /// 一度でも要件確認（Refresh）を実行したかどうか。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRequirementsWarningVisible))]
    public partial bool HasCheckedRequirements { get; private set; }

    /// <summary>
    /// 現在のリソース制限を正常に読み込めているかどうか。読み込みに失敗した状態では
    /// 空欄・古い入力で <c>.wslconfig</c> を上書きしてしまわないよう編集を無効化する。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditResourceLimits))]
    public partial bool HasLoadedResourceLimits { get; private set; }

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
    /// ステータスメッセージを表示するかどうか。
    /// </summary>
    public bool IsStatusMessageVisible => !string.IsNullOrEmpty(StatusMessage);

    /// <summary>
    /// リソース制限を編集できるかどうか。要件を満たし、リソース制限を読み込めており、
    /// かつ保存中でないときのみ編集可能。
    /// </summary>
    public bool CanEditResourceLimits => MeetsRequirements && HasLoadedResourceLimits && !IsSaving;

    /// <summary>
    /// 要件未達の警告を表示するかどうか。
    /// </summary>
    public bool IsRequirementsWarningVisible => HasCheckedRequirements && !MeetsRequirements;

    /// <summary>
    /// WSL連携状態と現在のリソース制限を読み込む。
    /// </summary>
    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        StatusMessage = null;
        HasLoadedResourceLimits = false;
        try
        {
            var status = await settingsService.GetIntegrationStatusAsync();
            WslVersionText = string.IsNullOrEmpty(status.WslVersion) ? NotDetectedText : status.WslVersion;
            IsWslContainersAvailable = status.IsWslContainersAvailable;
            MeetsRequirements = status.MeetsRequirements;
            IsWslDetected = status.IsWslDetected;
            HasCheckedRequirements = true;

            await ReloadResourceLimitsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 入力されたリソース制限を保存する。要件未達・不正入力の場合はサービスを呼び出さない。
    /// </summary>
    [RelayCommand]
    public async Task SaveAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;

        if (!MeetsRequirements)
        {
            ErrorMessage = RequirementsNotMetMessage;
            return;
        }

        if (!TryParseResourceInput(MemoryMegabytesInput, out var memoryMegabytes)
            || !TryParseResourceInput(ProcessorCountInput, out var processorCount))
        {
            ErrorMessage = InvalidInputMessage;
            return;
        }

        IsSaving = true;
        try
        {
            await settingsService.SaveResourceLimitsAsync(new WslResourceLimits(memoryMegabytes, processorCount));
            await ReloadResourceLimitsAsync();
            StatusMessage = SavedStatusMessage;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>
    /// リソース制限をWSLの既定にリセットする。要件未達の場合はサービスを呼び出さない。
    /// </summary>
    [RelayCommand]
    public async Task ResetAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;

        if (!MeetsRequirements)
        {
            ErrorMessage = RequirementsNotMetMessage;
            return;
        }

        IsSaving = true;
        try
        {
            await settingsService.ResetResourceLimitsAsync();
            await ReloadResourceLimitsAsync();
            StatusMessage = ResetStatusMessage;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }

    private async Task ReloadResourceLimitsAsync()
    {
        var limits = await settingsService.GetResourceLimitsAsync();
        MemoryMegabytesInput = FormatLimit(limits.MemoryMegabytes);
        ProcessorCountInput = FormatLimit(limits.ProcessorCount);
        HasLoadedResourceLimits = true;
    }

    private static string FormatLimit(int? value) => value?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;

    private static bool TryParseResourceInput(string input, out int? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            return true;
        }

        if (int.TryParse(input.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var parsed) && parsed > 0)
        {
            value = parsed;
            return true;
        }

        return false;
    }
}
