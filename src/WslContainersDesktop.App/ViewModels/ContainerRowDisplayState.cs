// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using WslContainersDesktop.Domain;

namespace WslContainersDesktop_App.ViewModels;

/// <summary>
/// State列の表示に必要な情報をまとめた値。実際のコンテナ状態と、進行中の操作種別（あれば）を組み合わせる。
/// </summary>
/// <param name="State">実際のコンテナ状態。</param>
/// <param name="PendingOperation">進行中の操作種別。進行中の操作がない場合は <see cref="ContainerRowOperation.None"/>。</param>
public readonly record struct ContainerRowDisplayState(ContainerState State, ContainerRowOperation PendingOperation);
