using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Keyboard3D
{
    /// <summary>
    /// WebGL 构建前自动刷新 shader 变体 keeper（Assets/Keyboard3D/Resources/）：
    /// 运行时 EnableKeyword 的变体（_EMISSION、_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A、
    /// URP/Particles/Unlit 透明）如果构建时没有任何"打进包的材质"使用过，会被 Unity 裁剪——
    /// 编辑器按需编译看不出问题，WebGL 里表现为键帽侧面无发光/无光泽、粒子方块。
    /// Resources 里的 keeper 材质必然进包，其关键字组合的变体也就必然被收录。
    /// </summary>
    public class KeyboardShaderKeeper : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        const string LitKeeperPath = "Assets/Keyboard3D/Resources/UrpLitKeeper.mat";
        const string ParticlesKeeperPath = "Assets/Keyboard3D/Resources/UrpParticlesKeeper.mat";

        public void OnPreprocessBuild(BuildReport report)
        {
            var refreshed = new List<string>();

            // 1. URP/Lit：发光 + 分区光滑度遮罩（顶面镜面/侧壁光泽由遮罩 alpha 提供）
            if (EnsureMat(LitKeeperPath, "Universal Render Pipeline/Lit", m =>
            {
                m.EnableKeyword("_EMISSION");
                m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
                if (m.HasProperty("_SmoothnessTextureChannel")) m.SetFloat("_SmoothnessTextureChannel", 1f);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 1f);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", new Color(0f, 1f, 0.12f) * 2.6f);
            })) refreshed.Add("UrpLitKeeper(_EMISSION + _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A)");

            // 2. URP/Particles/Unlit：透明 alpha 混合（粒子圆形软边 + WebGL 可见）
            if (EnsureMat(ParticlesKeeperPath, "Universal Render Pipeline/Particles/Unlit", m =>
            {
                if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
                if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", 5f);
                if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", 10f);
                if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
                m.renderQueue = 3000;
            })) refreshed.Add("UrpParticlesKeeper(transparent)");

            if (refreshed.Count > 0)
                Debug.LogWarning("[Keyboard3D] 构建 keeper 已刷新（保证运行时变体进包）：" + string.Join("、", refreshed));
        }

        static bool EnsureMat(string path, string shaderName, System.Action<Material> setup)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) return false;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var dir = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
                if (!AssetDatabase.IsValidFolder(dir))
                {
                    System.IO.Directory.CreateDirectory(dir);
                    AssetDatabase.Refresh();
                }
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }
            setup(mat);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return true;
        }
    }
}
