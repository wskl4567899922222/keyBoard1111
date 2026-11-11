using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Keyboard3D
{
    /// <summary>
    /// 在线数据拉取失败弹窗（运行时自建，零场景依赖）：
    /// 全屏半透明遮罩（挡点击）+ 居中面板 + 提示文本 + "重试"按钮。
    /// 点重试 → 销毁弹窗并回调（重新拉取）；WebGL 下用户刷新页面等效重试。
    /// </summary>
    public class FetchFailDialog : MonoBehaviour
    {
        [Tooltip("重试回调（销毁弹窗后触发）")]
        public System.Action onRetry;

        /// <summary>创建弹窗。message 为提示文本；onRetry 在用户点击"重试"时触发（弹窗已销毁）。</summary>
        public static FetchFailDialog Create(string message, System.Action onRetry)
        {
            // 独立 Canvas（置顶，高于进度条 500）
            var canvasGo = new GameObject("FetchFailCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // 全屏遮罩：半透明黑，挡住下层所有点击
            var dimGo = new GameObject("Dim", typeof(Image), typeof(Button));
            dimGo.transform.SetParent(canvasGo.transform, false);
            var dimRect = dimGo.GetComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            dimGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            // 居中面板
            var panelGo = new GameObject("Panel", typeof(Image));
            panelGo.transform.SetParent(dimGo.transform, false);
            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(560f, 260f);
            panelGo.GetComponent<Image>().color = new Color(0.14f, 0.14f, 0.17f, 0.98f);

            // 提示文本
            var textGo = new GameObject("Message", typeof(TextMeshProUGUI));
            textGo.transform.SetParent(panelGo.transform, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0.45f);
            textRect.anchorMax = new Vector2(1f, 0.92f);
            textRect.offsetMin = new Vector2(40f, 0f);
            textRect.offsetMax = new Vector2(-40f, 0f);
            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.text = message;
            tmp.fontSize = 34f;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = true; // 旧版 TMP API（textWrappingMode 为 2022+ 才有）

            // 重试按钮
            var btnGo = new GameObject("RetryButton", typeof(Image), typeof(Button));
            btnGo.transform.SetParent(panelGo.transform, false);
            var btnRect = btnGo.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0f);
            btnRect.anchorMax = new Vector2(0.5f, 0f);
            btnRect.pivot = new Vector2(0.5f, 0f);
            btnRect.anchoredPosition = new Vector2(0f, 36f);
            btnRect.sizeDelta = new Vector2(220f, 68f);
            btnGo.GetComponent<Image>().color = new Color(0.24f, 0.55f, 0.35f);
            var btnLabelGo = new GameObject("Label", typeof(TextMeshProUGUI));
            btnLabelGo.transform.SetParent(btnGo.transform, false);
            var labelRect = btnLabelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var label = btnLabelGo.GetComponent<TextMeshProUGUI>();
            label.text = "重 试";
            label.fontSize = 30f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;

            var dialog = canvasGo.AddComponent<FetchFailDialog>();
            dialog.onRetry = onRetry;
            btnGo.GetComponent<Button>().onClick.AddListener(dialog.HandleRetry);
            Object.DontDestroyOnLoad(canvasGo); // 场景可能没有常驻根对象，弹窗自行持有
            return dialog;
        }

        void HandleRetry()
        {
            var cb = onRetry;
            Destroy(gameObject);
            cb?.Invoke();
        }
    }
}
