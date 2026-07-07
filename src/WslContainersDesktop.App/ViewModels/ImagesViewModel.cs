// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;
using WslContainersDesktop_App.Collections;

namespace WslContainersDesktop_App.ViewModels;

/// <summary>
/// コンテナーイメージ一覧の表示・操作を管理するViewModel。
/// </summary>
public sealed partial class ImagesViewModel : ObservableObject
{
    private const string PullingStatusMessage = "Pulling image...";
    private const string PullCompletedStatusMessage = "Pull completed.";
    private const string EmptyPullReferenceMessage = "Image reference is required.";
    private const string RunCompletedStatusMessage = "Container started.";

    private readonly IImageManagementService _imageManagementService;
    private readonly IContainerManagementService _containerManagementService;

    /// <summary>
    /// <see cref="ImagesViewModel"/> の新しいインスタンスを初期化する。
    /// </summary>
    /// <param name="imageManagementService">イメージ管理ユースケース。</param>
    /// <param name="containerManagementService">コンテナー管理ユースケース。</param>
    public ImagesViewModel(IImageManagementService imageManagementService, IContainerManagementService containerManagementService)
    {
        _imageManagementService = imageManagementService;
        _containerManagementService = containerManagementService;
    }

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
    /// 起動するコンテナー名。
    /// </summary>
    [ObservableProperty]
    public partial string RunContainerName { get; set; } = string.Empty;

    /// <summary>
    /// 停止時にコンテナーを自動削除するかどうか。
    /// </summary>
    [ObservableProperty]
    public partial bool RunRemoveWhenStopped { get; set; }

    /// <summary>
    /// 起動時に公開するポートマッピングの複数行テキスト。
    /// </summary>
    [ObservableProperty]
    public partial string RunPortMappingsText { get; set; } = string.Empty;

    /// <summary>
    /// 起動時に渡す環境変数の複数行テキスト。
    /// </summary>
    [ObservableProperty]
    public partial string RunEnvironmentVariablesText { get; set; } = string.Empty;

    /// <summary>
    /// イメージ既定コマンドの代わりに実行するコマンド。
    /// </summary>
    [ObservableProperty]
    public partial string RunCommandText { get; set; } = string.Empty;

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
    /// イメージからのコンテナー起動が進行中かどうか。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRunImage))]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    public partial bool IsRunningImage { get; private set; }

    /// <summary>
    /// イメージからコンテナーを起動できるかどうか。
    /// </summary>
    public bool CanRunImage => !IsRunningImage;

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
            var images = await _imageManagementService.GetImagesAsync();
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
            await _imageManagementService.PullAsync(imageReference);
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
            await _imageManagementService.DeleteAsync(row.Id);
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

    /// <summary>
    /// 指定したイメージから新しいコンテナーを起動する。
    /// </summary>
    /// <param name="row">起動元イメージの行。</param>
    [RelayCommand(CanExecute = nameof(CanRunImage))]
    public async Task RunAsync(ImageRowViewModel row)
    {
        StatusMessage = null;
        ErrorMessage = null;
        IsRunningImage = true;
        try
        {
            var request = new ContainerRunRequest
            {
                ImageReference = GetImageReference(row),
                ContainerName = RunContainerName.Trim(),
                RemoveWhenStopped = RunRemoveWhenStopped,
                PortMappings = SplitTrimmedLines(RunPortMappingsText),
                EnvironmentVariables = SplitTrimmedLines(RunEnvironmentVariablesText),
                Command = RunCommandText.Trim(),
            };

            await _containerManagementService.RunAsync(request);
            StatusMessage = RunCompletedStatusMessage;
            ErrorMessage = null;
            ClearRunInputs();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = null;
        }
        finally
        {
            IsRunningImage = false;
        }
    }

    private async Task RefreshImagesAfterPullAsync()
    {
        try
        {
            var images = await _imageManagementService.GetImagesAsync();
            ReplaceImages(images);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void ReplaceImages(IReadOnlyList<ContainerImage> images)
    {
        ObservableCollectionReconciler.Reconcile(
            Images,
            images,
            row => BuildImageKey(row.Id, row.Repository, row.Tag),
            image => BuildImageKey(image.Id, image.Repository, image.Tag),
            image => new ImageRowViewModel(image));

        IsEmpty = Images.Count == 0;
    }

    private static string BuildImageKey(string id, string repository, string tag)
        => string.Join('\u001f', id, repository, tag);

    private static string GetImageReference(ImageRowViewModel row)
    {
        if (!string.IsNullOrEmpty(row.Repository)
            && !string.IsNullOrEmpty(row.Tag)
            && row.Repository != "<none>"
            && row.Tag != "<none>")
        {
            return $"{row.Repository}:{row.Tag}";
        }

        return row.Id;
    }

    private static IReadOnlyList<string> SplitTrimmedLines(string text)
    {
        return text
            .Split(['\r', '\n'])
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();
    }

    private void ClearRunInputs()
    {
        RunContainerName = string.Empty;
        RunRemoveWhenStopped = false;
        RunPortMappingsText = string.Empty;
        RunEnvironmentVariablesText = string.Empty;
        RunCommandText = string.Empty;
    }
}
