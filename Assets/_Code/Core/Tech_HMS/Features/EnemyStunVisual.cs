using UnityEngine;

public sealed class EnemyStunVisual : MonoBehaviour
    {
        [Header("Stun Effect")]
        [Tooltip("Stun 여부에 따라 활성화/비활성화되는 비주얼 목적 오브젝트입니다.\n(StunEffectOrbit 컴포넌트의 부착은 필수가 아닙니다.)")]
        [SerializeField] private GameObject stunEffect;

        [Header("Texture")]
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Texture normalTexture;
        [SerializeField] private Texture stunnedTexture;

        [Tooltip("Texture를 변경할 Material Slot입니다.")]
        [SerializeField, Min(0)] private int materialIndex;

        [Tooltip("기본 텍스처 속성입니다. URP Lit은 _BaseMap, Unity Toon은 _MainTex를 사용합니다.")]
        [SerializeField] private string texturePropertyName = "_BaseMap";

        [Tooltip("같은 텍스처로 함께 변경할 속성입니다. 별도 음영 텍스처를 사용하는 셰이더에 설정합니다.")]
        [SerializeField] private string[] additionalTexturePropertyNames = new string[0];

        private IStunState stunState;
        private MaterialPropertyBlock propertyBlock;
        private int texturePropertyId;
        private int[] additionalTexturePropertyIds;

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

            int additionalCount = additionalTexturePropertyNames != null ? additionalTexturePropertyNames.Length : 0;
            additionalTexturePropertyIds = new int[additionalCount];
            for (int index = 0; index < additionalCount; index++)
            {
                string propertyName = additionalTexturePropertyNames[index];
                if (string.IsNullOrWhiteSpace(propertyName) || !material.HasProperty(propertyName))
                {
                    Debug.LogError($"Material does not contain texture property '{propertyName}'.", this);
                    enabled = false;
                    return;
                }

                additionalTexturePropertyIds[index] = Shader.PropertyToID(propertyName);
            }

            propertyBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (stunState == null || propertyBlock == null)
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
            foreach (int additionalPropertyId in additionalTexturePropertyIds)
                propertyBlock.SetTexture(additionalPropertyId, texture);
            targetRenderer.SetPropertyBlock(propertyBlock, materialIndex);
        }
    }
