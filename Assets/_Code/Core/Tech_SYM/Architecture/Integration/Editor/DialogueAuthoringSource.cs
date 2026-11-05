using System;
using System.Linq;
using Cooked.Dialogue;
using Cooked.Foundation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cooked.Integration.Editor
{
    /// <summary>Inspector 전용. 설치된 Bootstrap의 단일 원본을 읽으며 씬을 저장하지 않는다.</summary>
    public static class DialogueAuthoringSource
    {
        private static string cachedPath;
        private static Hash128 cachedHash;
        private static TextAsset cachedSource;
        private static string cachedJson;
        private static DialogueTableService cachedService;

        public static TextAsset Resolve()
        {
            var installation = AssetDatabase.LoadAssetAtPath<ArchitectureInstallation>(ArchitectureInstallation.AssetPath);
            if (installation == null) throw new InvalidOperationException("ArchitectureInstallation을 연결하세요.");
            string path = installation.CreateCatalog().BootstrapPath;
            var loaded = SceneManager.GetSceneByPath(path);
            if (loaded.IsValid() && loaded.isLoaded) return Read(loaded);
            var hash = AssetDatabase.GetAssetDependencyHash(path);
            if (cachedPath == path && cachedHash == hash && cachedSource != null) return cachedSource;
            var preview = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                var source = Read(preview);
                cachedPath = path; cachedHash = hash; cachedSource = source;
                return source;
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static TextAsset Read(Scene scene)
        {
            var apps = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AppComposition>(true)).ToArray();
            if (apps.Length != 1) throw new InvalidOperationException("Bootstrap 씬에 AppComposition이 정확히 하나 필요합니다.");
            return apps[0].DialogueSource != null ? apps[0].DialogueSource
                : throw new InvalidOperationException("Bootstrapper의 대화 원본 JSON을 연결하세요.");
        }

        public static string[] ReadChoices(out TextAsset source)
        {
            source = Resolve();
            string json = source.text;
            // 내용 비교로 같은 자산 재임포트와 키 추가/삭제를 즉시 반영한다. 실패 결과는 게시하지 않는다.
            if (cachedService == null || !string.Equals(cachedJson, json, StringComparison.Ordinal))
            {
                var service = new DialogueTableService(source.name);
                service.LoadJson(json);
                cachedJson = json; cachedService = service;
            }
            return cachedService.Codes.ToArray();
        }
    }
}
