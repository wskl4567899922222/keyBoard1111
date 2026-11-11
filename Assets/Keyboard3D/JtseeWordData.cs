using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Keyboard3D
{
    /// <summary>
    /// jtsee 在线单词数据层（2026-11-09）：
    /// 1. 课程/单词 JSON 均为 IV:hex 密文（AES-256-CBC），Decode 负责解密；
    /// 2. 单词 JSON 里的 y_phonics / my_syllable / v_status / audio_file_name /
    ///    四组 Speech 拆分字段本身是 JSON 字符串，需二次解析；
    /// 3. 音频 URL 规则（飞书文档）：英音只看 v_status.uk（1=真人 scott，0=非真人 ryan），
    ///    完整音频 gb/scott_word|gb/ryan_word，拆分音频 gb/scott_split|gb/ryan_split，
    ///    时间段字段：uk=1 → mySpeech_en_ryan_split，uk=0 → oldSpeech_en_ryan_split
    ///    （字段名里的 ryan 不代表音频目录，按表配对）。
    /// 4. 时间数组结构 = [整读, 片段1..N, 整读]；第 n 个片段取数组第 n+1 项，播放 [s, s+d] 毫秒。
    /// </summary>
    internal static class JtseeWordData
    {
        public const string CourseBase = "https://static.jtsee.com/web/json/courses/";
        public const string WordBase = "https://static.jtsee.com/web/json/words/";
        public const string AudioBase = "https://static.jtsee.com/app_media/words_mp3_v2";
        const string Key = "supersoftwordbook" + "1234ruanshukeji"; // 32 字节

        // 并发下载瓶颈（2026-11-11）：mono 的 HttpWebRequest 默认每主机仅 2 个并发连接，
        // 21 个单词详情"并发"请求实际按 2 个一批排队（实测单词比手势晚 ~9s 出现）。
        // 静态构造在首次访问本类（InitOnline 组 URL）时执行，编辑器/PC 端立即放开限制；
        // WebGL 走浏览器网络栈（HTTP/2 多路复用）不受此限制，设置了也无害。
        static JtseeWordData()
        {
            try { System.Net.ServicePointManager.DefaultConnectionLimit = 32; } catch { }
        }

        // ───────────────────────── 解密数据落盘缓存（2026-11-11 加速单词显示） ─────────────────────────
        // 直连实测单元 JSON 下载仅 ~0.1s，但 Unity 走系统代理（Clash）时被绕到境外节点 → ~6s。
        // 缓存的是"本次网络成功拉到的解密明文"，每次成功拉取后覆盖写入 → 数据始终以网络为准；
        // 用途仅为下次启动时单词秒显（与手势同时出现），网络随后校验刷新。
        // 拉取失败仍走"重试 3 次 → 弹窗"，绝不把缓存当作失败回退（用户 2026-11-09 明确要求）。

        [Serializable]
        public class UnitCacheWord { public int wid; public string json; }

        [Serializable]
        public class UnitCache
        {
            public string unitId;
            public string courseJson;              // 解密后的单元 JSON 明文
            public List<UnitCacheWord> words = new List<UnitCacheWord>(); // 解密后的单词 JSON 明文（按 wid）
        }

        public static string CachePath(string unitId)
        {
            return Path.Combine(Application.persistentDataPath, "jtsee_cache", unitId + ".json");
        }

        /// <summary>写缓存（courseJson 可为 null 表示只更新单词部分；wordJsons 同理可传 null）。</summary>
        public static void SaveCache(string unitId, string courseJson, Dictionary<int, string> wordJsons)
        {
            try
            {
                string path = CachePath(unitId);
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                UnitCache c;
                // 增量写：先读旧缓存，本次只更新传入的部分（单词部分整体替换）
                if (File.Exists(path))
                {
                    try { c = JsonUtility.FromJson<UnitCache>(File.ReadAllText(path)); } catch { c = null; }
                }
                else c = null;
                if (c == null || c.unitId != unitId) c = new UnitCache { unitId = unitId };
                if (courseJson != null) c.courseJson = courseJson;
                if (wordJsons != null)
                {
                    c.words = new List<UnitCacheWord>();
                    foreach (var kv in wordJsons) c.words.Add(new UnitCacheWord { wid = kv.Key, json = kv.Value });
                }
                File.WriteAllText(path, JsonUtility.ToJson(c));
            }
            catch (Exception ex) { Debug.LogWarning($"[Jtsee] 缓存写入失败（不影响功能）：{ex.Message}"); }
        }

        public static UnitCache LoadCache(string unitId)
        {
            try
            {
                string path = CachePath(unitId);
                if (!File.Exists(path)) return null;
                var c = JsonUtility.FromJson<UnitCache>(File.ReadAllText(path));
                return c != null && c.unitId == unitId ? c : null;
            }
            catch { return null; }
        }

        // ───────────────────────── 解密 ─────────────────────────

        /// <summary>IV:hex 密文 → 明文 JSON；已是普通 JSON 原样返回。</summary>
        public static string Decode(string raw)
        {
            var input = raw.Trim();
            if (input.StartsWith("{") || input.StartsWith("[")) return input;

            var sep = input.IndexOf(':');
            if (sep != 32) throw new FormatException("不是 IV:hex 密文格式（IV 应为 32 位 hex）");
            byte[] iv = HexToBytes(input.Substring(0, 32));
            byte[] cipher = HexToBytes(input.Substring(33));
            if (cipher.Length == 0 || cipher.Length % 16 != 0) throw new FormatException("密文长度非法");

            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(Key);
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            using var dec = aes.CreateDecryptor();
            byte[] plain = dec.TransformFinalBlock(cipher, 0, cipher.Length);
            return Encoding.UTF8.GetString(plain);
        }

        static byte[] HexToBytes(string hex)
        {
            var b = new byte[hex.Length / 2];
            for (int i = 0; i < b.Length; i++)
                b[i] = byte.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return b;
        }

        // ───────────────────────── DTO（JsonUtility；未知字段自动忽略） ─────────────────────────

        [Serializable] public class CourseEntry { public string type; public int wid; public string english; public string us; public string uk; public string chinese; }
        [Serializable] public class CourseData { public List<CourseEntry> items; }

        [Serializable] public class VStatus { public int us; public int uk; }
        [Serializable] public class AudioFiles { public string word; public string split; public string old_word; public string old_split; }
        [Serializable] public class YPhoneme { public string sound; public List<int> position; }
        [Serializable] public class YPhonemeList { public List<YPhoneme> items; } // JsonUtility 不吃顶层数组 → 包一层
        [Serializable] public class MySyllable { public string uk; public string us; public string ws; }
        [Serializable] public class Seg { public float s; public float d; public string t; }
        [Serializable] public class SegList { public List<Seg> items; }

        [Serializable] public class WordInfoDto
        {
            public int wid; public string word; public string us; public string uk;
            public string v_status; public string audio_file_name; public string my_syllable; public string y_phonics;
        }
        [Serializable] public class SyllablesDto
        {
            public string my_syllable;
            public string mySpeech_en_ryan_split;
            public string oldSpeech_en_ryan_split;
        }
        [Serializable] public class WordRootDto { public WordInfoDto wordInfo; public SyllablesDto syllables; }

        // ───────────────────────── 解析后的单词数据 ─────────────────────────

        public class Chunk { public string sound; public List<int> positions; }
        public class SlicedSeg { public float startMs; public float endMs; public string label; }

        public class OnlineWord
        {
            public int wid;
            public string word;
            public string usIpa, ukIpa;
            public List<Chunk> phonics = new List<Chunk>();     // y_phonics：拼读块（音素粒度）
            public List<string> sylls = new List<string>();     // my_syllable.ws 拆分（音节粒度）
            public List<string> ipas;                           // my_syllable.uk 拆分（段数与 sylls 对齐才填）
            public bool isRealVoice;                            // v_status.uk == 1
            public string fullUrl;                              // 整词完整音频（整词播放用）
            public string splitUrl;                             // 拆分音频（分段播放用；无拆分音频为 null）
            public bool hasSplitAudio;                          // 该词是否有可用的拆分音频
            public string timeField;                            // 使用的时间段字段名（诊断用）
            public List<SlicedSeg> middleSegs = new List<SlicedSeg>(); // 去掉首尾整读后的中间片段
            public bool Valid => sylls.Count > 0 && middleSegs.Count == sylls.Count;

            /// <summary>第 syllIndex 个音节（0 起）的播放区间；非法返回 null。</summary>
            public SlicedSeg SegmentForSyllable(int syllIndex)
                => syllIndex >= 0 && syllIndex < middleSegs.Count ? middleSegs[syllIndex] : null;

            /// <summary>字母下标（0 起）所在音节的播放区间；无音节数据返回 null。</summary>
            public SlicedSeg SegmentForLetter(int letterIndex)
            {
                int cum = 0;
                for (int i = 0; i < sylls.Count; i++)
                {
                    cum += sylls[i].Length;
                    if (letterIndex < cum) return SegmentForSyllable(i);
                }
                return null;
            }
        }

        /// <summary>解析单词 JSON 明文 → OnlineWord（口音固定英音）；解析失败返回 null（reason 给原因）。</summary>
        public static OnlineWord ParseWord(string plainJson, out string reason)
        {
            reason = null;
            try
            {
                var root = JsonUtility.FromJson<WordRootDto>(plainJson);
                var wi = root?.wordInfo;
                var sy = root?.syllables;
                if (wi == null || string.IsNullOrEmpty(wi.word)) { reason = "wordInfo 缺失"; return null; }

                var status = Parse<VStatus>(wi.v_status);
                var files = Parse<AudioFiles>(wi.audio_file_name);
                // 整词音频必须有（真人选 word，非真人选 old_word），缺失才整体跳过；
                // 拆分音频允许缺失/占位（如 "null.mp3"）→ hasSplitAudio=false，仅禁用分段播放
                bool real = status != null && status.uk == 1;
                string wordFile = real ? files?.word : files?.old_word;
                string splitFile = real ? files?.split : files?.old_split;
                if (string.IsNullOrEmpty(wordFile))
                {
                    reason = "整词音频文件名缺失（v_status / audio_file_name 不完整）";
                    return null;
                }
                bool hasSplitAudio = !string.IsNullOrEmpty(splitFile) &&
                                     !splitFile.Equals("null.mp3", StringComparison.OrdinalIgnoreCase);

                var w = new OnlineWord
                {
                    wid = wi.wid,
                    word = wi.word,
                    usIpa = wi.us,
                    ukIpa = wi.uk,
                    isRealVoice = real,
                };

                // 拼读块：y_phonics 的字段名是 position（单数），转成 positions
                var yph = Parse<YPhonemeList>(wi.y_phonics == null ? null : "{\"items\":" + wi.y_phonics + "}")?.items;
                if (yph != null)
                    foreach (var p in yph)
                        if (p != null && !string.IsNullOrEmpty(p.sound) && p.position != null && p.position.Count > 0)
                            w.phonics.Add(new Chunk { sound = p.sound, positions = p.position });

                // 音节拆分（my_syllable 优先 wordInfo，缺失用 syllables.my_syllable 兜底）
                var ms = Parse<MySyllable>(!string.IsNullOrEmpty(wi.my_syllable) ? wi.my_syllable : sy?.my_syllable);
                if (ms != null && !string.IsNullOrEmpty(ms.ws))
                {
                    w.sylls = ms.ws.Split('-').ToList();
                    if (!string.IsNullOrEmpty(ms.uk))
                    {
                        var ipas = ms.uk.Split('-').ToList();
                        if (ipas.Count == w.sylls.Count) w.ipas = ipas;
                    }
                }

                // 音频 URL（英音；按 v_status.uk 配对，字段名里的 ryan 不代表目录）
                w.fullUrl = $"{AudioBase}/gb/{(w.isRealVoice ? "scott" : "ryan")}_word/{wordFile}";
                w.hasSplitAudio = hasSplitAudio;
                if (hasSplitAudio)
                {
                    w.splitUrl = $"{AudioBase}/gb/{(w.isRealVoice ? "scott" : "ryan")}_split/{splitFile}";
                    w.timeField = w.isRealVoice ? "mySpeech_en_ryan_split" : "oldSpeech_en_ryan_split";
                }

                // 时间数组：[整读, 片段1..N, 整读] → 取中间项（按位置，不按 t 文本）
                // 字段配对固定：uk=1 → mySpeech_en_ryan_split，uk=0 → oldSpeech_en_ryan_split
                // （美音为 mySpeech/oldSpeech_us_aria_split，本项目口音固定英音，暂不使用）
                if (hasSplitAudio)
                {
                    string timesJson = w.isRealVoice ? sy?.mySpeech_en_ryan_split : sy?.oldSpeech_en_ryan_split;
                    var times = Parse<SegList>(timesJson == null ? null : "{\"items\":" + timesJson + "}");
                    if (times?.items != null && times.items.Count >= 2)
                    {
                        for (int i = 1; i < times.items.Count - 1; i++)
                        {
                            var g = times.items[i];
                            if (g == null) continue;
                            w.middleSegs.Add(new SlicedSeg { startMs = g.s, endMs = g.s + g.d, label = g.t });
                        }
                    }
                }
                return w;
            }
            catch (Exception ex)
            {
                reason = ex.Message;
                return null;
            }
        }

        static T Parse<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonUtility.FromJson<T>(json); } catch { return null; }
        }

        /// <summary>解析课程 JSON 明文 → 单词词条列表（type=="word"）。</summary>
        public static List<CourseEntry> ParseCourse(string plainJson)
        {
            // 顶层数组 → 包一层 items（JsonUtility 不吃顶层数组）
            var data = JsonUtility.FromJson<CourseData>("{\"items\":" + plainJson.Trim() + "}");
            return data?.items?.Where(e => e != null && e.type == "word" && e.wid > 0 &&
                                           !string.IsNullOrEmpty(e.english)).ToList()
                   ?? new List<CourseEntry>();
        }

        public static string WordJsonUrl(int wid) => $"{WordBase}word_{wid}.json";
        public static string CourseJsonUrl(string unitId) => $"{CourseBase}{unitId}.json";
    }

    /// <summary>在线音频 AudioClip 加载器：URL → AudioClip，内存缓存（含失败标记）。</summary>
    internal static class OnlineAudioCache
    {
        static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        static readonly HashSet<string> Failed = new HashSet<string>();

        public static bool IsCached(string url) => Clips.ContainsKey(url);
        public static bool HasFailed(string url) => Failed.Contains(url);

        /// <summary>取缓存（未加载返回 null，不发起请求）。</summary>
        public static AudioClip Peek(string url) => Clips.TryGetValue(url, out var c) ? c : null;

        /// <summary>异步加载；完成后回调（失败回调 null）。已缓存/已失败立即同步回调。</summary>
        public static IEnumerator Load(string url, Action<AudioClip> done)
        {
            if (Clips.TryGetValue(url, out var cached)) { done?.Invoke(cached); yield break; }
            if (Failed.Contains(url)) { done?.Invoke(null); yield break; }

            using var req = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG);
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success || req.downloadHandler is not DownloadHandlerAudioClip h ||
                h.audioClip == null)
            {
                Debug.LogWarning($"[OnlineAudio] 加载失败 {url} : {req.error}");
                Failed.Add(url);
                done?.Invoke(null);
                yield break;
            }
            Clips[url] = h.audioClip;
            done?.Invoke(h.audioClip);
        }
    }
}
