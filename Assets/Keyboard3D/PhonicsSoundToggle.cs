using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Keyboard3D
{
    /// <summary>
    /// 拼读声音开关按钮：玩家点击切换"自然拼读/单词发音"的开关（默认开启），
    /// 按钮文字随状态切换（"拼读声音：开" / "拼读声音：关"）。
    /// 挂在 UI 按钮物体上（含 Button 组件 + TMP_Text 或 Text 子文本，如场景里的 OpenButton）。
    /// </summary>
    public class PhonicsSoundToggle : MonoBehaviour
    {
        [Tooltip("要控制的按钮（留空 = 当前物体上的 Button）")]
        public Button target;

        [Tooltip("按钮文本（留空 = 子物体里的 TMP_Text 或 Text）")]
        public Component label;

        [Tooltip("开启时显示的文字")]
        public string onText = "拼读声音：开";

        [Tooltip("关闭时显示的文字")]
        public string offText = "拼读声音：关";

        bool on = true; // 默认开启：玩家能听到自然拼读音频（切换单词时保持此状态不变）

        void Awake()
        {
            Refresh();
            if (target == null) target = GetComponent<Button>();
            if (target == null)
            {
                Debug.LogError($"[PhonicsSoundToggle] {name} 上找不到 Button，开关停用。", this);
                enabled = false;
                return;
            }
            // label 被拖成 RectTransform 等非文本组件时自动重新解析（与 SyllableSplitToggle 同款兜底）
            if (!(label is TMP_Text) && !(label is Text))
            {
                label = GetComponentInChildren<TMP_Text>();
                if (label == null) label = GetComponentInChildren<Text>();
            }
            target.onClick.AddListener(Toggle);

        }

        void OnEnable()
        {
            WordTrailTyping.PhonicsStateSync += OnPhonicsStateSync; // 切换单词 → 按按钮当前状态重绘（状态不丢）
        }

        void OnDestroy()
        {
            WordTrailTyping.PhonicsStateSync -= OnPhonicsStateSync;
            if (target != null) target.onClick.RemoveListener(Toggle);
        }

        // 切换单词后的同步回调：按钮 on 字段是唯一事实源——重绘文字并把状态回写
        // 给 WordTrailTyping，即使中途有其它代码动过 PhonicsEnabled 也会被纠正回来
        void OnPhonicsStateSync() => Refresh();

        // 按钮点击：切换开关并刷新文字
        public void Toggle()
        {
            on = !on;
            Refresh();
        }

        void Refresh()
        {
            if (WordTrailTyping.Instance != null)
                WordTrailTyping.Instance.PhonicsEnabled = on;
            if (label is TMP_Text tmp) tmp.text = on ? onText : offText;
            else if (label is Text t) t.text = on ? onText : offText;
        }
    }
}
