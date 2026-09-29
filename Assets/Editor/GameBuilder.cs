#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEditor.Build.Reporting;
using System.IO;

public static class GameBuilder
{
    private const string ScenePath = "Assets/Scenes/Main.unity";

    public static void BuildWindows()
    {
        Directory.CreateDirectory("Builds/Windows");
        BuildReport report = BuildPipeline.BuildPlayer(new[] { ScenePath }, "Builds/Windows/Game-Unity.exe", BuildTarget.StandaloneWindows64, BuildOptions.None);
        Debug.Log("Windows build result: " + report.summary.result);
        if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    public static void BuildAndroid()
    {
        Directory.CreateDirectory("Builds/Android");
        EditorUserBuildSettings.buildAppBundle = false;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        BuildReport report = BuildPipeline.BuildPlayer(new[] { ScenePath }, "Builds/Android/Game-Unity.apk", BuildTarget.Android, BuildOptions.None);
        Debug.Log("Android build result: " + report.summary.result);
        if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
#endif
