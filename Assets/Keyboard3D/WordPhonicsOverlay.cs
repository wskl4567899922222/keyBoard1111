using System.Collections.Generic;
using UnityEngine;
using KeyboardGame; // KeyboardSingleTrail 在此命名空间

namespace Keyboard3D
{
    /// <summary>
    /// 自然拼读下划线/音标叠加层（世界空间，挂在 KeyboardSingleTrail 的单词 root 下）：
    /// 每个拼读块一条下划线（按块内已打对字母数从左往右填绿色），块下方显示音标
    /// （sound 为音频文件键 / "null" 的块不显示）。挂在单词 root 上 → 自动跟随单词的
    /// 缩放、倾斜与屏幕锚定；切词时随旧词 root 一起销毁，无需手动清理。
    /// 2026-11-05：替代误放屏幕顶部的 WordPhonicsPanel——"单词文本"就是 3D 彩色字母，
    /// 下划线必须贴着它。块区间用字母实际包围盒（TryGetLetterLocalXRange）对齐。
    /// </summary>
    public sealed class WordPhonicsOverlay : MonoBehaviour
    {
        public struct ChunkInfo { public int start; public int count; public string sound; }

        static readonly Color FillColor = new Color(0.10f, 0.45f, 0.16f); // 深绿（2026-11-05 用户要求一致深绿）
        static readonly Color BgColor = new Color(0.6f, 0.6f, 0.6f, 0.35f); // 未填充段 = 灰色半透明（2026-11-05 用户要求）
        static readonly Color SoundColor = new Color(1f, 0.85f, 0.35f);
        static Sprite whiteSquare;

        readonly List<Transform> fills = new List<Transform>();
        readonly List<SpriteRenderer> bgs = new List<SpriteRenderer>(); // 灰色底条：块打完即隐藏 → 整段纯深绿
        readonly List<float> fillWidths = new List<float>();
        readonly List<int> chunkStarts = new List<int>();
        readonly List<int> chunkCounts = new List<int>();
        readonly List<Transform> sounds = new List<Transform>();        // 音标节点
        readonly List<int> soundChunkIdx = new List<int>();             // 各音标所属块（chunkStarts 下标）
        readonly List<float> chunkMidX = new List<float>();             // 各块中心 x / 宽度（点击命中用）
        readonly List<float> chunkW = new List<float>();
        float bandTop, bandBottom;                                      // 可点击纵向范围（字母顶 → 音标底）

        /// <summary>叠加层是否已构建（有块且 Build 成功）；外部位者据此决定释义行放在音标带下还是字母下。</summary>
        public bool Built => chunkStarts.Count > 0;
        /// <summary>音标带底部 y（word root 局部空间；中文释义行贴着它下面放）。</summary>
        public float BandBottomLocal => bandBottom;

        static Sprite WhiteSquare()
        {
            if (whiteSquare == null)
            {
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var px = new Color32[16];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(px); tex.Apply();
                whiteSquare = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f); // 1 sprite 单位 = 1 世界单位
            }
            return whiteSquare;
        }

        static bool HasDigit(string s)
        {
            foreach (var ch in s) if (ch >= '0' && ch <= '9') return true;
            return false;
        }

