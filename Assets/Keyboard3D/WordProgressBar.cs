using UnityEngine;
using UnityEngine.UI;

namespace Keyboard3D
{
    /// <summary>
    /// 单词进度条（运行时装配，无场景依赖，由 WordTrailTyping.Create）：
    /// 屏幕顶部居中一条连续填充条，显示"已打完单词数 / 总单词数"（2026-11-05 改），
    /// 每打完一个单词前进一步；循环词库新一轮从头填充。只显示填充条，不显示比例数字。
    /// </summary>
    public sealed class WordProgressBar : MonoBehaviour
    {
        RectTransform fillRect;
        Image fillImage;

        public static WordProgressBar Create()
        {
            var canvasGo = new GameObject("WordProgressCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500; // 独立 Canvas：不依赖场景里已有的 UI 层级
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var root = new GameObject("Bar", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(canvasGo.transform, false);
            root.anchorMin = new Vector2(0.5f, 1f);
            root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = new Vector2(0f, -32f); // 顶部居中，留出安全边距
            root.sizeDelta = new Vector2(720f, 16f);

            CreateImage(root, "Background", new Color(1f, 1f, 1f, 0.16f));

            var fillGo = new GameObject("Fill", typeof(Image));
            fillGo.transform.SetParent(root, false);
            var fillImage = fillGo.GetComponent<Image>();
            fillImage.color = Color.white;
            var fillRect = fillImage.rectTransform;
            fillRect.anchorMin = Vector2.zero;                  // 填充宽度 = anchorMax.x × 条宽
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            var bar = canvasGo.AddComponent<WordProgressBar>();
            bar.fillImage = fillImage;
            bar.fillRect = fillRect;
            return bar;
        }

        static void CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>fraction = 当前单词的字母拼写进度（0~1）；fillColor = 填充色（固定绿色）。</summary>
        public void SetProgress(float fraction, Color fillColor)
        {
            if (fillRect == null) return;
            fillRect.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            if (fillImage != null) fillImage.color = fillColor;
        }

        /// <summary>清零进度（切到新单词时调用）。</summary>
        public void ResetBar()
        {
            if (fillRect != null) fillRect.anchorMax = new Vector2(0f, 1f);
        }
    }
}
