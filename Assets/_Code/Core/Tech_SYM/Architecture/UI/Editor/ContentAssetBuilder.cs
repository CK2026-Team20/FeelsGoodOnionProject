#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Cooked.UI.Editor
{
    public static class ContentAssetBuilder
    {
        [MenuItem("Cooked/Content/Build All Content Assets")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
            var font = UiPrefabBuilder.BuildFont();
            // Dynamic fonts clear on Editor shutdown. Warm from the actual shipped strings before assertions.
            string corpus = "게임 종료 어니와 포포의 주방 탈출 전체 음량 음악 효과음 체력 눈물 조각 체크포인트 재시도 타이틀로 닫기 이동 점프 상호작용 형태 전환 껍질 회수 트레이 움직이기 옵션 건너뛰기 자동 이전 기록 아직 출력된 대화가 없습니다 ▼ ESC WASD Q R E F Space OVEN_SAFE 0123456789 /—·%!";
            string json = File.ReadAllText("Assets/_Scenes/Tech_SYM/Architecture/Dialogue/Data/DialogueTable.json");
            new Cooked.Dialogue.DialogueTableService("font-validation").LoadJson(json);
            foreach (var row in (JArray)JObject.Parse(json)["Rows"])
                corpus += " " + (string)row["Name"] + " " + (string)row["Context"];
            corpus = new string(corpus.Where(c => !char.IsControl(c)).Distinct().ToArray());
            if (!font.HasCharacters(corpus, out uint[] missing, false, true))
                throw new InvalidOperationException("Korean font cannot render content codepoints: " + string.Join(",", missing.Select(c => "U+" + c.ToString("X4"))));
            EditorUtility.SetDirty(font);
            UiPrefabBuilder.BuildAll();
            Cooked.Dialogue.Editor.DialoguePrefabBuilder.Build(font);
            Cooked.Cinematics.Editor.CinematicAssetBuilder.BuildAll(font);
            Cooked.Audio.Editor.AudioPrefabBuilder.Build();
            Cooked.Background.Editor.KitchenContentBackdropBuilder.Build();
            AssetDatabase.SaveAssets();
            Debug.Log("Content authored: HUD, Dialogue, 2 Timelines/8 artwork, 16 audio clips, 14 backdrop model wrappers.");
        }
    }
}
#endif
