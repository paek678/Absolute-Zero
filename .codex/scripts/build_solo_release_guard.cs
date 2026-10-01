using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public static class BuildSoloReleaseGuard
{
    public static string Main()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        string path = Path.Combine(Path.GetTempPath(), "AZPlan036ReleaseGuard", "Player", "AbsoluteZeroReleaseGuard.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(x => x.enabled).Select(x => x.path).ToArray(),
            locationPathName = path,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.DetailedBuildReport,
            extraScriptingDefines = new[] { "PLAN036_RELEASE_GUARD" }
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("Non-development guard build failed: " + report.summary.totalErrors);
        return "Succeeded|errors=" + report.summary.totalErrors + "|warnings=" + report.summary.totalWarnings
            + "|backend=" + PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone)
            + "|stripping=" + PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Standalone) + "|path=" + path;
    }
}
