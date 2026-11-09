using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
        [Tooltip("随机顺序（false = 按列表顺序循环）")]
        public bool randomOrder = false;

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
        class SyllableData { public int wid; public List<string> sylls; public List<string> ipas; public List<SyllableSeg> segs; }
        Dictionary<string, SyllableData> syllableByWord;        // 小写单词 → 音节拆分数据
        AudioSource splitSource;                                // 音节片段专用音源（要能中途 Stop，与 PlayOneShot 音源分开）
        float splitStopAt = float.NegativeInfinity;             // 当前片段应停止的时刻
        readonly Dictionary<string, AudioClip> voiceClipCache = new Dictionary<string, AudioClip>();
        AudioSource voiceSource;                                 // 拼读块/单词发音共用（2D）
        bool voiceWarmed;        // WebGL：音频引擎首次交互才唤醒，同帧调度的音标音会被吞 → 首个音延后播
        const float FirstVoiceDelay = 0.1f;                      // 未预热时首个音标音的延后秒数

        [Tooltip("音效切换下拉框（拖入 Canvas 下的 Dropdown，选项即音效名）")]
        public TMPro.TMP_Dropdown arrivalDropdown;
        [Tooltip("所有候选到达音效容器（留空则自动收集 Resources/ArrivalHits + ArrivalChimes）")]
        public List<AudioClip> arrivalClipList = new List<AudioClip>();
        int selectedArrivalClip;
        AudioClip ArrivalClip=>arrivalClipList!=null&&arrivalClipList.Count>0
            ?arrivalClipList[Mathf.Clamp(selectedArrivalClip,0,arrivalClipList.Count-1)]
            :arrivalHitSound!=null?arrivalHitSound.clip:null;
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

        [Header("字母配色（逐字母循环，参考视频糖果色）")]
        public Color[] palette =
        {
            new Color(1f, 0.2f, 0.3f), new Color(1f, 0.65f, 0.12f), new Color(0.3f, 1f, 0.35f),
            new Color(0.1f, 0.85f, 1f), new Color(0.65f, 0.35f, 1f), new Color(1f, 0.25f, 0.85f),
        };

        // ───────────────────────── 内部状态 ─────────────────────────

        KeyboardSingleTrail trails;   // 原项目飞行/字母/退场系统（完整搬运）
        WordPracticeSession session;  // 练习进度（Position/HasError/Submitted）
        WordProgressBar progressBar;  // 单词进度条（当前单词的字母拼写进度，切词清零）
        WordPhonicsOverlay phonicsOverlay; // 自然拼读叠加层（挂在 3D 单词 root 下：拼读块下划线 + 块下音标）
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
            // 右上角面板可切换试听（Resources/ArrivalHits + ArrivalChimes 全部候选）
            BuildArrivalClipOptions();
            trails.LetterRevealed += (wordId, index, position, color) =>
            {
                var clip=ArrivalClip;
                if (arrivalHitSound != null && clip != null)
                    arrivalHitSound.PlayOneShot(clip); // PlayOneShot 支持快速连击叠音
            };
            trails.WordCompleted += OnWordCompleted;
            trails.WordExitStarted += OnWordExitStarted; // 单词退场动画开始 → 播放退场音效（按动画时长压缩）
            trails.WordPulseStarted += OnWordPulseStarted; // 拼完单词（脉冲开始）→ 读出单词发音
            SetVoiceReady();
            trails.SetWaitingPhonicsOpacity(waitingPhonicsOpacity);

            session = new WordPracticeSession(words, randomOrder ? 1 : 0, Environment.TickCount);
            if (session.WordCount == 0)
            {
                Debug.LogWarning("[WordTrail] 单词库为空/无合法项，单词打字特效禁用。", this);
                Instance = null;
                enabled = false;
                return;
            }
            AnchorWord(); // PrepareWord 用 targetB 定位单词根：先生效一次
            progressBar = WordProgressBar.Create(); // 单词进度条（当前单词的字母拼写进度）
            UpdateProgressBar();
            PrepareCurrentWord();

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
            var asset = Resources.Load<TextAsset>("WordSyllableMap");
            if (asset == null) return; // 没数据就整个功能静默停用（按钮开了也只是不拆分）
            var data = JsonUtility.FromJson<SyllableMapData>(asset.text);
            if (data?.entries == null) return;
            foreach (var e in data.entries)
            {
                if (e == null || string.IsNullOrEmpty(e.word) || string.IsNullOrEmpty(e.my_syllable)) continue;
                MySyllable ms;
                try { ms = JsonUtility.FromJson<MySyllable>(e.my_syllable); } catch { continue; }
                if (ms == null || string.IsNullOrEmpty(ms.ws)) continue;
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
            if (phonicsByWord != null && session != null &&
                phonicsByWord.TryGetValue(session.CurrentWord.ToLowerInvariant(), out var e) &&
                !string.IsNullOrEmpty(e.audio))
            {
                var clip = GetVoiceClip(e.audio);
                if (clip != null) voiceSource.PlayOneShot(clip, 0f); // 零音量预热，不产生可听声音
            }
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
                    if (idx - 1 != index) continue;
                    if (sd.segs != null && i < sd.segs.Count) PlaySplitSegment(sd, sd.segs[i]);
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

        // 播英式拆分音频里 [s, s+d] 毫秒的片段（播够时长即停，用专用音源以便中途截断）
        void PlaySplitSegment(SyllableData sd, SyllableSeg seg)
        {
            var clip = GetVoiceClip("split_en/split_" + sd.wid);
            if (clip == null) return; // 没拷到该词的拆分音频 → 静默跳过
            if (splitSource == null)
            {
                splitSource = gameObject.AddComponent<AudioSource>();
                splitSource.playOnAwake = false;
                splitSource.spatialBlend = 0f; // 与 voiceSource 同款 2D
            }
            splitSource.clip = clip;
            splitSource.time = Mathf.Clamp(seg.s / 1000f, 0f, Mathf.Max(0f, clip.length - 0.01f));
            splitSource.volume = phonicsVolume;
            splitSource.Play();
            splitStopAt = Time.time + Mathf.Max(0.05f, seg.d / 1000f);
        }

        // 拼完单词（完词脉冲开始，按 Enter 之前）：读出整词发音
        void OnWordPulseStarted(int wordId)
        {
            if (!PhonicsEnabled) return;
            if (phonicsByWord == null || voiceSource == null || session == null || session.Finished) return;
            if (!phonicsByWord.TryGetValue(session.CurrentWord.ToLowerInvariant(), out var e)) return;
            var clip = GetVoiceClip(e.audio);
            PlayVoice(clip);
        }

        // ───────────────────────── 主循环 ─────────────────────────

        void Update()
        {
            if (trails == null || session == null) return;
            if (cam == null) cam = Camera.main;
            // 音节片段播够 d 毫秒 → 截断（拆分音频里音节之间有静音间隔，不截会串音）
            if (splitSource != null && splitSource.isPlaying && Time.time >= splitStopAt) splitSource.Stop();

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
            }
            catch { inputUnavailable = true; } // 新 Input System 下旧 Input 不可用
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
            if (!trails.PrepareWord(session.CurrentWord, upperWordWidth, CurrentSyllableStarts()))
                Debug.LogWarning($"[WordTrail] 单词 {session.CurrentWord} 含 LetterCatalog 没有的字母，跳过。", this);
            UpdatePhonicsOverlay(); // 拼读块下划线/音标（须在 PrepareWord 之后建，挂在单词 root 下）
            UpdateTargetGlow(); // 键盘绿光指向首字母（新词生成，恢复被挂起的发光）
        }

        // 音节拆分模式（且该词有多音节数据）→ 返回各音节起始字母下标；否则 null（单词文本走自然拼读色）
        List<int> CurrentSyllableStarts()
        {
            if (!SyllableSplitEnabled || session == null || session.Finished || syllableByWord == null) return null;
            string w = session.CurrentWord.ToLowerInvariant();
            if (!syllableByWord.TryGetValue(w, out var sd) || sd.sylls.Count <= 1) return null;
            var starts = new List<int>();
            int total = 0;
            foreach (var s in sd.sylls)
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
            var chunks = new List<WordPhonicsOverlay.ChunkInfo>();
            if (!session.Finished)
            {
                string w = session.CurrentWord.ToLowerInvariant();
                // 音节拆分模式（SyllablesplitButton 开启）：多音节词按音节分段（breakfast → break-fast），
                // 段下文本优先英式音标（my_syllable.uk），段数对不上退回拼写分段；单词节/无数据的词走原拼读分段
                if (SyllableSplitEnabled && syllableByWord != null &&
                    syllableByWord.TryGetValue(w, out var sd) && sd.sylls.Count > 1)
                {
                    int idx = 0;
                    for (int i = 0; i < sd.sylls.Count; i++)
                    {
                        int len = sd.sylls[i].Length;
                        chunks.Add(new WordPhonicsOverlay.ChunkInfo
                        {
                            start = idx,
                            count = len,
                            sound = sd.ipas != null ? sd.ipas[i] : sd.sylls[i],
                        });
                        idx += len;
                    }
                }
                else if (phonicsByWord != null &&
                         phonicsByWord.TryGetValue(w, out var e) &&
                         e?.phonemes != null)
                {
                    foreach (var c in e.phonemes)
                    {
                        if (c?.positions == null || c.positions.Count == 0) continue;
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
            if (chunks.Count == 0) return;
            var go = new GameObject("PhonicsOverlay");
            go.transform.SetParent(trails.WordRoot, false);
            phonicsOverlay = go.AddComponent<WordPhonicsOverlay>();
            if (!phonicsOverlay.Build(trails, chunks))
            {
                Destroy(go);
                phonicsOverlay = null;
                return;
            }
            phonicsOverlay.SetProgress(session.Position);
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
            var starts = CurrentSyllableStarts();
            if (starts != null) trails.ApplySyllableSplit(starts);
            else trails.ClearSyllableSplit();
        }

        void OnWordCompleted(int id) // 退场动画播完后回调（KeyboardSingleTrail.Update 末尾）
        {
            if (exitSound != null) exitSound.Stop(); // 退场结束 → 停止退场音效
            if (session == null || !session.Submitted) return;
            bool advanced = session.AdvanceWord();
            UpdateProgressBar(); // 打完一个词 → 顶部进度条按单词数推进（须在 AdvanceWord 计数之后）
            if (advanced)
            {
                PrepareCurrentWord();
                PhonicsStateSync?.Invoke(); // 切词后通知开关按钮按自身状态重绘（保持点击时的开/关）
            }
        }

        // 切换单词后触发：PhonicsSoundToggle 收到后按按钮当前状态刷新文字并回写 PhonicsEnabled，
        // 保证开关状态跨单词保持（按钮是唯一事实源）
        public static event Action PhonicsStateSync;

        void OnWordExitStarted(int id, float duration) // 退场动画开始（WordExitSequence 创建时）
        {
            if (exitSound == null || exitSound.clip == null) return;
            // 播放原版 heal（不压缩），退场动画结束（WordCompleted）时 Stop 截断
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
