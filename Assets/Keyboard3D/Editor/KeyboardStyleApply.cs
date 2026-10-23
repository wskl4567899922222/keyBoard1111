using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Keyboard3D;

namespace Keyboard3D.EditorTools
{
    /// <summary>
    /// 把"游戏键盘"（原始 Keyboard68.fbx 实例）升级为参考图样式：
    /// 换成 Keyboard68_Gloss.fbx（统一拔模角+侧棱圆角+顶肩倒边）+ 全套调优参数。
    /// 菜单：Tools/3D 键盘/应用参考样式到游戏键盘（Gloss 模型+调优参数）
    ///
    /// 用法：菜单执行后保存场景（Ctrl+S）再 Play。
    /// 说明：KeyboardModelInput 的键位布局/相机/灯光/粒子全部由运行时代码构建，
    /// 组件上没有需要保留的场景数据，因此直接"新模型实例 + 新组件"即可无损替换。
    /// </summary>
    public static class KeyboardStyleApply
    {
        [MenuItem("Tools/3D 键盘/应用参考样式到游戏键盘（Gloss 模型+调优参数）")]
        public static void ApplyToGameKeyboard()
        {
            // 找游戏键盘：名称不带 _Gloss 的 KeyboardModelInput 实例（排除对比键盘）
            KeyboardModelInput src = null;
            foreach (var mi in Object.FindObjectsOfType<KeyboardModelInput>())
            {
                if (mi.gameObject.name.EndsWith("_Gloss")) continue;
                src = mi;
                break;
            }
            if (src == null)
            {
                Debug.LogError("[应用样式] 场景里没找到游戏键盘（KeyboardModelInput 且名称不带 _Gloss）");
                return;
            }

            var root = src.gameObject;
            var parent = root.transform.parent;
            var localPos = root.transform.localPosition;
            var localRot = root.transform.localRotation;
            var localScale = root.transform.localScale;
            var sibling = root.transform.GetSiblingIndex();
            var scene = root.scene;
            var rootName = root.name;

            const string GlossModelPath = "Assets/Models/Keyboard68_Gloss.fbx";
            var gloss = AssetDatabase.LoadAssetAtPath<GameObject>(GlossModelPath);
            if (gloss == null)
            {
                Debug.LogError("[应用样式] 找不到 " + GlossModelPath + "（模型还没生成/导入）");
                return;
            }

            // 重复运行时先删上一次的结果
            var existing = GameObject.Find(rootName + "_GlossGame");
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
                Debug.Log("[应用样式] 已删除上次的结果，重建中……");
            }

            var clone = (GameObject)PrefabUtility.InstantiatePrefab(gloss, scene);
            clone.name = rootName + "_GlossGame";
            clone.transform.SetParent(parent, false);
            clone.transform.localPosition = localPos;
            clone.transform.localRotation = localRot;
            clone.transform.localScale = localScale;
            clone.transform.SetSiblingIndex(sibling);

            // 新组件 + 参考图调优参数（与 KeyboardStyleComparison 一致）
            var fx = clone.AddComponent<KeyboardModelInput>();
            ApplyTuned(fx);

            // 移除旧游戏键盘
            Undo.DestroyObjectImmediate(root);
            Selection.activeGameObject = clone;
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[应用样式] 游戏键盘已换成 Gloss 模型 + 参考图调优参数，旧键盘已删除。" +
                      "请保存场景(Ctrl+S)后 Play。");
        }

        /// <summary>参考图调优参数（与 KeyboardStyleComparison 保持一致，2026-10-19 定稿值）</summary>
        static void ApplyTuned(KeyboardModelInput fx)
        {
            fx.enableOrbit = true;          // 游戏键盘保留拖拽旋转/缩放
            // 侧壁：深色渐变（顶缘绿线、快速沉黑）
            fx.sideAlbedoDark = 0.15f;
            fx.keycapSmoothness = 0.62f;    // 半哑光硬塑料：有一定反光但柔和
            fx.sideSmoothnessMask = 0.62f;  // 侧壁柔光
            fx.capTopNormalMinY = 0.70f;
            fx.sideGlowTop = 0.55f;
            fx.sideGlowBottom = 0.05f;
            fx.sideGlowCurve = 2.0f;
            // 顶面：暗翡翠 + 中心光斑
            fx.topGlowFalloff = 0.8f;
            fx.idleGlowTightness = 0.45f;   // 平顶化+全椭圆UV：端部达中心 93%，配合宽键自发光增强成均匀长条
            fx.idleTopScale = 0.25f;        // 初始顶面发光亮度 25%：玉绿不炸
            fx.glowAlbedoLerp = 0.42f;
            fx.glowIntensity = 1.5f;
            // 按下：顶面均匀亮绿 + 中心光斑，侧壁压暗
            fx.pressGlowCore = 0.5f;
            fx.pressGlowCoreSize = 0.22f;
            fx.pressGlowBoost = 0.3f;
            fx.pressGlowEdgeKeep = 0.45f;
            fx.sideGlowPressDim = 0.55f;
            // 灯光：顶灯按压驱动，初始态顶灯熄灭
            fx.lightOnPressOnly = true;
            fx.lightPressBase = 0.4f;
            fx.lightPressPeak = 1.0f;
            fx.haloPressPeak = 0.3f;
            fx.haloSideIntensity = 0f;      // 用户要求去掉两盏侧灯，亮度由自发光承担
            // 补光（双高光）
            fx.enableFillLight = true;
            fx.fillLightIntensity = 0.45f;
            fx.fillLightAngles = new Vector3(35f, 55f, 0f);
            // 邻键溢光
            fx.enableSpillLight = true;
            fx.spillLightIntensity = 1.6f;
        }
    }
}
