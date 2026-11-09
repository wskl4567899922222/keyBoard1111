using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ByteTyping
{
    /// <summary>
    /// 双手手指高亮叠加：多张同画布透明 PNG 依次叠加，还原网站「按键亮起 + 手指高亮」效果。
    /// 图层精灵位于 Resources/Art/Hands：hands_base（基础双手）+ hl_*（各手指/Shift 高亮层）。
    /// </summary>
    public class HandOverlay : MonoBehaviour
    {
        private const float Aspect = 1880f / 962f; // 图层画布宽高比

        private Image _left, _right, _shift;
        private readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();

        private static readonly string[] LayerNames =
        {
            "hl_L_pinky", "hl_L_ring", "hl_L_middle", "hl_L_index",
            "hl_R_index", "hl_R_middle", "hl_R_ring", "hl_R_pinky", "hl_R_thumb",
            "hl_ShiftL", "hl_ShiftR",
        };

        /// <summary>创建手部叠加图（按高度自适应，保持 1880x962 宽高比）</summary>
        public static HandOverlay Create(Transform parent, float height, Vector2 pos)
        {
            var rt = UI.Rect(parent, "HandOverlay", new Vector2(height * Aspect, height), pos);
            var o = rt.gameObject.AddComponent<HandOverlay>();
            o.Build();
            return o;
        }

        private void Build()
        {
            MakeLayer("base", Resources.Load<Sprite>("Art/Hands/hands_base"));
            _left = MakeLayer("left", null);
            _right = MakeLayer("right", null);
            _shift = MakeLayer("shift", null);

            foreach (var n in LayerNames)
            {
                var s = Resources.Load<Sprite>("Art/Hands/" + n);
                if (s != null) _sprites[n] = s;
            }
            SetHighlight("", "", "");
        }

        private Image MakeLayer(string name, Sprite sprite)
        {
            var img = UI.Stretch(transform, name).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.enabled = sprite != null;
            return img;
        }

        /// <summary>设置高亮图层；空串表示无高亮。shiftKeyCode 为 "ShiftLeft"/"ShiftRight"/""</summary>
        public void SetHighlight(string leftLayer, string rightLayer, string shiftKeyCode = "")
        {
            Apply(_left, leftLayer);
            Apply(_right, rightLayer);
            string shiftLayer = shiftKeyCode == "ShiftLeft" ? "hl_ShiftL"
                              : shiftKeyCode == "ShiftRight" ? "hl_ShiftR" : "";
            Apply(_shift, shiftLayer);
        }

        private void Apply(Image img, string layerName)
        {
            _sprites.TryGetValue(layerName ?? "", out var s);
            img.sprite = s;
            img.enabled = s != null;
        }
    }
}
