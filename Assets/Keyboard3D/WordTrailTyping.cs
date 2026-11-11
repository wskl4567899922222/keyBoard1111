using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using KeyboardGame;             // 原项目（D:\UnityProjects\Keyboard）搬运的完整特效系统
using KeyboardGame.Letters;     // LetterCatalog / LetterView（3D 网格字母）
using TMPro;
using UnityEngine.UI;

namespace Keyboard3D
{
    /// <summary>
    /// 单词打字特效（原项目 KeyboardSingleTrail 完整移植的驱动器）：
    /// 1. 键盘上方悬浮目标单词：3D 网格字母（LetterView，body+bevel 双材质），
    ///    未输入的字母显示灰色等待字形（WaitingSDF），整词按视口宽度自适应缩放；
    /// 2. 按下正确按键 → 键帽顶面发射 EnhancedTrailGame 彗星（蛇形弧线 + 拖尾 +
    ///    掠过键帽的水波纹），命中时 LetterImpact 溅射 + 字母弹跳点亮（调色板逐字母配色）；
    /// 3. 整词输完按 Enter → FlyingLetters 退场动画（WordExitStyle.mode=FlyingLetters：
    ///    字母下蹲压扁 → 弹起拉伸 → 依次翻转飞出）；
    /// 4. 新单词经 WordEnergyStyle 能量线入场（WordPromptEntrance）。
    /// 由 KeyboardModelInput 按开关自动挂载（多键盘场景只挂第一份），无需场景配置。
    /// </summary>
    [DisallowMultipleComponent]
    public class WordTrailTyping : MonoBehaviour
    {
        public static WordTrailTyping Instance { get; private set; }

        [Header("单词库（显示大小写保留，只比较 A-Z；空项自动跳过）")]
        [Tooltip("默认 = WordPhonicsMap.json 里的 163 个有单词音频+拼读映射的词（打字时可发音标/整词音）")]
        public string[] words =
        {
            "emotion", "coach", "global", "vary", "focus", "mild", "identify", "victim", "tendency",
            "previous", "discipline", "virtue", "video", "calculate", "candidate", "personal", "ruin",
            "valid", "integrate", "explode", "insurance", "security", "domestic", "barrier", "community",
            "awful", "encounter", "register", "disturb", "resolve", "scan", "organ", "infer", "decade",
            "attitude", "yield", "network", "slide", "trap", "ban", "zone", "racial", "burst",
            "consistent", "competition", "cancel", "individual", "export", "calendar", "acid", "bacteria",
            "transport", "wonder", "scale", "alcohol", "subsequent", "tend", "version", "adapt", "import",
            "boundary", "appeal", "issue", "temple", "preserve", "core", "expansion", "extraordinary",
            "poverty", "extent", "debt", "substance", "talent", "career", "shift", "route", "acknowledge",
            "explore", "comment", "range", "abuse", "discrimination", "commit", "approve", "vocabulary",
            "origin", "prospect", "consume", "facility", "battery", "private", "transform", "moral",
            "aspect", "expensive", "decline", "vivid", "decent", "negative", "strategy", "mood", "valley",
            "globe", "medium", "acquire", "debate", "clue", "neutral", "external", "promote", "splendid",
            "target", "pursue", "bother", "device", "budget", "substantial", "religion", "region",
            "outstanding", "tidy", "mixture", "optimistic", "slip", "campus", "authority", "capture",
            "secure", "distinguish", "awkward", "agency", "radiation", "virtual", "wander", "delay",
            "automatic", "internal", "split", "grand", "tension", "satellite", "prosperity", "petrol",
            "waist", "spot", "advertise", "expand", "constant", "expense", "remote", "nevertheless",
            "adopt", "appreciate", "stimulate", "casual", "academic", "maintain", "compete", "deserve",
            "offend", "professional", "adult", "degree",
        };
        [Tooltip("随机顺序（true = 打乱单词顺序；无论开关，都只打一轮不循环）")]
        public bool randomOrder = false;

        [Header("在线单词数据（jtsee，2026-11-09）")]
        [Tooltip("true = 单词文本/音标/音源全部在线拉取（单元 JSON + 单词 JSON 解密 + 音频 URL 流式播放），"
                 + "拉取失败自动重试、不回退本地数据；false = 旧本地模式（硬编码词表 + Resources 音频）")]
        public bool useOnlineData = true;
        [Tooltip("在线单元 ID：词库 = https://static.jtsee.com/web/json/courses/<ID>.json 里的全部纯字母单词。"
                 + "可被外部覆盖：URL 查询参数 ?unitId=xxx，或宿主页面 JS 调 window.KB_SetUnitId('xxx')")]
        public string unitId = "superword_book872_Unit_1";
        [Tooltip("实验（2026-11-10）：true = 拼读块打完不播本地 Audio/uk 音素音，改播网站拆分音频中该块所在音节的片段；" +
                 "false = 播本地音素音（原行为）。注意 jtsee 线上无音素级音源，音节片段与音素块可能不对齐（如打完 t 播 nit）")]
        public bool phonemeFromOnline = true;
        int onlineGen;      // InitOnline 代际号：外部改 unitId 时 +1，旧协程的过期结果全部丢弃（防竞态）

        // ── 单元 JSON 预热（2026-11-11 提速）：引擎初始化最早期（进 Play/开页）就发出单元 JSON
        // 请求，InitOnline 到 Awake 时请求已在路上 → 省掉一次启动管线延迟。URL 与场景 unitId
        // 不一致时预热作废（无害），走正常 FetchText。static 字段每次 Play 重置，方法每次都跑。
        static UnityWebRequest prewarmedCourseReq;
        static UnityWebRequestAsyncOperation prewarmedCourseOp;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        static void PrewarmCourseRequest()
        {
            try
            {
                var req = UnityWebRequest.Get(JtseeWordData.CourseJsonUrl("superword_book872_Unit_1"));
                req.timeout = 10;
                prewarmedCourseOp = req.SendWebRequest();
                prewarmedCourseReq = req; // 保留引用：完成后读 downloadHandler
            }
            catch (Exception ex) { Debug.LogWarning($"[WordTrail] 单元 JSON 预热失败（走正常拉取）：{ex.Message}"); }
        }

        [Header("单词布局")]
        [Tooltip("大写字母高度（世界单位）。原项目 0.02，本项目键盘尺度按 25 倍换算")]
        public float letterCapHeight = 0.5f;
        [Tooltip("屏幕锚点 X（0=左边缘，1=右边缘）")]
        [Range(0f, 1f)] public float screenAnchorX = 0.5f;
        [Tooltip("屏幕锚点 Y（0=下边缘，1=上边缘；0.78 ≈ 悬浮在键盘上方）")]
        [Range(0f, 1f)] public float screenAnchorY = 0.78f;
        [Tooltip("相机缺失时的兜底锚点：键盘包围盒顶部上方高度（世界单位）")]
        public float hoverHeight = 1.6f;
        [Tooltip("单词目标宽度占屏幕宽度的比例（PrepareWord 内部自适应缩放）")]
        [Range(0.1f, 0.95f)] public float upperWordWidth = 0.5f;
        [Tooltip("等待字母显示拼读色的不透明度（0=无颜色提示，1=全色）")]
        [Range(0f, 1f)] public float waitingPhonicsOpacity = 0.2f;
        [Tooltip("打错时等待字母变红")]
        public Color errorColor = new Color(1f, 0.15f, 0.15f, 1f);
        [Tooltip("打错后红字滞留时长（秒），重复打错重新计时（原项目同款）")]
        public float errorRecoveryDelay = 1.5f;

        [Header("飞行/发光")]
        [Tooltip("发光亮度（写入原项目材质 _Intensity，配合 Bloom）")]
        public float brightness = 2f;
        [Tooltip("拖尾发光增益（>1 = 尾巴像参考视频一样被 Bloom 撑粗变柔；参考观感约 2.5）")]
        public float tailGlowBoost = 3f;
        [Tooltip("拖尾宽度增益（1 = 参考工程原始宽度；参考视频细丝观感约 1.5）")]
        public float tailWidthBoost = 1.5f;
        [Tooltip("头部光晕增益（弥补本工程全局 Bloom 弱，参考观感约 1.8）")]
        public float tailHeadGlowBoost = 1.8f;
        [Tooltip("拖尾碎点/星星增益（尺寸与亮度同乘，参考观感约 1.5）")]
        public float tailDetailBoost = 1.5f;

        [Header("到达音效")]
        [Tooltip("粒子到达目标字母时播放此音源（拖入 Test 场景的 HitSound 对象）")]
        public AudioSource arrivalHitSound;

        [Header("退场音效")]
        [Tooltip("单词退场动画开始时播放此音源（原版 heal 不压缩），退场动画结束时停止播放")]
        public AudioSource exitSound;

        [Header("自然拼读/单词发音")]
        [Tooltip("拼读块与单词发音音量")]
        [Range(0f, 1f)] public float phonicsVolume = 1f;
        [Tooltip("玩家开关（PhonicsSoundToggle 按钮控制）：false 时只打字不发音")]
        public bool PhonicsEnabled { get; set; } = true;
        [Tooltip("玩家开关（SyllablesplitButton 按钮控制，2026-11-05 新增）：开启后多音节词的下划线按音节分段" +
                 "（breakfast → break-fast，段下显示英式音标），按完一个音节读英式拆分音频（split_en/split_<wid>.mp3 内的 s~d 片段）")]
        public bool SyllableSplitEnabled { get; set; } = false;
        [Tooltip("拼读块音标音频（Resources/Audio/uk/{音标}.mp3）与单词音频（Resources/Audio/word_en/{wid}.mp3）映射表")]
        public TextAsset phonicsMapAsset;

        // WordPhonicsMap.json 结构（JsonUtility 可解析；sound="null" = 不发音字母块，无音频）
        [Serializable] class PhonemeChunk { public string sound; public List<int> positions; }
        [Serializable] class PhonicsMapEntry { public int wid; public string word; public string audio; public List<PhonemeChunk> phonemes; }
        [Serializable] class PhonicsMapData { public List<PhonicsMapEntry> entries; }
        Dictionary<string, PhonicsMapEntry> phonicsByWord;      // 小写单词 → 映射条目

        // WordSyllableMap.json（2026-11-05 新增，ipa_export 全量导出转制）：音节拆分数据。
        // my_syllable 内嵌 JSON：uk = 英式音标连字格式（"ɪ-ˈməʊ-ʃ(ə)n"）、ws = 拼写连字格式（"e-mo-tion"）；
        // oldSpeech_en_ryan_split 内嵌 JSON：英式整词音频内的音节片段（s=开始 ms，d=持续 ms，t=片段文本，
        // 首尾两条 t=整词），音频文件 = Resources/Audio/split_en/split_<wid>.mp3（已拷入 163 个）。
        [Serializable] class SyllableSeg { public int s; public int d; public string t; }
        [Serializable] class MySyllable { public string uk; public string ws; }
        [Serializable] class SyllableEntry { public int wid; public string word; public string my_syllable; public string oldSpeech_en_ryan_split; }
        [Serializable] class SyllableMapData { public List<SyllableEntry> entries; }
        [Serializable] class SegList { public List<SyllableSeg> items; } // JsonUtility 不吃顶层数组 → 包一层
        class SyllableData { public int wid; public List<string> sylls; public List<string> ipas; public List<SyllableSeg> segs; public string fullIpa; }
        Dictionary<string, SyllableData> syllableByWord;        // 小写单词 → 音节拆分数据
        Dictionary<string, string> fullIpaByWord;               // 小写单词 → 完整单词音标（ms.uk 去连字符；拆分模式无对齐分段时整词下划线用，2026-11-10）
        AudioSource splitSource;                                // 音节片段专用音源（要能中途 Stop，与 PlayOneShot 音源分开）
        float splitStopAt = float.NegativeInfinity;             // 当前片段应停止的时刻
        readonly Dictionary<string, AudioClip> splitSegClipCache = new Dictionary<string, AudioClip>(); // 已裁剪的音节子片段（避免 WebGL 上 AudioSource.time seek 失效）
        readonly Dictionary<string, AudioClip> voiceClipCache = new Dictionary<string, AudioClip>();
        AudioSource voiceSource;                                 // 拼读块/单词发音共用（2D）
        Dictionary<string, JtseeWordData.OnlineWord> onlineWords; // 在线模式：小写单词 → 解析后的在线数据
        JtseeWordData.OnlineWord pendingWholeWordOd;             // 在线拆分模式完词后待读整词的数据源
        bool voiceWarmed;        // WebGL：音频引擎首次交互才唤醒，同帧调度的音标音会被吞 → 首个音延后播
        const float FirstVoiceDelay = 0.1f;                      // 未预热时首个音标音的延后秒数
        string pendingWholeWord;                                 // 拆分模式完词后待读的整词（等音节音播完）
        float pendingWholeWordAt = float.NegativeInfinity;       // 整词音应播放的时刻
        int pendingWholeWordRepeat = 1;                          // 整词朗读遍数（无拼读/音节显示数据的词读 2 遍）
        FetchFailDialog fetchFailDialog;                         // 拉取失败弹窗（防重复创建）

