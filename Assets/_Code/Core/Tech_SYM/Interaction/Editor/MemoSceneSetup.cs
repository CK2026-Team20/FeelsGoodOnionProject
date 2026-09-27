using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using FeelsGoodOnion.TechSYM.Interaction;

namespace FeelsGoodOnion.TechSYM.EditorTools
{
    /// <summary>현재 활성 씬에만 메모 검증용 리소스를 배치한다. 자동 실행하지 않는다.</summary>
    public static class MemoSceneSetup
    {
        private const string Root = "Assets/_Scenes/Tech_SYM/Memo";
        public static string Build()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != "Assets/_Scenes/Tech_SYM/03_InGame.unity")
                throw new InvalidOperationException("확인한 활성 인게임 씬의 Edit Mode에서만 배치할 수 있습니다.");
            if (scene.GetRootGameObjects().Any(g => g.name == "MemoInteraction"))
                throw new InvalidOperationException("MemoInteraction이 이미 있습니다. 기존 배치를 덮어쓰지 않습니다.");
            Directory.CreateDirectory(Root + "/Art");
            Directory.CreateDirectory(Root + "/Models");
            Directory.CreateDirectory(Root + "/Prefabs");
            AssetDatabase.Refresh();
            var player = UnityEngine.Object.FindObjectsByType<PlayerFacade>(FindObjectsSortMode.None).Single();
            var camera = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).First(c => c.name == "Camera");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (font == null) throw new InvalidOperationException("기존 TMP Font가 필요합니다.");

            var root = new GameObject("MemoInteraction");
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Create Memo Interaction");
            var controller = root.AddComponent<MemoUIController>();

            var screen = Rect("MemoScreenCanvas", root.transform);
            var screenCanvas = screen.gameObject.AddComponent<Canvas>();
            screenCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            screenCanvas.sortingOrder = 100;
            var scaler = screen.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080);
            scaler.matchWidthOrHeight = 0.5f;
            // 키보드 전용 표시 UI이므로 EventSystem/GraphicRaycaster를 추가하지 않는다.
            Set(controller, "screenRoot", screen);

            var memoRect = Rect("MemoView", root.transform);
            memoRect.anchorMin = new Vector2(.24f,.1f);
            memoRect.anchorMax = new Vector2(.76f,.9f);
            memoRect.offsetMin = memoRect.offsetMax = Vector2.zero;
            var image = memoRect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            var memoTween = memoRect.gameObject.AddComponent<UIScaleTween>();
            var memoView = memoRect.gameObject.AddComponent<MemoView>();
            Set(memoView,"memoImage",image);
            Set(memoView,"scaleTween",memoTween);
            var prefab = PrefabUtility.SaveAsPrefabAsset(memoRect.gameObject, Root + "/Prefabs/MemoView.prefab");
            Set(controller,"viewPrefab",prefab.GetComponent<MemoView>());
            UnityEngine.Object.DestroyImmediate(memoRect.gameObject);

            var world = Rect("InteractionPromptCanvas",root.transform);
            world.sizeDelta = new Vector2(100,110);
            world.localScale = Vector3.one * .012f;
            var canvas = world.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvas.sortingOrder = 20;
            var prompt = world.gameObject.AddComponent<InteractionPromptView>();
            var bubble = Rect("Bubble",world);
            bubble.sizeDelta = new Vector2(100,110);
            var bubbleImage = bubble.gameObject.AddComponent<UnityEngine.UI.Image>();
            bubbleImage.sprite = MakeBubble();
            bubbleImage.raycastTarget = false;
            var bubbleTween = bubble.gameObject.AddComponent<UIScaleTween>();
            var labelRect = Rect("KeyLabel",bubble);
            labelRect.sizeDelta = new Vector2(85,80);
            labelRect.anchoredPosition = new Vector2(0,10);
            var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = "E";
            label.fontSize = 68;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(.1f,.12f,.14f);
            label.raycastTarget = false;
            Set(prompt,"bubble",bubble.gameObject);
            Set(prompt,"scaleTween",bubbleTween);
            bubble.gameObject.SetActive(false);

            var check = root.AddComponent<CheckInteract>();
            Set(check,"player",player);
            Set(check,"promptView",prompt);
            Set(check,"viewCamera",camera);
            Set(check,"overlaySource",controller);
            for (int index = 0; index < 2; index++)
            {
                var data = ScriptableObject.CreateInstance<MemoModel>();
                Set(data,"memoID",1001 + index);
                Set(data,"memoImage",MakePaper(index));
                AssetDatabase.CreateAsset(data,Root + "/Models/Memo_" + (1001+index) + ".asset");

                var item = new GameObject("Memo_" + (1001+index));
                item.transform.SetParent(root.transform,false);
                item.transform.position = player.transform.position + player.transform.forward * 1.8f + Vector3.right * index * 2.4f - Vector3.up * .4f;
                var anchor = item.AddComponent<InteractionPromptAnchor>();
                Set(anchor,"localOffset",new Vector3(0,.85f,0));
                var memo = item.AddComponent<MemoObject>();
                Set(memo,"memoModel",data);
                Set(memo,"memoUI",controller);
                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "MemoBody_Collider";
                body.transform.SetParent(item.transform,false);
                body.transform.localScale = new Vector3(.8f,.9f,.12f);
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.color = index == 0 ? new Color(.82f,.62f,.31f) : new Color(.38f,.66f,.62f);
                AssetDatabase.CreateAsset(material,Root + "/Art/MemoBody_" + index + ".mat");
                body.GetComponent<Renderer>().sharedMaterial = material;
            }
            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            return "Created MemoInteraction in " + scene.path + "; save after verification.";
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent,false);
            return rect;
        }

        public static void Set(UnityEngine.Object owner, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(owner);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Set(UnityEngine.Object owner, string field, int value)
        {
            var serialized = new SerializedObject(owner);
            serialized.FindProperty(field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Set(UnityEngine.Object owner, string field, Vector3 value)
        {
            var serialized = new SerializedObject(owner);
            serialized.FindProperty(field).vector3Value = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Sprite MakeBubble()
        {
            const int size = 128;
            var texture = new Texture2D(size,size,TextureFormat.RGBA32,false);
            for (int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x-63.5f)-41,0);
                float dy = Mathf.Max(Mathf.Abs(y-75f)-32,0);
                bool outer = dx*dx+dy*dy < 16*16;
                float ix = Mathf.Max(Mathf.Abs(x-63.5f)-39,0);
                float iy = Mathf.Max(Mathf.Abs(y-75f)-30,0);
                bool inner = ix*ix+iy*iy < 12*12;
                bool tail = y >= 4 && y < 28 && Mathf.Abs(x-64) < (y-4)*.7f;
                texture.SetPixel(x,y,inner ? new Color(.98f,.97f,.92f) :
                    outer || tail ? new Color(.12f,.14f,.17f) : Color.clear);
            }
            return SaveSprite(texture,Root + "/Art/PromptBubble.png");
        }
        private static Sprite MakePaper(int index)
        {
            const int width=600,height=760;
            var texture = new Texture2D(width,height,TextureFormat.RGBA32,false);
            Color paper = index==0 ? new Color(.96f,.89f,.73f) : new Color(.83f,.92f,.86f);
            Color ink = index==0 ? new Color(.51f,.34f,.18f) : new Color(.23f,.43f,.39f);
            // 검증용 임시 이미지. 승인된 메모 원화 Sprite로 교체한다.
            for(int y=0;y<height;y++) for(int x=0;x<width;x++)
            {
                bool border = x<12 || x>=width-12 || y<12 || y>=height-12;
                bool line = y>280 && y<570 && y%58<4 && x>80 && x<width-80;
                float dx=x-width*.5f,dy=y-160;
                bool seal = dx*dx+dy*dy < 54*54 && dx*dx+dy*dy > 47*47;
                texture.SetPixel(x,y,border || line || seal ? ink : paper);
            }
            return SaveSprite(texture,Root + "/Art/PlaceholderMemo_" + (index+1) + ".png");
        }
        private static Sprite SaveSprite(Texture2D texture,string path)
        {
            File.WriteAllBytes(path,texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;
            importer.spriteImportMode=SpriteImportMode.Single;
            importer.alphaIsTransparency=true;
            importer.mipmapEnabled=false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
