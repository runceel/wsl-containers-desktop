using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop_App.ViewModels;

/// <summary>
/// コンテナーボリューム一覧の表示・操作を管理するViewModel。
/// </summary>
public sealed partial class VolumesViewModel(IVolumeManagementService volumeManagementService) : ObservableObject
{
    private const string CreatingStatusMessage = "Creating volume...";
    private const string CreateCompletedStatusMessage = "Volume created.";
    private const string EmptyVolumeNameMessage = "Volume name is required.";

    /// <summary>
    /// 現在表示中のコンテナーボリューム一覧。
    /// </summary>
    public ObservableCollection<VolumeRowViewModel> Volumes { get; } = [];

    /// <summary>
    /// 作成する新しいボリューム名。
    /// </summary>
    [ObservableProperty]
    public partial string NewVolumeName { get; set; } = string.Empty;

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
    /// ボリューム作成操作が進行中かどうか。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCreate))]
    public partial bool IsCreating { get; private set; }

    /// <summary>
    /// ボリューム作成操作を開始できるかどうか。
    /// </summary>
    public bool CanCreate => !IsCreating;

    /// <summary>
    /// <see cref="Volumes"/> が空かどうか。
    /// </summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; } = true;

    /// <summary>
    /// コンテナーボリューム一覧を手動で再取得する。
    /// </summary>
    [RelayCommand]
    public async Task RefreshAsync()
    {
        StatusMessage = null;
        try
        {
            var volumes = await volumeManagementService.GetVolumesAsync();
            ReplaceVolumes(volumes);
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = null;
        }
    }

    /// <summary>
    /// 入力されたボリューム名で新しいボリュームを作成し、成功後に一覧を再取得する。
    /// </summary>
    [RelayCommand]
    public async Task CreateAsync()
    {
        var volumeName = NewVolumeName.Trim();
        StatusMessage = null;
        ErrorMessage = null;
        if (volumeName.Length == 0)
        {
            ErrorMessage = EmptyVolumeNameMessage;
            return;
        }

        StatusMessage = CreatingStatusMessage;
        IsCreating = true;
        try
        {
            var created = await volumeManagementService.CreateAsync(volumeName);
            NewVolumeName = string.Empty;
            StatusMessage = CreateCompletedStatusMessage;
            ErrorMessage = null;
            await RefreshVolumesAfterCreateAsync(created);
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
    /// 指定したコンテナーボリュームを削除する。
    /// </summary>
    /// <param name="row">対象の行。</param>
    [RelayCommand]
    public async Task DeleteAsync(VolumeRowViewModel row)
    {
        StatusMessage = null;
        if (row.IsInUse)
        {
            ErrorMessage = FormatInUseMessage(row.Name, row.ReferencingContainerNames);
            return;
        }

        row.IsBusy = true;
        try
        {
            await volumeManagementService.DeleteAsync(row.Name);
            var liveRow = Volumes.FirstOrDefault(volume => volume.Name == row.Name);
            if (liveRow is not null)
            {
                liveRow.IsBusy = false;
                Volumes.Remove(liveRow);
            }
            else
            {
                row.IsBusy = false;
            }

            IsEmpty = Volumes.Count == 0;
            ErrorMessage = null;
        }
        catch (VolumeInUseException ex)
        {
            row.IsBusy = false;
            ErrorMessage = FormatInUseMessage(ex.VolumeName, ex.ReferencingContainerNames);
        }
        catch (Exception ex)
        {
            row.IsBusy = false;
            ErrorMessage = ex.Message;
        }
    }

    private async Task RefreshVolumesAfterCreateAsync(ContainerVolume created)
    {
        try
        {
            var volumes = await volumeManagementService.GetVolumesAsync();
            ReplaceVolumes(volumes.Count == 0 ? [created] : volumes);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = null;
        }
    }

    private void ReplaceVolumes(IReadOnlyList<ContainerVolume> volumes)
    {
        Volumes.Clear();
        foreach (var volume in volumes)
        {
            Volumes.Add(new VolumeRowViewModel(volume));
        }

        IsEmpty = Volumes.Count == 0;
    }

    private static string FormatInUseMessage(string volumeName, IReadOnlyList<string> referencingContainerNames)
    {
        return $"Volume '{volumeName}' is in use by: {string.Join(", ", referencingContainerNames)}";
    }
}
