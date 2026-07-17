using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using WslContainersDesktop_App_Tests.Testing;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]
[assembly: WinUITestTarget(typeof(UiTestApplication))]
