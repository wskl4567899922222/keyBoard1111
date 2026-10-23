using UnityEditor;
using UnityEngine;

namespace Keyboard3D
{
    public static class KeyboardModelInputMenu
    {
        [MenuItem("Tools/3D 键盘/为模型添加按键输入 (选中 Keyboard68)")]
        public static void AddInput()
        {
            var go = Selection.activeGameObject;
            if (go == null)
            {
                // 尝试在场景里自动找 Keyboard68 实例
                foreach (var tr in Object.FindObjectsOfType<Transform>())
                {
                    if (tr.parent == null && tr.name.ToLower().Contains("keyboard"))
                    {
                        go = tr.gameObject;
                        break;
                    }
                }
            }
            if (go == null)
            {
                EditorUtility.DisplayDialog("模型键盘", "请先在场景中选中 Keyboard68 模型实例，再执行本菜单。", "好的");
                return;
            }
            if (go.GetComponent<KeyboardModelInput>() == null)
            {
                Undo.AddComponent<KeyboardModelInput>(go);
                Debug.Log($"[模型键盘] 已为 {go.name} 添加 KeyboardModelInput，进 Play 即可测试按键下沉。", go);
            }
            else
            {
                Debug.Log("[模型键盘] 该物体已有 KeyboardModelInput。");
            }
            Selection.activeGameObject = go;
        }
    }
}
