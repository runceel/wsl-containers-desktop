// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Data;

namespace WslContainersDesktop_App.Converters;

/// <summary>
/// <see cref="DateTimeOffset"/> をローカルタイムゾーンの表示テキストに変換する。
/// </summary>
public sealed class DateTimeOffsetToLocalStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is DateTimeOffset dateTimeOffset
            ? dateTimeOffset.LocalDateTime.ToString("yyyy-MM-dd HH:mm")
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
