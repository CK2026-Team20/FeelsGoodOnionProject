using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Cooked.Foundation;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Cooked.Integration.Editor
{
    /// <summary>Explicit-scene build. It never sets global EditorBuildSettings or PlayerSettings.</summary>
    public static class IntegrationBuildPipeline
    {
        [Serializable] private sealed class BuildEvidence
        {
            public string startedUtc, finishedUtc, projectPath, executable, result;
            public string[] scenes, changedProjectSettings;
            public int errors, warnings;
            public ulong bytes;
        }
        [MenuItem("Cooked/Integration/Build Windows Player")]
        public static void BuildWindowsPlayer()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Build requires idle Edit Mode.");
            var installation = AssetDatabase.LoadAssetAtPath<ArchitectureInstallation>(ArchitectureInstallation.AssetPath);
            if (installation == null) throw new InvalidOperationException("Architecture installation is not authored yet.");
            var catalog = installation.CreateCatalog();
            var scenes = new[] { catalog.BootstrapPath, catalog.TitlePath, catalog.CorePath }.Concat(catalog.StagePaths).ToArray();
            if (scenes.Length != 6) throw new InvalidOperationException("The approved prototype requires six scenes.");
            foreach (var path in scenes)
            {
                if (!path.StartsWith("Assets/_Scenes/Tech_SYM/Architecture/", StringComparison.Ordinal) ||
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                    throw new InvalidOperationException("Missing or unapproved scene: " + path);
            }
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save reviewed scene edits explicitly before building.");
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string output = Path.Combine(project, "output/Tech_SYM/integration/build/Windows");
            Directory.CreateDirectory(output);
            string executable = Path.Combine(output, "CookedPrototype.exe");
            var before = SettingsHashes(project);
            var evidence = new BuildEvidence { projectPath = project, startedUtc = DateTime.UtcNow.ToString("O"), scenes = scenes, executable = executable };
            try
            {
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes, locationPathName = executable, target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development | BuildOptions.DetailedBuildReport
                });
                evidence.result = report.summary.result.ToString();
                evidence.errors = report.summary.totalErrors;
                evidence.warnings = report.summary.totalWarnings;
                evidence.bytes = report.summary.totalSize;
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Player build failed: " + evidence.result);
            }
            catch (Exception error)
            {
                if (string.IsNullOrEmpty(evidence.result)) evidence.result = "Exception: " + error.Message;
                throw;
            }
            finally
            {
                evidence.finishedUtc = DateTime.UtcNow.ToString("O");
                var after = SettingsHashes(project);
                evidence.changedProjectSettings = before.Keys.Union(after.Keys).Where(p => !before.TryGetValue(p, out var a) || !after.TryGetValue(p, out var b) || a != b).ToArray();
                File.WriteAllText(Path.Combine(output, "build-evidence.json"), JsonUtility.ToJson(evidence, true));
                if (evidence.changedProjectSettings.Length != 0)
                    Debug.LogError("Build changed ProjectSettings; review required: " + string.Join(", ", evidence.changedProjectSettings));
            }
            if (evidence.changedProjectSettings.Length != 0)
                throw new InvalidOperationException("Build did not preserve ProjectSettings. Do not approve for release.");
            Debug.Log("Cooked build created. Runtime LOOP is still unverified: " + executable);
        }
        private static Dictionary<string, string> SettingsHashes(string project)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            using (var sha = SHA256.Create())
                foreach (string file in Directory.GetFiles(Path.Combine(project, "ProjectSettings"), "*", SearchOption.AllDirectories))
                    result.Add(file, BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file))));
            return result;
        }
    }
}

