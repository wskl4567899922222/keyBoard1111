using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;

namespace Keyboard3D
{
    public static class MiniKeyboardDemoMenu
    {
        [MenuItem("Tools/3D 键盘/创建六键演示键盘 (视频复刻)")]
        public static void CreateDemo()
        {
            // ── 地面（浅灰平面，接收接触阴影）──
            var floor = GameObject.Find("MiniKeyboardFloor");
            if (floor == null)
            {
                floor = new GameObject("MiniKeyboardFloor");
                floor.transform.position = Vector3.zero;
                floor.transform.localScale = new Vector3(12f, 1f, 12f);
                var mf = floor.AddComponent<MeshFilter>();
                mf.sharedMesh = Resources.GetBuiltinResource<Mesh>("New-Plane.fbx");
                var lit = Shader.Find("Universal Render Pipeline/Lit");
                if (lit == null) lit = Shader.Find("Standard");
                var mat = new Material(lit);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.76f, 0.77f, 0.79f));
                else if (mat.HasProperty("_Color")) mat.SetColor("_Color", new Color(0.76f, 0.77f, 0.79f));
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.35f);
                floor.AddComponent<MeshRenderer>().sharedMaterial = mat;
                Undo.RegisterCreatedObjectUndo(floor, "Create MiniKeyboard Floor");
            }

            // ── 方向光（柔和主光 + 软阴影出接触阴影）──
            Light dir = null;
            foreach (var l in Object.FindObjectsOfType<Light>())
                if (l.type == LightType.Directional) { dir = l; break; }
            if (dir == null)
            {
                var lightGo = new GameObject("MiniKeyboard Directional Light");
                lightGo.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
                dir = lightGo.AddComponent<Light>();
                dir.type = LightType.Directional;
                dir.shadows = LightShadows.Soft;
                Undo.RegisterCreatedObjectUndo(lightGo, "Create MiniKeyboard Light");
            }
            dir.intensity = 1.1f;

            // ── 相机（斜上方俯视：能看到顶面、正面侧壁和右侧侧壁）──
            var cam = Camera.main;
            GameObject camGo;
            if (cam == null)
            {
                camGo = new GameObject("MiniKeyboard Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
                Undo.RegisterCreatedObjectUndo(camGo, "Create MiniKeyboard Camera");
            }
            else camGo = cam.gameObject;
            camGo.transform.position = new Vector3(1.7f, 2.2f, 2.3f);
            camGo.transform.LookAt(new Vector3(0f, 0.05f, 0f));
            cam.allowHDR = true;
            var extra = cam.GetComponent<UniversalAdditionalCameraData>();
            if (extra == null) extra = camGo.AddComponent<UniversalAdditionalCameraData>();
            extra.renderPostProcessing = true;

            // ── 全局 Volume：Bloom（辉光）+ ACES 色调映射 ──
            var vol = Object.FindObjectOfType<Volume>();
            if (vol == null)
            {
                var volGo = new GameObject("MiniKeyboardGlobalVolume");
                vol = volGo.AddComponent<Volume>();
                vol.isGlobal = true;
                vol.profile = ScriptableObject.CreateInstance<VolumeProfile>();
                Undo.RegisterCreatedObjectUndo(volGo, "Create MiniKeyboard Volume");
            }
            var profile = vol.profile;
            Bloom bloom = null;
            Tonemapping tm = null;
            foreach (var c in profile.components)
            {
                if (c is Bloom bl) bloom = bl;
                else if (c is Tonemapping t2) tm = t2;
            }
            if (bloom == null) bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.45f);
            if (tm == null) tm = profile.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.ACES);

            // ── 环境光（浅灰摄影棚）──
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.60f, 0.61f, 0.63f);

            // ── 键盘本体（几何体在 Play 时程序化生成）──
            MiniKeyboard6Key kb = null;
            foreach (var k in Object.FindObjectsOfType<MiniKeyboard6Key>()) { kb = k; break; }
            if (kb == null)
            {
                var kbGo = new GameObject("MiniKeyboard6Key");
                kb = kbGo.AddComponent<MiniKeyboard6Key>();
                Undo.RegisterCreatedObjectUndo(kbGo, "Create MiniKeyboard");
            }
            Selection.activeGameObject = kb.gameObject;
            Debug.Log("[MiniKeyboard] 演示场景已就绪。按 Play 后：鼠标左键按住绿色 Main 键（或按 A 键）测试下沉+发光+粒子。", kb);
        }
    }
}
