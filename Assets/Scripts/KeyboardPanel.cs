using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ByteTyping
{
    /// <summary>
    /// 程序化 UGUI 键盘（行布局与键宽取自网站前端 module 2956）。
    /// 功能：目标键紫色高亮 + Shift 配合键高亮 + 按键按下闪粉（#DB73EB）。
    /// </summary>
    public class KeyboardPanel : MonoBehaviour
    {
        private struct KeyDef
        {
            public string code, label;
            public float w;
            public KeyDef(string code, string label, float w) { this.code = code; this.label = label; this.w = w; }
        }

        // 普通键宽 48，宽键 56/64/80/96，空格 360；行内间距 6
        private static readonly KeyDef[][] Rows =
        {
            new[]
            {
                new KeyDef("Escape", "esc", 56),
                new KeyDef("Digit1", "1", 48), new KeyDef("Digit2", "2", 48), new KeyDef("Digit3", "3", 48),
                new KeyDef("Digit4", "4", 48), new KeyDef("Digit5", "5", 48), new KeyDef("Digit6", "6", 48),
                new KeyDef("Digit7", "7", 48), new KeyDef("Digit8", "8", 48), new KeyDef("Digit9", "9", 48),
                new KeyDef("Digit0", "0", 48),
                new KeyDef("Minus", "-", 48), new KeyDef("Equal", "=", 48),
                new KeyDef("Backspace", "backspace", 80),
            },
            new[]
            {
                new KeyDef("Tab", "tab", 56),
                new KeyDef("KeyQ", "Q", 48), new KeyDef("KeyW", "W", 48), new KeyDef("KeyE", "E", 48),
                new KeyDef("KeyR", "R", 48), new KeyDef("KeyT", "T", 48), new KeyDef("KeyY", "Y", 48),
                new KeyDef("KeyU", "U", 48), new KeyDef("KeyI", "I", 48), new KeyDef("KeyO", "O", 48),
                new KeyDef("KeyP", "P", 48),
                new KeyDef("BracketLeft", "[", 48), new KeyDef("BracketRight", "]", 48),
                new KeyDef("Backslash", "\\", 64),
            },
            new[]
            {
                new KeyDef("CapsLock", "caps", 64),
                new KeyDef("KeyA", "A", 48), new KeyDef("KeyS", "S", 48), new KeyDef("KeyD", "D", 48),
                new KeyDef("KeyF", "F", 48), new KeyDef("KeyG", "G", 48), new KeyDef("KeyH", "H", 48),
                new KeyDef("KeyJ", "J", 48), new KeyDef("KeyK", "K", 48), new KeyDef("KeyL", "L", 48),
                new KeyDef("Semicolon", ";", 48), new KeyDef("Quote", "'", 48),
                new KeyDef("Enter", "return", 80),
            },
            new[]
            {
                new KeyDef("ShiftLeft", "shift", 80),
                new KeyDef("KeyZ", "Z", 48), new KeyDef("KeyX", "X", 48), new KeyDef("KeyC", "C", 48),
                new KeyDef("KeyV", "V", 48), new KeyDef("KeyB", "B", 48), new KeyDef("KeyN", "N", 48),
                new KeyDef("KeyM", "M", 48),
                new KeyDef("Comma", ",", 48), new KeyDef("Period", ".", 48), new KeyDef("Slash", "/", 48),
                new KeyDef("ShiftRight", "shift", 96),
            },
            new[]
            {
                new KeyDef("ControlLeft", "ctrl", 56), new KeyDef("AltLeft", "alt", 56),
                new KeyDef("MetaLeft", "cmd", 56), new KeyDef("Space", "", 360),
                new KeyDef("MetaRight", "cmd", 56), new KeyDef("AltRight", "alt", 56),
            },
        };

        private class KeyView
        {
            public KeyCode kc;
            public bool mapped;
            public Image bg;
            public Outline outline;
            public Color baseColor; // 常态色（含目标/Shift 高亮态）
            public Coroutine flash;
        }

        private const float KeyH = 48f, Gap = 6f;
        private static readonly Color NormalBg = new Color32(0x2E, 0x2E, 0x38, 0xFF);
        private static readonly Color TargetBg = new Color32(0x6D, 0x28, 0xD9, 0xFF);
        private static readonly Color ShiftBg = new Color32(0x4C, 0x1D, 0x95, 0xFF);
        private static readonly Color TargetEdge = new Color32(0xDB, 0x73, 0xEB, 0xFF);
        private static readonly Color ShiftEdge = new Color32(0xA8, 0x55, 0xF7, 0xFF);
        private static readonly Color LabelColor = new Color32(0xC9, 0xC9, 0xD4, 0xFF);

        private readonly Dictionary<string, KeyView> _byCode = new Dictionary<string, KeyView>();
        private readonly List<KeyView> _keys = new List<KeyView>();

        /// <summary>创建键盘；pos 为键盘几何中心</summary>
        public static KeyboardPanel Create(Transform parent, Vector2 pos)
        {
            var rt = UI.Rect(parent, "KeyboardPanel", Vector2.zero, pos);
            var p = rt.gameObject.AddComponent<KeyboardPanel>();
            p.Build();
            return p;
        }

        private void Build()
        {
            const float halfH = 132f; // (5*48 + 4*6) / 2
            for (int r = 0; r < Rows.Length; r++)
            {
                float rowW = Gap * (Rows[r].Length - 1);
                foreach (var k in Rows[r]) rowW += k.w;
                float yc = halfH - r * (KeyH + Gap) - KeyH * 0.5f;
                float x = -rowW * 0.5f;
                foreach (var def in Rows[r])
                {
                    BuildKey(def, x + def.w * 0.5f, yc);
                    x += def.w + Gap;
                }
            }
        }

        private void BuildKey(KeyDef def, float cx, float cy)
        {
            var rt = UI.Rect(transform, "Key_" + def.code, new Vector2(def.w, KeyH), new Vector2(cx, cy));
            var view = new KeyView { baseColor = NormalBg };
            view.bg = rt.gameObject.AddComponent<Image>();
            view.bg.color = NormalBg;
            view.bg.raycastTarget = false;
            view.outline = rt.gameObject.AddComponent<Outline>();
            view.outline.effectDistance = new Vector2(2, -2);
            view.outline.enabled = false;

            UI.Txt(rt, "label", def.label, def.label.Length > 3 ? 13 : 16, LabelColor,
                new Vector2(def.w - 6, KeyH - 6), Vector2.zero);

            if (FingerMap.TryGetKeyCode(def.code, out var kc)) { view.kc = kc; view.mapped = true; }
            _byCode[def.code] = view;
            _keys.Add(view);
        }

        /// <summary>设置当前目标键与配合的 Shift 键；传空串清除</summary>
        public void SetTarget(string keyCode, string shiftKeyCode)
        {
            foreach (var v in _keys)
            {
                v.outline.enabled = false;
                v.baseColor = NormalBg;
            }
            var k = Get(keyCode);
            if (k != null)
            {
                k.baseColor = TargetBg;
                k.outline.effectColor = TargetEdge;
                k.outline.enabled = true;
            }
            var s = Get(shiftKeyCode);
            if (s != null)
            {
                s.baseColor = ShiftBg;
                s.outline.effectColor = ShiftEdge;
                s.outline.enabled = true;
            }
            foreach (var v in _keys)
                if (v.flash == null) v.bg.color = v.baseColor; // 闪烁中的键由协程恢复
        }

        private KeyView Get(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            return _byCode.TryGetValue(code, out var v) ? v : null;
        }

        /// <summary>轮询本帧按下的按键并播放按下闪烁</summary>
        public void PollPresses()
        {
            foreach (var v in _keys)
                if (v.mapped && Input.GetKeyDown(v.kc))
                    FlashPress(v);
        }

        public void FlashPress(KeyCode kc)
        {
            foreach (var v in _keys)
                if (v.mapped && v.kc == kc) { FlashPress(v); return; }
        }

        private void FlashPress(KeyView v)
        {
            if (v.flash != null) StopCoroutine(v.flash);
            v.flash = StartCoroutine(Flash(v));
        }

        private IEnumerator Flash(KeyView v)
        {
            v.bg.color = TargetEdge; // 按下闪粉 #DB73EB
            yield return new WaitForSeconds(0.12f);
            v.flash = null;
            v.bg.color = v.baseColor;
        }
    }
}
