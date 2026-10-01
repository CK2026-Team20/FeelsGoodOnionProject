using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
namespace Cooked.Level.Editor
{
    public static class LevelPreviewCapture
    {
        public static void BuildAndCapture() { LevelAssetBuilder.Build(); Capture(); }
        public static void Capture()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Capture requires Edit mode.");
            for(int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Do not discard dirty authoring scenes.");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            var stage = EditorSceneManager.OpenScene(LevelAssetBuilder.StageRoot+"/03_1_Stage.unity",OpenSceneMode.Single);
            EditorSceneManager.OpenScene(LevelAssetBuilder.StageRoot+"/03_2_Stage.unity",OpenSceneMode.Additive);
            EditorSceneManager.OpenScene(LevelAssetBuilder.StageRoot+"/03_3_Stage.unity",OpenSceneMode.Additive);
            GameObject backdrop=null, cameraObject=null, lightObject=null;
            try
            {
                SceneManager.SetActiveScene(stage);
                backdrop=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LevelAssetBuilder.AssetRoot+"/Prefabs/KitchenBackdrop.prefab"));
                cameraObject=new GameObject("LevelEvidenceCamera"); var camera=cameraObject.AddComponent<Camera>();
                camera.backgroundColor=new Color(.55f,.72f,.78f); camera.clearFlags=CameraClearFlags.SolidColor; camera.farClipPlane=250;
                lightObject=new GameObject("LevelEvidenceLight"); var light=lightObject.AddComponent<Light>(); light.type=LightType.Directional; light.intensity=2; light.transform.rotation=Quaternion.Euler(45,-35,0);
                Directory.CreateDirectory("output/Tech_SYM/level/evidence");
                Render(camera,"stage1",new Vector3(18,9,-25),new Vector3(18,1,0),60);
                Render(camera,"stage2",new Vector3(39,17,-22),new Vector3(51,1,1),55);
                Render(camera,"stage3",new Vector3(88,10,-30),new Vector3(88,1,0),60);
                Render(camera,"overview",new Vector3(55,38,-80),new Vector3(55,0,0),70);
            }
            finally
            {
                if(backdrop!=null) Object.DestroyImmediate(backdrop);
                if(cameraObject!=null) Object.DestroyImmediate(cameraObject);
                if(lightObject!=null) Object.DestroyImmediate(lightObject);
                // These actual Stage scenes were only used for non-saved rendering. Never persist preview objects.
                EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }
        private static void Render(Camera camera,string name,Vector3 position,Vector3 look,float fov)
        {
            var texture=new RenderTexture(1600,900,24); var pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);
            var old=RenderTexture.active;
            try
            {
                camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(look-position)); camera.fieldOfView=fov;
                camera.targetTexture=texture; texture.Create(); camera.Render(); camera.Render(); RenderTexture.active=texture;
                pixels.ReadPixels(new Rect(0,0,1600,900),0,0); pixels.Apply();
                File.WriteAllBytes("output/Tech_SYM/level/evidence/"+name+".png",pixels.EncodeToPNG());
            }
            finally { RenderTexture.active=old; camera.targetTexture=null; texture.Release(); Object.DestroyImmediate(texture); Object.DestroyImmediate(pixels); }
        }
    }
}