        [Tooltip("音效切换下拉框（拖入 Canvas 下的 Dropdown，选项即音效名）")]
        public TMPro.TMP_Dropdown arrivalDropdown;
        [Tooltip("所有候选到达音效容器（留空则自动收集 Resources/ArrivalHits + ArrivalChimes）")]
        public List<AudioClip> arrivalClipList = new List<AudioClip>();
        [Tooltip("辅音音效下拉框（拖入 Canvas 下的 Dropdown；留空则运行时自动克隆元音下拉框放到其正下方）")]
        public TMPro.TMP_Dropdown consonantDropdown;
        [Tooltip("辅音字母命中音效候选（留空则自动收集 Resources/ConsonantHits；全部由元音 hit 音效加工而来，整体更弱）")]
        public List<AudioClip> consonantClipList = new List<AudioClip>();
        int selectedArrivalClip;
        int selectedConsonantClip;
        AudioClip ArrivalClip=>arrivalClipList!=null&&arrivalClipList.Count>0
            ?arrivalClipList[Mathf.Clamp(selectedArrivalClip,0,arrivalClipList.Count-1)]
            :arrivalHitSound!=null?arrivalHitSound.clip:null;
        AudioClip ConsonantClip=>consonantClipList!=null&&consonantClipList.Count>0
            ?consonantClipList[Mathf.Clamp(selectedConsonantClip,0,consonantClipList.Count-1)]
            :arrivalHitSound!=null?arrivalHitSound.clip:null;
        // 辅音字母命中 → 换用辅音下拉框所选音效（判定与 PhonicsColors 蓝色=辅音 完全一致）；
        // 元音字母（红）仍用元音下拉框选中的到达音效。辅音候选缺失时退回元音音效。
        AudioClip ArrivalClipForLetter(int index)
        {
            if (session != null)
            {
                string w = session.CurrentWord.ToLowerInvariant();
                if (index >= 0 && index < w.Length)
                {
                    var c = PhonicsColors.PerLetter(w)[index];
                    if (c == PhonicsColors.Consonant || c == PhonicsColors.ConsonantGroup)
                    {
                        if (consonantClipList == null || consonantClipList.Count == 0)
                            consonantClipList = new List<AudioClip>(Resources.LoadAll<AudioClip>("ConsonantHits"));
                        if (consonantClipList.Count > 0) return ConsonantClip;
                    }
                }
            }
            return ArrivalClip;
        }
        void BuildArrivalClipOptions()
        {
            if(arrivalClipList==null)arrivalClipList=new List<AudioClip>();
            if(arrivalClipList.Count==0)
                foreach(var folder in new[]{"ArrivalHits","ArrivalChimes"})
                    arrivalClipList.AddRange(Resources.LoadAll<AudioClip>(folder));
            if(arrivalDropdown==null)return;
            arrivalDropdown.ClearOptions();
            arrivalDropdown.AddOptions(arrivalClipList.ConvertAll(c=>c!=null?c.name:"(空)"));
            selectedArrivalClip=Mathf.Clamp(arrivalDropdown.value,0,arrivalClipList.Count-1);
            arrivalDropdown.onValueChanged.AddListener(i=>
            {
                selectedArrivalClip=i;
                if(arrivalHitSound==null)return;
                var clip=ArrivalClip;
                if(clip!=null){arrivalHitSound.clip=clip;arrivalHitSound.Play();} // 切换即试听
            });
        }

        static TMPro.TMP_FontAsset smileyTmp;
        // SmileySans 动态 TMP 字体（从 Resources/Fonts/SmileySans.otf 创建，字形按需光栅化，任意中文可显示）
        static TMPro.TMP_FontAsset SmileyTmpFont()
        {
            if (smileyTmp != null) return smileyTmp;
            var font = Resources.Load<Font>("Fonts/SmileySans");
            if (font == null) return null;
            smileyTmp = TMPro.TMP_FontAsset.CreateFontAsset(font);
            return smileyTmp;
        }

        // 辅音音效下拉框：逻辑与元音下拉完全一致（切换即试听）。
        // 下拉框 UI 未拖入时自动克隆元音下拉框 → 按用户指定位置摆放（右上角锚点，元音下拉框正下方），
        // 全部 TMP 文本换 SmileySans 动态字体（选项含"辅音_"中文前缀，默认 LiberationSans 无 CJK 字形 → 空白）。
        void BuildConsonantClipOptions()
        {
            if(consonantClipList==null)consonantClipList=new List<AudioClip>();
            if(consonantClipList.Count==0)
                consonantClipList.AddRange(Resources.LoadAll<AudioClip>("ConsonantHits"));
            if(consonantDropdown==null&&arrivalDropdown!=null)
            {
                var clone=Instantiate(arrivalDropdown.gameObject,arrivalDropdown.transform.parent,false);
                clone.name="ConsonantHitDropdown";
                var rt=clone.GetComponent<RectTransform>();
                // 用户指定位置（2026-11-10 Inspector 实测值）：锚点/轴心 (1,1)/(0.5,0.5)，宽高 160×30，缩放 2
                rt.anchorMin=rt.anchorMax=new Vector2(1f,1f);
                rt.pivot=new Vector2(0.5f,0.5f);
                rt.sizeDelta=new Vector2(160f,30f);
                rt.anchoredPosition=new Vector2(-169.61f,-190f);
                rt.localScale=new Vector3(2f,2f,1f);
                var cjkFont=SmileyTmpFont();
                if(cjkFont!=null)
                    foreach(var t in clone.GetComponentsInChildren<TMPro.TMP_Text>(true))
                        t.font=cjkFont;
                consonantDropdown=clone.GetComponent<TMPro.TMP_Dropdown>();
            }
            if(consonantDropdown==null)return;
            consonantDropdown.ClearOptions();
            consonantDropdown.AddOptions(consonantClipList.ConvertAll(c=>c!=null?c.name:"(空)"));
            selectedConsonantClip=Mathf.Clamp(consonantDropdown.value,0,consonantClipList.Count-1);
            consonantDropdown.onValueChanged.AddListener(i=>
            {
                selectedConsonantClip=i;
                if(arrivalHitSound==null)return;
                var clip=ConsonantClip;
                if(clip!=null){arrivalHitSound.clip=clip;arrivalHitSound.Play();} // 切换即试听
            });
        }

        [Header("字母配色（逐字母循环，参考视频糖果色）")]
        public Color[] palette =
        {
            new Color(1f, 0.2f, 0.3f), new Color(1f, 0.65f, 0.12f), new Color(0.3f, 1f, 0.35f),
            new Color(0.1f, 0.85f, 1f), new Color(0.65f, 0.35f, 1f), new Color(1f, 0.25f, 0.85f),
        };

        // ───────────────────────── 内部状态 ─────────────────────────

        KeyboardSingleTrail trails;   // 原项目飞行/字母/退场系统（完整搬运）
        WordPracticeSession session;  // 练习进度（Position/HasError/Submitted）
        List<string> currentWordList; // 当前 session 的词表（未洗牌，网络校验与缓存版比对用）
        WordProgressBar progressBar;  // 单词进度条（当前单词的字母拼写进度，切词清零）
        WordPhonicsOverlay phonicsOverlay; // 自然拼读叠加层（挂在 3D 单词 root 下：拼读块下划线 + 块下音标）
        WordMeaningPopup meaningPopup;     // 完词脉冲弹出的中文释义（挂单词 root 下，默认隐藏，随切词销毁）
        readonly Dictionary<string, string> meaningByWord = new Dictionary<string, string>(); // 小写单词 → 中文释义（课程 JSON 的 chinese 字段，仅在线数据有）
        Transform allLearnedText;     // "单词已经全部学习"提示（不循环模式全部打完后显示在单词位置）
        KeyboardModelInput keyboard;  // 粒子发射源 + 发光目标
        readonly List<KeyboardModelInput> keyboards = new List<KeyboardModelInput>(); // 全部键盘（发光同步）
        Camera cam;
        bool inputUnavailable;

        Transform runtimeRoot;   // targetB/emitOrigin 的父节点
        Transform targetB;       // 单词锚点（首字母命中前每帧跟随键盘上方）
        Transform emitOrigin;    // 发射原点替身（键帽顶面没有 Transform，用替身传位置）

        float errorAt = float.NegativeInfinity; // 最近一次打错的时间（红字滞留计时）
        const float ShakeDuration = 0.25f;      // 错误抖动时长（原项目默认）
        const float ShakePixels = 6f;           // 错误抖动幅度（1080p 像素，按实际分辨率换算）

        // ───────────────────────── 装配 ─────────────────────────

        // ───────────────────────── 外部 unitId 注入（2026-11-10） ─────────────────────────
        // 通道 1：页面 URL 查询参数 ?unitId=xxx（Awake 里解析 Application.absoluteURL）
        // 通道 2：宿主页面 JS 调 window.KB_SetUnitId('xxx')（WebGLTemplates/NoUI/index.html 定义，
        //         内部 unityInstance.SendMessage("Keyboard68_GlossGame", "SetUnitIdFromWeb", id)）
        // 两条通道都落到这里：立即按新单元重建词库（代际号作废在途的旧 InitOnline 结果）。
        public void SetUnitIdFromWeb(string newUnitId)
        {
            if (string.IsNullOrWhiteSpace(newUnitId)) { Debug.LogWarning("[WordTrail] 外部 unitId 为空，忽略。"); return; }
            newUnitId = newUnitId.Trim();
            if (newUnitId == unitId) { Debug.Log($"[WordTrail] 外部 unitId 与当前相同，忽略：{newUnitId}"); return; }
            Debug.Log($"[WordTrail] 外部设置 unitId：{unitId} → {newUnitId}，重建词库。");
            unitId = newUnitId;
            if (!useOnlineData) return;
            onlineGen++; // 在途 InitOnline 结果作废
            if (fetchFailDialog != null) { Destroy(fetchFailDialog.gameObject); fetchFailDialog = null; }
            StartCoroutine(InitOnline());
        }

