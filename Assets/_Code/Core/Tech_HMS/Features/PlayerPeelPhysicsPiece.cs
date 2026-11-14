using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cooked.HMS.AnimationIntegration
{
    /// <summary>게임 판정에 참여하지 않는 짧은 수명의 껍질 물리 연출 조각입니다.</summary>
    public sealed class PlayerPeelPhysicsPiece : MonoBehaviour
    {
        private float remaining;
        private float shrinkSeconds;
        private Vector3 originalScale;
        private Collider shape;

        public static PlayerPeelPhysicsPiece Create(MeshRenderer source, Vector3 velocity,
            Vector3 angularVelocity, float lifetime, float shrinkSeconds, LayerMask collisionLayers)
        {
            var filter = source.GetComponent<MeshFilter>();
            if (!filter || !filter.sharedMesh) return null;
            var piece = new GameObject(source.name + " (Physics VFX)");
            SceneManager.MoveGameObjectToScene(piece, source.gameObject.scene);
            // 일반 카메라 Raycast와 CharacterMovement의 기본 접지 검사에서 제외합니다.
            piece.layer = Physics.IgnoreRaycastLayer;
            piece.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            piece.transform.localScale = source.transform.lossyScale;
            piece.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var renderer = piece.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = source.sharedMaterials;
            renderer.shadowCastingMode = source.shadowCastingMode;
            renderer.receiveShadows = source.receiveShadows;
            var shape = piece.AddComponent<BoxCollider>();
            shape.center = filter.sharedMesh.bounds.center;
            shape.size = filter.sharedMesh.bounds.size;
            shape.excludeLayers = ~collisionLayers.value;
            var body = piece.AddComponent<Rigidbody>();
            body.mass = .02f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.maxAngularVelocity = Mathf.Max(7f, angularVelocity.magnitude);
            body.linearVelocity = velocity;
            body.angularVelocity = angularVelocity;
            // 레이어가 공통인 현재 프로젝트에서도 연출이 액터·센서·동적 소품에 힘을 가하지 않게 합니다.
            foreach (var other in Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude))
            {
                if (other == shape || other.gameObject.scene != piece.scene) continue;
                if (other.isTrigger || (other.attachedRigidbody && !other.attachedRigidbody.isKinematic) ||
                    other.GetComponentInParent<PlayerFacade>() || other.GetComponentInParent<PlayerDebris>() ||
                    other.GetComponentInParent<IStunnable>() != null)
                    Physics.IgnoreCollision(shape, other);
            }
            var effect = piece.AddComponent<PlayerPeelPhysicsPiece>();
            effect.remaining = Mathf.Max(.01f, lifetime);
            effect.shrinkSeconds = Mathf.Clamp(shrinkSeconds, 0f, effect.remaining);
            effect.originalScale = piece.transform.localScale;
            effect.shape = shape;
            return effect;
        }

        private void Update()
        {
            remaining -= Time.deltaTime;
            if (remaining <= 0f)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }
            if (shrinkSeconds <= 0f || remaining > shrinkSeconds) return;
            shape.enabled = false;
            transform.localScale = originalScale * Mathf.SmoothStep(0f, 1f, remaining / shrinkSeconds);
        }
    }
}
