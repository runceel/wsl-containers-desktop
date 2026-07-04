// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace WslContainersDesktop_App.Navigation;

/// <summary>
/// アプリケーション内でナビゲーション可能なページを識別するキー。
/// </summary>
public enum NavigationPageKey
{
    /// <summary>コンテナー一覧ページ。</summary>
    Containers,

    /// <summary>コンテナーイメージ一覧ページ。</summary>
    Images,

    /// <summary>コンテナーボリューム一覧ページ。</summary>
    Volumes,

    /// <summary>設定ページ。</summary>
    Settings,
}
