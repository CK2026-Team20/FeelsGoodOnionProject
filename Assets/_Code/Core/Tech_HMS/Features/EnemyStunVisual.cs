using UnityEngine;

public sealed class EnemyStunVisual : MonoBehaviour
    {
        [Header("Stun Effect")]
        [SerializeField] private GameObject stunEffect;

        [Header("Texture")]
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Texture normalTexture;
        [SerializeField] private Texture stunnedTexture;

        [Tooltip("Texture를 변경할 Material Slot입니다.")]
        [SerializeField, Min(0)] private int materialIndex;

        [Tooltip("URP Lit의 Base Map은 _BaseMap입니다.")]
        [SerializeField] private string texturePropertyName = "_BaseMap";

        private IStunState stunState;
        private MaterialPropertyBlock propertyBlock;
        private int texturePropertyId;

        private void Awake()
        {
            stunState = GetComponentInParent<IStunState>();

            if (stunState == null)
            {
                Debug.LogError($"{nameof(EnemyStunVisual)} requires {nameof(IStunState)} in its parent hierarchy.",this);
                enabled = false;
                return;
            }

            if (stunEffect == null || targetRenderer == null ||
                normalTexture == null || stunnedTexture == null)
            {
                Debug.LogError($"{nameof(EnemyStunVisual)} setup is incomplete.", this);
                enabled = false;
                return;
            }

            if (materialIndex >= targetRenderer.sharedMaterials.Length)
            {
                Debug.LogError($"{nameof(EnemyStunVisual)} has an invalid material index.", this);

                enabled = false;
                return;
            }

            texturePropertyId = Shader.PropertyToID(texturePropertyName);

            Material material = targetRenderer.sharedMaterials[materialIndex];

            if (material == null || !material.HasProperty(texturePropertyId))
            {
                Debug.LogError($"Material does not contain texture property '{texturePropertyName}'.", this);
                enabled = false;
                return;
            }

            propertyBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (stunState == null)
                return;

            stunState.StunStateChanged += OnStunStateChanged;
            ApplyStunVisual(stunState.IsStunned);
        }

        private void OnDisable()
        {
            if (stunState != null)
                stunState.StunStateChanged -= OnStunStateChanged;
        }

        private void OnStunStateChanged(bool isStunned)
        {
            ApplyStunVisual(isStunned);
        }

        private void ApplyStunVisual(bool isStunned)
        {
            stunEffect.SetActive(isStunned);

            SetTexture(isStunned ? stunnedTexture : normalTexture);
        }

        private void SetTexture(Texture texture)
        {
            targetRenderer.GetPropertyBlock(propertyBlock, materialIndex);
            propertyBlock.SetTexture(texturePropertyId, texture);
            targetRenderer.SetPropertyBlock(propertyBlock, materialIndex);
        }
    }
