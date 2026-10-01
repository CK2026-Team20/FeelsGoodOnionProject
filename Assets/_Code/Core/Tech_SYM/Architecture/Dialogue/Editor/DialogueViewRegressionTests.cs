using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
namespace Cooked.Dialogue.Tests
{
    /// <summary>Run with the authored prefab during Play Mode. Does not author a substitute scene or start Editor.</summary>
    public static class DialogueViewRegressionTests
    {
        public static IReadOnlyList<string> Run(GameObject prefab, Transform screenCanvas)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Dialogue View regression requires Play Mode.");
            if (prefab == null || screenCanvas == null) throw new ArgumentNullException("Supply Dialogue prefab and the existing Core screen Canvas.");
            var passed = new List<string>();
            var table = new DialogueTableService("view regression");
            table.LoadJson("{\"Rows\":[{\"Dialogue_Code\":\"REPEAT\",\"Sequence\":1,\"Name\":\"어니\",\"Context\":\"같은 대화의 기록\",\"ContextRevealDuration\":0}]}");
            var control = new DialogueRegressionTests.FakeControl();
            using (var service = new DialogueService(table, control, new DOTweenDialogueDelayScheduler()))
            using (var vm = new DialogueViewModel(service))
            {
                var root = UnityEngine.Object.Instantiate(prefab, screenCanvas, false);
                try
                {
                    var view = root.GetComponent<DialogueView>();
                    var text = root.GetComponentsInChildren<TMP_Text>(true).Single(item => item.name == "HistoryText");
                    view.Bind(vm);
                    for (int run = 0; run < 2; run++)
                    {
                        if (!service.TryStart("REPEAT")) throw new Exception("Cannot start replay");
                        service.OpenLog();
                        if (!text.text.Contains("같은 대화의 기록")) throw new Exception("History is blank on repeat " + run);
                        service.SkipAll();
                        if (text.text != string.Empty) throw new Exception("Closed history not cleared");
                    }
                    passed.Add("PASS: actual DialogueView history renders identical conversation twice after closing");
                    service.TryStart("REPEAT"); view.Bind(vm); view.Bind(vm);
                    int ended = 0; service.Ended += _ => ended++;
                    var skip = root.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(item => item.name == "Skip");
                    skip.onClick.Invoke();
                    if (ended != 1 || service.IsActive || control.Blocks != 0) throw new Exception("Rebind/command lifetime failure");
                    passed.Add("PASS: repeated Bind, one button callback ends once and returns lease");
                    service.TryStart("REPEAT"); root.SetActive(false);
                    if (service.IsActive || control.Blocks != 0) throw new Exception("Disable strands dialogue/lease");
                    root.SetActive(true); view.Bind(vm); service.TryStart("REPEAT"); service.OpenLog();
                    if (!text.text.Contains("같은 대화의 기록")) throw new Exception("Explicit OnEnable rebind did not restore presentation");
                    passed.Add("PASS: Disable cancels; explicit rebind after Enable supports fresh conversation");
                }
                finally { UnityEngine.Object.Destroy(root); }
            }
            return passed.AsReadOnly();
        }
    }
}
