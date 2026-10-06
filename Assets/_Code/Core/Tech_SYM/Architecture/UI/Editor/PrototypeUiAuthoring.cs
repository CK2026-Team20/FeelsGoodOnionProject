using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Cooked.UI.Editor
{
    public static class PrototypeUiAuthoring
    {
        public static void Apply()
        {
            Edit(UiPrefabBuilder.TitlePath, ConfigureTitle);
            Edit(UiPrefabBuilder.GlobalPath, root =>
            {
                var panel = root.transform.Find("OptionsCanvas/OptionsModal/Panel");
                Place(panel.Find("Close"), new Vector2(1,1), new Vector2(-20,-20), new Vector2(48,48));
                panel.Find("Close").GetComponentInChildren<TMP_Text>(true).text = "X";
                panel.Find("Heading").GetComponent<TMP_Text>().text = "설정";
                // The existing retry command stays available in-game; title has only volume controls.
            });
            Edit(UiPrefabBuilder.GamePath, root =>
            {
                var hud = root.transform.Find("ScreenCanvas/HudRoot");
                hud.Find("FormAbility/Keycap/Key").GetComponent<TMP_Text>().text = "Q";
                hud.Find("TearAbility/Keycap/Key").GetComponent<TMP_Text>().text = "F";
                var recover = hud.Find("RecoverAbility");
                if (recover != null) UnityEngine.Object.DestroyImmediate(recover.gameObject);
                var so = new SerializedObject(root.GetComponent<HudView>());
                so.FindProperty("recoverCard").objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
                root.transform.Find("WorldCanvas/PromptAnchor/Label").GetComponent<TMP_Text>().text = "E  상호작용";
            });
        }
        public static void ConfigureTitle(GameObject root)
        {
            var canvas = root.transform.Find("TitleCanvas");
            Place(canvas.Find("Heading"), new Vector2(.23f,.75f), Vector2.zero, new Vector2(650,170));
            Place(canvas.Find("Subtitle"), new Vector2(.23f,.63f), Vector2.zero, new Vector2(650,80));
            Place(canvas.Find("NewGame"), new Vector2(.23f,.48f), Vector2.zero, new Vector2(460,80));
            Place(canvas.Find("Quit"), new Vector2(.23f,.18f), Vector2.zero, new Vector2(460,80));
            var settings = canvas.Find("Settings");
            if (settings == null)
            {
                settings = UnityEngine.Object.Instantiate(canvas.Find("Quit").gameObject, canvas).transform;
                settings.name = "Settings";
            }
            Place(settings, new Vector2(.23f,.33f), Vector2.zero, new Vector2(460,80));
            settings.GetComponentInChildren<TMP_Text>(true).text = "설정";
            var band = (RectTransform)canvas.Find("KitchenBand");
            band.anchorMax = new Vector2(1,.08f); band.offsetMin = band.offsetMax = Vector2.zero;
            var team = canvas.Find("TeamName");
            if(team == null) { team = UnityEngine.Object.Instantiate(canvas.Find("Subtitle").gameObject,canvas).transform; team.name="TeamName"; }
            team.gameObject.SetActive(true);
            var teamRect=(RectTransform)team; teamRect.anchorMin=teamRect.anchorMax=new Vector2(1,0);teamRect.pivot=new Vector2(1,0);
            teamRect.anchoredPosition=new Vector2(-40,18);teamRect.sizeDelta=new Vector2(620,48);
            var teamText=team.GetComponent<TMP_Text>();teamText.text="FeelsGoodOnionProject"; // Existing project designation; no invented team name.
            teamText.fontSize=28;teamText.color=Color.white;teamText.alignment=TextAlignmentOptions.MidlineRight;teamText.raycastTarget=false;
            var so = new SerializedObject(root.GetComponent<TitleView>());
            so.FindProperty("settings").objectReferenceValue = settings.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Place(Transform transform, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            var r = (RectTransform)transform;
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = anchor == Vector2.one ? Vector2.one : new Vector2(.5f,.5f);
            r.sizeDelta = size; r.anchoredPosition = offset;
        }
        private static void Edit(string path, Action<GameObject> edit)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { edit(root); PrefabUtility.SaveAsPrefabAsset(root,path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
