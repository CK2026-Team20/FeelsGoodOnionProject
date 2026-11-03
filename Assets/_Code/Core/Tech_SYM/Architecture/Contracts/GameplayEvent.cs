using System;
using UnityEngine;
namespace Cooked.Authoring
{
    /// <summary>수락된 동작의 관측자 실패가 나머지 관측자와 게임 판정을 취소하지 않게 한다.</summary>
    public static class GameplayEvent
    {
        public static void Publish(Action handlers, UnityEngine.Object context)
        {
            if (handlers == null) return;
            foreach (Action handler in handlers.GetInvocationList())
                try { handler(); } catch (Exception error) { Debug.LogException(error, context); }
        }
        public static void Publish<T>(Action<T> handlers, T value, UnityEngine.Object context)
        {
            if (handlers == null) return;
            foreach (Action<T> handler in handlers.GetInvocationList())
                try { handler(value); } catch (Exception error) { Debug.LogException(error, context); }
        }
    }
}
