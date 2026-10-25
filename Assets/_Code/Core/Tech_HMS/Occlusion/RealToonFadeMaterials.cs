using UnityEngine;
using UnityEngine.Rendering;
namespace Cooked.Occlusion
{
    /// <summary>원본을 보존하고 실행 중 복사본으로 RealToon의 투명 기능을 제어합니다.</summary>
    public static class RealToonFadeMaterials
    {
        public const string OriginalShader = "Universal Render Pipeline/RealToon/Version 5/Default/Default";
        public const string FadeShader = "Universal Render Pipeline/RealToon/Version 5/Default/Cooked Occlusion Fade";
        public static bool Supported(Material m) => m && m.shader &&
            (m.shader.name == OriginalShader || m.shader.name == FadeShader) &&
            !m.IsKeywordEnabled("N_F_CO_ON") && m.GetFloat("_N_F_CO") < 0.5f &&
            !m.IsKeywordEnabled("N_F_TRANS_ON") && m.GetFloat("_TRANSMODE") < 0.5f;
        public static Material Create(Material source, Shader shader)
        {
            var copy = new Material(source) { name = source.name + " (Occlusion Runtime)", hideFlags = HideFlags.HideAndDontSave };
            if (shader) copy.shader = shader;
            copy.SetFloat("_TRANSMODE", 1f); copy.EnableKeyword("N_F_TRANS_ON");
            copy.SetFloat("_SimTrans", 1f); copy.EnableKeyword("N_F_SIMTRANS_ON");
            copy.SetInt("_BleModSour", (int)BlendMode.SrcAlpha);
            copy.SetInt("_BleModDest", (int)BlendMode.OneMinusSrcAlpha);
            copy.SetInt("_ZWrite", 1); copy.SetInt("_OutZWrite", 0);
            copy.SetInt("_ZTest", (int)CompareFunction.LessEqual);
            copy.SetInt("_OutZTest", (int)CompareFunction.LessEqual);
            copy.SetFloat("_TransAffSha", 0f); copy.DisableKeyword("N_F_TRANSAFFSHA_ON");
            copy.SetOverrideTag("RenderType", "Transparent"); copy.renderQueue = (int)RenderQueue.Transparent;
            copy.SetShaderPassEnabled("DepthOnly", false); copy.SetShaderPassEnabled("DepthNormals", false);
            copy.SetShaderPassEnabled("MotionVectors", false); copy.SetShaderPassEnabled("XRMotionVectors", false);
            copy.enableInstancing = false;
            return copy;
        }
        public static void Destroy(Material material)
        {
            if (!material) return;
            if (Application.isPlaying) Object.Destroy(material); else Object.DestroyImmediate(material);
        }
    }
}
