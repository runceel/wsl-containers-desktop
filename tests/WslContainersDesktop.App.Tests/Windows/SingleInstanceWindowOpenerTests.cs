// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using WslContainersDesktop_App.Windows;

namespace WslContainersDesktop_App_Tests.Windows;

[TestClass]
public sealed class SingleInstanceWindowOpenerTests
{
    // テスト対象は実際のWinUI Windowを必要としない、ただのCLRオブジェクト。
    // ShowOrActivateがWindow型に依存せず「生成/再利用/Closed後の再生成」だけを
    // 検証できることを示す。
    private sealed class FakeWindow
    {
        public int ActivateCallCount { get; private set; }

        public void Activate() => ActivateCallCount++;
    }

    [TestMethod]
    public void ShowOrActivate_FirstCall_CreatesAndActivatesWindow()
    {
        // Arrange
        var createCallCount = 0;
        FakeWindow? createdWindow = null;
        var sut = new SingleInstanceWindowOpener<FakeWindow>(
            createWindow: () =>
            {
                createCallCount++;
                createdWindow = new FakeWindow();
                return createdWindow;
            },
            activateWindow: w => w.Activate(),
            registerClosedHandler: (_, _) => { });

        // Act
        sut.ShowOrActivate();

        // Assert
        Assert.AreEqual(1, createCallCount, "初回呼び出しではウィンドウを1つ生成する必要がある。");
        Assert.AreEqual(1, createdWindow!.ActivateCallCount, "生成しただけでなく、必ずActivateする必要がある(前面表示されないと導線として機能しない)。");
    }

    [TestMethod]
    public void ShowOrActivate_CalledAgainWithoutClosing_ReusesExistingWindowAndActivatesIt()
    {
        // Arrange
        var createCallCount = 0;
        FakeWindow? createdWindow = null;
        var sut = new SingleInstanceWindowOpener<FakeWindow>(
            createWindow: () =>
            {
                createCallCount++;
                createdWindow = new FakeWindow();
                return createdWindow;
            },
            activateWindow: w => w.Activate(),
            registerClosedHandler: (_, _) => { });

        // Act
        sut.ShowOrActivate();
        sut.ShowOrActivate();

        // Assert
        Assert.AreEqual(1, createCallCount, "既存ウィンドウが開いている間は新規に生成してはいけない。");
        Assert.AreEqual(2, createdWindow!.ActivateCallCount, "2回目の呼び出しでは既存ウィンドウをActivateする必要がある。");
    }

    [TestMethod]
    public void ShowOrActivate_CalledAfterClosedCallbackInvoked_CreatesNewWindow()
    {
        // Arrange
        var createCallCount = 0;
        Action? closedCallback = null;
        var sut = new SingleInstanceWindowOpener<FakeWindow>(
            createWindow: () =>
            {
                createCallCount++;
                return new FakeWindow();
            },
            activateWindow: w => w.Activate(),
            registerClosedHandler: (_, onClosed) => closedCallback = onClosed);

        // Act
        sut.ShowOrActivate();
        closedCallback!.Invoke();
        sut.ShowOrActivate();

        // Assert
        Assert.AreEqual(2, createCallCount, "Closedコールバック後の呼び出しでは新しいウィンドウを生成し直す必要がある。");
    }

    [TestMethod]
    public void ShowOrActivate_ReopenedAfterClose_RegistersClosedHandlerAgainForSecondWindow()
    {
        // Arrange: Closed後の再オープン→再クローズという2サイクル目でも、2つ目のウィンドウに対して
        // ちゃんとClosedハンドラーが登録され、3回目のShowOrActivateで正しく作り直されることを確認する。
        var createCallCount = 0;
        Action? closedCallback = null;
        var sut = new SingleInstanceWindowOpener<FakeWindow>(
            createWindow: () =>
            {
                createCallCount++;
                return new FakeWindow();
            },
            activateWindow: w => w.Activate(),
            registerClosedHandler: (_, onClosed) => closedCallback = onClosed);

        // Act
        sut.ShowOrActivate(); // 1つ目生成
        closedCallback!.Invoke(); // 1つ目クローズ
        sut.ShowOrActivate(); // 2つ目生成
        closedCallback!.Invoke(); // 2つ目クローズ(2つ目に対してもハンドラーが登録されている必要がある)
        sut.ShowOrActivate(); // 3つ目生成

        // Assert
        Assert.AreEqual(3, createCallCount, "何度クローズ→再オープンを繰り返しても、そのたびに新しいウィンドウを生成し直す必要がある。");
    }
}
