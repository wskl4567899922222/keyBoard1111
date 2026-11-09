using UnityEngine;
using System.Collections.Generic;

namespace ByteTyping
{
    /// <summary>
    /// 手指-按键映射（源自 typing.byteworld.cn 前端数据）。
    /// Resolve 返回：目标键码、左右手高亮图层、需要配合的 Shift 高亮。
    /// 图层名对应 Assets/Art/Hands 下的 hl_* 精灵。
    /// </summary>
    public static class FingerMap
    {
        public struct Target
        {
            public string keyCode;       // 逻辑键码，如 KeyF / Digit1 / Space
            public string display;       // 键帽显示
            public string leftLayer;     // 左手高亮图层名（无则 ""）
            public string rightLayer;    // 右手高亮图层名（无则 ""）
            public string shiftKeyCode;  // 需配合高亮的 Shift（无则 ""）
        }

        // 左手字母：QAZ=小指 WSX=无名指 EDC=中指 RTFGVB=食指
        // 右手字母：YUHJNM=食指 IK,=中指 OL.=无名指 P;'/=小指
        private static readonly Dictionary<char, (string hand, string finger)> Letters = new()
        {
            {'q',("L","pinky")}, {'a',("L","pinky")}, {'z',("L","pinky")},
            {'w',("L","ring")}, {'s',("L","ring")}, {'x',("L","ring")},
            {'e',("L","middle")}, {'d',("L","middle")}, {'c',("L","middle")},
            {'r',("L","index")}, {'t',("L","index")}, {'f',("L","index")},
            {'g',("L","index")}, {'v',("L","index")}, {'b',("L","index")},
            {'y',("R","index")}, {'u',("R","index")}, {'h',("R","index")},
            {'j',("R","index")}, {'n',("R","index")}, {'m',("R","index")},
            {'i',("R","middle")}, {'k',("R","middle")}, {',',("R","middle")},
            {'o',("R","ring")}, {'l',("R","ring")}, {'.',("R","ring")},
            {'p',("R","pinky")}, {';',("R","pinky")}, {'\'',("R","pinky")}, {'/',("R","pinky")},
        };

        // 数字行：手指归属（与网站上位符号高亮一致）
        private static readonly (string hand, string finger)[] DigitFingers =
        {
            ("L","pinky"), ("L","ring"), ("L","middle"), ("L","index"), ("L","index"), // 1-5
            ("R","index"), ("R","index"), ("R","middle"), ("R","ring"), ("R","pinky"), // 6-0
        };

        // 符号 → (基础键码, 键帽, hand, finger, shiftSide)  shiftSide: "L"/"R"/""
        private static readonly Dictionary<char, (string code, string disp, string hand, string finger, string shift)> Symbols = new()
        {
            {'!',("Digit1","!","L","pinky","R")},
            {'@',("Digit2","@","L","ring","R")},
            {'#',("Digit3","#","L","middle","R")},
            {'$',("Digit4","$","L","index","R")},
            {'%',("Digit5","%","L","index","R")},
            {'^',("Digit6","^","R","index","L")},
            {'&',("Digit7","&","R","index","L")},
            {'*',("Digit8","*","R","middle","L")},
            {'(',("Digit9","(","R","ring","L")},
            {')',("Digit0",")","R","pinky","L")},
            {'-',("Minus","-","R","pinky","")},
            {'_',("Minus","_","R","pinky","L")},
            {'=',("Equal","=","R","pinky","")},
            {'+',("Equal","+","R","pinky","L")},
            {'[',("BracketLeft","[","R","pinky","")},
            {'{',("BracketLeft","{","R","pinky","L")},
            {']',("BracketRight","]","R","pinky","")},
            {'}',("BracketRight","}","R","pinky","L")},
            {'\\',("Backslash","\\","R","pinky","")},
            {'|',("Backslash","|","R","pinky","L")},
            {':',("Semicolon",":","R","pinky","L")},
            {'"',("Quote","\"","R","pinky","L")},
            {'`',("Backquote","`","L","pinky","")},
            {'~',("Backquote","~","L","pinky","R")},
            {';',("Semicolon",";","R","pinky","")},
            {'\'',("Quote","'","R","pinky","")},
            {',',("Comma",",","R","middle","")},
            {'.',("Period",".","R","ring","")},
            {'/',("Slash","/","R","pinky","")},
            {'<',("Comma","<","R","middle","L")},
            {'>',("Period",">","R","ring","L")},
            {'?',("Slash","?","R","pinky","L")},
        };

