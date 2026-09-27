using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerFacade))]
public sealed class Test_PlayerFunc : MonoBehaviour
{
    private PlayerFacade player;
    
    private void Awake()
    {
        player = GetComponent<PlayerFacade>();
    }
    
    private void Update()
    {
#if UNITY_EDITOR
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.lKey.wasPressedThisFrame)
        {
            int appliedDamage = player.Damage(1);
            Debug.Log($"[TestCode] 실제 피해량: {appliedDamage}", this);
        }

        if (keyboard.oKey.wasPressedThisFrame)
        {
            player.SetInputBlocked(true);
            Debug.Log("[TestCode] 이동 및 점프 입력 차단 요청", this);
        }

        if (keyboard.pKey.wasPressedThisFrame)
        {
            player.SetInputBlocked(false);
            Debug.Log("[TestCode] 이동 및 점프 입력 차단 해제 요청", this);
        }

        if (keyboard.kKey.wasPressedThisFrame)
        {
            player.ApplyKnockback(new Vector3(-3f, 3f, 0f), 1000f);
            Debug.Log("[TestCode] 넉백 요청: 속도 (-3, 3, 0), 제어 제한 1초", this);
        }
#endif
    }
    
    private void OnEnable()
    {
#if UNITY_EDITOR
        Debug.LogWarning("[TestCode] L: 피해 1 / O: 이동·점프 입력 차단 / P: 차단 해제 / K: 넉백 및 제어 제한 1초", this);
#endif
    }
}