        // 从 Application.absoluteURL 解析查询参数（WebGL 专用通道；编辑器返回本地路径，无参数时 null）
        static string ReadQueryParam(string key)
        {
            try
            {
                string url = Application.absoluteURL;
                if (string.IsNullOrEmpty(url)) return null;
                int q = url.IndexOf('?');
                if (q < 0) return null;
                foreach (var seg in url.Substring(q + 1).Split('&'))
                {
                    int eq = seg.IndexOf('=');
                    if (eq <= 0) continue;
                    if (!string.Equals(seg.Substring(0, eq), key, StringComparison.OrdinalIgnoreCase)) continue;
                    return Uri.UnescapeDataString(seg.Substring(eq + 1).Replace("+", "%20")).Trim();
                }
            }
            catch { }
            return null;
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            // 单词屏幕锚点强制值（2026-11-05 用户逐步上移：0.78 → 0.80 → 0.82 → 0.85）
            screenAnchorY = 0.85f;

            keyboard = GetComponent<KeyboardModelInput>();
            if (keyboard == null) keyboard = FindObjectOfType<KeyboardModelInput>();
            if (keyboard == null) { Debug.LogWarning("[WordTrail] 场景里没有 KeyboardModelInput，单词打字特效禁用。", this); Instance = null; enabled = false; return; }

            // 多键盘场景（如 Candy 双键盘）：目标键发光同步到所有实例
            keyboards.Add(keyboard);
            //foreach (var kb in FindObjectsOfType<KeyboardModelInput>())
            //    if (!keyboards.Contains(kb)) keyboards.Add(kb);

            // 原项目特效资产（已随包搬入本工程 Resources）：
            // SingleTrail.prefab（飞行器，含 EnhancedTrailGame 光头/水波纹）、
            // SingleTrailImpact.prefab（命中溅射 LetterImpact）、LetterCatalog（字母网格目录）、
            // LetterView.prefab + LetterBody/LetterBevel（3D 字母）、WordExit*/WordImpact 样式由各系统内部加载
            var trailGo = Resources.Load<GameObject>("SingleTrail");
            var impactPrefab = Resources.Load<SingleTrailImpact>("SingleTrailImpact");
            var catalog = Resources.Load<LetterCatalog>("LetterCatalog");
            var letterViewPrefab = Resources.Load<LetterView>("LetterView");
            var bodyMat = Resources.Load<Material>("LetterBody");
            var bevelMat = Resources.Load<Material>("LetterBevel");
            if (trailGo == null || trailGo.GetComponent<SingleTrailEffect>() == null || impactPrefab == null ||
                catalog == null || letterViewPrefab == null || bodyMat == null || bevelMat == null)
            {
                Debug.LogError("[WordTrail] 缺少原项目特效资产（SingleTrail/SingleTrailImpact/LetterCatalog/LetterView/LetterBody/LetterBevel，应位于 Assets/Keyboard3D/Effects/Resources/ 与 Letters/），单词打字特效禁用。", this);
                Instance = null;
                enabled = false;
                return;
            }

            // 水波纹定位：KeyWaterWave 默认找原项目 KeyboardInputMapping（本工程没有），
            // 注入解析器 → 用本工程 KeyboardModelInput 的键帽表找最近键帽
            KeyboardGame.TrailFlightStudy.KeyWaterWave.NearestKeyResolver =
                pos => keyboard != null ? keyboard.GetNearestKeycap(pos) : null;

            cam = Camera.main;
            CacheKeyboardBounds(); // 键盘静止：包围盒只算一次，避免动效网格污染联合包围盒导致单词跟随跳动

            trails = gameObject.AddComponent<KeyboardSingleTrail>();
            runtimeRoot = new GameObject("WordTrail_Runtime").transform;
            targetB = new GameObject("WordTrail_TargetB").transform;
            targetB.SetParent(runtimeRoot, false);
            targetB.position = KeyboardAnchor(); // 立即摆位：PrepareWord 用 targetB 定位单词根（Update 首帧之前就要正确）
            emitOrigin = new GameObject("WordTrail_EmitOrigin").transform;
            emitOrigin.SetParent(runtimeRoot, false);

            // 长度参数按字高比换算（原项目 letterCapHeight=0.02）
            float lengthScale = letterCapHeight / 0.02f;
            trails.Configure(targetB, trailGo.GetComponent<SingleTrailEffect>(), catalog, letterViewPrefab,
                bodyMat, bevelMat, impactPrefab, cam, letterCapHeight, palette, brightness,
                flightSpeed: 12.5f,                         // 原始等比放大值（0.5×25）
                maxArcHeight: 0.04f * lengthScale,    // 原项目 0.04（字高 2 倍）
                arrivalDistance: 0.008f * lengthScale,
                trailLifetime: 0.25f);
            trails.upAxis = keyboard.SurfaceNormal; // 键盘根是躺倒的，transform.up 不朝上
            KeyboardGame.TrailFlightStudy.EnergyFlightPreview.GainBoost = tailGlowBoost; // 拖尾泛光增益（粗柔尾观感）
            KeyboardGame.TrailFlightStudy.EnergyFlightPreview.WidthBoost = tailWidthBoost; // 拖尾宽度增益
            KeyboardGame.TrailFlightStudy.EnergyFlightPreview.HeadGlowBoost = tailHeadGlowBoost; // 头部光晕增益
            KeyboardGame.TrailFlightStudy.EnergyFlightDetails.DetailBoost = tailDetailBoost; // 碎点/星星增益
            // 到达音效：Inspector 拖入 HitSound 对象，粒子到达字母时 PlayOneShot；
            // 元音下拉可切换试听（Inspector 列表 + Resources/ArrivalHits/ArrivalChimes），
            // 辅音下拉（ConsonantHits 加工组，整体更弱）未拖入 UI 时自动克隆元音下拉框
            BuildArrivalClipOptions();
            BuildConsonantClipOptions();
            trails.LetterRevealed += (wordId, index, position, color) =>
            {
                var clip=ArrivalClipForLetter(index);
                if (arrivalHitSound != null && clip != null)
                    arrivalHitSound.PlayOneShot(clip); // PlayOneShot 支持快速连击叠音
            };
            trails.WordCompleted += OnWordCompleted;
            trails.WordExitStarted += OnWordExitStarted; // 单词退场动画开始 → 播放退场音效（按动画时长压缩）
            trails.WordPulseStarted += OnWordPulseStarted; // 拼完单词（脉冲开始）→ 读出单词发音
            SetVoiceReady();
            trails.SetWaitingPhonicsOpacity(waitingPhonicsOpacity);

            if (useOnlineData)
            {
                // 外部注入 unitId 通道 1：URL 查询参数 ?unitId=xxx（WebGL 上 Application.absoluteURL = 页面完整 URL）
                var extUnit = ReadQueryParam("unitId");
                if (!string.IsNullOrEmpty(extUnit) && extUnit != unitId)
                {
                    Debug.Log($"[WordTrail] URL 参数注入 unitId：{unitId} → {extUnit}");
                    unitId = extUnit;
                }
                StartCoroutine(InitOnline()); // 词库/单词详情/音频全部在线拉取，就绪后再建 session
            }
            else
                InitLocalSession();

            // 音节拆分开关按钮：场景里只有 SyllablesplitButton 物体（未手工挂脚本）→ 运行时补挂，
            // 零场景依赖（2026-11-05，模式与 PhonicsSoundToggle 一致：按钮自身是唯一事实源）
            //var splitBtnGo = GameObject.Find("SyllablesplitButton");
            //if (splitBtnGo != null && splitBtnGo.GetComponent<SyllableSplitToggle>() == null)
            //    splitBtnGo.AddComponent<SyllableSplitToggle>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (trails != null) { trails.WordCompleted -= OnWordCompleted; trails.WordPulseStarted -= OnWordPulseStarted; }
            if (progressBar != null) Destroy(progressBar.gameObject);
            if (runtimeRoot != null) Destroy(runtimeRoot.gameObject);
        }



        // Awake 里装配完成后调用：建发音音源 + 加载映射表（phonicsMapAsset 留空则自动 Resources 加载）
        void SetVoiceReady()
        {
            voiceSource = gameObject.AddComponent<AudioSource>();
            voiceSource.playOnAwake = false;
            voiceSource.spatialBlend = 0f; // 2D：发音不随空间位置衰减
            if (phonicsMapAsset == null) phonicsMapAsset = Resources.Load<TextAsset>("WordPhonicsMap");
            if (phonicsMapAsset == null)
            {
                Debug.LogWarning("[WordTrail] 未找到 WordPhonicsMap.json（应在 Resources 下），拼读/单词发音停用。", this);
                return;
            }
            var data = JsonUtility.FromJson<PhonicsMapData>(phonicsMapAsset.text);
            if (data?.entries == null || data.entries.Count == 0) return;
            phonicsByWord = new Dictionary<string, PhonicsMapEntry>(data.entries.Count);
            foreach (var e in data.entries)
                if (!string.IsNullOrEmpty(e?.word)) phonicsByWord[e.word.ToLowerInvariant()] = e;
            LoadSyllableMap();
        }

        // 加载音节拆分数据（WordSyllableMap.json，Resources 下；见 SyllableEntry 字段注释）
        void LoadSyllableMap()
        {
            syllableByWord = new Dictionary<string, SyllableData>();
            fullIpaByWord = new Dictionary<string, string>();
            var asset = Resources.Load<TextAsset>("WordSyllableMap");
            if (asset == null) return; // 没数据就整个功能静默停用（按钮开了也只是不拆分）
            var data = JsonUtility.FromJson<SyllableMapData>(asset.text);
            if (data?.entries == null) return;
            foreach (var e in data.entries)
            {
                if (e == null || string.IsNullOrEmpty(e.word) || string.IsNullOrEmpty(e.my_syllable)) continue;
                MySyllable ms;
                try { ms = JsonUtility.FromJson<MySyllable>(e.my_syllable); } catch { continue; }
                if (ms == null) continue;
                // 完整单词音标所有词条都记（含拼写分段对不齐而被跳过的词，如 start/greet）：
                // 拆分模式下这类词整词一条下划线 + 完整音标（参考 app 行为，2026-11-10）
                if (!string.IsNullOrEmpty(ms.uk))
                    fullIpaByWord[e.word.ToLowerInvariant()] = ms.uk.Replace("-", "");
                if (string.IsNullOrEmpty(ms.ws)) continue;
                var sylls = new List<string>(ms.ws.Split('-'));
                // 拼写分段必须与单词字母逐一对齐（breakfast → break+fast），对不齐的词不做拆分
                if (string.Concat(sylls).ToLowerInvariant() != e.word.ToLowerInvariant()) continue;
                var sd = new SyllableData { wid = e.wid, sylls = sylls };
                if (!string.IsNullOrEmpty(ms.uk))
                {
                    var ipas = new List<string>(ms.uk.Split('-'));
                    if (ipas.Count == sylls.Count) sd.ipas = ipas; // 段数对不上 → 退回显示拼写分段
                }
                if (!string.IsNullOrEmpty(e.oldSpeech_en_ryan_split))
                {
                    try
                    {
                        var segs = JsonUtility.FromJson<SegList>("{\"items\":" + e.oldSpeech_en_ryan_split + "}").items;
                        if (segs != null)
                            sd.segs = segs.FindAll(g => g != null && !string.IsNullOrEmpty(g.t) &&
                                                        g.t.ToLowerInvariant() != e.word.ToLowerInvariant()); // 去掉首尾整词条目
                    }
                    catch { }
                }
                syllableByWord[e.word.ToLowerInvariant()] = sd;
            }
        }

        // ───────────────────────── 在线数据（jtsee） ─────────────────────────

        // 当前单词的在线数据；无数据（未就绪/解析失败被跳过）返回 null
        JtseeWordData.OnlineWord CurrentOnline =>
            useOnlineData && onlineWords != null && session != null &&
            onlineWords.TryGetValue(session.CurrentWord.ToLowerInvariant(), out var od) ? od : null;