        // 估算音标的屏幕像素高度 → 返回 2 倍超采样的光栅字号（步进 16，限制动态字体图集变体）
        int RasterFontSize(float soundH)
        {
            var cam = Camera.main;
            if (cam == null) return 128;
            float worldH = soundH * transform.lossyScale.y; // 音标的世界空间高度
            float dist = Mathf.Max(0.1f, Vector3.Distance(cam.transform.position, transform.position));
            float pxH = worldH * cam.pixelHeight / (2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
            return Mathf.Clamp(Mathf.RoundToInt(pxH * 2f / 16f) * 16, 64, 256);
        }

        /// <summary>按拼读块构建下划线 + 音标。失败（拿不到字母度量）返回 false。</summary>
        public bool Build(KeyboardSingleTrail trail, List<ChunkInfo> chunks)
        {
            if (trail == null || chunks == null || chunks.Count == 0 ||
                !trail.TryGetWordLocalMetrics(out _, out _, out float bottomY, out float letterH))
                return false;

            float barH = letterH * 0.09f;      // 下划线粗细 ≈ 字高 9%
            float gap1 = letterH * 0.10f;      // 字母底 → 下划线（收紧：音标别够到键盘 2026-11-05）
            float gap2 = letterH * 0.10f;      // 下划线 → 音标
            float soundH = letterH * 0.25f;    // 音标字高（2026-11-06 用户要求：在原 0.5 基础上缩小一倍）
            float barY = bottomY - gap1 - barH * 0.5f;
            float soundY = barY - barH * 0.5f - gap2 - soundH * 0.5f;
            bandTop = barY + barH * 0.5f + gap1 + letterH; // 覆盖字母本身 + 下划线 + 音标
            bandBottom = soundY - soundH * 0.5f;

            foreach (var c in chunks)
            {
                if (c.count <= 0 ||
                    !trail.TryGetLetterLocalXRange(c.start, out float x0, out _) ||
                    !trail.TryGetLetterLocalXRange(c.start + c.count - 1, out _, out float x1) ||
                    x1 <= x0) continue;

                float w = x1 - x0;
                float midX = (x0 + x1) * 0.5f;
                chunkMidX.Add(midX);
                chunkW.Add(w);

                var bg = NewBar("Underline" + c.start, transform, new Vector3(midX, barY, -0.02f), new Vector3(w, barH, 1f), BgColor);
                bgs.Add(bg.GetComponent<SpriteRenderer>());
                var fill = NewBar("Fill" + c.start, bg.transform, Vector3.zero, new Vector3(0.0001f, 1f, 1f), FillColor);
                fills.Add(fill.transform);
                fillWidths.Add(w);
                chunkStarts.Add(c.start);
                chunkCounts.Add(c.count);

                // 音标显示规则：sound 字段本质是音频资源键——"null" = 不发音块不显示；
                // 纯音标符号（ə ʃ ɪ əʊ…）直接显示；m_front / word_en/12 这类发音文件名
                // 显示不了 → 退级显示前缀字母（m_front → m，2026-11-05 用户要求）
                string snd = c.sound;
                if (string.IsNullOrEmpty(snd) || snd == "null") snd = "";
                else if (snd.IndexOf('_') >= 0 || snd.IndexOf('/') >= 0 || HasDigit(snd))
                {
                    // 发音文件键退级显示：截到第一个 '_' / '/'（m_front→m、n_back→n）。
                    // 2026-11-06：重叠块合并后音标可能是 "əʊn_back"（əʊ+n_back 拼接），旧逻辑
                    // 只保留开头 ASCII 字母，ə 非 ASCII → 直接得空串不显示。改为按分隔符截断，
                    // IPA 字符原样保留 → "əʊn_back" → "əʊn"。
                    int cut = snd.IndexOf('_');
                    int slash = snd.IndexOf('/');
                    if (slash >= 0 && (cut < 0 || slash < cut)) cut = slash;
                    if (cut >= 0) snd = snd.Substring(0, cut);
                    if (HasDigit(snd)) snd = ""; // 纯数字文件键无音标可显示
                }
                if (snd.Length > 0)
                {
                    var tmGo = new GameObject("Sound" + c.start);
                    tmGo.transform.SetParent(transform, false);
                    tmGo.transform.localPosition = new Vector3(midX, soundY, -0.02f);
                    var tm = tmGo.AddComponent<TextMesh>();
                    tm.text = snd;
                    tm.font = BuiltinFont();
                    // 清晰度：字形贴图按 fontSize 光栅化，再用 characterSize 缩到目标字高。
                    // 固定 192 仍偏虚（2026-11-06 用户反馈）→ 改自适应超采样：先估算音标在屏幕上的
                    // 实际像素高度，按 2 倍取光栅字号（步进 16 限制动态字体图集变体数量）。
                    int fs = RasterFontSize(soundH);
                    tm.fontSize = fs;
                    tm.characterSize = soundH / (fs * 0.1f); // 字高 ≈ fontSize×0.1×characterSize = soundH
                    tm.anchor = TextAnchor.MiddleCenter;
                    tm.alignment = TextAlignment.Center;
                    tm.color = SoundColor;
                    var mr = tmGo.GetComponent<MeshRenderer>();
                    mr.sharedMaterial = tm.font.material;
                    mr.enabled = false; // 音标默认隐藏：该拼读块打完才显示（SetProgress 控制）
                    sounds.Add(tmGo.transform);
                    soundChunkIdx.Add(chunkStarts.Count - 1);
                }
            }
            return fills.Count > 0;
        }

        /// <summary>点击命中检测：word-root 本地坐标落在某块的字母/下划线/音标带内 → 返回块下标，否则 null。</summary>
        public int? ChunkIndexAtLocal(Vector2 localPos)
        {
            if (localPos.y < bandBottom || localPos.y > bandTop) return null;
            for (int i = 0; i < chunkMidX.Count; i++)
                if (localPos.x >= chunkMidX[i] - chunkW[i] * 0.5f &&
                    localPos.x <= chunkMidX[i] + chunkW[i] * 0.5f)
                    return i;
            return null;
        }

        /// <summary>刷新进度：typed = 当前单词已打对字母数（session.Position）。
        /// fill 是 bg（已按块宽缩放）的子级 → localScale 用 0~1 比例。</summary>
        public void SetProgress(int typed)
        {
            for (int i = 0; i < fills.Count; i++)
            {
                int count = chunkCounts[i];
                int done = Mathf.Clamp(typed - chunkStarts[i], 0, count);
                float frac = count > 0 ? (float)done / count : 0f;
                fills[i].localScale = new Vector3(Mathf.Max(frac, 0.0001f), 1f, 1f);
                fills[i].localPosition = new Vector3(-0.5f + frac * 0.5f, 0f, 0f);
                // 分段打完 → 关掉灰色底条，整段呈现纯深绿（2026-11-05 用户要求）
                if (bgs[i] != null) bgs[i].enabled = frac < 1f;
            }
            // 音标：所属拼读块打完才显示（2026-11-05 用户要求）
            for (int i = 0; i < sounds.Count; i++)
            {
                int ci = soundChunkIdx[i];
                int done = Mathf.Clamp(typed - chunkStarts[ci], 0, chunkCounts[ci]);
                sounds[i].GetComponent<MeshRenderer>().enabled = done >= chunkCounts[ci];
            }
        }

        static GameObject NewBar(string name, Transform parent, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = WhiteSquare();
            sr.color = color;
            return go;
        }

        // 音标字体：必须用打进包里的 TTF（带 IPA 字形 ɪ ə ʃ ʊ ˈ ː…）。
        // 内置字体 LegacyRuntime/Arial 缺 IPA 扩展区字形：编辑器里动态回退到系统字体能显示，
        // WebGL 包访问不了系统字体 → 音标全变空白（2026-11-05 实测翻车）。
        // Segoe UI.ttf 已拷入 Resources/Fonts（动态渲染，字形随用随生成，WebGL 可用）。
        /// <summary>内置动态字体（中/英/音标都靠 OS 字体回退渲染），公开给 WordTrailTyping 的完课提示复用。</summary>
        public static Font BuiltinFont()
        {
            var f = Resources.Load<Font>("Fonts/SegoeUI");
            if (f != null) return f;
            f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }
    }
}
