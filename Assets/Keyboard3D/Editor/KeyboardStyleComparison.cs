using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Keyboard3D.EditorTools
{
    /// <summary>
    /// 添加"对比键盘"：克隆现有键盘（KeyboardModelInput 所在物体），去掉交互脚本做成静态展示，
    /// 并换上参考图风格的高光材质——钢琴黑键帽（Smoothness ~0.96 强镜面）+
    /// 深色金属光泽底座（Metallic ~0.75）。菜单：Tools/3D 键盘/添加对比键盘（参考图高光样式）
    /// </summary>
    public static class KeyboardStyleComparison
    {
        [MenuItem("Tools/3D 键盘/添加对比键盘（参考图高光样式）")]
        public static void AddComparisonKeyboard()
        {
            var src = Object.FindObjectOfType<KeyboardModelInput>();
            if (src == null)
            {
                Debug.LogError("[对比键盘] 场景里没找到 KeyboardModelInput（原键盘根物体）");
                return;
            }
            var root = src.gameObject;

            // 已存在则先删除重建——保证每次运行都用最新的 Gloss 模型/材质参数
            var existing = GameObject.Find(root.name + "_Gloss");
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
                Debug.Log("[对比键盘] 已删除旧对比键盘，重建中……");
            }

            // 使用带弧度键帽的专用模型（顶部内收拔模 + Bevel 倒边 + 平滑着色）
            const string GlossModelPath = "Assets/Models/Keyboard68_Gloss.fbx";
            var glossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GlossModelPath);
            if (glossPrefab == null)
            {
                Debug.LogError("[对比键盘] 找不到 " + GlossModelPath + "（模型还没生成/导入）");
                return;
            }
            var clone = (GameObject)PrefabUtility.InstantiatePrefab(glossPrefab);
            clone.name = root.name + "_Gloss";
            // 摆在原键盘右侧（沿原键盘自身的右方向偏移，兼容任意旋转），留出 18 单位键宽的间隔
            clone.transform.position = root.transform.position + root.transform.right * 21f;
            clone.transform.rotation = root.transform.rotation;

            // 静态展示模型不含交互脚本；运行时残留物清理兜底
            var input = clone.GetComponent<KeyboardModelInput>();
            if (input != null) Object.DestroyImmediate(input);
            // 反射探针不重复（原键盘的探针已覆盖场景）
            foreach (var probe in clone.GetComponentsInChildren<ReflectionProbe>(true))
                Object.DestroyImmediate(probe.gameObject);
            // 运行时生成的灯光/粒子若在编辑器里残留也一并清理
            foreach (var lt in clone.GetComponentsInChildren<Light>(true))
                Object.DestroyImmediate(lt.gameObject);
            foreach (var ps in clone.GetComponentsInChildren<ParticleSystem>(true))
                Object.DestroyImmediate(ps.gameObject);

            // 效果与原键盘完全一致（目标键常亮绿光、按下更亮、初始/按压粒子）——
            // 全部由 KeyboardModelInput 运行时提供：键位映射、发光 UV/遮罩、灯光组、粒子系统。
            // 旋转/缩放关掉（enableOrbit=false）：视角控制由原键盘实例统一负责，
            // 两个实例同时响应拖拽/滚轮会导致转速与缩放速度翻倍；按压交互不受影响。
            var fx = clone.AddComponent<KeyboardModelInput>();
            fx.enableOrbit = false;
            // 发光键 = Spline Main 键的绿玉质感：整键染深绿（glowAlbedoLerp 0.9）、
            // 顶面均匀亮绿（topGlowFalloff 0.55，边缘 0.45×2.6≈1.17）+ 柔和宽高光、
            // 侧壁亮绿长渐变沉到暗绿（top 0.40 → bottom 0.10，curve 1.4），
            // 侧壁 albedo 0.25 让深绿透出、光滑度 0.9 出光泽高光。
            fx.sideAlbedoDark = 0.15f;      // 侧壁漫反射压暗（用户反馈初始态"右侧面太亮/前面有点亮"）：0.25 → 0.15
            fx.keycapSmoothness = 0.62f;    // 半哑光硬塑料：有一定反光但柔和
            fx.sideSmoothnessMask = 0.62f;  // 侧壁柔光渐变而非镜面亮条
            fx.capTopNormalMinY = 0.70f;
            fx.sideGlowTop = 0.55f;         // 顶缘绿线 0.40→0.55：提亮端部弧面侧壁
            fx.sideGlowBottom = 0.05f;      // 底部 0.10 → 0.05：接近黑
            fx.sideGlowCurve = 2.0f;        // 渐变 1.4 → 2.0：绿光集中在顶缘、往下快速沉黑
            fx.topGlowFalloff = 0.8f;       // 初始顶面：绿光集中在中心，边缘 emission≈0（暗翡翠）
            fx.idleGlowTightness = 0.45f;   // 平顶化+全椭圆UV：端部达中心 93%，配合宽键自发光增强成均匀长条
            fx.idleTopScale = 0.25f;        // 初始顶面发光亮度 25%（参考图无白色爆点，玉绿不炸）
            fx.glowAlbedoLerp = 0.42f;      // 染色提亮（用户反馈初始态"为什么还是暗"，红圈=顶面周围）：
                                            // 0.32 → 0.42；配合泛光 0.45，周围明显亮起、中心光斑仍突出
            fx.glowIntensity = 1.5f;        // 对比键盘降亮度（原键盘 2.6）：中心亮斑外整体沉暗
            fx.pressGlowCore = 0.5f;        // 按下中心轻微增亮（参考图：顶面均匀绿、中心只是略亮）
            fx.pressGlowCoreSize = 0.22f;   // 光斑缩小（用户反馈"光斑有点大"）：0.3 → 0.22
            fx.pressGlowBoost = 0.3f;       // 按压加成收敛：避免整键过亮淹没字符
            fx.pressGlowEdgeKeep = 0.45f;   // 按下基底改为未收紧径向 × 0.45：顶面整体均匀亮绿（参考图），
                                            // 不再是"亮斑+黑边"
            fx.lightOnPressOnly = true;     // 初始态顶灯熄灭：顶面只剩自发光径向渐变（暗翡翠），按下顶灯才点亮
            fx.lightPressBase = 0.4f;       // 按下即有 40% 顶灯亮度，跟手提亮
            fx.lightPressPeak = 1.0f;       // 按到底顶灯峰值 1.6 → 1.0（用户反馈"按下时顶面全亮了"）
            fx.haloPressPeak = 0.3f;        // 按到底泛光恢复 100% → 30%（用户反馈"侧面有点亮"：泛光贴键顶，
                                            // 按下时把侧壁照亮了；顶面亮度改由均匀 emission 基底承担）
            fx.sideGlowPressDim = 0.55f;    // 按下时侧壁绿光压到 55%（参考图按下=顶亮侧暗）
            fx.enableFillLight = true;      // 补光（参考图双高光）：主光+补光在圆角两侧各切出一条竖向高光
            fx.fillLightIntensity = 0.45f;
            fx.fillLightAngles = new Vector3(35f, 55f, 0f);
            fx.enableSpillLight = true;     // 邻键溢光：发光键把绿光洒到邻键侧壁（参考图）
            fx.spillLightIntensity = 1.6f;
            fx.haloPressDim = 0.35f;        // 初始泛光 0.45 → 0.35：泛光也照侧壁，配合侧壁压暗
            fx.haloSideIntensity = 0f;      // 用户要求去掉两盏侧灯，亮度由自发光承担

            // 参考图风格材质：钢琴黑高光键帽 / 银白字符 / 深色金属底座
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var keycap = new Material(lit); // 键帽：近黑 + 高光滑度（强镜面高光）
            keycap.SetColor("_BaseColor", new Color(0.024f, 0.028f, 0.033f));
            keycap.SetFloat("_Metallic", 0f);
            keycap.SetFloat("_Smoothness", 0.92f);
            var character = new Material(lit); // 字符：自发光白——不依赖光照、不受阴影影响。
                                               // 之前白漫反射贴着键帽面（间隙 0.004 < 阴影贴图精度），
                                               // 键帽的 shadow map 深度抖动随机遮住字符 → 字符碎片化
            character.SetColor("_BaseColor", Color.white);
            character.SetFloat("_Metallic", 0f);
            character.SetFloat("_Smoothness", 0.5f);
            character.EnableKeyword("_EMISSION");
            character.SetColor("_EmissionColor", new Color(1f, 1f, 1f));
            var baseCase = new Material(lit); // 底座：深灰黑金属光泽 + 亮边反射
            baseCase.SetColor("_BaseColor", new Color(0.055f, 0.06f, 0.066f));
            baseCase.SetFloat("_Metallic", 0.65f);
            baseCase.SetFloat("_Smoothness", 0.8f);

            int nCap = 0, nChar = 0, nBase = 0;
            var sample = new System.Text.StringBuilder();
            foreach (var r in clone.GetComponentsInChildren<MeshRenderer>())
            {
                // 按材质名分类（Blender 端已确定性赋值：GlossKeycap / GlossChar / GlossBase），
                // 不再依赖网格名——之前网格名分类出现键帽/字符颠倒
                var matName = r.sharedMaterial != null ? r.sharedMaterial.name : "";
                if (matName.Contains("Keycap")) { r.sharedMaterial = keycap; nCap++; }
                else if (matName.Contains("Char"))
                {
                    r.sharedMaterial = character; nChar++;
                    // 字符不收发阴影：平面贴着键帽面，收阴影会被 shadow map 精度抖动打碎
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
                else { r.sharedMaterial = baseCase; nBase++; }
                if (sample.Length < 600) sample.AppendLine($"  {r.name}  [{matName}]");
            }
            Debug.Log($"[对比键盘] 材质：键帽 {nCap} / 字符 {nChar} / 底座 {nBase}\n前几项：\n{sample}");

            Undo.RegisterCreatedObjectUndo(clone, "Add Comparison Keyboard");
            EditorSceneManager.MarkSceneDirty(clone.scene);
            Selection.activeGameObject = clone;
            Debug.Log($"[对比键盘] 已创建 {clone.name}（键帽 {nCap} / 字符 {nChar} / 底座 {nBase}），位于原键盘右侧 21 单位。可整体移动/缩放调整位置。");
        }
    }
}
