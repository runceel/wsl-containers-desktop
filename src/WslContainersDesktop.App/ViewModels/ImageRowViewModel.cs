// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.ComponentModel;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop_App.ViewModels;

/// <summary>
/// XAMLバインディング用に <see cref="ContainerImage"/> をラップする行ViewModel。
/// </summary>
public sealed partial class ImageRowViewModel : ObservableObject
{
    /// <summary>
    /// <see cref="ImageRowViewModel"/> の新しいインスタンスを初期化する。
    /// </summary>
    /// <param name="image">初期状態の元となるコンテナーイメージ。</param>
    public ImageRowViewModel(ContainerImage image)
    {
        Id = image.Id;
        Repository = image.Repository;
        Tag = image.Tag;
        DisplayName = image.DisplayName;
        SizeBytes = image.SizeBytes;
        CreatedAt = image.CreatedAt;
    }

    /// <summary>
    /// イメージID。
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// リポジトリ名。
    /// </summary>
    public string Repository { get; }

    /// <summary>
    /// タグ名。
    /// </summary>
    public string Tag { get; }

    /// <summary>
    /// 表示用イメージ名。
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// イメージサイズ（バイト）。
    /// </summary>
    public long SizeBytes { get; }

    /// <summary>
    /// 作成日時。
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// この行に対する操作が進行中かどうか。
    /// </summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; } = false;
}
