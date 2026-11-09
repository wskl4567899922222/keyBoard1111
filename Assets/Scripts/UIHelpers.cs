using UnityEngine;
using UnityEngine.UI;

namespace ByteTyping
{
    /// <summary>程序化 UGUI 构建的小工具</summary>
    internal static class UI
    {
        public static Font DefaultFont
        {
            get
            {
                Font f = null;
                try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
                if (f == null) { try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
                return f;
            }
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return rt;
        }

        /// <summary>四边拉伸铺满父节点</summary>
        public static RectTransform Stretch(Transform parent, string name)
        {
            var rt = Rect(parent, name, Vector2.zero, Vector2.zero);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Img(Transform parent, string name, Color color, Vector2 size, Vector2 pos)
        {
            var img = Rect(parent, name, size, pos).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Text Txt(Transform parent, string name, string content, int fontSize, Color color,
            Vector2 size, Vector2 pos, TextAnchor anchor = TextAnchor.MiddleCenter,
            FontStyle style = FontStyle.Bold)
        {
            var t = Rect(parent, name, size, pos).gameObject.AddComponent<Text>();
            t.font = DefaultFont;
            t.text = content;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button Btn(Transform parent, string label, Vector2 size, Vector2 pos,
            System.Action onClick, int fontSize = 22)
        {
            var img = Img(parent, "Btn_" + label, new Color(1f, 1f, 1f, 0.08f), size, pos);
            img.raycastTarget = true;
            var btn = img.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());
            var t = Txt(img.transform, "label", label, fontSize, new Color(0.85f, 0.85f, 0.9f),
                size - new Vector2(8, 8), Vector2.zero);
            return btn;
        }
    }
}
