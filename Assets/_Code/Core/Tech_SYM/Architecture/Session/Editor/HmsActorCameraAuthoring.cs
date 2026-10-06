using System;
using Cooked.Level;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Cooked.Session.Editor
{
    /// <summary>Uses HMS components/settings in owned placements; never applies to protected originals.</summary>
    public static class HmsActorCameraAuthoring
    {
        public const string ReferenceScene = "Assets/_Scenes/Tech_HMS/01_PlayerMovementTest.unity";
        public static ActorCameraRig Assemble(GameObject owner, PlayerFacade facade, bool waitForSession)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Author HMS references in Edit Mode.");
            if (owner == null || facade == null) throw new ArgumentNullException(nameof(owner));
            var visual = facade.transform.Find("VisualRoot");
            if (visual == null) throw new InvalidOperationException("Player requires its direct VisualRoot child.");
            if (visual.Find("NormalVisual") == null || visual.Find("SmallVisual") == null ||
                visual.GetComponentsInChildren<Collider>(true).Length != 0 ||
                visual.GetComponentsInChildren<Rigidbody>(true).Length != 0)
                throw new InvalidOperationException("HMS form requires NormalVisual/SmallVisual under a physics-free VisualRoot.");
            var mode = facade.GetComponent<PlayerMovementModeController>() ?? facade.gameObject.AddComponent<PlayerMovementModeController>();
            var modeFields = new SerializedObject(mode);
            foreach (string field in new[] { "sideMoveSpeed", "quarterMoveSpeed", "backMoveSpeed" })
                modeFields.FindProperty(field).floatValue = 5;
            modeFields.ApplyModifiedPropertiesWithoutUndo();
            var presentation = facade.GetComponent<PlayerVisualController>() ?? facade.gameObject.AddComponent<PlayerVisualController>();
            Set(presentation, "playerVisualRoot", visual);
            var existing = owner.GetComponentInChildren<ActorCameraRig>(true);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            var root = new GameObject("CameraRig"); root.SetActive(false); root.transform.SetParent(owner.transform, false);
            var rig = root.AddComponent<ActorCameraRig>();
            Scene reference = EditorSceneManager.OpenPreviewScene(ReferenceScene);
            try
            {
                PlayerCameraController source = null;
                foreach (var go in reference.GetRootGameObjects())
                    if ((source = go.GetComponentInChildren<PlayerCameraController>(true)) != null) break;
                if (source == null) throw new InvalidOperationException("HMS reference scene has no camera controller.");
                var fields = new SerializedObject(source);
                var names = new[] { "sideCamera", "quarterCamera", "backCamera", "backFixedCamera" };
                var cameras = new CinemachineCamera[4];
                for (int i = 0; i < cameras.Length; i++)
                {
                    var cameraSource = fields.FindProperty(names[i]).objectReferenceValue as CinemachineCamera;
                    if (cameraSource == null) throw new InvalidOperationException("HMS camera reference missing: " + names[i]);
                    var clone = Object.Instantiate(cameraSource.gameObject, root.transform);
                    clone.name = cameraSource.name;
                    clone.transform.localRotation = cameraSource.transform.rotation;
                    cameras[i] = clone.GetComponent<CinemachineCamera>();
                    cameras[i].Follow = i == 2 ? visual : facade.transform;
                    cameras[i].LookAt = i == 2 ? visual : facade.transform;
                }
                var output = Object.Instantiate(source.gameObject, root.transform);
                output.name = "Main Camera"; output.tag = "MainCamera";
                var controller = output.GetComponent<PlayerCameraController>();
                Set(controller, "movementModeController", mode);
                for (int i = 0; i < names.Length; i++) Set(controller, names[i], cameras[i]);
                var brain = output.GetComponent<CinemachineBrain>();
                brain.IgnoreTimeScale = false;
                brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, .6f);
                Set(rig, "movementMode", mode); Set(rig, "outputCamera", output.GetComponent<Camera>());
                Set(rig, "brain", brain); Set(rig, "hmsController", controller);
                foreach (var pair in new[] { ("side", 0), ("quarter", 1), ("back", 2), ("backFixed", 3) }) Set(rig, pair.Item1, cameras[pair.Item2]);
                var rigFields = new SerializedObject(rig); rigFields.FindProperty("waitForSession").boolValue = waitForSession;
                rigFields.ApplyModifiedPropertiesWithoutUndo();
                rig.ValidateConfiguration(); root.SetActive(true);
                return rig;
            }
            catch { Object.DestroyImmediate(root); throw; }
            finally { EditorSceneManager.ClosePreviewScene(reference); }
        }
        private static void Set(Object value, string field, Object reference)
        {
            var serialized = new SerializedObject(value);
            var property = serialized.FindProperty(field) ?? throw new InvalidOperationException("Missing serialized field: " + field);
            property.objectReferenceValue = reference; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
