using UnityEngine;

namespace KeyboardGame
{
    /// 自然拼读按段上色（对应 Flutter 端 phonics_handler.dart 的 letter_type 规则 + a3 配色方案）：
    ///
    /// letter_type → 颜色完整映射（判定顺序与 dart getPhonicsLetterType 完全一致，2026-11-10 校正）：
    ///   200 基础辅音                     → consonant  蓝   #42A5F5
    ///   100 基础元音                     → vowel      亮红 #FF5252
    ///   101 元音组合 / 104 L控制 / 105 静音e / 103 R控制(len<3) → vowels 深红 #E53935
    ///     （dart 第二个 if 无条件命中 104 → L控制恒为深红；其 104&&len<3 与 combo 分支里的 104 均不可达）
    ///   201 辅音连缀 / 202 辅音双写
    ///   203 辅音复合 / 206 辅音组合特例     → consonants 深蓝 #1E88E5
    ///   103 R控制(len≥3) / 102 复杂元音组合
    ///   204 元辅混合 / 501 不规则组合      → combo      紫   #7E57C2
    ///   其他（DB 无记录）                 → other      灰   #9E9E9E
    ///
    /// 分段：词表内的词用参考项目音标 DB 的实际分段（WordSegments，逐词与参考 app 核对）；
    /// 未知词从左到右最长匹配启发式近似。
    public static class PhonicsColors
    {
        public static readonly Color Vowel = Hex(0xFF5252);        // 100
        public static readonly Color VowelGroup = Hex(0xE53935);   // 101/104/105/103(<3)
        public static readonly Color Combo = Hex(0x7E57C2);        // 103(≥3)/102/204/501
        public static readonly Color Consonant = Hex(0x42A5F5);    // 200
        public static readonly Color ConsonantGroup = Hex(0x1E88E5); // 201/202/203/206
        public static readonly Color Other = Hex(0x9E9E9E);
        static Color Hex(int rgb) => new Color(((rgb>>16)&255)/255f,((rgb>>8)&255)/255f,(rgb&255)/255f,1f);

        static readonly string[] Irregular501 = {"ough","augh","eigh","ight"};
        static readonly string[] Complex102 = {"ew","ue","ui","eu"};
        static readonly string[] RControl3 = {"air","are","ear","eer","ere","ire","oar","ore","our","ure"};
        static readonly string[] RControl2 = {"ar","er","ir","or","ur"};
        static readonly string[] LControl = {"all","alk","alm","oll","old","olt","ild","ind"};
        static readonly string[] VowelGroup101 = {"ai","ay","ea","ee","ei","ey","ie","oa","oo","ou","ow"};
        static readonly string[] Double202 = {"bb","cc","dd","ff","gg","ll","mm","nn","pp","rr","ss","tt","zz"};
        static readonly string[] Digraph203 = {"ch","sh","th","ph","wh","ng","ck"};
        static readonly string[] Special206 = {"tch","dge"};
        static readonly string[] CvMix204 = {"qu"};
        static readonly string[] Blend201 = {"bl","br","cl","cr","dr","fl","fr","gl","gr","pl","pr",
            "sc","sk","sl","sm","sn","sp","st","sw","tr","tw","scr","shr","spl","spr","str","thr"};
        const string VowelChars = "aeiou";

        // ───── 参考项目音标 DB 的实际分段（与参考 app 截图逐词核对）─────
        // 段类型：v=基础元音100(亮红) vg=元音组合101/104/105(深红)
        //         c=基础辅音200(蓝)  cg=辅音组合201/202/203/206(深蓝)
        // 未知词走下方通用启发式；已知词以 DB 分段为准（如 grape 的 g|ra|p|e、orange 的 o|ran|ge）
        static readonly System.Collections.Generic.Dictionary<string, string[]> WordSegments =
            new System.Collections.Generic.Dictionary<string, string[]>
        {
            { "apple",  new[] { "a:v", "pp:cg", "le:vg" } },
            { "grape",  new[] { "g:c", "ra:vg", "p:c", "e:vg" } },
            { "orange", new[] { "ora:vg", "nge:cg" } },
            { "cherry", new[] { "ch:cg", "e:v", "rr:cg", "y:v" } },
            { "pear",   new[] { "p:c", "ear:m" } },
            { "banana", new[] { "b:c", "a:v", "n:c", "a:v", "n:c", "a:v" } },
            { "lemon",  new[] { "l:c", "e:v", "m:c", "o:v", "n:c" } },
            { "watermelon", new[] { "w:c", "a:v", "t:c", "e:v", "r:c", "m:c", "e:v", "l:c", "o:v", "n:c" } },
            { "oh",     new[] { "oh:vg" } }, // 参考 app：oh(/əʊ/) 整段元音组合全红（通用启发式会误拆成 o红+h蓝）
            { "everyone", new[] { "e:v", "ve:m", "r:c", "y:v", "one:m" } }, // 参考 app：e 亮红、ve 紫、r 蓝、y(/i/) 亮红、one(/wʌn/) 紫
        };

