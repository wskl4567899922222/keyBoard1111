using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using System.Linq;

namespace Keyboard3D
{
    /// <summary>
    /// 修复 FBX 键盘在 Unity 里显得黯淡的问题：
    /// 1. 材质：Phong → Standard，去掉冲淡颜色的灰色高光，轻微提升饱和度
    /// 2. 光照：把场景调亮到接近 Spline 影棚预览的效果
    /// </summary>
    public static class KeyboardLookFixer
    {
        const float SaturationBoost = 1.18f; // 饱和度提升倍率，可自行调整

        [MenuItem("Tools/3D 键盘/修复键盘材质 (还原鲜艳度)")]
        public static void FixMaterials()
        {
            if (PlayerSettings.colorSpace == ColorSpace.Gamma)
            {
                Debug.LogWarning("[键盘修复] 当前项目色彩空间是 Gamma，建议 Project Settings → Player → Other Settings → Color Space 改为 Linear，光照层次会明显变好。");
            }

            int matCount = 0;
            // FBX 内嵌材质（项目资源）+ 场景实例都收集
            var mats = Resources.FindObjectsOfTypeAll<Material>()
                .Where(m => m != null && (m.name.StartsWith("Key_") || m.name == "Case"))
                .Distinct()
                .ToList();
            foreach (var r in Object.FindObjectsOfType<Renderer>())
                foreach (var m in r.sharedMaterials)
                    if (m != null && !mats.Contains(m)) mats.Add(m);

            foreach (var m in mats)
            {
                if (!(m.name.StartsWith("Key_") || m.name == "Case" || m.name.Contains("Keyboard68")))
                    continue;

                // 找到项目里实际可用的 Standard 着色器
                Shader std = Shader.Find("Standard");
                if (std == null)
                {
                    // URP 项目
                    Debug.LogWarning("[键盘修复] 找不到 Standard 着色器（URP 项目），请确认材质已用 URP Lit，或通过 Window → Rendering → Render Pipeline Converter 转换材质。");
                    return;
                }

                m.shader = std;
                Color c = m.color; // Standard 的 _Color / albedo
                m.SetColor("_Color", BoostSaturation(c));
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_Glossiness", 0.35f);          // 低光滑度，避免灰色反光冲淡颜色
                m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                m.SetFloat("_SpecularHighlights", 0f);     // 关闭镜面高光
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                matCount++;
                EditorUtility.SetDirty(m);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[键盘修复] 已处理 {matCount} 个材质：Standard 着色器、关闭镜面高光、饱和度 ×{SaturationBoost}。");
        }

        static Color BoostSaturation(Color c)
        {
            float h, s, v;
            Color.RGBToHSV(c, out h, out s, out v);
            s = Mathf.Clamp01(s * SaturationBoost);
            v = Mathf.Clamp01(v * 1.04f); // 亮度也稍微提一点
            Color r = Color.HSVToRGB(h, s, v);
            r.a = c.a;
            return r;
        }

        [MenuItem("Tools/3D 键盘/优化场景光照 (接近 Spline 预览)")]
        public static void FixLighting()
        {
            // 主光：亮一点的暖白平行光
            Light key = FindOrCreateLight("Keyboard Key Light");
            key.type = LightType.Directional;
            key.intensity = 1.35f;
            key.color = new Color(1f, 0.98f, 0.95f);
            key.shadows = LightShadows.Soft;
            key.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 补光：冷色弱光，从另一侧填充
            Light fill = FindOrCreateLight("Keyboard Fill Light");
            fill.type = LightType.Directional;
            fill.intensity = 0.55f;
            fill.color = new Color(0.85f, 0.9f, 1f);
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(35f, 140f, 0f);

            // 环境：三段式提亮，避免暗面死黑发灰
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.64f, 0.70f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.5f, 0.55f);
            RenderSettings.ambientGroundColor = new Color(0.38f, 0.37f, 0.40f);

            Debug.Log("[键盘修复] 已布置主光 + 补光 + 三段式环境光。");
        }

        static Light FindOrCreateLight(string name)
        {
            GameObject go = GameObject.Find(name);
            if (go == null)
            {
                go = new GameObject(name);
                go.AddComponent<Light>();
                Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            }
            Light l = go.GetComponent<Light>();
            Undo.RecordObject(l, "Fix Light");
            Undo.RecordObject(l.transform, "Fix Light");
            return l;
        }
    }
}
