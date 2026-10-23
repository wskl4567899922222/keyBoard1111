using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Keyboard3D
{
    /// <summary>
    /// 修复 WebGL/Android 构建后键盘发光变黑的问题：
    /// 运行时 EnableKeyword("_EMISSION") 选择的着色器变体，如果构建时
    /// 没有任何材质使用过，会被 Unity 裁剪掉（编辑器按需编译所以看不出）。
    /// 把相关着色器加入 Always Included Shaders 强制打包全部变体即可。
    /// </summary>
    public static class KeyboardGlowBuildFix
    {
        [MenuItem("Tools/3D 键盘/修复构建后发光变黑 (Always Included Shaders)")]
        public static void Fix()
        {
            string[] shaderNames =
            {
                "Standard",
                "Standard (Specular setup)",
                "Universal Render Pipeline/Lit",
                // 粒子材质运行时通过 Shader.Find 创建，必须强制进包，否则构建后粒子消失
                "Particles/Standard Unlit",
                "Sprites/Default",
                "Legacy Shaders/Particles/Additive",
                "Universal Render Pipeline/Particles/Unlit",
            };

            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError("[发光修复] 找不到 ProjectSettings/GraphicsSettings.asset");
                return;
            }
            var so = new SerializedObject(assets[0]);
            var prop = so.FindProperty("m_AlwaysIncludedShaders");
            if (prop == null || !prop.isArray)
            {
                Debug.LogError("[发光修复] GraphicsSettings 中找不到 m_AlwaysIncludedShaders 字段（Unity 版本不兼容？）");
                return;
            }

            int added = 0;
            var addedNames = new List<string>();
            foreach (var shaderName in shaderNames)
            {
                var shader = Shader.Find(shaderName);
                if (shader == null) continue; // 该管线不存在此着色器，跳过

                bool exists = false;
                for (int i = 0; i < prop.arraySize; i++)
                    if (prop.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                    {
                        exists = true;
                        break;
                    }
                if (exists) continue;

                prop.InsertArrayElementAtIndex(prop.arraySize);
                prop.GetArrayElementAtIndex(prop.arraySize - 1).objectReferenceValue = shader;
                added++;
                addedNames.Add(shaderName);
            }
            so.ApplyModifiedProperties();

            // 双保险：在 Resources 里放一个启用自发光的 Standard 材质资产，
            // Resources 内容必然进包，其 _EMISSION 变体也就必然被收录
            string keeperDir = "Assets/Keyboard3D/Resources";
            if (!AssetDatabase.IsValidFolder(keeperDir))
                AssetDatabase.CreateFolder("Assets/Keyboard3D", "Resources");
            string keeperPath = keeperDir + "/GlowEmissionKeeper.mat";
            var keeper = AssetDatabase.LoadAssetAtPath<Material>(keeperPath);
            if (keeper == null)
            {
                var std = Shader.Find("Standard");
                if (std != null)
                {
                    keeper = new Material(std);
                    keeper.EnableKeyword("_EMISSION");
                    keeper.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    keeper.SetColor("_EmissionColor", new Color(0f, 1f, 0.12f) * 1.5f);
                    AssetDatabase.CreateAsset(keeper, keeperPath);
                    addedNames.Add("GlowEmissionKeeper.mat");
                }
            }
            if (keeper != null) EditorUtility.SetDirty(keeper);
            AssetDatabase.SaveAssets();

            if (added > 0)
                Debug.Log($"[发光修复] 已加入 Always Included Shaders：{string.Join("、", addedNames)}。请重新 Build WebGL 并重新发布。");
            else
                Debug.Log("[发光修复] 相关着色器已在 Always Included Shaders 中，无需改动。");
        }
    }
}
