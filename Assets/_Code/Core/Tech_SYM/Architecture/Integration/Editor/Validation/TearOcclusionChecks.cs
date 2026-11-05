using System;
using System.Collections.Generic;
using System.Reflection;
using Cooked.Session;
using FeelsGoodOnion.TechSYM.Enemies;
using UnityEngine;

namespace Cooked.Validation
{
    /// <summary>Play 없이 격리된 위치에서 실제 PhysX 차폐 검사를 확인한다.</summary>
    public static class TearOcclusionChecks
    {
        [Serializable] public sealed class Report { public int passed, failed; public List<string> cases = new List<string>(); }
        public static Report Run()
        {
            var report = new Report();
            var method = typeof(PlayerBridgeService).GetMethod("IsTearPathClear", BindingFlags.NonPublic | BindingFlags.Static);
            var root = new GameObject("__TearOcclusionValidation") { hideFlags = HideFlags.HideAndDontSave };
            root.transform.position = new Vector3(10000, 10000, 10000);
            GameObject Node(string name, Vector3 offset, bool enemy = false)
            {
                var node = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
                node.SetActive(false);
                node.transform.SetParent(root.transform, false); node.transform.localPosition = offset;
                if (enemy) node.AddComponent<EnemyActor>().enabled = false;
                node.SetActive(true); return node;
            }
            try
            {
                var player = Node("Player", Vector3.zero);
                var target = Node("Target", Vector3.right * 4, true).AddComponent<BoxCollider>();
                target.isTrigger = true;
                bool Clear()
                {
                    Physics.SyncTransforms();
                    return (bool)method.Invoke(null, new object[] { player.transform.position, target, player.transform });
                }
                void Check(bool expected, string name)
                {
                    bool ok = Clear() == expected;
                    if (ok) report.passed++; else report.failed++;
                    report.cases.Add((ok ? "P " : "F ") + name);
                }
                Check(true, "대상 적 단독 적중");
                var blocker = Node("OverlappingEnemy", Vector3.right * 3.2f, true).AddComponent<BoxCollider>();
                blocker.size = Vector3.one * 2; blocker.isTrigger = true;
                Check(true, "서로 겹친 적의 몸체 Trigger는 차폐하지 않음");
                blocker.isTrigger = false;
                Check(true, "다른 적의 일반 Collider도 차폐하지 않음");
                var child = new GameObject("EnemyChild"); child.transform.SetParent(blocker.transform, false);
                child.AddComponent<BoxCollider>(); blocker.enabled = false;
                Check(true, "적 자식 Collider도 차폐하지 않음");
                var wall = Node("Wall", Vector3.right * 2).AddComponent<BoxCollider>();
                Check(false, "다른 적과 겹쳐도 실제 벽은 차단");
                wall.transform.localPosition = Vector3.right * 5;
                Check(true, "대상 몸체 뒤의 벽은 차단하지 않음");
                wall.transform.localPosition = Vector3.right * 2; wall.isTrigger = true;
                Check(true, "일반 상호작용 Trigger는 차단하지 않음");
                var self = new GameObject("PlayerChild"); self.transform.SetParent(player.transform, false);
                self.transform.localPosition = Vector3.right; self.AddComponent<BoxCollider>();
                Check(true, "플레이어 자식 Collider는 차단하지 않음");
                target.transform.position = player.transform.position;
                Check(true, "시전 중심과 적 몸체가 겹쳐도 적중");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); Physics.SyncTransforms(); }
            return report;
        }
    }
}
