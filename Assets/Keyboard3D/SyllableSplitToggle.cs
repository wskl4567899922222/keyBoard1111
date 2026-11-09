using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Keyboard3D
{
    /// <summary>
    /// 拼读模式按钮（2026-11-06 重构：把原来一个按钮来回切换的逻辑拆成两个独立按钮）：
    /// - 挂在"单词拆分"按钮上 → 点击启用音节拆分模式（下划线按音节分段 + 读英式拆分音频）；
    /// - 挂在"自然拼读"按钮上 → 点击恢复自然拼读模式（拼读块分段 + 读音标音频）。
    /// 两种模式都发音（发音总开关仍是 PhonicsSoundToggle 按钮）。
    /// 职责按按钮文本自动判定：文本含"拆分" = 拆分按钮，否则视为自然拼读按钮；
    /// 当前模式的按钮文本高亮为绿色，非当前模式为灰色。场景里两个按钮各挂一份本组件即可。
    /// </summary>
    public class SyllableSplitToggle : MonoBehaviour
    {
        [Tooltip("要控制的按钮（留空 = 当前物体上的 Button）")]
        public Button target;

        [Tooltip("按钮文本（留空 = 子物体里的 TMP_Text 或 Text）")]
        public Component label;

        bool syllableMode;        // 本按钮代表的模式：true=单词拆分，false=自然拼读（按按钮文本判定）
        Color inactiveColor = new Color(0.196f, 0.196f, 0.196f, 1f); // 未激活文本色（按钮原色）
        static readonly Color activeColor = new Color(0.1f, 0.65f, 0.25f, 1f); // 当前模式按钮高亮绿

        static readonly List<SyllableSplitToggle> all = new List<SyllableSplitToggle>();

        void Awake()
        {
            if (target == null) target = GetComponent<Button>();
            // label 被拖成 RectTransform 等非文本组件时自动重新解析
            if (!(label is TMP_Text) && !(label is Text))
            {
                label = GetComponentInChildren<TMP_Text>();
                if (label == null) label = GetComponentInChildren<Text>();
            }
            // 记住按钮原文本色（高亮切换用），并按文本判定本按钮代表的模式
            var tmp = label as TMP_Text;
            var legacy = label as Text;
           
            string txt = tmp != null ? tmp.text : legacy != null ? legacy.text : "";
            syllableMode = txt.Contains("拆分"); // "单词拆分"=true；"自然拼读"等=false

            all.Add(this);
            Refresh();
            if (target != null) target.onClick.AddListener(OnClick);
        }

        void OnDestroy()
        {
            all.Remove(this);
            if (target != null) target.onClick.RemoveListener(OnClick);
        }

        void OnEnable() => WordTrailTyping.PhonicsStateSync += Refresh;      // 切词后重绘高亮（状态保持）
        void OnDisable() => WordTrailTyping.PhonicsStateSync -= Refresh;

        // 点击：启用本按钮对应的模式（互斥），并立即重建当前单词的叠加层
        void OnClick()
        {
            var t = WordTrailTyping.Instance;
            if (t != null)
            {
                t.SyllableSplitEnabled = syllableMode;
                t.RebuildPhonicsOverlay();
            }
            Refresh();
        }

        // 高亮当前模式的按钮：激活=绿色，非激活=按钮原色
        void Refresh()
        {
            var t = WordTrailTyping.Instance;
            bool splitOn = t != null ? t.SyllableSplitEnabled : false; // 默认自然拼读模式
            bool active = splitOn == syllableMode;
  
        }
    }
}
