using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
namespace Cooked.Dialogue.Tests
{
    /// <summary>Native Tween proof prepared for the assigned Editor slot. Must run in the existing Core scene.</summary>
    public static class DialogueTweenRegressionTests
    {
        public static IEnumerator Run(GameObject prefab, Transform screenCanvas, Action<string> report)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Requires Play Mode.");
            if (report == null) throw new ArgumentNullException(nameof(report));
            var scheduler = new DOTweenDialogueDelayScheduler();
            float originalScale = Time.timeScale;
            IDialogueDelayHandle handle = null;
            try
            {
                Time.timeScale = 0;
                int calls = 0;
                handle = scheduler.Schedule(0.3f, () => calls++);
                yield return new WaitForSecondsRealtime(0.4f);
                Require(calls == 1, "Unscaled delay must finish once at timeScale 0");
                report("PASS: native DOTween unscaled delay at timeScale=0, once only");
                handle.Dispose(); calls = 0;
                handle = scheduler.Schedule(0.5f, () => calls++);
                yield return new WaitForSecondsRealtime(0.1f);
                handle.SetPaused(true);
                float remaining = handle.Remaining;
                Require(remaining > 0 && remaining < 0.5f, "Delay must be partially elapsed");
                yield return new WaitForSecondsRealtime(0.6f);
                Require(calls == 0 && Math.Abs(handle.Remaining - remaining) < 0.01f, "Pause preserves remaining delay");
                handle.SetPaused(false);
                yield return new WaitForSecondsRealtime(remaining + 0.1f);
                Require(calls == 1, "Resume must finish from remaining time");
                report("PASS: native DOTween Pause/Play retains remaining delay");
                handle.Dispose(); calls = 0;
                handle = scheduler.Schedule(0.3f, () => calls++);
                handle.Dispose(); handle.Dispose();
                yield return new WaitForSecondsRealtime(0.4f);
                Require(calls == 0, "Kill(false) must not report normal completion");
                report("PASS: native DOTween cancel is idempotent, no completion callback");
                yield return CheckView(prefab, screenCanvas, scheduler, report);
            }
            finally { handle?.Dispose(); Time.timeScale = originalScale; }
        }
        private static IEnumerator CheckView(GameObject prefab, Transform screenCanvas, IDialogueDelayScheduler scheduler, Action<string> report)
        {
            if (prefab == null || screenCanvas == null) throw new ArgumentNullException("Supply real Dialogue prefab and existing Core Canvas.");
            var table = new DialogueTableService("native tween regression");
            table.LoadJson("{\"Rows\":[{\"Dialogue_Code\":\"NATIVE\",\"Sequence\":1,\"Name\":\"어니\",\"Context\":\"포포를 찾으러 주방으로 가 보자!\",\"ContextRevealDuration\":0.8},{\"Dialogue_Code\":\"NATIVE\",\"Sequence\":2,\"Name\":\"어니\",\"Context\":\"다음 이야기\",\"ContextRevealDuration\":0.5}]}");
            var control = new DialogueRegressionTests.FakeControl();
            using (var service = new DialogueService(table, control, scheduler, new DialogueSettings(0.3f, 2f)))
            using (var vm = new DialogueViewModel(service))
            {
                GameObject root = UnityEngine.Object.Instantiate(prefab, screenCanvas, false);
                IDisposable option = null;
                try
                {
                    var view = root.GetComponent<DialogueView>();
                    var body = root.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "Context");
                    var panel = root.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b => b.name == "ContextPanel");
                    view.Bind(vm); service.TryStart("NATIVE");
                    yield return new WaitForSecondsRealtime(0.15f);
                    Require(service.State == DialogueState.Revealing && body.text.Length > 0 && body.text != vm.Context, "DOText visibly reveals partial string");
                    option = control.PausePresentation("options");
                    string frozen = body.text;
                    yield return new WaitForSecondsRealtime(0.35f);
                    Require(body.text == frozen, "Options pause actual typing");
                    option.Dispose(); option = null;
                    yield return new WaitForSecondsRealtime(0.1f);
                    service.SetAuto(true); service.OpenLog(); frozen = body.text;
                    yield return new WaitForSecondsRealtime(0.3f);
                    Require(body.text == frozen && !service.AutoEnabled, "Log pauses typing and disables auto");
                    service.CloseLog();
                    panel.onClick.Invoke();
                    Require(body.text == vm.Context && service.State == DialogueState.AwaitingAdvance, "Panel click kills typing and shows current full text");
                    panel.onClick.Invoke();
                    Require(service.CurrentRow.Sequence == 1, "Same frame panel click cannot advance again");
                    report("PASS: actual DOText typing, options/log pause, panel full reveal and duplicate click guard");
                    service.SetAuto(true);
                    yield return new WaitForSecondsRealtime(0.5f);
                    option = control.PausePresentation("options");
                    float remaining = service.AutoRemaining;
                    yield return new WaitForSecondsRealtime(0.4f);
                    Require(Math.Abs(service.AutoRemaining - remaining) < 0.01f && service.CurrentRow.Sequence == 1, "Native automatic hold pauses");
                    option.Dispose(); option = null;
                    yield return new WaitForSecondsRealtime(remaining + 0.1f);
                    Require(service.CurrentRow.Sequence == 2, "Native automatic hold resumes to next row");
                    service.SkipAll();
                    yield return new WaitForSecondsRealtime(0.7f);
                    Require(!service.IsActive && control.Blocks == 0 && service.Outcome == DialogueOutcome.Skipped, "Skip cancels typing/delays/lease");
                    report("PASS: native auto hold remaining-time resume and Skip cancels active row");
                    service.TryStart("NATIVE"); root.SetActive(false);
                    yield return new WaitForSecondsRealtime(1f);
                    Require(!service.IsActive && control.Blocks == 0, "Disable cancels tween and lease, no late callback");
                    root.SetActive(true); view.Bind(vm); service.TryStart("NATIVE");
                    yield return new WaitForSecondsRealtime(0.1f);
                    Require(body.text.Length > 0, "Rebind starts a fresh presentation");
                    report("PASS: disable cancellation and explicit rebind create no stale presentation");
                }
                finally { option?.Dispose(); UnityEngine.Object.Destroy(root); }
            }
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Dialogue native test failed: " + message);
        }
    }
}
