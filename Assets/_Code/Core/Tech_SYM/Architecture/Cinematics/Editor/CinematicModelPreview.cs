using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cooked.Cinematics.Editor
{
    public static class CinematicModelPreview
    {
        public static void RunBatch()
        {
            try
            {
                Render("Onion", "ch_onion", "Ch_onion_default");
                Render("Potato", "ch_potato", "ch_potato_default");
                File.WriteAllText("output/Tech_SYM/Cinematics/model-preview-complete.txt", "Original FBX and materials rendered without modifying source assets.");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Render(string label, string modelName, string materialName)
        {
            string directory = "output/Tech_SYM/Cinematics/References";
            Directory.CreateDirectory(directory);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Art/Characters/Models/" + modelName + ".fbx");
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Art/Characters/Materials/" + materialName + ".mat");
            if (model == null || material == null) throw new InvalidOperationException("Missing original model/material: " + modelName);
            var preview = new PreviewRenderUtility();
            try
            {
                var actor = UnityEngine.Object.Instantiate(model);
                preview.AddSingleGO(actor);
                Renderer[] renderers = actor.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) throw new InvalidOperationException("Model has no renderer: " + modelName);
                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                    var materials = new Material[renderer.sharedMaterials.Length];
                    for (int i = 0; i < materials.Length; i++) materials[i] = material;
                    renderer.sharedMaterials = materials;
                }
                preview.cameraFieldOfView = 30;
                preview.camera.nearClipPlane = 0.01f;
                preview.camera.farClipPlane = 1000;
                preview.camera.backgroundColor = new Color(0.24f, 0.30f, 0.34f, 1);
                preview.camera.clearFlags = CameraClearFlags.Color;
                preview.lights[0].intensity = 1.2f;
                preview.lights[0].transform.rotation = Quaternion.Euler(40, 30, 0);
                preview.lights[1].intensity = 0.8f;
                preview.ambientColor = new Color(0.7f, 0.7f, 0.7f);
                float distance = Mathf.Max(bounds.extents.magnitude, 0.1f) * 3.6f;
                Vector3[] directions = { new Vector3(0, 0.15f, -1), new Vector3(0.7f, 0.2f, -1), new Vector3(1, 0.1f, 0), new Vector3(0, 0.15f, 1) };
                for (int i = 0; i < directions.Length; i++)
                {
                    preview.camera.transform.position = bounds.center + directions[i].normalized * distance;
                    preview.camera.transform.LookAt(bounds.center);
                    preview.BeginPreview(new Rect(0, 0, 768, 768), GUIStyle.none);
                    preview.Render(true);
                    var rendered = preview.EndPreview();
                    RenderTexture previous = RenderTexture.active;
                    var pixels = new Texture2D(768, 768, TextureFormat.RGB24, false);
                    try
                    {
                        RenderTexture.active = (RenderTexture)rendered;
                        pixels.ReadPixels(new Rect(0, 0, 768, 768), 0, 0);
                        pixels.Apply();
                        File.WriteAllBytes(directory + "/" + label + "_" + i + ".png", pixels.EncodeToPNG());
                    }
                    finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(pixels); }
                }
            }
            finally { preview.Cleanup(); }
        }
    }
}
