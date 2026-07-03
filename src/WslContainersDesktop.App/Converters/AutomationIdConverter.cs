// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Data;

namespace WslContainersDesktop_App.Converters;

/// <summary>
/// コンテナIDとConverterParameter(ボタンの用途を表すprefix)から、
/// 行ごとに一意なAutomationIdを生成する（E2Eテストでの要素特定用）。
/// </summary>
public sealed class AutomationIdConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        $"{parameter}_{value}";

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
