#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using DG.Tweening;
using UnityEditor;
using UnityEngine;

namespace Cooked.Audio.Editor
{
    /// <summary>Run after PM assigns an Editor slot. Never advances or kills unrelated tweens.</summary>
    public static class AudioTweenVerification
    {
        [MenuItem("Cooked/Audio/Verify Real DOTween In Play Mode")]
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run in Play Mode after PM assigns the Editor slot.");
            var log = new List<string> { "Actual DOTween manual-handle execution; not audio device verification." };
            int completion = 0; float a = 0, b = 0;
            using (var tween = new DotweenMusicCrossfadeDriver(null, UpdateType.Manual))
            using (var unrelated = new DotweenMusicCrossfadeDriver(null, UpdateType.Manual))
            {
                unrelated.Start(0, 1, 0, 0, 1, false, (_, __) => { }, () => { });
                tween.Start(1, 0, 0, 1, .3f, false, (x, y) => { a = x; b = y; }, () => completion++);
                tween.AdvanceForVerification(0, .1f);
                Near(a, 2f / 3); Near(b, 1f / 3);
                log.Add("PASS unscaled progress with zero scaled delta");
                tween.SetPaused(true); tween.AdvanceForVerification(0, 5);
                Near(a, 2f / 3); Near(b, 1f / 3);
                tween.SetPaused(false); tween.AdvanceForVerification(0, .2f);
                Near(a, 0); Near(b, 1); Check(completion == 1, "normal completion count");
                Check(!tween.HasOwnedTweenForVerification, "auto-kill handle release");
                log.Add("PASS pause/resume remaining duration and one completion");

                tween.Start(1, 0, 0, 1, .3f, false, (x, y) => { a = x; b = y; }, () => completion++);
                tween.AdvanceForVerification(0, .1f); float cancelledA = a;
                tween.Cancel(); tween.AdvanceForVerification(0, 4);
                Near(a, cancelledA); Check(completion == 1, "Cancel reported success");
                Check(!tween.HasOwnedTweenForVerification && unrelated.HasOwnedTweenForVerification, "Cancel affected another owner");
                log.Add("PASS cancel has no success callback and preserves another owner's tween");

                tween.Start(0, 1, 1, 0, .3f, false, (_, __) => { }, () => completion += 100);
                tween.Start(0, 1, 0, 0, .3f, false, (x, y) => { a = x; b = y; }, () => completion++);
                tween.AdvanceForVerification(0, .3f);
                Near(a, 1); Check(completion == 2, "replaced tween completed");
                log.Add("PASS replaced handle cannot complete");

                tween.Start(0, 1, 0, 0, .3f, true, (x, y) => { a = x; b = y; }, () => completion++);
                tween.AdvanceForVerification(0, 1); Check(completion == 2, "started-paused tween advanced");
                tween.SetPaused(false); tween.AdvanceForVerification(0, .3f); Check(completion == 3, "paused start resume failed");
                log.Add("PASS started-paused tween resumes normally");

                for (int i = 0; i < 10; i++)
                {
                    tween.Start(0, 1, 0, 0, .3f, false, (_, __) => { }, () => completion++);
                    tween.Cancel(); Check(!tween.HasOwnedTweenForVerification, "rebind handle leak");
                }
                tween.Dispose(); Check(completion == 3 && unrelated.HasOwnedTweenForVerification, "Dispose affects another owner");
                log.Add("PASS ten cancel/rebind cycles and Dispose leave no owned handles");
            }
            string directory = Path.Combine(Application.dataPath, "../output/Tech_SYM/Audio/Verification");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "unity-dotween-execution.log");
            File.WriteAllLines(path, log); Debug.Log("Audio real DOTween verification passed: " + path);
        }
        private static void Near(float actual, float expected)
        { if (Math.Abs(actual - expected) > .001f) throw new InvalidOperationException($"Expected {expected}, got {actual}."); }
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
#endif
