using System;
using System.IO;
using JobCheck.Persistence;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 每次 Standalone Build 完成後，將版本控制內的 Demo 複製到執行檔旁。
/// 只替換 JobCheckData/demo，絕不碰觸 JobCheckData/personal。
/// </summary>
public sealed class CopyJobCheckDemoDataToBuild : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.StandaloneWindows
            && report.summary.platform != BuildTarget.StandaloneWindows64)
        {
            throw new BuildFailedException(
                "JobCheck 可攜式資料目前只支援 Windows Standalone Build。");
        }

        string outputPath = Path.GetFullPath(report.summary.outputPath);
        string outputDirectory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new BuildFailedException("無法取得 Build 輸出目錄：" + outputPath);
        }

        JobCheckDataPathSet editorPaths =
            JobCheckDataPathResolver.ResolveForEditor(Application.dataPath);
        string source = editorPaths.DemoRoot;
        string destination;
        try
        {
            destination = JobCheckDemoDataPublisher.Publish(source, outputDirectory);
        }
        catch (Exception exception) when (
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is ArgumentException
            || exception is InvalidOperationException
            || exception is NotSupportedException)
        {
            throw new BuildFailedException(
                "複製 JobCheck Demo 資料失敗：" + exception.Message);
        }

        Debug.Log("JobCheck Demo 資料已複製到 Build：" + destination);
    }
}