        // 词库/单词详情全部在线拉取：courses/<unitId>.json → 逐词 words/word_<wid>.json → 解密 → 建词库
        // 每个请求内部重试 3 次；单元数据 3 次仍失败 → 不再尝试，弹窗提示"获取数据失败，请刷新重试"
        //（点弹窗"重试"重新拉取，WebGL 刷新页面等效）；个别单词失败 → 仅跳过该词，不弹窗
        const int FetchAttempts = 3;          // 单次请求重试次数
        const float FetchRetryDelay = 1.5f;   // 请求级重试间隔（秒）

        IEnumerator InitOnline()
        {
            int gen = onlineGen; // 代际快照：外部改 unitId 后本协程结果作废
            float t0 = Time.realtimeSinceStartup;
            onlineWords = new Dictionary<string, JtseeWordData.OnlineWord>();
            var wordJsonCache = new Dictionary<int, string>(); // 解密后的单词 JSON 明文（写缓存用）

            // ── 缓存秒显（2026-11-11）：上次成功拉到的解密数据落盘 → 本次启动直接显示单词+拼读，
            // 与手势同步出现；网络随后校验刷新。数据始终以网络为准：成功拉取后覆盖写缓存，
            // 拉取失败仍走"重试 3 次 → 弹窗"，绝不把缓存当作失败回退（用户 2026-11-09 要求）。
            var cached = JtseeWordData.LoadCache(unitId);
            if (cached != null && !string.IsNullOrEmpty(cached.courseJson))
            {
                var cachedEntries = TryParseCourse(cached.courseJson);
                if (cachedEntries != null && cachedEntries.Count > 0)
                {
                    int cachedWords = 0;
                    foreach (var cw in cached.words ?? new List<JtseeWordData.UnitCacheWord>())
                    {
                        if (cw == null || string.IsNullOrEmpty(cw.json)) continue;
                        var od = TryParseWord(cw.json);
                        if (od == null) continue;
                        onlineWords[od.word.ToLowerInvariant()] = od;
                        wordJsonCache[cw.wid] = cw.json;
                        cachedWords++;
                    }
                    var cachedList = BuildSessionFromEntries(cachedEntries);
                    if (cachedList.Count > 0)
                        Debug.Log($"[WordTrail] 缓存秒显：{cachedList.Count} 词（拼读数据 {cachedWords} 条）已就绪，单词与手势同步出现；网络校验中…耗时 {(Time.realtimeSinceStartup - t0) * 1000f:F0} ms");
                }
            }

            // ── 网络拉取单元 JSON（复用启动预热请求，失败再走完整重试；带分段计时） ──
            string courseUrl = JtseeWordData.CourseJsonUrl(unitId);
            string courseJson = null;
            float netAt = Time.realtimeSinceStartup;
            if (prewarmedCourseReq != null && prewarmedCourseOp != null &&
                prewarmedCourseReq.url == courseUrl)
            {
                yield return FetchPrewarmedCourse(prewarmedCourseReq, prewarmedCourseOp, v => courseJson = v);
                if (courseJson == null)
                    yield return FetchText(courseUrl, v => courseJson = v); // 预热请求失败 → 走完整重试
            }
            else
                yield return FetchText(courseUrl, v => courseJson = v);
            Debug.Log($"[WordTrail] 单元 JSON 下载：{(Time.realtimeSinceStartup - netAt) * 1000f:F0} ms");

            var entries = TryParseCourse(courseJson);
            if (entries == null || entries.Count == 0)
            {
                Debug.LogError("[WordTrail] 在线单元数据拉取失败（已重试），弹窗提示。", this);
                ShowFetchFailDialog();
                yield break;
            }
            Debug.Log($"[WordTrail] 单元 JSON 解密+解析：{(Time.realtimeSinceStartup - netAt) * 1000f:F0} ms");
            if (gen != onlineGen) { Debug.Log("[WordTrail] InitOnline 已过期（unitId 被外部更改）→ 丢弃本轮单元结果。"); yield break; }
            JtseeWordData.SaveCache(unitId, courseJson, null); // 单元明文先落盘（单词部分后续增量更新）

            // ── 词表校验：无缓存 session → 用网络词表建；已有缓存 session：
            //    词表一致 → 不动（用户可能已在打字）；不一致且未动过 → 重建；已动过 → 下轮生效 ──
            var netList = FilterPureLetterWords(entries);
            if (session == null)
            {
                if (netList.Count == 0)
                {
                    Debug.LogError("[WordTrail] 单元内无纯字母词条（全部被过滤），弹窗提示。", this);
                    ShowFetchFailDialog();
                    yield break;
                }
                BuildSessionFromEntries(entries);
            }
            else if (!netList.SequenceEqual(currentWordList))
            {
                if (session.Position == 0 && session.CompletedWords == 0 && !session.Submitted)
                {
                    Debug.Log("[WordTrail] 网络词表与缓存不一致且尚未开打 → 按网络词表重建。");
                    BuildSessionFromEntries(entries);
                }
                else
                    Debug.LogWarning("[WordTrail] 网络词表已更新，本轮继续用缓存词表，下次启动生效。");
            }
            else
                Debug.Log("[WordTrail] 网络词表与缓存一致 → 沿用当前 session。");
            Debug.Log($"[WordTrail] 单词显示总耗时：{(Time.realtimeSinceStartup - t0) * 1000f:F0} ms");

            // ── 单词详情并发拉取（全部请求同时发出；缓存命中的词也重新拉取校验新鲜度）──
            var loadList = entries.Where(e => !e.english.Any(c => !char.IsLetter(c))).ToList();
            int loaded = 0;
            int remaining = loadList.Count;
            for (int i = 0; i < loadList.Count; i++)
            {
                var idx = i; // 闭包捕获固定下标（仅用于请求发起），到达即入库无需保序
                StartCoroutine(LoadOnlineWord(loadList[idx], (od, decodedJson) =>
                {
                    if (gen != onlineGen) return; // unitId 已被外部更改 → 旧词详情作废
                    remaining--;
                    if (od == null) return; // 拉取/解析失败已在 LoadOnlineWord 内警告
                    loaded++;
                    if (decodedJson != null) wordJsonCache[loadList[idx].wid] = decodedJson;
                    string key = od.word.ToLowerInvariant();
                    onlineWords[key] = od; // 网络数据为准，覆盖缓存版
                    Debug.Log($"[WordTrail] 在线词详情 {loaded}/{loadList.Count}：{od.word}（wid {od.wid}，{(od.isRealVoice ? "真人" : "非真人")}英音，{(od.Valid ? "音节/时间段匹配" : "无拆分数据")}）");
                    // 详情恰为当前正在显示的词且还没有拼读叠加层 → 立即补建下划线/音标/音节双色
                    if (session != null && !session.Finished &&
                        string.Equals(session.CurrentWord.ToLowerInvariant(), key) &&
                        phonicsOverlay == null)
                        RebuildPhonicsOverlay();
                }));
            }
            while (remaining > 0) yield return null; // 等全部详情落地（只为写缓存/汇总/全失败判定）
            if (gen != onlineGen) { Debug.Log("[WordTrail] InitOnline 已过期（unitId 被外部更改）→ 丢弃本轮词详情结果。"); yield break; }
            JtseeWordData.SaveCache(unitId, courseJson, wordJsonCache); // 全量覆盖写缓存
            if (loaded == 0)
            {
                Debug.LogError("[WordTrail] 全部单词详情拉取失败（已重试），弹窗提示。", this);
                ShowFetchFailDialog();
            }
            else
                Debug.Log($"[WordTrail] 单词详情全部就绪：{loaded}/{loadList.Count}，在线总耗时 {(Time.realtimeSinceStartup - t0) * 1000f:F0} ms，音频走 URL 流式播放。");
        }

        // ───────────────────────── 在线数据装配辅助 ─────────────────────────

        // 单元 JSON 明文 → CourseEntry 列表（解密/解析异常只警告返回 null）
        List<JtseeWordData.CourseEntry> TryParseCourse(string courseJson)
        {
            if (string.IsNullOrEmpty(courseJson)) return null;
            try { return JtseeWordData.ParseCourse(JtseeWordData.Decode(courseJson)); }
            catch (Exception ex) { Debug.LogWarning($"[WordTrail] 单元 JSON 解密失败：{ex.Message}"); return null; }
        }

        // 单词 JSON 明文 → OnlineWord（异常/失败返回 null）
        JtseeWordData.OnlineWord TryParseWord(string wordJson)
        {
            try { return JtseeWordData.ParseWord(JtseeWordData.Decode(wordJson), out _); }
            catch { return null; }
        }

        // 过滤纯字母词（LetterCatalog 无撇号/空格字形，"starter unit"/"you're welcome" 之类跳过）；
        // 被过滤的词条打日志，避免"就绪 n/总数"看起来少词
        List<string> FilterPureLetterWords(List<JtseeWordData.CourseEntry> entries)
        {
            foreach (var e in entries.Where(e => e.english.Any(c => !char.IsLetter(c))))
                Debug.Log($"[WordTrail] 跳过非纯字母词条：{e.english}（wid {e.wid}，含空格/符号，LetterCatalog 无法渲染）");
            return entries.Where(e => !e.english.Any(c => !char.IsLetter(c))).Select(e => e.english).ToList();
        }

        // 用词条列表建 session 并立即显示第一个词（缓存秒显/网络重建共用）：
        // 返回过滤后的词表（可能为 0 → 调用方判空弹窗）。重复调用先拆旧进度条。
        List<string> BuildSessionFromEntries(List<JtseeWordData.CourseEntry> entries)
        {
            CollectMeanings(entries); // 中文释义表（课程 JSON 的 chinese 字段，仅在线数据提供）
            var wordTexts = FilterPureLetterWords(entries);
            currentWordList = new List<string>(wordTexts);
            if (wordTexts.Count == 0) return wordTexts;
            if (randomOrder) ShuffleList(wordTexts, Environment.TickCount); // 打乱顺序（仍只一轮）
            // mode 2 = 打完一轮即止，不循环（2026-11-10 用户要求）
            HideAllLearned(); // 重建词库 → 清掉上一轮的完课提示
            if (progressBar != null) Destroy(progressBar.gameObject); // 缓存→网络重建时防重复建进度条
            session = new WordPracticeSession(wordTexts.ToArray(), 2, Environment.TickCount);
            AnchorWord();
            progressBar = WordProgressBar.Create();
            UpdateProgressBar();
            PrepareCurrentWord();
            return wordTexts;
        }

        // 课程词条 → 中文释义表（chinese 字段，如 "int. 噢；啊；哎哟"）；
        // 缓存秒显与网络重建都会经过 BuildSessionFromEntries → 两路都覆盖。
        void CollectMeanings(List<JtseeWordData.CourseEntry> entries)
        {
            if (entries == null) return;
            foreach (var e in entries)
                if (e != null && !string.IsNullOrEmpty(e.english) && !string.IsNullOrEmpty(e.chinese))
                    meaningByWord[e.english.ToLowerInvariant()] = e.chinese;
        }

