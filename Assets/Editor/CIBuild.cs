#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Game.Terrain;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds para CI (GameCI unity-builder con buildMethod).
    ///
    /// Las escenas se pasan aquí de forma explícita, así que el build no depende de
    /// EditorBuildSettings. Los cambios de PlayerSettings que se hacen solo viven en el
    /// workspace del runner (no se guardan en el repositorio).
    /// </summary>
    public static class CIBuild
    {
        static readonly string[] Scenes = { "Assets/Scenes/World.unity" };

        const string BundleId = "com.peterolvera.proyectofinal";

        /// <summary>Native Linux player; the packaged launcher needs no Editor or Unity Hub.</summary>
        public static void BuildLinux()
        {
            string path = GetArgument("-customBuildPath") ?? "Builds/Linux/PKETER/PKETER.x86_64";
            path = Path.Combine(Path.HasExtension(path) ? Path.GetDirectoryName(path) : path, "PKETER.x86_64");
            var backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            int width = PlayerSettings.defaultScreenWidth, height = PlayerSettings.defaultScreenHeight;
            bool resizable = PlayerSettings.resizableWindow;
            var fullscreen = PlayerSettings.fullScreenMode;
            BuildReport report;
            try
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                PlayerSettings.defaultScreenWidth = 1280;
                PlayerSettings.defaultScreenHeight = 720;
                PlayerSettings.resizableWindow = true;
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = Scenes,
                    locationPathName = path,
                    target = BuildTarget.StandaloneLinux64,
                    options = BuildOptions.None
                });
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, backend);
                PlayerSettings.defaultScreenWidth = width;
                PlayerSettings.defaultScreenHeight = height;
                PlayerSettings.resizableWindow = resizable;
                PlayerSettings.fullScreenMode = fullscreen;
            }
            ReportSummary(report.summary);
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Linux: " + report.summary.result);
            string directory = Path.GetDirectoryName(path);
            File.Copy("Tools/Linux/Jugar.sh", Path.Combine(directory, "Jugar.sh"), true);
            File.Copy("Tools/Linux/LEEME.txt", Path.Combine(directory, "LEEME.txt"), true);
            File.WriteAllText(Path.Combine(directory, "build-info.txt"),
                "Unity " + Application.unityVersion + "\nLinux x86_64 / Mono\n" +
                "Built UTC: " + DateTime.UtcNow.ToString("O") + "\n");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>Exporta el proyecto de Xcode para iOS (el .ipa se genera después con xcodebuild).</summary>
        public static void BuildIOS()
        {
            string path = GetArgument("-customBuildPath") ?? "build/iOS/iOS";

            var terrain = AssetDatabase.LoadAssetAtPath<TerrainSettings>("Assets/Settings/TerrainSettings.asset");
            if (terrain == null || terrain.terrainMaterial == null || terrain.terrainMaterial.shader == null)
                throw new BuildFailedException("Falta TerrainSettings o M_TerrainBase: el terreno no se verá en iPhone.");
            if (!System.IO.File.Exists(Scenes[0]))
                throw new BuildFailedException("La escena World no existe en " + Scenes[0]);

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.iOS.appleEnableAutomaticSigning = false;

            var options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = path,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[CIBuild] iOS: " + report.summary.result + " -> " + path +
                      " (" + report.summary.totalErrors + " errores)");

            ReportSummary(report.summary);

            if (Application.isBatchMode)
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>
        /// GameCI (unity-builder v6) revisa el log y marca el build como fallido si no encuentra
        /// "Build succeeded!" o la sección "# Build results #", aunque Unity haya salido con 0.
        /// </summary>
        static void ReportSummary(BuildSummary summary)
        {
            Console.WriteLine(
                "\n###########################\n" +
                "#      Build results      #\n" +
                "###########################\n\n" +
                "Duration: " + summary.totalTime + "\n" +
                "Warnings: " + summary.totalWarnings + "\n" +
                "Errors: " + summary.totalErrors + "\n" +
                "Size: " + summary.totalSize + " bytes\n");

            Console.WriteLine(summary.result == BuildResult.Succeeded
                ? "Build succeeded!"
                : "Build failed! (" + summary.result + ")");
        }

        static string GetArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
#endif
