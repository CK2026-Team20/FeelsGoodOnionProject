using System;
using System.Collections.Generic;
using System.Linq;
using Cooked.Audio;
using UnityEditor;
using UnityEngine;

namespace Cooked.Validation
{
    public static class AudioCatalogChecks
    {
        [Serializable] public sealed class Report { public int passed; public int failed; public List<string> cases = new List<string>(); }
        public static Report Run()
        {
            var report = new Report();
            Action<bool,string> check = (ok, name) => { if(ok) report.passed++; else report.failed++; report.cases.Add((ok?"P ":"F ")+name); };
            var catalog = AssetDatabase.LoadAssetAtPath<AudioCueCatalog>(AudioCueCatalog.AssetPath);
            var clips = catalog.CreateClipMap();
            var expected = new Dictionary<string,string>
            {
                { "music.title", "Bgm_Title" },
                { "music.opening", "Bgm_Opening" },
                { "music.stage1", "Bgm_Normal_Stage" },
                { "music.chase", "Bgm_Final_Stage" },
                { "music.ending", "Bgm_Ending" },
                { "sfx.ui.click", "Ui_Button" },
                { "sfx.pickup", "Sfx_Teardrop" },
                { "sfx.form", "Sfx_Pc_Size_Down" },
                { "sfx.tear", "Sfx_Pc_Tear" },
                { "sfx.checkpoint", "Sfx_Checkpoint" },
                { "sfx.unlock", "Sfx_Ability" },
                { "sfx.death", "Sfx_Pc_Death" },
                { "sfx.respawn", "Sfx_Pc_Respawn" },
                { "sfx.form.up", "Sfx_Pc_Size_Up" },
                { "sfx.jump", "Sfx_Pc_Jump" },
                { "sfx.land", "Sfx_Pc_Land" },
                { "sfx.run", "Sfx_Pc_Run" },
                { "sfx.hit", "Sfx_Pc_Hit" },
                { "sfx.monster.idle", "Sfx_Monster_Idle" },
                { "sfx.monster.hit", "Sfx_Monster_Hit" },
                { "sfx.weight.on", "Sfx_Weight_On" },
                { "sfx.weight.off", "Sfx_Weight_Off" },
                { "sfx.platform.break", "Sfx_Platform_Break" },
                { "sfx.oil", "Sfx_Oil" },
                { "sfx.interaction", "Sfx_Interaction" },
                { "sfx.fire", "Sfx_Fire" },
                { "sfx.pot", "Sfx_Pot" },
            };
            check(clips.Count == expected.Count, "카탈로그는 확보한 27개 고유 리소스");
            foreach(var item in expected)
                check(clips.TryGetValue(item.Key,out var clip) && clip != null &&
                    string.Equals(clip.name,item.Value,StringComparison.OrdinalIgnoreCase),item.Value+" 실제 음원 연결");
            check(PlayerAudioCueSelection.FormChanged(true)=="sfx.form","축소 성공은 Size_Down");
            check(PlayerAudioCueSelection.FormChanged(false)=="sfx.form.up","확대 성공은 Size_Up");
            check(PlayerAudioCueSelection.FragmentsChanged(4,5)=="sfx.unlock","4→5 준비음만 선택");
            check(PlayerAudioCueSelection.FragmentsChanged(3,4)=="sfx.pickup","일반 조각 획득음 선택");
            check(PlayerAudioCueSelection.FragmentsChanged(5,5)==null&&PlayerAudioCueSelection.FragmentsChanged(5,0)==null,"상한 유지·소비는 획득음 없음");
            var loopIds=catalog.CreateSettings().Where(c=>c.Loop).Select(c=>c.Id).ToHashSet();
            check(loopIds.SetEquals(new[]{"music.title","music.stage1","music.chase","sfx.fire","sfx.pot"}),"시트 Loop 5종과 One Shot 구분");
            return report;
        }
    }
}
