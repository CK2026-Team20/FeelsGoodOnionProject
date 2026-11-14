using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cooked.DebugTesting;

namespace Cooked.Boids.Editor
{
    [InitializeOnLoad]
    public static class BoidTestPlayModeGuard
    {
        const string Owned = "Cooked.Boids.TestOwnsEntry", Previous = "Cooked.Boids.PreviousDirectEntry";
        static BoidTestPlayModeGuard()
        {
            EditorApplication.playModeStateChanged += Changed;
        }

        static void Changed(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                var scene = SceneManager.GetActiveScene();
                bool test = false;
                foreach (var r in scene.GetRootGameObjects())
                {
                    var session = r.GetComponentInChildren<BoidTestSession>(true);
                    if (session && session.isActiveAndEnabled)
                    {
                        test = true;
                        break;
                    }
                }

                if (test)
                {
                    SessionState.SetBool(Previous, DirectSceneTestEntry.IsSelected);
                    SessionState.SetBool(Owned, true);
                    DirectSceneTestEntry.IsSelected = false;
                }
            }

            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Owned, false))
            {
                DirectSceneTestEntry.IsSelected = SessionState.GetBool(Previous, false);
                SessionState.SetBool(Owned, false);
            }
        }
    }
}
