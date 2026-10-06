using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace Cooked.Cinematics.Editor
{
    /// <summary>Updates only owned assets; never saves or changes the user's open scene.</summary>
    public static class SingleArtworkAuthoring
    {
        public static void Apply()
        {
            foreach (string name in new[] { "Opening", "Ending" })
            {
                var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(CinematicAssetBuilder.AssetRoot + "/Timelines/" + name + ".playable");
                if (timeline == null) throw new InvalidOperationException("Missing " + name);
                var track = timeline.GetOutputTracks().OfType<CinematicArtworkTrack>().Single();
                var clips = track.GetClips().OrderBy(c => c.start).ToArray();
                if (clips.Length != 4) throw new InvalidOperationException("Expected four source artworks.");
                for (int i = 0; i < clips.Length; i++)
                {
                    clips[i].start = i * 3;
                    clips[i].duration = 3;
                    clips[i].easeInDuration = clips[i].easeOutDuration = 0;
                    var data = new SerializedObject(clips[i].asset);
                    data.FindProperty("endScale").floatValue = 1;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(clips[i].asset);
                }
                timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
                timeline.fixedDuration = 12;
                EditorUtility.SetDirty(track);
                EditorUtility.SetDirty(timeline);
                AssetDatabase.SaveAssetIfDirty(timeline);
            }
            var root = PrefabUtility.LoadPrefabContents(CinematicAssetBuilder.PrefabPath);
            try
            {
                var visual = root.transform.Find("PresentationRoot/Visual");
                visual.Find("HalftoneGutter").gameObject.SetActive(false);
                var board = (RectTransform)visual.Find("ComicBoard");
                Stretch(board);
                for (int i = 1; i <= 4; i++)
                {
                    var panel = (RectTransform)board.Find("Panel" + i);
                    Stretch(panel);
                    Stretch((RectTransform)panel.Find("Inner"));
                    panel.GetComponent<UnityEngine.UI.Image>().enabled = false;
                    panel.Find("Inner").GetComponent<UnityEngine.UI.Image>().enabled = false;
                }
                PrefabUtility.SaveAsPrefabAsset(root, CinematicAssetBuilder.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }
}
