using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Keyboard3D.EditorTools
{
    /// <summary>
    /// 一键迁移到 URP：管线资产、Graphics/Quality 设置、左上主光、
    /// 相机 HDR+后处理、全局 Bloom Volume、反射探针。
    /// 菜单：Tools/3D 键盘/迁移到 URP
    /// </summary>
    public static class KeyboardUrpMigrator
    {
        [MenuItem("Tools/3D 键盘/迁移到 URP（管线+后处理+探针）")]
        public static void Migrate()
        {
            if (GraphicsSettings.defaultRenderPipeline != null)
            {
                Debug.LogWarning("[URP迁移] 项目已在 SRP 管线下，跳过管线创建，只做场景部分。");
            }
            else
            {
                Directory.CreateDirectory("Assets/Settings");

                // 1. 渲染器数据 + 管线资产
                var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, "Assets/Settings/URP_Renderer.asset");

                var pipeline = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
                AssetDatabase.CreateAsset(pipeline, "Assets/Settings/URP_Pipeline.asset");
                var so = new SerializedObject(pipeline);
                var list = so.FindProperty("m_RendererDataList");
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).objectReferenceValue = rendererData;
                so.FindProperty("m_DefaultRendererIndex").intValue = 0;
                so.ApplyModifiedPropertiesWithoutUndo();

                pipeline.supportsHDR = true;   // HDR：自发光 >1 与 Bloom 的前提
                pipeline.msaaSampleCount = 4;
                pipeline.shadowDistance = 60f;

                // 2. Graphics 默认管线 + 全部 Quality 级别都指向 URP
                GraphicsSettings.defaultRenderPipeline = pipeline;
                int current = QualitySettings.GetQualityLevel();
                for (int i = 0; i < QualitySettings.names.Length; i++)
                {
                    QualitySettings.SetQualityLevel(i, false);
                    QualitySettings.renderPipeline = pipeline;
                }
                QualitySettings.SetQualityLevel(current, false);
                AssetDatabase.SaveAssets();
                Debug.Log("[URP迁移] URP 资产创建并应用完成（HDR + MSAA4x）");
            }

            // 2.5 shader 打包占位：场景里没有直接引用 URP/Lit 的资产时，
            // 构建会把该 shader 裁剪掉，运行时 Shader.Find 会返回 null。
            // 放一个引用它的材质资产强制进包。
            // 注意：材质必须启用 _EMISSION 关键字——URP/Lit 的自发光是 shader_feature，
            // 只有被构建中的材质引用，运行时 EnableKeyword 的变体才不会被裁剪。
            if (AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/UrpLitPlaceholder.mat") == null)
            {
                var lit = Shader.Find("Universal Render Pipeline/Lit");
                if (lit != null)
                {
                    var ph = new Material(lit);
                    ph.EnableKeyword("_EMISSION");
                    ph.SetColor("_EmissionColor", Color.black);
                    // 光滑度遮罩（顶面镜面/侧壁哑光分区）也是 shader_feature：占位材质必须带上，
                    // 否则 WebGL 构建裁掉该变体，运行时 EnableKeyword 静默失效（侧壁又反光发灰）
                    ph.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
                    ph.SetFloat("_SmoothnessTextureChannel", 1f);
                    AssetDatabase.CreateAsset(ph, "Assets/Settings/UrpLitPlaceholder.mat");
                }
            }
            else
            {
                // 旧占位材质补新关键字（幂等升级）
                var ph = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/UrpLitPlaceholder.mat");
                if (ph != null && !ph.IsKeywordEnabled("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A"))
                {
                    ph.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
                    ph.SetFloat("_SmoothnessTextureChannel", 1f);
                    EditorUtility.SetDirty(ph);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[URP迁移] UrpLitPlaceholder 已补充 _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A 变体");
                }
            }

            // 2.6 粒子 shader 打包占位：运行时 Shader.Find("URP/Particles/Unlit") 创建尘埃材质，
            // 没有资产引用它 → 构建裁剪 → 粒子在 WebGL 完全不可见（编辑器不裁剪所以正常）。
            if (AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/UrpParticlesPlaceholder.mat") == null)
            {
                var psh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (psh != null)
                    AssetDatabase.CreateAsset(new Material(psh), "Assets/Settings/UrpParticlesPlaceholder.mat");
            }

            // 3. 主光：从左上方照射，形成顶面高光和侧面阴影
            foreach (var light in Object.FindObjectsOfType<Light>())
            {
                if (light.type != LightType.Directional) continue;
                light.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
                light.intensity = 1.0f;
                light.shadows = LightShadows.Soft;
            }

            // 4. 相机：开启 HDR 与后处理
            var cam = Camera.main;
            if (cam != null)
            {
                cam.allowHDR = true;
                var extra = cam.GetComponent<UniversalAdditionalCameraData>();
                if (extra == null) extra = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                extra.renderPostProcessing = true;
            }

            // 5. 全局 Volume：Bloom（克制参数，保持键帽边缘和文字可读）+ ACES 色调映射
            var volGo = GameObject.Find("KeyboardGlobalVolume");
            if (volGo == null)
            {
                volGo = new GameObject("KeyboardGlobalVolume");
                var vol = volGo.AddComponent<Volume>();
                vol.isGlobal = true;
                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                var bloom = profile.Add<Bloom>(true);
                bloom.threshold.Override(1.1f);
                bloom.intensity.Override(0.3f);
                bloom.scatter.Override(0.4f);
                var tm = profile.Add<Tonemapping>(true);
                tm.mode.Override(TonemappingMode.ACES);
                vol.profile = profile;
                AssetDatabase.CreateAsset(profile, "Assets/Settings/KeyboardVolumeProfile.asset");
            }

            // 6. 反射探针：罩住键盘，让亮面键帽呈现环境反射（运行时脚本刷新）
            var keyboard = GameObject.Find("Keyboard68");
            if (keyboard != null && keyboard.GetComponentInChildren<ReflectionProbe>() == null)
            {
                var probeGo = new GameObject("KeyboardReflectionProbe");
                probeGo.transform.SetParent(keyboard.transform, false);
                probeGo.transform.localPosition = new Vector3(0f, 3f, 0f);
                var probe = probeGo.AddComponent<ReflectionProbe>();
                probe.mode = ReflectionProbeMode.Realtime;
                probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
                probe.size = new Vector3(24f, 8f, 14f);
                probe.intensity = 1f;
                probe.boxProjection = true;
            }

            AssetDatabase.SaveAssets();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.isDirty) UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            Debug.Log("[URP迁移] 完成：管线 / 主光(左上) / 相机HDR+后处理 / Bloom / 反射探针。回 Play 后运行时脚本会自动刷新探针。");
        }
    }
}
