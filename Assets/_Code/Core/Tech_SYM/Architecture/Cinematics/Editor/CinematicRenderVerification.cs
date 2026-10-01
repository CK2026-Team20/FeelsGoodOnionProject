using System;
using System.IO;
using System.Threading;
using Cooked.Contracts;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

namespace Cooked.Cinematics.Editor
{
    // Synthetic order probe; final real Options/Fade rendering must also be verified in integration.
    public static class CinematicRenderVerification
    {
        public static void Capture()
        {
            string output = "output/Tech_SYM/Cinematics/Rendered";
            Directory.CreateDirectory(output);
            var preview = new PreviewRenderUtility();
            var root = new GameObject("CinematicRenderProbe", typeof(RectTransform), typeof(Canvas));
            preview.AddSingleGO(root);
            preview.camera.clearFlags = CameraClearFlags.Color;
            preview.camera.backgroundColor = Color.magenta;
            preview.camera.nearClipPlane = 0.01f;
            preview.camera.farClipPlane = 100;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = preview.camera;
            canvas.planeDistance = 1;
            canvas.sortingOrder = 20;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CinematicAssetBuilder.PrefabPath);
            var instance = UnityEngine.Object.Instantiate(prefab, root.transform, false);
            var catalog = AssetDatabase.LoadAssetAtPath<CinematicCatalog>(CinematicAssetBuilder.CatalogPath);
            var service = new CinematicService(instance.GetComponentInChildren<PlayableDirector>(), instance.GetComponentInChildren<CinematicFrameBridge>(), catalog, new RenderControlService());
            var vm = new CinematicViewModel(service, service.Presentation);
            var view = instance.GetComponentInChildren<CinematicView>();
            view.Bind(vm);
            try
            {
                Prepare(service, CinematicId.Opening);
                Save(preview, output + "/opening-ready-1920x1080.png", 1920, 1080);
                Save(preview, output + "/opening-ready-1920x1200.png", 1920, 1200);
                Save(preview, output + "/opening-ready-2560x1080.png", 2560, 1080);
                var result = new OperationResult();
                var play = service.Play(CancellationToken.None, result);
                play.MoveNext(); SeekForCapture(instance, service, 2.85);
                Save(preview, output + "/opening-crossfade.png", 1920, 1080);
                SeekForCapture(instance, service, 11.099999);
                Save(preview, output + "/opening-held-last.png", 1920, 1080);
                service.Hide(); Prepare(service, CinematicId.Ending);
                Save(preview, output + "/ending-ready.png", 1920, 1080);
                var options = Overlay("SyntheticOptions200", preview, 200, new Color(0.1f, 0.8f, 0.2f, 1), false);
                Color optionsPixel = Save(preview, output + "/order-options200.png", 1920, 1080);
                if (optionsPixel.g < 0.6f || optionsPixel.r > 0.3f) throw new InvalidOperationException("Order 200 did not render above cinematic canvas 20.");
                Overlay("SyntheticFade300", preview, 300, Color.black, true);
                Color fadePixel = Save(preview, output + "/order-fade300.png", 1920, 1080);
                if (fadePixel.maxColorComponent > 0.05f) throw new InvalidOperationException("Order 300 did not fully cover cinematics/options.");
                File.WriteAllText(output + "/render-order.txt", "PASS: synthetic ScreenCamera Canvas20 < Options200 < Fade300. Real Bootstrap Options/Fade and player build still require integration rendering.\n");
            }
            finally
            {
                view.Unbind(); vm.Dispose(); service.Dispose(); preview.Cleanup();
            }
        }

        private static void SeekForCapture(GameObject instance, CinematicService service, double time)
        {
            var director = instance.GetComponentInChildren<PlayableDirector>();
            director.time = time;
            director.Evaluate();
            service.ObservePlayback();
        }
        private static void Prepare(CinematicService service, CinematicId id)
        {
            var result = new OperationResult();
            var operation = service.Prepare(id, CancellationToken.None, result);
            while (operation.MoveNext()) { }
            if (result.Status != OperationStatus.Succeeded) throw new InvalidOperationException(result.Error);
        }
        private static GameObject Overlay(string name, PreviewRenderUtility preview, int order, Color color, bool full)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            preview.AddSingleGO(root);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = preview.camera;
            canvas.planeDistance = 0.5f; canvas.sortingOrder = order;
            var imageObject = new GameObject("Panel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            imageObject.transform.SetParent(root.transform, false);
            var rect = (RectTransform)imageObject.transform;
            if (full) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
            else { rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f); rect.sizeDelta = new Vector2(640, 400); }
            imageObject.GetComponent<UnityEngine.UI.Image>().color = color;
            return root;
        }
        private static Color Save(PreviewRenderUtility preview, string path, int width, int height)
        {
            preview.BeginPreview(new Rect(0, 0, width, height), GUIStyle.none);
            Canvas.ForceUpdateCanvases();
            preview.Render(true);
            var rendered = preview.EndPreview();
            var previous = RenderTexture.active;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = (RenderTexture)rendered;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                return image.GetPixel(width / 2, height / 2);
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image); }
        }
        private sealed class RenderControlService : IGameplayControlService
        {
            public ControlState State => new ControlState(true, false, false);
            public event Action<ControlState> Changed { add { } remove { } }
            public IDisposable BlockGameplay(string owner) => throw new NotSupportedException();
            public IDisposable PauseWorld(string owner) => throw new NotSupportedException();
            public IDisposable PausePresentation(string owner) => throw new NotSupportedException();
            public void Dispose() { }
        }
    }
}
