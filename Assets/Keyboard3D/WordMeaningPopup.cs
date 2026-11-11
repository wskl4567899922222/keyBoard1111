using UnityEngine;

namespace Keyboard3D
{
    /// <summary>
    /// 完词脉冲时弹出的中文释义（世界空间 TextMesh，挂在单词 root 下）：
    /// 创建时 scale=0 隐藏，PopUp() 用回弹动画（0 → 过冲 → 1）弹出中文意思；
    /// 挂在单词 root 上 → 自动跟随单词缩放/倾斜/屏幕锚定，完词脉冲放大时一起放大；
    /// 切词时随旧词 root 销毁，无需手动清理（2026-11-10）。
    /// 中文用 SmileySans 动态字体（Resources/Fonts/SmileySans，字形运行时光栅化，
    /// 在线释义字符集不固定 → 不预裁子集）。
    /// </summary>
    public sealed class WordMeaningPopup : MonoBehaviour
    {
        const float PopDuration = 0.35f; // 弹出总时长（秒）
        const float Overshoot = 1.18f;   // 回弹过冲倍数（弹出感）
        const int RasterFontSize = 160;  // 光栅字号：中文笔画多，比音标(自适应≤256)取一档足够清晰的高分辨率
        float popT = -1f;                // 弹出动画进度（-1 未弹出；本次完词只弹一次）

        /// <summary>创建释义弹出层（parent=单词 root；charH=文字世界空间字高；初始 scale=0 隐藏）。</summary>
        public static WordMeaningPopup Create(Transform parent, string text, float charH, Color color)
        {
            var go = new GameObject("WordMeaningPopup");
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.zero; // 隐藏，等 PopUp()
            var popup = go.AddComponent<WordMeaningPopup>();

            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            var font = Resources.Load<Font>("Fonts/SmileySans"); // 全量动态字体：任意中文运行时渲染
            tm.font = font != null ? font : WordPhonicsOverlay.BuiltinFont();
            tm.fontSize = RasterFontSize;
            tm.characterSize = charH / (RasterFontSize * 0.1f); // 字高 ≈ fontSize×0.1×characterSize = charH
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = tm.font.material;
            return popup;
        }

        /// <summary>完词脉冲开始 → 回弹弹出（重复调用无效）。</summary>
        public void PopUp()
        {
            if (popT >= 0f) return;
            popT = 0f;
        }

        void Update()
        {
            if (popT < 0f || popT >= 1f) return;
            popT = Mathf.Min(1f, popT + Time.deltaTime / PopDuration);
            // 0 → 过冲 → 1：前 70% 时间平滑冲到 Overshoot，后 30% 回落到 1（回弹观感）
            float s = popT < 0.7f
                ? Mathf.Lerp(0f, Overshoot, Mathf.SmoothStep(0f, 1f, popT / 0.7f))
                : Mathf.Lerp(Overshoot, 1f, (popT - 0.7f) / 0.3f);
            transform.localScale = Vector3.one * s;
        }
    }
}
