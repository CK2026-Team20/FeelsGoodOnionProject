using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using FeelsGoodOnion.TechSYM.Interaction;
namespace FeelsGoodOnion.TechSYM.EditorTools
{
    public static class MemoValidation
    {
        [MenuItem("Tools/Tech SYM/Validate Memo Data")]
        public static void ValidateMenu() => Debug.Log(Validate());
        /// <summary>모든 MemoModel의 PK와 로드된 씬의 비활성 객체까지 참조 중복을 검사한다.</summary>
        public static string Validate()
        {
            var errors = new List<string>();
            var ids = new Dictionary<int,string>();
            foreach (string guid in AssetDatabase.FindAssets("t:MemoModel"))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);
                var data=AssetDatabase.LoadAssetAtPath<MemoModel>(path);
                if(data.MemoID<=0 || data.MemoImage==null) errors.Add(path + ": PK/Sprite 누락");
                if(ids.TryGetValue(data.MemoID,out var previous)) errors.Add(path + ": PK 중복: " + previous);
                else ids.Add(data.MemoID,path);
            }
            var owners = new Dictionary<MemoModel,MemoObject>();
            foreach(var owner in UnityEngine.Object.FindObjectsByType<MemoObject>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(owner.Data==null) { errors.Add(owner.name + ": 모델 누락"); continue; }
                if(owners.TryGetValue(owner.Data,out var previous)) errors.Add(owner.name + ": 모델 공유: " + previous.name);
                else owners.Add(owner.Data,owner);
            }
            if(errors.Count>0) throw new InvalidOperationException(string.Join("\n",errors));
            return $"PASS: {ids.Count} unique MemoModel PKs; {owners.Count} exclusive scene owners (including inactive).";
        }
    }
}