        /// <summary>整词逐字母颜色（按拼读段展开，段内字母同色）</summary>
        public static Color[] PerLetter(string word)
        {
            if (string.IsNullOrEmpty(word)) return System.Array.Empty<Color>();
            string w = word.ToLowerInvariant();
            var colors = new Color[w.Length];
            if (TryWordSegments(w, colors)) return colors;
            int i = 0;
            while (i < w.Length)
            {
                int len; Color color;
                ClassifyAt(w, i, out len, out color);
                for (int k = 0; k < len && i + k < w.Length; k++) colors[i + k] = color;
                i += len;
            }
            return colors;
        }

        static bool TryWordSegments(string w, Color[] colors)
        {
            string[] segs;
            if (!WordSegments.TryGetValue(w, out segs)) return false;
            int i = 0;
            foreach (var entry in segs)
            {
                int sep = entry.IndexOf(':');
                string seg = entry.Substring(0, sep);
                Color color = entry.EndsWith("vg") ? VowelGroup        // 元音组合(深红)
                    : entry.EndsWith("cg") ? ConsonantGroup            // 辅音组合(深蓝)
                    : entry.EndsWith("m") ? Combo                      // 元辅混合/R控制(紫)
                    : entry[entry.Length - 1] == 'v' ? Vowel           // 基础元音(亮红)
                    : Consonant;                                       // 基础辅音(蓝)
                for (int k = 0; k < seg.Length && i + k < colors.Length; k++) colors[i + k] = color;
                i += seg.Length;
            }
            return i == colors.Length;
        }

        /// <summary>无整词上下文时的单字母兜底（自由打字路径）</summary>
        public static Color Letter(char c)
        {
            c = char.ToLowerInvariant(c);
            if (VowelChars.IndexOf(c) >= 0 || c == 'y') return Vowel;
            if (c >= 'a' && c <= 'z') return Consonant;
            return Other;
        }

        static void ClassifyAt(string w, int i, out int len, out Color color)
        {
            int remaining = w.Length - i;
            // 静音e 105：词尾 元音+单辅音+e（如 ake/ine/ote）整段按元音组合
            if (remaining >= 3 && VowelChars.IndexOf(w[i]) >= 0
                && IsConsonant(w[i + 1]) && w[i + 2] == 'e' && i + 3 == w.Length)
            { len = 3; color = VowelGroup; return; }
            // 词尾 le（apple/little 的 -le，/əl/）整段按元音组合深红
            if (remaining == 2 && w[i] == 'l' && w[i + 1] == 'e' && i > 0)
            { len = 2; color = VowelGroup; return; }
            // 通用最长匹配：4→2 字母
            for (int tryLen = Mathf.Min(4, remaining); tryLen >= 2; tryLen--)
            {
                string seg = w.Substring(i, tryLen);
                if (tryLen >= 3)
                {
                    if (System.Array.IndexOf(Irregular501, seg) >= 0) { len = tryLen; color = Combo; return; }
                    if (System.Array.IndexOf(Special206, seg) >= 0) { len = tryLen; color = ConsonantGroup; return; }
                    if (System.Array.IndexOf(RControl3, seg) >= 0) { len = tryLen; color = Combo; return; }
                    if (System.Array.IndexOf(LControl, seg) >= 0) { len = tryLen; color = VowelGroup; return; } // 104 L控制 → 元音组合深红（dart 无条件命中 104）
                    if (tryLen == 3 && System.Array.IndexOf(Blend201, seg) >= 0) { len = tryLen; color = ConsonantGroup; return; }
                }
                if (System.Array.IndexOf(VowelGroup101, seg) >= 0) { len = tryLen; color = VowelGroup; return; }
                if (System.Array.IndexOf(Complex102, seg) >= 0) { len = tryLen; color = Combo; return; }
                if (System.Array.IndexOf(RControl2, seg) >= 0) { len = tryLen; color = VowelGroup; return; }
                if (System.Array.IndexOf(Double202, seg) >= 0) { len = tryLen; color = ConsonantGroup; return; }
                if (System.Array.IndexOf(Digraph203, seg) >= 0) { len = tryLen; color = ConsonantGroup; return; }
                if (System.Array.IndexOf(CvMix204, seg) >= 0) { len = tryLen; color = Combo; return; }
                if (System.Array.IndexOf(Blend201, seg) >= 0) { len = tryLen; color = ConsonantGroup; return; }
            }
            char c = w[i];
            len = 1;
            if (VowelChars.IndexOf(c) >= 0) color = Vowel;            // 100
            else if (c == 'y' && i > 0) color = Vowel;                // 词中词尾 y 作元音
            else if (c == 'e' && i == w.Length - 1 && i > 0) color = VowelGroup; // 词尾静音e 105
            else if (c >= 'a' && c <= 'z') color = Consonant;         // 200
            else color = Other;
        }

        static bool IsConsonant(char c) => c >= 'a' && c <= 'z' && VowelChars.IndexOf(c) < 0;
    }
}
