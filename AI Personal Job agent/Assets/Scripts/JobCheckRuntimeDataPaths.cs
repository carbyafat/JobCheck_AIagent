using JobCheck.Persistence;
using UnityEngine;

/// <summary>將 Unity 執行環境轉成 JobCheck 的集中式資料路徑。</summary>
public static class JobCheckRuntimeDataPaths
{
    public static JobCheckDataPathSet Current => Application.isEditor
        ? JobCheckDataPathResolver.ResolveForEditor(Application.dataPath)
        : JobCheckDataPathResolver.ResolveForPlayer(Application.dataPath);

    public static string DemoRoot => Current.DemoRoot;
    public static string PersonalRoot => Current.PersonalRoot;
}
