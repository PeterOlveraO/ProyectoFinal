#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

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

        /// <summary>Exporta el proyecto de Xcode para iOS (el .ipa se genera después con xcodebuild).</summary>
        public static void BuildIOS()
        {
            string path = GetArgument("-customBuildPath") ?? "build/iOS/iOS";

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

            if (Application.isBatchMode)
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
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
