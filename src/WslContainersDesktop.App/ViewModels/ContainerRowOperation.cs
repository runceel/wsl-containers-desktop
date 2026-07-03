// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace WslContainersDesktop_App.ViewModels;

/// <summary>
/// コンテナ行に対して現在進行中の操作の種別。一覧のState列に途中状態を表示するために使う。
/// </summary>
public enum ContainerRowOperation
{
    /// <summary>
    /// 進行中の操作はない。
    /// </summary>
    None,

    /// <summary>
    /// 起動処理が進行中。
    /// </summary>
    Starting,

    /// <summary>
    /// 停止処理が進行中。
    /// </summary>
    Stopping,

    /// <summary>
    /// 再起動処理が進行中。
    /// </summary>
    Restarting,

    /// <summary>
    /// 削除処理が進行中。
    /// </summary>
    Deleting,
}
