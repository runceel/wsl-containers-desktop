// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop_App.ViewModels;

/// <summary>
/// コンテナーイメージ一覧の表示・操作を管理するViewModel。
/// </summary>
public sealed partial class ImagesViewModel(IImageManagementService imageManagementService) : ObservableObject
{
    private const string PullingStatusMessage = "Pulling image...";
    private const string PullCompletedStatusMessage = "Pull completed.";
    private const string EmptyPullReferenceMessage = "Image reference is required.";

    /// <summary>
    /// 現在表示中のコンテナーイメージ一覧。
    /// </summary>
    public ObservableCollection<ImageRowViewModel> Images { get; } = [];

    /// <summary>
    /// Pull操作で取得するイメージ参照。
    /// </summary>
    [ObservableProperty]
    public partial string PullReference { get; set; } = string.Empty;

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
    /// Pull操作が進行中かどうか。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPull))]
    public partial bool IsPulling { get; private set; }

    /// <summary>
    /// Pull操作を開始できるかどうか。
    /// </summary>
    public bool CanPull => !IsPulling;

    /// <summary>
    /// <see cref="Images"/> が空かどうか。
    /// </summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; } = true;

    /// <summary>
    /// コンテナーイメージ一覧を手動で再取得する。
    /// </summary>
    [RelayCommand]
    public async Task RefreshAsync()
    {
        StatusMessage = null;
        try
        {
            var images = await imageManagementService.GetImagesAsync();
            ReplaceImages(images);
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>
    /// 入力されたイメージ参照を取得し、成功後に一覧を再取得する。
    /// </summary>
    [RelayCommand]
    public async Task PullAsync()
    {
        var imageReference = PullReference.Trim();
        StatusMessage = null;
        ErrorMessage = null;
        if (imageReference.Length == 0)
        {
            ErrorMessage = EmptyPullReferenceMessage;
            return;
        }

        StatusMessage = PullingStatusMessage;
        IsPulling = true;
        try
        {
            await imageManagementService.PullAsync(imageReference);
            PullReference = string.Empty;
            StatusMessage = PullCompletedStatusMessage;
            ErrorMessage = null;
            await RefreshImagesAfterPullAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = null;
        }
        finally
        {
            IsPulling = false;
        }
    }

    /// <summary>
    /// 指定したコンテナーイメージを削除する。
    /// </summary>
    /// <param name="row">対象の行。</param>
    [RelayCommand]
    public async Task DeleteAsync(ImageRowViewModel row)
    {
        StatusMessage = null;
        row.IsBusy = true;
        try
        {
            await imageManagementService.DeleteAsync(row.Id);
            var liveRow = Images.FirstOrDefault(image => image.Id == row.Id);
            if (liveRow is not null)
            {
                liveRow.IsBusy = false;
                Images.Remove(liveRow);
            }
            else
            {
                row.IsBusy = false;
            }

            IsEmpty = Images.Count == 0;
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            row.IsBusy = false;
            ErrorMessage = ex.Message;
        }
    }

    private async Task RefreshImagesAfterPullAsync()
    {
        try
        {
            var images = await imageManagementService.GetImagesAsync();
            ReplaceImages(images);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void ReplaceImages(IReadOnlyList<ContainerImage> images)
    {
        Images.Clear();
        foreach (var image in images)
        {
            Images.Add(new ImageRowViewModel(image));
        }

        IsEmpty = Images.Count == 0;
    }
}