        // 完词脉冲弹出中文释义：预建在音标带下方（scale=0 隐藏），OnWordPulseStarted 时回弹弹出；
        // 无释义数据（本地模式/词条缺 chinese）不建。切词随 UpdatePhonicsOverlay 重建 → 换词自动复位。
        void ShowMeaningPopup()
        {
            if (meaningPopup != null) Destroy(meaningPopup.gameObject);
            meaningPopup = null;
            if (session == null || session.Finished || trails == null || trails.WordRoot == null) return;
            string w = session.CurrentWord.ToLowerInvariant();
            if (!meaningByWord.TryGetValue(w, out var meaning) || string.IsNullOrEmpty(meaning)) return;
            if (!trails.TryGetWordLocalMetrics(out _, out _, out float bottomY, out float letterH)) return;
            float textH = letterH * 0.30f; // 释义字高（略大于音标 0.25，中文笔画多需要更大才清晰）
            float y;
            if (phonicsOverlay != null && phonicsOverlay.Built)
                y = phonicsOverlay.BandBottomLocal - letterH * 0.10f - textH * 0.5f; // 音标带下方（用户红圈位置）
            else
                y = bottomY - letterH * 0.12f - textH * 0.5f; // 无叠加层 → 字母下方
            meaningPopup = WordMeaningPopup.Create(trails.WordRoot, meaning, textH, Color.white);
            meaningPopup.transform.localPosition = new Vector3(0f, y, -0.03f);
        }

        // 预热请求收尾：读完结果并清理静态引用（失败返回 null，由调用方走完整重试）
        IEnumerator FetchPrewarmedCourse(UnityWebRequest req, UnityWebRequestAsyncOperation op, Action<string> done)
        {
            prewarmedCourseReq = null;
            prewarmedCourseOp = null;
            yield return op;
            bool ok = req.result == UnityWebRequest.Result.Success;
            if (ok) done(req.downloadHandler.text); // 先取文本再释放
            else Debug.LogWarning($"[WordTrail] 预热单元请求失败：{req.error}，改走完整重试。");
            req.Dispose();
        }

        // 弹"获取数据失败，请刷新重试"弹窗；点重试 → 重新走 InitOnline（先防重复弹窗）
        void ShowFetchFailDialog()
        {
            if (fetchFailDialog != null) return; // 已有弹窗不重复建
            fetchFailDialog = FetchFailDialog.Create("获取数据失败，请刷新重试", () =>
            {
                fetchFailDialog = null;
                StartCoroutine(InitOnline());
            });
        }

        // 单词详情拉取+解析协程（可并发启动多个）：完成时回调 (od, 解密明文 json)；
        // 失败 od=null（json=null），仅警告跳过
        IEnumerator LoadOnlineWord(JtseeWordData.CourseEntry e, Action<JtseeWordData.OnlineWord, string> done)
        {
            string wj = null;
            yield return FetchText(JtseeWordData.WordJsonUrl(e.wid), v => wj = v);
            JtseeWordData.OnlineWord od = null;
            string decoded = null;
            if (wj != null)
            {
                string reason = null;
                try
                {
                    decoded = JtseeWordData.Decode(wj);
                    od = JtseeWordData.ParseWord(decoded, out reason);
                }
                catch (Exception ex) { reason = ex.Message; }
                if (od == null) { Debug.LogWarning($"[WordTrail] word_{e.wid}（{e.english}）解析失败：{reason}，跳过"); decoded = null; }
            }
            else
                Debug.LogWarning($"[WordTrail] word_{e.wid}（{e.english}）拉取失败，跳过该词");
            done(od, decoded);
        }

        IEnumerator FetchText(string url, Action<string> done)
        {
            for (int attempt = 1; attempt <= FetchAttempts; attempt++)
            {
                using var req = UnityWebRequest.Get(url);
                req.timeout = 10; // 挂死超时兜底（默认无限等待会让重试机制失效）
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success) { done(req.downloadHandler.text); yield break; }
                if (attempt < FetchAttempts)
                {
                    Debug.LogWarning($"[WordTrail] 拉取失败（第 {attempt}/{FetchAttempts} 次）{url} : {req.error}，{FetchRetryDelay}s 后重试…");
                    yield return new WaitForSeconds(FetchRetryDelay);
                }
                else
                    Debug.LogWarning($"[WordTrail] 拉取失败（已重试 {FetchAttempts} 次）{url} : {req.error}");
            }
            done(null);
        }

        // 旧本地模式：硬编码词表 + Resources 映射/音频（仅 useOnlineData=false 时使用；在线失败不回退这里）
        void InitLocalSession()
        {
            // mode 2 = 打完一轮即止，不循环（2026-11-10 用户要求）
            var localWords = randomOrder ? ShuffleList(new List<string>(words), Environment.TickCount).ToArray() : words;
            HideAllLearned(); // 重建词库 → 清掉上一轮的完课提示
            session = new WordPracticeSession(localWords, 2, Environment.TickCount);
            if (session.WordCount == 0)
            {
                Debug.LogWarning("[WordTrail] 单词库为空/无合法项，单词打字特效禁用。", this);
                Instance = null;
                enabled = false;
                return;
            }
            AnchorWord();
            progressBar = WordProgressBar.Create();
            UpdateProgressBar();
            PrepareCurrentWord();
        }

        // 按相对路径取音频（如 "uk/ɪ"、"word_en/12" → Resources/Audio/...），带缓存（含 miss）
        AudioClip GetVoiceClip(string relative)
        {
            if (voiceClipCache.TryGetValue(relative, out var c)) return c;
            c = Resources.Load<AudioClip>("Audio/" + relative);
            voiceClipCache[relative] = c; // null 也缓存，避免每词重复 IO
            return c;
        }

        // WebGL 音频预热：由 KeyClickSound 在首次交互时调用。解锁 autoplay + 提前建好 voiceSource 声道。
        // fromClick=true（鼠标点击，引擎此刻已解锁，后续按键音可立即播）；
        // fromClick=false（按键同帧，引擎刚唤醒，首个音标音仍走延后播放兜底）。
        public void WarmVoice(bool fromClick)
        {
            if (fromClick) voiceWarmed = true;
            if (voiceSource == null) return;
            if (useOnlineData)
            {
                var od = CurrentOnline;
                if (od != null) StartCoroutine(LoadAndWarmWhole(od)); // 提前拉整词音频并零音量预热
                return;
            }
            if (phonicsByWord != null && session != null &&
                phonicsByWord.TryGetValue(session.CurrentWord.ToLowerInvariant(), out var e) &&
                !string.IsNullOrEmpty(e.audio))
            {
                var clip = GetVoiceClip(e.audio);
                if (clip != null) voiceSource.PlayOneShot(clip, 0f); // 零音量预热，不产生可听声音
            }
        }

        IEnumerator LoadAndWarmWhole(JtseeWordData.OnlineWord od)
        {
            AudioClip clip = null;
            yield return OnlineAudioCache.Load(od.fullUrl, c => clip = c);
            if (clip != null && voiceSource != null) voiceSource.PlayOneShot(clip, 0f);
        }

        // 播音标/单词音：未预热（首次按键那一帧）时延后 0.1s，错开音频引擎唤醒帧，否则声音会被浏览器吞掉
        void PlayVoice(AudioClip clip)
        {
            if (clip == null || voiceSource == null) return;
            if (voiceWarmed) { voiceSource.PlayOneShot(clip, phonicsVolume); return; }
            StartCoroutine(PlayVoiceDelayed(clip));
        }

        IEnumerator PlayVoiceDelayed(AudioClip clip)
        {
            yield return new WaitForSeconds(FirstVoiceDelay);
            voiceWarmed = true; // 此刻引擎已在运行，后续声音可立即播
            if (voiceSource != null) voiceSource.PlayOneShot(clip, phonicsVolume);
        }

        // 打完某字母（index = 已输入的字母下标）时：若它是某个拼读块/音节的最后一个字母，读对应音频
        void PlayPhonemeFor(int index)
        {
            if (!PhonicsEnabled) return;
            if (voiceSource == null || session == null) return;
            if (useOnlineData) { PlayPhonemeForOnline(index); return; }
            string w = session.CurrentWord.ToLowerInvariant();
            // 音节拆分模式（SyllablesplitButton 开启）：按完一个音节的最后一个字母 →
            // 读英式拆分音频（split_en/split_<wid>.mp3）里该音节的 s~s+d 片段
            if (SyllableSplitEnabled && syllableByWord != null &&
                syllableByWord.TryGetValue(w, out var sd) && sd.sylls.Count > 1)
            {
                int idx = 0;
                for (int i = 0; i < sd.sylls.Count; i++)
                {
                    idx += sd.sylls[i].Length;
                    if (idx - 1 != index) continue; // 音节末字母触发
                    // 优先按音节文本匹配分段（battery 等词条段数与音节数不一致时序号会错位），
                    // 对不上再退回按序号取
                    SyllableSeg seg = null;
                    if (sd.segs != null)
                    {
                        string syllText = sd.sylls[i].Trim().ToLowerInvariant();
                        foreach (var g in sd.segs)
                            if (g?.t != null && g.t.Trim().ToLowerInvariant() == syllText) { seg = g; break; }
                        if (seg == null && i < sd.segs.Count) seg = sd.segs[i];
                    }
                    if (seg != null) { PlaySplitSegment(sd, seg); return; } // 无分段/裁剪失败 → 静默跳过
                    return;
                }
                return;
            }
            if (phonicsByWord == null) return;
            if (!phonicsByWord.TryGetValue(w, out var e)) return;
            foreach (var chunk in e.phonemes)
            {
                if (chunk?.positions == null || chunk.positions.Count == 0) continue;
                if (chunk.positions[chunk.positions.Count - 1] != index) continue; // 块内最后一个字母打完才发声
                var clip = GetVoiceClip("uk/" + chunk.sound);
                PlayVoice(clip);
            }
        }

        // 在线模式发音：
        // 音节拆分模式 → 打完某音节最后一个字母播该音节的在线拆分音频片段（按位置对应，缺失静默跳过）；
        // 普通拼读模式 → 发声来源由 phonemeFromOnline 决定：
        //                 true = 实验通道，播网站拆分音频该块所在音节片段（jtsee 线上无音素级音源，
        //                        音节段是最小粒度，可能与音素块不对齐，如打完 t 播 nit）；
        //                 false = 本地音素音（Audio/uk/<sound>.mp3，逐级前缀回退）
        void PlayPhonemeForOnline(int index)
        {
            var od = CurrentOnline;
            if (od == null) return;
            if (SyllableSplitEnabled && od.sylls.Count > 1)
            {
                int cum = 0;
                for (int i = 0; i < od.sylls.Count; i++)
                {
                    cum += od.sylls[i].Length;
                    if (cum - 1 != index) continue; // 音节末字母触发
                    var seg = od.SegmentForSyllable(i); // 按位置取，不按 t 文本
                    if (seg != null) PlaySplitSegmentOnline(od, seg, index); // 加载/裁剪失败 → 静默跳过
                    return;
                }
                return;
            }
            // 拼读模式：找以下标 index 结尾的拼读块 → 发声来源由 phonemeFromOnline 决定
            foreach (var c in od.phonics)
            {
                if (c?.positions == null || c.positions.Count == 0) continue;
                if (c.positions[c.positions.Count - 1] != index) continue;
                if (phonemeFromOnline)
                {
                    // 实验通道：不用本地 Audio/uk 音素库 → 播网站拆分音频中该块末字母所在音节的片段
                    //（线上无音素级音源，音节段是最小粒度；无拆分音频的词静默跳过）
                    var seg = od.SegmentForLetter(index);
                    if (seg != null) PlaySplitSegmentOnline(od, seg, index);
                    return;
                }
                PlayVoice(GetPhonemeClip(c.sound)); // 原行为：本地音素音（缺失逐级前缀回退）
                return;
            }
        }

