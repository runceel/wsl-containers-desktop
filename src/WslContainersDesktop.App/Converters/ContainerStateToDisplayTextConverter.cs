// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Data;
using Windows.ApplicationModel.Resources;
using WslContainersDesktop.Domain;
using WslContainersDesktop_App.ViewModels;

namespace WslContainersDesktop_App.Converters;

/// <summary>
/// <see cref="ContainerRowDisplayState"/>（実際の状態＋進行中の操作種別）に対応するリソースキーを選択する
/// 純粋ロジック。WinUIの型に依存しないため単体テストできる。実際のローカライズ済みテキスト取得は
/// <see cref="ContainerStateToDisplayTextConverter"/> が <see cref="ResourceLoader"/> 経由で行う。
/// </summary>
public static class ContainerDisplayStateResourceKeySelector
{
    public static string GetResourceKey(ContainerRowDisplayState displayState) => displayState.PendingOperation switch
    {
        ContainerRowOperation.Starting => "ContainerState_Starting",
        ContainerRowOperation.Stopping => "ContainerState_Stopping",
        ContainerRowOperation.Restarting => "ContainerState_Restarting",
        ContainerRowOperation.Deleting => "ContainerState_Deleting",
        _ => displayState.State switch
        {
            ContainerState.Running => "ContainerState_Running",
            ContainerState.Stopped => "ContainerState_Stopped",
            _ => string.Empty,
        },
    };
}

/// <summary>
/// <see cref="ContainerRowDisplayState"/> をローカライズされた表示テキストに変換する。
/// ListView.ItemTemplate内のx:Bindはページ(名前付き要素)を直接参照できないため、
/// 変換ロジックはコンバーターとして切り出している。
/// </summary>
public sealed class ContainerStateToDisplayTextConverter : IValueConverter
{
    private readonly ResourceLoader _resourceLoader = new();

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not ContainerRowDisplayState displayState)
        {
            return string.Empty;
        }

        var resourceKey = ContainerDisplayStateResourceKeySelector.GetResourceKey(displayState);
        return resourceKey.Length == 0 ? string.Empty : _resourceLoader.GetString(resourceKey);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

