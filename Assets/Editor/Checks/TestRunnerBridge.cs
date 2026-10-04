using System.IO;
using System.Text;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

// exec TestRunnerBridge.RunEditMode → resultados en FrostboundBridge/tests.txt
public static class TestRunnerBridge
{
    private static string ResultFile => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "FrostboundBridge", "tests.txt");

    public static string RunEditMode()
    {
        File.WriteAllText(ResultFile, "RUNNING\n");
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new Callbacks());
        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
        return "ejecutando";
    }

    private class Callbacks : ICallbacks
    {
        private readonly StringBuilder _sb = new StringBuilder();

        public void RunStarted(ITestAdaptor testsToRun) { }

        public void RunFinished(ITestResultAdaptor result)
        {
            _sb.Insert(0, string.Format("DONE pasaron {0} · fallaron {1} · omitidos {2} · {3:F1} s\n", result.PassCount, result.FailCount, result.SkipCount, result.Duration));
            File.WriteAllText(ResultFile, _sb.ToString());
        }

        public void TestStarted(ITestAdaptor test) { }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.HasChildren) return;
            _sb.Append(result.TestStatus).Append("  ").Append(result.Test.FullName);
            if (result.TestStatus == TestStatus.Failed) _sb.Append("\n    ").Append(result.Message?.Trim().Replace("\n", "\n    "));
            _sb.Append('\n');
        }
    }
}