        // 本地音素音频解析（Audio/uk/<sound>.mp3），带多级回退：
        // ① 精确匹配 → ② ASCII g ↔ IPA ɡ（线上数据混用）→ ③ 逐级去掉末尾字符（wʌn→wʌ、el→e、siː→s）
        // 全部未命中返回 null（调用方静默，绝不回退整词）。命中回退档时打警告便于发现数据缺口。
        AudioClip GetPhonemeClip(string sound)
        {
            if (string.IsNullOrEmpty(sound)) return null;
            var clip = GetVoiceClip("uk/" + sound);
            if (clip != null) return clip;

            var alt = sound.Contains('g') ? sound.Replace('g', 'ɡ')
                    : sound.Contains('ɡ') ? sound.Replace('ɡ', 'g') : null;
            if (!string.IsNullOrEmpty(alt)) clip = GetVoiceClip("uk/" + alt);
            if (clip != null) return clip;

            for (int len = sound.Length - 1; len >= 1; len--)
            {
                var candidate = sound.Substring(0, len);
                clip = GetVoiceClip("uk/" + candidate);
                if (clip != null)
                {
                    Debug.LogWarning($"[WordTrail] 本地音素库无 uk/{sound}.mp3，回退播放 uk/{candidate}.mp3", this);
                    return clip;
                }
            }
            return null;
        }

