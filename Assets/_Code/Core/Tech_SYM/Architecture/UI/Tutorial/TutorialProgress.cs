using System;
using System.Collections.Generic;
namespace Cooked.UI
{
    /// <summary>한 판의 튜토리얼 활성화 장부. Actor 교체와 무관하며 저장 파일에 쓰지 않는다.</summary>
    public sealed class TutorialProgress
    {
        private readonly HashSet<object> revealed = new HashSet<object>();
        public bool IsRevealed(object id) => revealed.Contains(id);
        public bool TryReveal(object id) => revealed.Add(id);
    }
}
