using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class TempFollowCamera : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("비워두면 시작 시 Player 태그로 찾는다.")]
    [SerializeField] private Transform target;

    [Tooltip("플레이어 원점에서 화면 중심으로 삼을 위치까지의 높이.")]
    [SerializeField] private float targetHeight = 1f;

    [Header("View")]
    [SerializeField, Range(5f, 70f)]
    private float pitch = 22f;

    [SerializeField, Min(0.1f)]
    private float distance = 11f;

    [Header("Follow")]
    [SerializeField, Min(0.01f)]
    private float smoothTime = 0.2f;

    private Vector3 followVelocity;

    private void Start()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");

            if (player != null)
            {
                target = player.transform;
            }
        }

        if (target == null)
        {
            Debug.LogWarning(
                "카메라 추적 대상이 없습니다. Player 태그를 설정해주세요.",
                this);

            return;
        }

        SnapToTarget();
    }

    private void LateUpdate()
    {
        if (target == null || Time.deltaTime <= 0f)
        {
            return;
        }

        Quaternion rotation = Quaternion.Euler(pitch, 0f, 0f);
        Vector3 desiredPosition = GetDesiredPosition(rotation);

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref followVelocity,
            smoothTime,
            Mathf.Infinity,
            Time.deltaTime);

        // 플레이어의 이동 방향과 무관하게 같은 각도를 유지한다.
        transform.rotation = rotation;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        SnapToTarget();
    }

    public void SnapToTarget()
    {
        followVelocity = Vector3.zero;

        if (target == null)
        {
            return;
        }

        Quaternion rotation = Quaternion.Euler(pitch, 0f, 0f);

        transform.SetPositionAndRotation(
            GetDesiredPosition(rotation),
            rotation);
    }

    private Vector3 GetDesiredPosition(Quaternion rotation)
    {
        Vector3 focusPosition =
            target.position + Vector3.up * targetHeight;

        return focusPosition - rotation * Vector3.forward * distance;
    }
}