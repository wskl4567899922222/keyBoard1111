using UnityEditor;
using UnityEngine;

namespace ByteTyping
{
    public static class IntroAnimationMenu
    {
        [MenuItem("Tools/键盘入场动画/添加到场景手部键盘")]
        public static void AddIntro()
        {
            var hands = Object.FindObjectOfType<TypingHands>();
            if (hands == null)
            {
                EditorUtility.DisplayDialog("键盘入场动画", "场景中未找到 TypingHands 组件，请先打开 Test 场景。", "好的");
                return;
            }
            var go = hands.gameObject;
            if (go.GetComponent<IntroAnimation>() != null)
            {
                Debug.Log("[入场动画] " + go.name + " 已有 IntroAnimation 组件。", go);
            }
            else
            {
                Undo.AddComponent<IntroAnimation>(go);
                Debug.Log($"[入场动画] 已为 {go.name} 添加 IntroAnimation：键盘从下方 2m 升起(1.1s) → 到位后手部淡入浮起(0.7s)。参数可在 Inspector 调。", go);
            }
            Selection.activeGameObject = go;
        }

        [MenuItem("Tools/键盘入场动画/从场景键盘移除")]
        public static void RemoveIntro()
        {
            var intro = Object.FindObjectOfType<IntroAnimation>();
            if (intro == null) { Debug.Log("[入场动画] 场景中没有 IntroAnimation。"); return; }
            Undo.DestroyObjectImmediate(intro);
            Debug.Log("[入场动画] 已移除。");
        }

        // 低 FOV 远距拍摄：透视畸变随 FOV 减小而趋近于零，边缘键（如左下 Ctrl）
        // 不再被拉宽变形。沿原视轴后退，保持键盘中心在画面中的位置与大小不变。
        // 幂等：FOV 已是目标值时重复点击不会再移动。
        [MenuItem("Tools/键盘入场动画/校准相机透视(拉远减小畸变)")]
        public static void FixCameraDistortion()
        {
            var cam = Camera.main;
            if (cam == null) { EditorUtility.DisplayDialog("相机校准", "场景中没有 MainCamera（Tag=MainCamera）。", "好的"); return; }
            var hands = Object.FindObjectOfType<TypingHands>();
            Vector3 center = hands != null ? hands.transform.position : new Vector3(0f, 0f, 1.7f); // 键盘根位置
            const float newFov = 28f;

            var t = cam.transform;
            float dist = Vector3.Dot(center - t.position, t.forward); // 当前沿视轴的相机-键盘距离
            if (dist < 0.05f) dist = 2f;
            // 相似三角形：FOV 变小 → 按同比例把视轴距离拉远，画面构图不变
            float ratio = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) /
                          Mathf.Tan(newFov * 0.5f * Mathf.Deg2Rad);
            float newDist = dist * ratio;
            Undo.RecordObject(t, "Calibrate Camera Perspective");
            t.position = center - t.forward * newDist; // 朝向不动，只沿视轴后退
            cam.fieldOfView = newFov;
            EditorUtility.SetDirty(cam);
            Debug.Log($"[相机校准] FOV {cam.fieldOfView:F1}°，视轴距离 {newDist:F2}m" +
                      $"（原 {dist:F2}m）。键盘边缘透视变形显著减小；如还想更平直可再手动降 FOV 至 20° 左右。", cam);
        }
    }
}
