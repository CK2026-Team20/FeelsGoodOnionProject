using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerFacade))]
public sealed class TestPlayerDamage : MonoBehaviour
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

        if (keyboard != null && keyboard.lKey.wasPressedThisFrame)
        {
            player.Damage(1);
        }
#endif
    }
    
    private void OnEnable()
    {
#if UNITY_EDITOR
        Debug.LogWarning("[TestCode] L키를 누르면 플레이어에게 피해 1을 가합니다.", this);
#endif
    }
}