        /// <summary>是否高亮普通数字键（网站原版不高亮，这里默认高亮更利于练习）</summary>
        public static bool HighlightPlainDigits = true;

        /// <summary>解析任意可打印字符为高亮目标；无法识别返回 keyCode 为空</summary>
        public static bool Resolve(char c, out Target t)
        {
            t = default;
            t.keyCode = ""; t.display = ""; t.leftLayer = ""; t.rightLayer = ""; t.shiftKeyCode = "";

            if (c == ' ')
            {
                t.keyCode = "Space"; t.display = "";
                t.rightLayer = "hl_R_thumb";
                return true;
            }
            if (c == '\t')
            {
                t.keyCode = "Tab"; t.display = "tab";
                t.leftLayer = "hl_L_pinky"; // Tab 由左手小指负责（含 pathTab 高亮）
                return true;
            }
            if (c == '\n' || c == '\r')
            {
                t.keyCode = "Enter"; t.display = "return";
                t.rightLayer = "hl_R_pinky";
                return true;
            }

            char lower = char.ToLowerInvariant(c);
            if (c >= 'a' && c <= 'z')
            {
                var (hand, finger) = Letters[lower];
                t.keyCode = "Key" + char.ToUpperInvariant(c);
                t.display = char.ToUpperInvariant(c).ToString();
                if (hand == "L") t.leftLayer = "hl_L_" + finger;
                else t.rightLayer = "hl_R_" + finger;
                // 大写字母：用对侧手 Shift（网站规则）
                if (char.IsUpper(c))
                    t.shiftKeyCode = hand == "L" ? "ShiftRight" : "ShiftLeft";
                return true;
            }
            if (c >= '0' && c <= '9')
            {
                int d = c - '0';
                t.keyCode = "Digit" + c;
                t.display = c.ToString();
                if (HighlightPlainDigits)
                {
                    var (hand, finger) = DigitFingers[d == 0 ? 9 : d - 1];
                    if (hand == "L") t.leftLayer = "hl_L_" + finger;
                    else t.rightLayer = "hl_R_" + finger;
                }
                return true;
            }
            if (Symbols.TryGetValue(c, out var s))
            {
                t.keyCode = s.code; t.display = s.disp;
                if (s.hand == "L") t.leftLayer = "hl_L_" + s.finger;
                else t.rightLayer = "hl_R_" + s.finger;
                if (s.shift == "L") t.shiftKeyCode = "ShiftLeft";
                else if (s.shift == "R") t.shiftKeyCode = "ShiftRight";
                return true;
            }
            return false;
        }

        /// <summary>把逻辑键码映射为 KeyCode（用于按压动画）</summary>
        public static bool TryGetKeyCode(string code, out KeyCode kc)
        {
            switch (code)
            {
                case "Space": kc = KeyCode.Space; return true;
                case "Tab": kc = KeyCode.Tab; return true;
                case "Enter": kc = KeyCode.Return; return true;
                case "Backspace": kc = KeyCode.Backspace; return true;
                case "ShiftLeft": kc = KeyCode.LeftShift; return true;
                case "ShiftRight": kc = KeyCode.RightShift; return true;
                case "CapsLock": kc = KeyCode.CapsLock; return true;
                case "Backquote": kc = KeyCode.BackQuote; return true;
                case "Minus": kc = KeyCode.Minus; return true;
                case "Equal": kc = KeyCode.Equals; return true;
                case "BracketLeft": kc = KeyCode.LeftBracket; return true;
                case "BracketRight": kc = KeyCode.RightBracket; return true;
                case "Backslash": kc = KeyCode.Backslash; return true;
                case "Semicolon": kc = KeyCode.Semicolon; return true;
                case "Quote": kc = KeyCode.Quote; return true;
                case "Comma": kc = KeyCode.Comma; return true;
                case "Period": kc = KeyCode.Period; return true;
                case "Slash": kc = KeyCode.Slash; return true;
            }
            if (code.StartsWith("Key") && code.Length == 4 &&
                System.Enum.TryParse(code.Substring(3), out kc)) return true;
            if (code.StartsWith("Digit") && code.Length == 6)
            {
                kc = KeyCode.Alpha0 + (code[5] - '0');
                return true;
            }
            kc = KeyCode.None;
            return false;
        }
    }
}