        // 播在线拆分音频的 [startMs, endMs] 片段（AudioClip 从 URL 流式加载，带内存缓存）
        void PlaySplitSegmentOnline(JtseeWordData.OnlineWord od, JtseeWordData.SlicedSeg seg, int index)
        {
            if (od == null || seg == null) return; // 无拆分数据（单词节/段数不匹配）→ 静默跳过
#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL 专用通道：Unity 的 GetData 对 CompressedInMemory clip 必失败、time seek 不可靠，
            // 改走浏览器 Web Audio API（decodeAudioData + start(0, offset, dur)，原生支持 offset 播放）
            PlaySplitSegmentJS(od, seg);
#else
            if (splitSource == null)
            {
                splitSource = gameObject.AddComponent<AudioSource>();
                splitSource.playOnAwake = false;
                splitSource.spatialBlend = 0f; // 与本地模式同款 2D
            }
            string expectWord = session != null ? session.CurrentWord.ToLowerInvariant() : od.word.ToLowerInvariant();
            StartCoroutine(LoadAndPlaySplitSegment(od, seg, expectWord, index));
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        // ── Web Audio 通道（SplitAudio.jslib）──
        // 解码完成后 JS 侧直接播放（本工程模板没有全局 unityInstance，SendMessage 回调不可用——
        // 曾因 "unityInstance is not defined" 抛异常导致 C# 收不到就绪回调、音节全静音）
        [DllImport("__Internal")] static extern void SplitAudioPlayAsync(string url, string key, float offset, float dur, float volume);

        void PlaySplitSegmentJS(JtseeWordData.OnlineWord od, JtseeWordData.SlicedSeg seg)
        {
            string key = od.splitUrl + ":" + seg.startMs + "_" + seg.endMs;
            float durSec = Mathf.Max(0.05f, (seg.endMs - seg.startMs) / 1000f);
            // 首次播放含异步 decode：整词延迟读的调度基于 splitStopAt，预加 decode 耗时余量避免叠音
            splitStopAt = Time.time + durSec + 0.5f;
            SplitAudioPlayAsync(od.splitUrl, key, seg.startMs / 1000f, durSec, phonicsVolume);
        }
#endif

        IEnumerator LoadAndPlaySplitSegment(JtseeWordData.OnlineWord od, JtseeWordData.SlicedSeg seg, string expectWord, int index)
        {
            AudioClip clip = null;
            yield return OnlineAudioCache.Load(od.splitUrl, c => clip = c);
            if (clip == null || splitSource == null || session == null) yield break;
            if (!string.Equals(session.CurrentWord.ToLowerInvariant(), expectWord)) yield break; // 加载期间已切词
            // 不用 AudioSource.time seek（WebGL 上 seek 会打断播放→静音，或失效→从 0 播出整词段）：裁出子片段直接播
            float durSec = Mathf.Max(0.05f, (seg.endMs - seg.startMs) / 1000f);
            var sub = GetSplitSubClip(clip, od.splitUrl + ":" + seg.startMs + "_" + seg.endMs,
                seg.startMs / 1000f, durSec);
            if (sub == null)
            {
                // 编辑器/桌面端：GetData 对压缩 clip 可能失败 → 回退 seek（桌面平台 seek 可靠）
                splitSource.clip = clip;
                splitSource.volume = phonicsVolume;
                splitSource.Play();
                splitSource.time = Mathf.Clamp(seg.startMs / 1000f, 0f, Mathf.Max(0f, clip.length - 0.01f));
                splitStopAt = Time.time + durSec;
                yield break;
            }
            if (!voiceWarmed) yield return new WaitForSeconds(FirstVoiceDelay); // WebGL 音频引擎未就绪 → 延后播，否则被吞
            voiceWarmed = true;
            splitSource.clip = sub;
            splitSource.volume = phonicsVolume;
            splitSource.time = 0f;
            splitSource.Play();
            splitStopAt = Time.time + sub.length;
        }

        // 播在线完整音频（整词）；repeat ≥ 2 时播完等 clip 时长 + 0.35s 间隔再播一遍
        // （用于无 y_phonics 显示数据的词，如 Ella：无拼读/音节/音标显示 → 完词读两遍补偿）
        void PlayOnlineWhole(JtseeWordData.OnlineWord od, int repeat = 1)
        {
            if (od == null || voiceSource == null) return;
            StartCoroutine(LoadAndPlayWhole(od, repeat));
        }

        IEnumerator LoadAndPlayWhole(JtseeWordData.OnlineWord od, int repeat = 1)
        {
            AudioClip clip = null;
            yield return OnlineAudioCache.Load(od.fullUrl, c => clip = c);
            if (clip == null || voiceSource == null || session == null) yield break;
            if (!string.Equals(session.CurrentWord.ToLowerInvariant(), od.word.ToLowerInvariant())) yield break;
            for (int i = 0; i < Mathf.Max(1, repeat); i++)
            {
                PlayVoice(clip);
                if (i < repeat - 1)
                    yield return new WaitForSeconds(clip.length + 0.35f); // 间隔略大于音频时长，两遍听感分明
                if (session == null ||
                    !string.Equals(session.CurrentWord.ToLowerInvariant(), od.word.ToLowerInvariant()))
                    yield break; // 期间单词已切换 → 不再读第二遍
            }
        }

        // 播英式拆分音频里 [s, s+d] 毫秒的片段（播够时长即停，用专用音源以便中途截断）。
        // split mp3 结构 = [整词, 音节1..N, 整词]：WebGL 上 AudioSource.time seek 会偶发失效，
        // 从 0 播出的正是开头的整词段——即"没打完就全读"的根因。
        // 所以不再 seek：首次播放时用 GetData 把音节段裁成独立子片段（缓存），直接整段播放。
        void PlaySplitSegment(SyllableData sd, SyllableSeg seg)
        {
            if (splitSource == null)
            {
                splitSource = gameObject.AddComponent<AudioSource>();
                splitSource.playOnAwake = false;
                splitSource.spatialBlend = 0f; // 与 voiceSource 同款 2D
            }
            var srcClip = GetVoiceClip("split_en/split_" + sd.wid);
            if (srcClip == null) return; // 没拷到该词的拆分音频 → 静默跳过
            var clip = GetSplitSubClip(srcClip, sd.wid + ":" + seg.s + "_" + seg.d,
                seg.s / 1000f, Mathf.Max(0.05f, seg.d / 1000f));
            if (clip == null) return; // 裁剪失败 → 静默跳过
            if (!voiceWarmed) { StartCoroutine(PlaySplitDelayed(clip)); return; } // WebGL 首键帧音频引擎未就绪 → 延后播，否则被吞
            splitSource.clip = clip;
            splitSource.volume = phonicsVolume;
            splitSource.time = 0f;
            splitSource.Play();
            splitStopAt = Time.time + clip.length;
        }

        // 音频引擎未预热时的延迟播放（与 PlayVoiceDelayed 同款保护）
        IEnumerator PlaySplitDelayed(AudioClip clip)
        {
            yield return new WaitForSeconds(FirstVoiceDelay);
            voiceWarmed = true;
            if (splitSource == null) yield break;
            splitSource.clip = clip;
            splitSource.volume = phonicsVolume;
            splitSource.time = 0f;
            splitSource.Play();
            splitStopAt = Time.time + clip.length;
        }

        // 取（并缓存）音节子片段：从源音频 [start, start+dur] 裁出独立 AudioClip，彻底绕开 seek。
        // src 直接传 AudioClip：本地=GetVoiceClip 结果；在线=OnlineAudioCache.Load 拉回的 clip
        //（不能传路径再 Resources.Load——在线 URL 不是资源路径，必失败 → 音节全静音）
        AudioClip GetSplitSubClip(AudioClip src, string cacheKey, float startSec, float durSec)
        {
            if (src == null) return null;
            if (splitSegClipCache.TryGetValue(cacheKey, out var cached) && cached != null) return cached;
            int channels = src.channels > 0 ? src.channels : 1;
            int freq = src.frequency > 0 ? src.frequency : 44100;
            int totalSamples = src.samples > 0 ? src.samples : Mathf.CeilToInt(src.length * freq);
            int offset = Mathf.Clamp(Mathf.RoundToInt(startSec * freq), 0, Mathf.Max(0, totalSamples - 1));
            int count = Mathf.Clamp(Mathf.RoundToInt(durSec * freq), 1, totalSamples - offset);
            var data = new float[count * channels];
            if (!src.GetData(data, offset))
            {
                Debug.LogWarning("[WordTrail] 音节子片段裁剪失败（GetData）：split seg " + cacheKey, this);
                return null;
            }
            var sub = AudioClip.Create("splitseg_" + cacheKey, count, channels, freq, false);
            sub.SetData(data, 0);
            if (splitSegClipCache.Count > 400) splitSegClipCache.Clear(); // 防长期运行缓存膨胀
            splitSegClipCache[cacheKey] = sub;
            return sub;
        }

        // 拼完单词（完词脉冲开始，按 Enter 之前）：读出整词发音。
        // 音节拆分模式下多音节词打完最后一个字母时会先读该音节音标 → 整词音延迟到音节音播完后再读
        void OnWordPulseStarted(int wordId)
        {
            if (meaningPopup != null) meaningPopup.PopUp(); // 完词放大 → 中文释义回弹弹出（2026-11-10）
            if (!PhonicsEnabled) return;
            if (voiceSource == null || session == null || session.Finished) return;
            string w = session.CurrentWord.ToLowerInvariant();
            if (useOnlineData)
            {
                var od = CurrentOnline;
                if (od == null) return;
                // 无拼读显示数据（y_phonics 缺失，如 Ella）的词：无任何音标/下划线可播 → 完词整词读两遍补偿
                int wholeRepeat = od.phonics.Count == 0 ? 2 : 1;
                // 普通拼读模式打完最后一个音节会先播拆分段（PlayPhonemeForOnline）→
                // 该段仍在播（splitStopAt 未到）时等播完再读整词，避免叠音；拆分模式同理
                if ((SyllableSplitEnabled && od.sylls.Count > 1) || splitStopAt > Time.time)
                {
                    // 调度：等最后一个音节片段播完（splitStopAt）再读整词，避免叠音
                    pendingWholeWord = w;
                    pendingWholeWordOd = od;
                    pendingWholeWordRepeat = wholeRepeat;
                    pendingWholeWordAt = (splitStopAt > Time.time ? splitStopAt : Time.time) + 0.2f;
                    return;
                }
                PlayOnlineWhole(od, wholeRepeat);
                return;
            }
            if (phonicsByWord == null) return;
            if (SyllableSplitEnabled && syllableByWord != null &&
                syllableByWord.TryGetValue(w, out var sdCheck) && sdCheck.sylls.Count > 1)
            {
                // 调度：等最后一个音节片段播完（splitStopAt）再读整词，避免叠音
                pendingWholeWord = w;
                pendingWholeWordAt = (splitStopAt > Time.time ? splitStopAt : Time.time) + 0.2f;
                return;
            }
            if (!phonicsByWord.TryGetValue(w, out var e)) return;
            var clip = GetVoiceClip(e.audio);
            PlayVoice(clip);
        }

        // ───────────────────────── 主循环 ─────────────────────────

        void Update()
        {
            if (trails == null || session == null) return;
            if (cam == null) cam = Camera.main;
            // 音节片段播够 d 毫秒 → 截断（拆分音频里音节之间有静音间隔，不截会串音）；
            // 截止前若发现播放位置掉回片段起点之前 → seek 失效（WebGL 偶发，从 0 播=整词音频）→ 重新定位
            if (splitSource != null && splitSource.isPlaying &&
                Time.time >= splitStopAt) splitSource.Stop(); // 子片段自然播完即停（保险丝）

            // 拆分模式完词：整词音调度到期 → 播放（单词已切换则作废）
            if (pendingWholeWord != null)
            {
                if (!string.Equals(session.CurrentWord.ToLowerInvariant(), pendingWholeWord))
                {
                    pendingWholeWord = null;
                    pendingWholeWordOd = null;
                }
                else if (Time.time >= pendingWholeWordAt)
                {
                    if (pendingWholeWordOd != null)
                        PlayOnlineWhole(pendingWholeWordOd, pendingWholeWordRepeat); // 在线：整词走 URL 完整音频
                    else if (phonicsByWord.TryGetValue(pendingWholeWord, out var pe) &&
                             !string.IsNullOrEmpty(pe.audio))
                        PlayVoice(GetVoiceClip(pe.audio));
                    pendingWholeWord = null;
                    pendingWholeWordOd = null;
                    pendingWholeWordRepeat = 1;
                }
            }

            // 单词屏幕锚定：固定在相机视野内（x 居中、y≈键盘上方），距离=键盘中心距。
            // 不用世界坐标"键盘顶部"：陡俯视角下它会投影进键帽区域被遮挡（探针 run120 实测）。
            // 首字母命中前 KeyboardSingleTrail 持续读取 targetB，命中后内部 anchorFixed 冻结（飞行终点稳定）
            AnchorWord();

            PollInput();

            // 红字滞留恢复（原项目 errorRecoveryDelay）
            if (session.HasError && !session.Submitted &&
                Time.unscaledTime - errorAt >= Mathf.Max(0f, errorRecoveryDelay))
            {
                session.ClearError();
                errorAt = float.NegativeInfinity;
            }
        }

        void LateUpdate()
        {
            if (trails == null || session == null || session.Finished) return;
            RefreshFeedback();
        }

        // ───────────────────────── 输入 ─────────────────────────

        void PollInput()
        {
            if (inputUnavailable) return;
            try
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) { TrySubmit(); return; }
                for (int k = (int)KeyCode.A; k <= (int)KeyCode.Z; k++)
                    if (Input.GetKeyDown((KeyCode)k)) OnLetterPressed((KeyCode)k);
                if (Input.GetMouseButtonDown(0)) HandleOverlayClick();
            }
            catch { inputUnavailable = true; } // 新 Input System 下旧 Input 不可用
        }

        // 点击拼读叠加层（字母/下划线/音标带）→ 播该音节的拆分音频段（jtsee 原版行为：
        // 音节与音标可点击发音）；无拆分音频/段的词回退播整词。拼读声音开关关闭时不响应。
        void HandleOverlayClick()
        {
            if (!PhonicsEnabled || phonicsOverlay == null || cam == null) return;
            var od = CurrentOnline;
            var root = trails != null ? trails.WordRoot : null;
            if (od == null || root == null) return;
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            Plane plane = new Plane(root.forward, root.position);
            float enter;
            if (!plane.Raycast(ray, out enter)) return;
            Vector3 local = root.InverseTransformPoint(ray.GetPoint(enter));
            int? ci = phonicsOverlay.ChunkIndexAtLocal(new Vector2(local.x, local.y));
            if (ci == null) return;
            var seg = od.SegmentForLetter(ci.Value); // 块起始字母所在音节 → 拆分音频段
            if (seg != null) { PlaySplitSegmentOnline(od, seg, ci.Value); return; }
            PlayOnlineWhole(od); // 无拆分音频（null.mp3 / 单音节词）→ 回退整词
        }

        void OnLetterPressed(KeyCode k)
        {
            if (session.Submitted) return; // 退场动画期间不接收
            var result = session.Input((char)('A' + (int)k - (int)KeyCode.A));
            if (result == WordPracticeSession.InputResult.Accepted)
            {
                errorAt = float.NegativeInfinity;
                int index = session.Position - 1;
                if (keyboard != null && keyboard.TryGetKeyTop(k, out var top))
                    emitOrigin.position = top; // 键帽顶面发射（飞行起点）
                trails.EmitPreparedLetter(index, emitOrigin);
                PlayPhonemeFor(index); // 打完拼读块（该块最后一个字母）→ 读音标
                UpdateProgressBar(); // 按对一个字母 → 进度条前进一步
                UpdatePhonicsOverlayProgress(); // 同步拼读块下划线填充
            }
            else if (result == WordPracticeSession.InputResult.Wrong)
            {
                errorAt = Time.unscaledTime; // 等待字母变红 + 抖动（LateUpdate 里刷新）
            }
            UpdateTargetGlow();
        }

        void TrySubmit()
        {
            if (session == null) return;
            if (!session.Submit()) return; // CanSubmit：整词输完才接受（参考视频行为）
            trails.SubmitCurrentWord();    // 触发 FlyingLetters 退场动画
            UpdateTargetGlow();            // Enter 引导光熄灭，退场期间不指向任何键
        }

        // ───────────────────────── 单词流转 ─────────────────────────

        void PrepareCurrentWord()
        {
            errorAt = float.NegativeInfinity;
            if (session.Finished) return;
            var syllStarts = CurrentSyllableStarts();
            if (!trails.PrepareWord(session.CurrentWord, upperWordWidth, syllStarts))
                Debug.LogWarning($"[WordTrail] 单词 {session.CurrentWord} 含 LetterCatalog 没有的字母，跳过。", this);
            else if (SyllableSplitEnabled && syllStarts == null)
                trails.ApplySyllableSolid(); // 拆分模式但该词无对齐音节数据 → 整词深蓝（参考 app 行为，2026-11-10）
            UpdatePhonicsOverlay(); // 拼读块下划线/音标（须在 PrepareWord 之后建，挂在单词 root 下）
            UpdateTargetGlow(); // 键盘绿光指向首字母（新词生成，恢复被挂起的发光）
        }

        // 音节拆分模式（且该词有多音节数据）→ 返回各音节起始字母下标；否则 null（单词文本走自然拼读色）
        List<int> CurrentSyllableStarts()
        {
            if (!SyllableSplitEnabled || session == null || session.Finished) return null;
            List<string> sylls;
            if (useOnlineData)
            {
                var od = CurrentOnline;
                if (od == null || od.sylls.Count <= 1) return null;
                sylls = od.sylls;
            }
            else
            {
                if (syllableByWord == null) return null;
                string w = session.CurrentWord.ToLowerInvariant();
                if (!syllableByWord.TryGetValue(w, out var sd) || sd.sylls.Count <= 1) return null;
                sylls = sd.sylls;
            }
            var starts = new List<int>();
            int total = 0;
            foreach (var s in sylls)
            {
                if (string.IsNullOrEmpty(s)) return null;
                starts.Add(total);
                total += s.Length;
            }
            if (total != session.CurrentWord.Length) return null; // 拼写分段对不齐 → 不上双色
            return starts;
        }

        // 进度条颜色固定为绿色（填充比例 = 已打完单词数 / 总单词数，见 UpdateProgressBar）
        static readonly Color ProgressBarColor = new Color(0.20f, 0.75f, 0.30f);
        // "单词已经全部学习"提示文本颜色（与进度条同款绿，传达完成态）
        static readonly Color AllLearnedColor = new Color(0.20f, 0.75f, 0.30f);

        void UpdateProgressBar()
        {
            if (progressBar == null || session == null) return;
            // 顶部进度条 = 已打完单词数 / 总单词数（2026-11-05 改，替代原字母拼写进度）
            int total = session.TotalWords;
            int done = total > 0 ? session.CompletedWords % total : 0; // 循环词库：新一轮从头填充
            if (session.Finished) done = total; // 不循环模式词库打完 → 满格
            progressBar.SetProgress(total > 0 ? (float)done / total : 0f, ProgressBarColor);
        }

        // 自然拼读叠加层：挂在 trails.PrepareWord 生成的 3D 单词 root 下（贴着彩色单词文本），
        // 每拼读块一条下划线（按块内已打对字母数填充颜色）+ 块下音标；无 WordPhonicsMap
        // 映射的词不建。切词时旧叠加层随旧词 root 一起销毁，这里每次新建。
        void UpdatePhonicsOverlay()
        {
            if (trails == null || session == null || trails.WordRoot == null) return;
            if (phonicsOverlay != null) Destroy(phonicsOverlay.gameObject); // 重建时先拆旧的（按钮切换拆分模式会重复进入）
            phonicsOverlay = null; // 显式清引用：无块路径（下方）要靠它 == null 判断释义行放位
            var chunks = new List<WordPhonicsOverlay.ChunkInfo>();
            if (!session.Finished)
            {
                string w = session.CurrentWord.ToLowerInvariant();
                // 在线模式：从解析后的单词 JSON 取拼读块/音节；本地模式：从两个映射 JSON 取
                JtseeWordData.OnlineWord od = useOnlineData && onlineWords != null &&
                                               onlineWords.TryGetValue(w, out var o) ? o : null;

                if (SyllableSplitEnabled)
                {
                    List<string> sylls = null, ipas = null;
                    if (useOnlineData)
                    {
                        if (od != null && od.sylls.Count > 1) { sylls = od.sylls; ipas = od.ipas; }
                    }
                    else if (syllableByWord != null && syllableByWord.TryGetValue(w, out var sd) && sd.sylls.Count > 1)
                    {
                        sylls = sd.sylls; ipas = sd.ipas;
                    }
                    if (sylls != null)
                    {
                        int idx = 0;
                        for (int i = 0; i < sylls.Count; i++)
                        {
                            chunks.Add(new WordPhonicsOverlay.ChunkInfo
                            {
                                start = idx,
                                count = sylls[i].Length,
                                sound = ipas != null ? ipas[i] : sylls[i],
                            });
                            idx += sylls[i].Length;
                        }
                    }
                    else
                    {
                        // 拆分模式但该词无对齐音节数据（单音节/拼写对不齐/词表缺失）→ 整词一条下划线
                        // + 完整单词音标（参考 app：start 整词 /stɑːt/、oh /əʊ/，2026-11-10）
                        string fullIpa = null;
                        if (useOnlineData) { if (od != null && !string.IsNullOrEmpty(od.ukIpa)) fullIpa = od.ukIpa; }
                        else if (fullIpaByWord != null) fullIpaByWord.TryGetValue(w, out fullIpa);
                        if (!string.IsNullOrEmpty(fullIpa))
                            chunks.Add(new WordPhonicsOverlay.ChunkInfo
                            {
                                start = 0,
                                count = session.CurrentWord.Length,
                                sound = fullIpa,
                            });
                    }
                }
                else
                {
                    List<(string sound, List<int> positions)> phonicsChunks = null;
                    if (useOnlineData)
                    {
                        // 下划线只认 y_phonics；无 y_phonics 的词（如 Ella，线上数据缺失）不建叠加层，
                        // 按用户规则改为完词后整词读两遍（见 OnWordPulseStarted 的 DoubleRead 分支）
                        if (od != null && od.phonics.Count > 0)
                            phonicsChunks = od.phonics.Select(c => (c.sound, c.positions)).ToList();
                    }
                    else if (phonicsByWord != null && phonicsByWord.TryGetValue(w, out var e) && e?.phonemes != null)
                        phonicsChunks = e.phonemes.Where(c => c != null).Select(c => (c.sound, c.positions)).ToList();

                    if (phonicsChunks != null)
                    {
                        foreach (var c in phonicsChunks)
                        {
                            if (c.positions == null || c.positions.Count == 0) continue;
                            int lo = c.positions[0], hi = c.positions[0];
                            foreach (var p in c.positions) { if (p < lo) lo = p; if (p > hi) hi = p; }
                            if (lo < 0 || hi >= session.CurrentWord.Length || hi < lo) continue;
                            chunks.Add(new WordPhonicsOverlay.ChunkInfo
                            {
                                start = lo,
                                count = hi - lo + 1,
                                sound = c.sound,
                            });
                        }
                    }
                }
            }
            // 区间去重叠（2026-11-06）：phoneme 数据可能给出跨字母的音素区间（zone 的
            // əʊ = positions[1,3] 覆盖 o..e），把中间字母自己的块（n_back = positions[2]）
            // 也圈进重叠区 → 两条下划线/两个音标叠画在同一个字母上（截图实测：ə 和 n 叠成一个字）。
            // 按 start 排序后合并重叠块：count 取并集、sound 拼接（əʊ + n_back → əʊn_back，
            // WordPhonicsOverlay 里按 '_' 截断后显示 əʊn）。
            chunks.Sort((a, b) => a.start.CompareTo(b.start));
            for (int i = chunks.Count - 1; i > 0; i--)
            {
                if (chunks[i].start >= chunks[i - 1].start + chunks[i - 1].count) continue;
                var merged = chunks[i - 1];
                merged.sound = (merged.sound ?? "") + (chunks[i].sound ?? "");
                int end = Mathf.Max(merged.start + merged.count, chunks[i].start + chunks[i].count);
                merged.count = end - merged.start;
                chunks[i - 1] = merged;
                chunks.RemoveAt(i);
            }
            if (chunks.Count == 0) { ShowMeaningPopup(); return; } // 无拼读块的词也要建释义弹出层
            var go = new GameObject("PhonicsOverlay");
            go.transform.SetParent(trails.WordRoot, false);
            phonicsOverlay = go.AddComponent<WordPhonicsOverlay>();
            if (!phonicsOverlay.Build(trails, chunks))
            {
                Destroy(go);
                phonicsOverlay = null;
                ShowMeaningPopup();
                return;
            }
            phonicsOverlay.SetProgress(session.Position);
            ShowMeaningPopup(); // 释义行位置依赖叠加层是否建成 → 建完再放
        }

        void UpdatePhonicsOverlayProgress()
        {
            if (phonicsOverlay != null && session != null) phonicsOverlay.SetProgress(session.Position);
        }

        // SyllableSplitToggle 点击后调用：按当前 SyllableSplitEnabled 重建当前单词的
        // 拼读/音节分段叠加层（下划线按新分段重画，进度按 session.Position 恢复）；
        // 同时把单词文本切到音节双色+圆点 / 恢复自然拼读色（2026-11-09）
        public void RebuildPhonicsOverlay()
        {
            if (this == null || session == null) return;
            ApplyWordSyllableColors();
            UpdatePhonicsOverlay();
            if (phonicsOverlay != null) phonicsOverlay.SetProgress(session.Position);
        }

        // 单词文本双色随拆分模式即时刷新：词中途点按钮也能立即换色（已打字母/等待字母都覆盖）
        void ApplyWordSyllableColors()
        {
            if (trails == null) return;
            if (!SyllableSplitEnabled) { trails.ClearSyllableSplit(); return; } // 模式关 → 恢复自然拼读色
            var starts = CurrentSyllableStarts();
            if (starts != null) trails.ApplySyllableSplit(starts);
            else trails.ApplySyllableSolid(); // 拆分模式但无对齐分段 → 整词深蓝（参考 app 行为，2026-11-10）
        }

        void OnWordCompleted(int id) // 退场动画播完后回调（KeyboardSingleTrail.Update 末尾）
        {
            // 退场音效不再截断：动画结束就 Stop 会切掉尾音 → 让它自然播完（2026-11-10）
            if (session == null || !session.Submitted) return;
            bool advanced = session.AdvanceWord();
            UpdateProgressBar(); // 打完一个词 → 顶部进度条按单词数推进（须在 AdvanceWord 计数之后）
            if (advanced)
            {
                PrepareCurrentWord();
                PhonicsStateSync?.Invoke(); // 切词后通知开关按钮按自身状态重绘（保持点击时的开/关）
            }
            else
            {
                ShowAllLearned(); // 词库打完（不循环模式一轮结束）→ 单词位置显示"单词已经全部学习"
                UpdateTargetGlow(); // 词库已空 → 熄灭 Enter 引导光（Finish 分支挂起发光）
            }
        }

        // ───────────────────────── 完课提示 ─────────────────────────

        // 词库全部打完（session.Finished）→ 在单词位置放"单词已经全部学习"文本。
        // 摆位/字号按用户在 Inspector 实测调好的数值写死（2026-11-10 截图）：
        // pos(-1.91, 2, -9.675) rot(75.666, -180, 0) scale 1 / fontSize 500 / characterSize 0.02734375。
        // 用 TextMesh + CJKSubset 子集字体（同截图 Font=CJKSubset）。
        void ShowAllLearned()
        {
            if (trails == null || runtimeRoot == null) return;
            HideAllLearned();
            var go = new GameObject("AllLearnedText");
            go.transform.SetParent(runtimeRoot, true); // 挂运行时根下，随 OnDestroy 一起清理
            go.transform.position = new Vector3(-1.91f, 2f, -9.675f);
            go.transform.eulerAngles = new Vector3(75.666f, -180f, 0f);
            go.transform.localScale = Vector3.one;
            var tm = go.AddComponent<TextMesh>();
            tm.text = "单词已经全部学习";
            // WebGL 没有 OS 字体回退：SegoeUI 无中文字形 → 动态渲染成空白（"没有提示文本"）。
            // 用 8 字子集字体（Assets/Keyboard3D/Resources/Fonts/CJKSubset.otf，由 SmileySans 裁出，2.7KB）
            var cjk = Resources.Load<Font>("Fonts/CJKSubset");
            tm.font = cjk != null ? cjk : WordPhonicsOverlay.BuiltinFont();
            tm.fontSize = 500;
            tm.characterSize = 0.02734375f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = AllLearnedColor;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null) mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = tm.font.material;
            allLearnedText = go.transform;
        }

        void HideAllLearned()
        {
            if (allLearnedText != null) Destroy(allLearnedText.gameObject);
            allLearnedText = null;
        }

        // 切换单词后触发：PhonicsSoundToggle 收到后按按钮当前状态刷新文字并回写 PhonicsEnabled，
        // 保证开关状态跨单词保持（按钮是唯一事实源）
        public static event Action PhonicsStateSync;

        void OnWordExitStarted(int id, float duration) // 退场动画开始（WordExitSequence 创建时）
        {
            if (exitSound == null || exitSound.clip == null) return;
            // 播放原版 heal（不压缩），自然播完不截断；下一个词退场 Play() 会从头重播
            exitSound.pitch = 1f;
            exitSound.Play();
        }

        // 键盘目标键发光跟随单词进度：当前待输入字母的键常亮（不再随机）；
        // 整词输完点亮 Enter 引导提交；退场动画期间挂起
        void UpdateTargetGlow()
        {
            if (keyboards.Count == 0) return;
            if (session == null || session.Finished || session.Submitted)
            {
                foreach (var kb in keyboards) kb.SuspendTargetGlow();
                return;
            }
            if (session.Position >= session.CurrentWord.Length)
            {
                foreach (var kb in keyboards) kb.SetForcedTarget(KeyCode.Return); // 引导按 Enter
                return;
            }
            char c = char.ToUpperInvariant(session.CurrentWord[session.Position]);
            if (c < 'A' || c > 'Z') return;
            var want = (KeyCode)((int)KeyCode.A + (c - 'A')); // KeyCode.A=97（非 ASCII 65），按枚举值偏移
            foreach (var kb in keyboards) kb.SetForcedTarget(want);
        }

        // ───────────────────────── 错误反馈（原项目 RefreshPrompt 同款） ─────────────────────────

        void RefreshFeedback()
        {
            if (trails == null || session == null) return;
            float elapsed = Time.unscaledTime - errorAt;
            bool shaking = session.HasError && elapsed >= 0f && elapsed < ShakeDuration;
            float shake = 0f;
            if (shaking)
            {
                float px = ShakePixels * (cam != null ? cam.pixelHeight / 1080f : 1f);
                shake = Mathf.Sin(elapsed * 95f) * px * (1f - elapsed / ShakeDuration);
            }
            trails.SetWaitingFeedback(session.Position, session.HasError, errorColor, 0); // 抖动反馈已禁用
        }

        // ───────────────────────── 外部接口（TypingHands 指法手用） ─────────────────────────

        // 指法手用：整词已输完，正在等待按 Enter
        public bool WaitingForEnter => session != null && session.CanSubmit;

        // 指法手（TypingHands）用：当前待输入字母（大写）；false = 不在打字阶段/整词已输完
        public bool TryGetTargetLetter(out char c)
        {
            c = default;
            if (session == null || session.Submitted || session.Finished || session.Position >= session.CurrentWord.Length) return false;
            c = char.ToUpperInvariant(session.CurrentWord[session.Position]);
            return true;
        }

        // ───────────────────────── 工具 ─────────────────────────

        // Fisher-Yates 原地洗牌（返回同一列表，方便链式调用）
        static List<string> ShuffleList(List<string> list, int seed)
        {
            var rng = new System.Random(seed);
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            return list;
        }

        // 把 targetB 摆到"屏幕上指定锚点、与键盘同距离"的位置（wordRoot 每帧跟随 targetB 直到首字母命中）
        void AnchorWord()
        {
            if (targetB == null) return;
            if (cam == null) { targetB.position = KeyboardAnchor(); return; }
            float dist = Vector3.Distance(cam.transform.position, KeyboardAnchor());
            var sp = new Vector3(screenAnchorX * cam.pixelWidth, screenAnchorY * cam.pixelHeight, 0f);
            targetB.position = cam.ScreenPointToRay(sp).GetPoint(Mathf.Max(0.1f, dist));
        }

        // 兜底锚点：键盘包围盒顶部上方（相机缺失时用；正常路径走屏幕锚定）
        Bounds keyboardAnchorBounds;
        bool keyboardBoundsReady;

        // 只在 Awake 缓存一次：keyboard 下若混入水波/提示光等动效渲染器，
        // 每帧重算联合包围盒会让锚点随动效脉动，单词（未锚定前每帧跟随 targetB）就会跳动
        void CacheKeyboardBounds()
        {
            bool first = true;
            Bounds b = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var r in keyboard.GetComponentsInChildren<Renderer>())
            {
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            keyboardAnchorBounds = b;
            keyboardBoundsReady = !first;
        }

        Vector3 KeyboardAnchor()
        {
            if (!keyboardBoundsReady) CacheKeyboardBounds();
            if (keyboardBoundsReady) return keyboardAnchorBounds.center + Vector3.up * (keyboardAnchorBounds.extents.y + hoverHeight);
            return keyboard.transform.position + Vector3.up * (hoverHeight + 1f);
        }
    }
}
