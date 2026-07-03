// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Data;
using Windows.ApplicationModel.Resources;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop_App.Converters;

/// <summary>
/// <see cref="ContainerState"/> をローカライズされた表示テキストに変換する。
/// ListView.ItemTemplate内のx:Bindはページ(名前付き要素)を直接参照できないため、
/// 変換ロジックはコンバーターとして切り出している。
/// </summary>
public sealed class ContainerStateToDisplayTextConverter : IValueConverter
{
    private readonly ResourceLoader _resourceLoader = new();

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not ContainerState state)
        {
            return string.Empty;
        }

        return state switch
        {
            ContainerState.Running => _resourceLoader.GetString("ContainerState_Running"),
            ContainerState.Stopped => _resourceLoader.GetString("ContainerState_Stopped"),
            _ => state.ToString(),
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
