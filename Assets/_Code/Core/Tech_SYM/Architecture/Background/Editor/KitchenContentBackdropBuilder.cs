#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Cooked.Background.Editor
{
    // Content owns these independent visual assets. Core owns the Stage/Core scene placement.
    public static class KitchenContentBackdropBuilder
    {
        public const string Root = "Assets/_Scenes/Tech_SYM/Architecture/Background";
        public const string PrefabPath = Root + "/Prefabs/KitchenContentBackdrop.prefab";
        private const string Art = "Assets/_Art/Environment/";
        private static readonly string[] Models = { "bg_table_L", "bg_table_S", "bg_oven", "bg_gasstove", "bg_sink", "bg_hanger", "bg_bottle", "bg_pot", "bg_pan", "bg_tool1", "bg_tool2", "bg_tool3", "bg_plant1", "bg_plant2" };

        [MenuItem("Cooked/Content/Build Kitchen Backdrop")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Build content in Edit Mode.");
            foreach (var model in Models)
                if (AssetDatabase.LoadAssetAtPath<GameObject>(Art + "Models/" + model + ".fbx") == null)
                    throw new InvalidOperationException("Missing supplied model: " + model);
            Directory.CreateDirectory(Root + "/Materials"); Directory.CreateDirectory(Root + "/Prefabs/Models"); AssetDatabase.Refresh();
            var materials = new Dictionary<string,Material>();
            foreach (string key in new[]{"bg_table_hanger_basecolor", "bg_oven_basecolor", "bg_oven_basecolor 1", "bg_sink_basecolor", "bg_kitchen_tools_basecolor"})
                materials.Add(key, Material(key));
            var preview = EditorSceneManager.NewPreviewScene();
            var wrappers = new Dictionary<string,GameObject>();
            try
            {
                foreach (var model in Models)
                {
                    var wrapper = new GameObject(model + "_Content");
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(wrapper, preview);
                    try
                    {
                        var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Art + "Models/" + model + ".fbx"), preview);
                        visual.name = "Visual"; visual.transform.SetParent(wrapper.transform, false);
                        foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                        var material = materials[TextureKey(model)];
                        foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
                        {
                            var slots = renderer.sharedMaterials;
                            for (int i=0;i<slots.Length;i++) slots[i]=material;
                            renderer.sharedMaterials=slots;
                        }
                        Bounds b = BoundsOf(visual);
                        float width=Mathf.Max(b.size.x,b.size.z);
                        if(width<.0001f)throw new InvalidOperationException("Empty supplied model bounds: "+model);
                        visual.transform.localScale/=width;
                        b=BoundsOf(visual);
                        visual.transform.localPosition-=new Vector3(b.center.x,b.min.y,b.center.z);
                        string path=Root+"/Prefabs/Models/"+model+".prefab";
                        wrappers.Add(model,PrefabUtility.SaveAsPrefabAsset(wrapper,path));
                    }
                    finally { UnityEngine.Object.DestroyImmediate(wrapper); }
                }
                var layout = new GameObject("KitchenContentBackdrop");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(layout,preview);
                try
                {
                    // Keep the route lanes x=0..36,z=0 / x=36,z=0..30 / x=-8..36,z=30 clear.
                    // Stage1 sees +Z scenery at z8; reversed Stage3 sees -Z scenery at z22. Stage2 sees exterior +X counters.
                    var shell = Group(layout,"KitchenInteriorShell");
                    var wall = SolidMaterial("WarmPlaster", new Color(.72f,.63f,.46f));
                    var trim = SolidMaterial("DarkWoodTrim", new Color(.27f,.15f,.07f));
                    var tile = SolidMaterial("WarmTile", new Color(.54f,.49f,.36f));
                    // Visual-only architecture outside the walkable lanes. Originals remain read-only.
                    Block(shell,"SouthBackWall",new Vector3(2,8,15),new Vector3(54,28,.6f),wall);
                    Block(shell,"NorthBackWall",new Vector3(16,8,46),new Vector3(82,28,.6f),wall);
                    Block(shell,"EastBackWall",new Vector3(50,8,23),new Vector3(.6f,28,70),wall);
                    Block(shell,"KitchenFloor",new Vector3(16,-3.4f,22),new Vector3(90,.3f,85),tile);
                    foreach(float z in new[]{14.5f,15.5f})
                    {
                        Block(shell,"BacksplashRail",new Vector3(2,3.3f,z),new Vector3(54,.24f,.3f),trim);
                        Block(shell,"UpperShelf",new Vector3(2,10,z < 15 ? z-1 : z+1),new Vector3(54,.35f,2.2f),trim);
                        for(int x=-18;x<=29;x+=6)
                            Block(shell,"PanelTrim",new Vector3(x,6.5f,z),new Vector3(.1f,6.5f,.1f),trim);
                    }
                    Block(shell,"EastShelf",new Vector3(48.5f,10,23),new Vector3(2.2f,.35f,52),trim);
                    var south = Group(layout,"Stage1CounterBackdrop");
                    Counter(south,wrappers,new Vector3(6,-3,8),0,"bg_table_L","bg_plant1","bg_pot");
                    Counter(south,wrappers,new Vector3(17,-3,8),0,"bg_table_L","bg_bottle","bg_pan");
                    Place(south,wrappers,"bg_oven",new Vector3(27,-3,8),8,180);
                    Counter(south,wrappers,new Vector3(-5,-3,8),0,"bg_table_L","bg_plant2","bg_bottle");
                    Counter(south,wrappers,new Vector3(-16,-3,8),0,"bg_table_L","bg_pot","bg_pan");
                    Place(south,wrappers,"bg_hanger",new Vector3(15,5.5f,13.8f),8,0);
                    Place(south,wrappers,"bg_pan",new Vector3(7,10.2f,13.5f),2,0);
                    Place(south,wrappers,"bg_plant2",new Vector3(22,10.2f,13.5f),2,0);
                    var east = Group(layout,"Stage2CounterBackdrop");
                    Counter(east,wrappers,new Vector3(44,-3,8),-90,"bg_table_S","bg_tool1","bg_plant2");
                    Place(east,wrappers,"bg_sink",new Vector3(44,-3,21),10,-90);
                    Place(east,wrappers,"bg_hanger",new Vector3(47,6,16),7,-90);
                    Place(east,wrappers,"bg_tool2",new Vector3(46,5,14),1.6f,-90);
                    Place(east,wrappers,"bg_tool3",new Vector3(46,5,18),1.6f,-90);
                    var north = Group(layout,"Stage3CounterBackdrop");
                    Counter(north,wrappers,new Vector3(26,-3,22),180,"bg_table_L","bg_plant2","bg_bottle");
                    Place(north,wrappers,"bg_gasstove",new Vector3(12,-3,22),9,0);
                    Counter(north,wrappers,new Vector3(-3,-3,22),180,"bg_table_S","bg_pot","bg_plant1");
                    Counter(north,wrappers,new Vector3(-14,-3,22),180,"bg_table_L","bg_pot","bg_pan");
                    Place(north,wrappers,"bg_hanger",new Vector3(15,6,16.2f),8,180);
                    Place(north,wrappers,"bg_plant1",new Vector3(5,10.2f,16.5f),2,180);
                    PrefabUtility.SaveAsPrefabAsset(layout,PrefabPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(layout); }
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            Debug.Log("Kitchen content backdrop built with 14 supplied models and 5 original texture references: "+PrefabPath);
        }
        private static Material SolidMaterial(string name,Color color)
        {
            string path=Root+"/Materials/"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",.15f);EditorUtility.SetDirty(mat);return mat;
        }
        private static void Block(Transform parent,string name,Vector3 position,Vector3 size,Material material)
        {
            var root=new GameObject(name);root.transform.SetParent(parent,false);root.transform.localPosition=position;
            var visual=GameObject.CreatePrimitive(PrimitiveType.Cube);visual.name="Visual";visual.transform.SetParent(root.transform,false);
            visual.transform.localScale=size;UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.GetComponent<Renderer>().sharedMaterial=material;
        }
        private static Transform Group(GameObject root,string name)
        { var item=new GameObject(name);item.transform.SetParent(root.transform,false);return item.transform; }
        private static GameObject Place(Transform parent,Dictionary<string,GameObject> wrappers,string model,Vector3 position,float width,float yaw)
        {
            var item=(GameObject)PrefabUtility.InstantiatePrefab(wrappers[model],parent);
            item.transform.localPosition=position;item.transform.localRotation=Quaternion.Euler(0,yaw,0);item.transform.localScale=Vector3.one*width;return item;
        }
        private static void Counter(Transform parent,Dictionary<string,GameObject> wrappers,Vector3 p,float yaw,string table,string propA,string propB)
        {
            var counter=Place(parent,wrappers,table,p,11,yaw);
            float top=BoundsOf(counter).max.y;
            var side=Quaternion.Euler(0,yaw,0)*Vector3.right*2.5f;
            Place(parent,wrappers,propA,new Vector3(p.x,top,p.z)-side,2.2f,yaw);
            Place(parent,wrappers,propB,new Vector3(p.x,top,p.z)+side,2.0f,yaw);
        }
        private static Bounds BoundsOf(GameObject obj)
        {
            var renderers=obj.GetComponentsInChildren<Renderer>(true);
            if(renderers.Length==0)throw new InvalidOperationException("Model has no renderer: "+obj.name);
            Bounds b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);return b;
        }
        private static string TextureKey(string model)
        {
            if(model=="bg_table_L"||model=="bg_table_S"||model=="bg_hanger")return "bg_table_hanger_basecolor";
            if(model=="bg_oven")return "bg_oven_basecolor";
            if(model=="bg_gasstove")return "bg_oven_basecolor 1";
            if(model=="bg_sink")return "bg_sink_basecolor";
            return "bg_kitchen_tools_basecolor";
        }
        private static Material Material(string textureName)
        {
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Textures/"+textureName+".png");
            if(texture==null)throw new InvalidOperationException("Missing supplied texture: "+textureName);
            string path=Root+"/Materials/"+textureName+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null)
            {
                var shader=Shader.Find("Universal Render Pipeline/Lit");
                if(shader==null)throw new InvalidOperationException("URP Lit shader required");
                mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);
            }
            mat.SetTexture("_BaseMap",texture);mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_Smoothness",.25f);EditorUtility.SetDirty(mat);return mat;
        }
    }
}
#endif
