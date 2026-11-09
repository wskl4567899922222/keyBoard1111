using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using KeyboardGame.Letters;

namespace KeyboardGame
{
    [DisallowMultipleComponent]
    public sealed class KeyboardSingleTrail : MonoBehaviour
    {
        public enum LetterCase { TitleCase, UpperCase, LowerCase }
        [Header("Scene target and reusable assets")]
        [SerializeField] Transform targetB;
        [SerializeField] SingleTrailEffect trailPrefab;
        [SerializeField] TextMeshPro letterPrefab;
        [Header("Upper word / 3D arrival letters")]
        [SerializeField] LetterCatalog letterCatalog;
        [SerializeField] LetterView letterViewPrefab;
        [SerializeField] Material letterBodyMaterial,letterBevelMaterial;
        [SerializeField,Min(0.0001f)] float letterCapHeight=0.02f;
        [SerializeField] SingleTrailImpact impactPrefab;
        [SerializeField] Camera displayCamera;
        [Header("Independent single-trail color")]
        [SerializeField] bool cycleColors = true;
        [SerializeField] Color singleColor = new Color(0.15f,0.8f,1f,1f);
        [SerializeField] Color[] palette = {
            new Color(1f,0.2f,0.3f),new Color(1f,0.65f,0.12f),new Color(0.3f,1f,0.35f),
            new Color(0.1f,0.85f,1f),new Color(0.65f,0.35f,1f),new Color(1f,0.25f,0.85f)};
        [SerializeField,Range(0.1f,8f)] float brightness = 2f;
        [Header("Rise and homing (world units)")]
        [SerializeField,Min(0.001f)] float flightSpeed = 0.5f;
        [SerializeField,Min(0)] float riseTime = 0.06f;
        [SerializeField,Min(1)] float turnSpeed = 720f;
        [SerializeField,Min(0)] float maxArcHeight = 0.04f;
        [Header("Curve trajectory (参考视频轨迹)")]
        [SerializeField,Min(0)] float curveWiggleAmplitude=0.08f;
        [SerializeField,Min(1)] float curveWiggleFrequency=22f;
        [SerializeField,Range(0,0.2f)] float screenMargin = 0.06f;
        [SerializeField,Min(0)] float letterRevealDelay = 0.06f;
        [SerializeField,Min(0.0001f)] float arrivalDistance = 0.008f;
        [SerializeField,Min(0.1f)] float maxFlightTime = 6f;
        [SerializeField,Min(0.05f)] float trailLifetime = 0.25f;
        [Header("Comet look (只影响发射彗星)")]
        [SerializeField,Min(0.1f)] float cometWidthFactor = 1.6f;
        [SerializeField,Min(0.1f)] float cometGlowFactor = 1.75f;
        [Header("Word display")]
        [SerializeField] LetterCase letterCase = LetterCase.TitleCase;
        [SerializeField] Vector3 wordOffset = new Vector3(0,0.015f,0);
        [SerializeField,Min(0)] float completedWordHold = 0.35f;
        [SerializeField,Min(0.01f)] float wordFadeDuration = 0.6f;
        [Header("Word completion pulse（完词脉冲：先放大再复原，之后才退场）")]
        [SerializeField,Range(1f,1.5f)] float wordPulseScale = 1.1f;   // 峰值放大倍数
        [SerializeField,Min(0.05f)] float wordPulseDuration = 0.35f;   // 放大→复原总时长（秒）
        [Header("Gray to 3D handoff (separate from impact style)")]
        [SerializeField,Range(0.02f,0.10f)] float grayContractionDuration=0.045f;
        [Header("Waiting glyph color")]
        [SerializeField,Range(0f,1f)] float waitingPhonicsOpacity=0.2f; // 等待态：拼读色×此不透明度（替代纯灰）
        [Header("Letter arrival pop")]
        [SerializeField] bool enableLetterPop = true;
        [SerializeField,Range(0.1f,1)] float letterPopStartScale = 0.85f;
        [SerializeField,Range(1,1.2f)] float letterPopOvershoot = 1.03f;
        [SerializeField,Min(0.01f)] float letterPopDuration = 0.22f;
        [Header("Word mouse tilt")]
        [SerializeField] bool enableWordMouseTilt = true;
        [SerializeField,Range(0,15)] float mouseYawAngle = 5f;
        [SerializeField,Range(0,15)] float mousePitchAngle = 3f;
        [SerializeField,Min(0.01f)] float mouseTiltSmoothTime = 0.18f;
        [Header("Object reuse")]
        [SerializeField,Range(0,64)] int prewarmTrails = 8;
        [SerializeField,Range(0,128)] int maxCachedTrails = 32;
        [SerializeField,Range(0,256)] int maxCachedLetters = 64;

        sealed class WordView
        {
            public TrailWordBatch model;
            public Transform root;
            public bool anchorFixed;
            public bool prepared;
            public WordExitSequence exit;
            public WordPromptEntrance promptEntrance;
            public Vector3 neutralPosition,tiltPivot;
            public Quaternion neutralRotation;
            public readonly WordMouseTilt mouseTilt=new WordMouseTilt();
            public float fontPen,previousAdvance;
            public char previousCharacter;
            public readonly HashSet<int> emitted=new HashSet<int>();
            public readonly HashSet<int> resolved=new HashSet<int>();
            public readonly List<LetterView> letters = new List<LetterView>();
            public readonly List<float> revealAt = new List<float>();
            public readonly List<Transform> destinations = new List<Transform>();
            public readonly List<Action> queuedLaunches=new List<Action>();
            public float pulseT=-1f;                 // 完词脉冲动画进度（-1 未开始，0~1 进行中，≥1 已结束）
            public Vector3 pulseBaseScale=Vector3.one; // 脉冲起点的 root 基准缩放（L280/L291 摆位后的值）
            public List<int> syllableStarts;         // 音节起始字母下标（音节拆分双色模式，null=未启用）
            public readonly List<Transform> syllableDots=new List<Transform>(); // 音节分隔圆点（纯装饰，不进打字索引）
            public List<float> syllableBaseX;        // 加宽音节间距前的字母基准 x（还原/幂等重排用）
        }
        readonly Stack<SingleTrailEffect> trailPool = new Stack<SingleTrailEffect>();
        readonly Stack<LetterView> letterPool = new Stack<LetterView>();
        readonly Stack<SingleTrailImpact> impactPool=new Stack<SingleTrailImpact>();
        readonly HashSet<SingleTrailEffect> activeTrails = new HashSet<SingleTrailEffect>();
        readonly List<WordView> words = new List<WordView>();
        WordView current;
        Transform runtimeRoot;
        int wordSerial;
        bool warningShown;
        WordExitStyle wordExitStyle;
        Material wordExitMaterial;
        WordEnergyStyle wordEnergyStyle;
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        MaterialPropertyBlock rendererProperties;
        public event Action<int,int,Vector3,Color> ImpactPlayed;
        public event Action<int,int,Vector3,Color> LetterRevealed;
        public event Action<int> WordCompleted;
        public event Action<int,float> WordExitStarted; // (单词Id, 退场动画时长秒) —— WordExitSequence 创建时触发，退场音效按此时长压缩
        public event Action<int> WordPulseStarted; // 单词拼完（全部字母点亮）完词脉冲开始时触发 —— 播放单词整词发音
        public void SetWaitingPhonicsOpacity(float opacity){waitingPhonicsOpacity=Mathf.Clamp01(opacity);}
        public void SetWaitingFeedback(int currentIndex,bool error,Color wrong,float shakePixels)
        {
            if(current==null||!current.prepared)return;
            for(int i=0;i<current.letters.Count;i++)
            {
                var letter=current.letters[i];var activation=letter.Activation;
                if(activation==null||!activation.IsWaiting)continue;
                Vector3 worldOffset=displayCamera!=null&&current.promptEntrance!=null?displayCamera.transform.up*current.promptEntrance.Offset:Vector3.zero;
                if(i==currentIndex&&error&&shakePixels!=0&&displayCamera!=null)
                {
                    var p=displayCamera.WorldToScreenPoint(letter.transform.position);
                    var world=displayCamera.ScreenToWorldPoint(p+Vector3.right*shakePixels)-displayCamera.ScreenToWorldPoint(p);
                    worldOffset+=world;
                }
                // 等待态：拼读色×低不透明度（如 20%），到达后才转全色；打错时当前字母变红
                Color color=i==currentIndex&&error?wrong:WaitingColor(current,i);
                if(current.promptEntrance!=null)color.a*=current.promptEntrance.Alpha;
                activation.SetFeedback(color,letter.Model.InverseTransformVector(worldOffset));
            }
        }
        Color WaitingColor(WordView word,int index)
        {
            Color color=word.model.GetColor(index);
            color.a*=waitingPhonicsOpacity;
            return color;
        }
        public TMP_FontAsset LetterFont=>letterPrefab!=null?letterPrefab.font:null;
        bool HasLetterAssets=>letterCatalog!=null&&letterViewPrefab!=null&&letterBodyMaterial!=null&&letterBevelMaterial!=null;
        public bool CanEmitPreparedLetter=>Application.isPlaying&&isActiveAndEnabled&&current!=null&&current.prepared&&
            targetB!=null&&trailPrefab!=null&&HasLetterAssets&&impactPrefab!=null;
        public LetterView GetLetterView(int wordId,int index)
        {foreach(var word in words)if(word.model.Id==wordId&&index>=0&&index<word.letters.Count)return word.letters[index];return null;}

        public Transform TargetB { get => targetB; set => targetB=value; }
        public int ActiveTrailCount => activeTrails.Count;
        public int PendingWordCount => words.Count;
        // 飞行上升轴：原项目键盘直立（transform.up 即世界向上）；本项目键盘根是躺倒的
        // （rot -90°X），transform.up 指向水平——由驱动器注入世界向上/SurfaceNormal
        [NonSerialized] public Vector3 upAxis = Vector3.up;
        // 运行时装配接口（本工程没有原始场景配置，由 WordTrailTyping 在 Awake 注入）
        public void Configure(Transform targetB, SingleTrailEffect trailPrefab, LetterCatalog letterCatalog,
            LetterView letterViewPrefab, Material letterBodyMaterial, Material letterBevelMaterial,
            SingleTrailImpact impactPrefab, Camera displayCamera, float letterCapHeight,
            Color[] palette, float brightness, float flightSpeed, float maxArcHeight,
            float arrivalDistance, float trailLifetime)
        {
            this.targetB=targetB;this.trailPrefab=trailPrefab;this.letterCatalog=letterCatalog;
            this.letterViewPrefab=letterViewPrefab;this.letterBodyMaterial=letterBodyMaterial;
            this.letterBevelMaterial=letterBevelMaterial;this.impactPrefab=impactPrefab;
            this.displayCamera=displayCamera;this.letterCapHeight=letterCapHeight;
            this.palette=palette;this.brightness=brightness;this.flightSpeed=flightSpeed;
            this.maxArcHeight=maxArcHeight;this.arrivalDistance=arrivalDistance;this.trailLifetime=trailLifetime;
            // 0.1s 到达：起飞段必须为 0（riseDuration 期间不转向、全速直冲，
            // 25u/s 下 0.12s 会冲出 3u 再拐回，观感严重偏慢）；转向 1440°/s ≈
            // 0.03s 内对准目标，保留轻微自然弯弧
            cycleColors=true;riseTime=0f;turnSpeed=1440f;maxFlightTime=6f;screenMargin=0.06f;
            letterRevealDelay=0.06f;grayContractionDuration=0.045f;completedWordHold=0.35f;wordFadeDuration=0.6f;
            enableLetterPop=false;letterPopStartScale=0.85f;letterPopOvershoot=1.03f;letterPopDuration=0.22f;
            enableWordMouseTilt=false;prewarmTrails=8;maxCachedTrails=32;maxCachedLetters=64;
            letterCase=LetterCase.LowerCase; // 参考视频单词全小写（banana）
        }
        public string CurrentWordText => current != null ? current.model.Text : "";
        public string VisibleWordText
        {
            get
            {
                if(words.Count==0)return "";
                var v=words[0];var chars=new char[v.model.Count];
                for(int i=0;i<chars.Length;i++) chars[i]=v.model.HasArrived(i)?v.model.GetCharacter(i):'_';
                return new string(chars);
            }
        }

        void OnEnable()
        {
            if(!Application.isPlaying)return;
            EnsureRuntime();
        }
        void OnDisable()
        {
            foreach(var word in words)word.exit?.Dispose();
            foreach(var effect in activeTrails)if(effect!=null)effect.ResetForPool();
            activeTrails.Clear();trailPool.Clear();letterPool.Clear();impactPool.Clear();words.Clear();current=null;
            if(runtimeRoot!=null)Destroy(runtimeRoot.gameObject);
            runtimeRoot=null;
        }
        void EnsureRuntime()
        {
            if(runtimeRoot!=null)return;
            rendererProperties=new MaterialPropertyBlock();
            runtimeRoot=new GameObject("Keyboard_SingleTrail_Runtime").transform;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(runtimeRoot.gameObject,gameObject.scene);
            if(displayCamera==null)displayCamera=Camera.main;
            wordExitStyle=Resources.Load<WordExitStyle>("WordExitStyle");
            wordExitMaterial=Resources.Load<Material>("WordExitGlow");
            wordEnergyStyle=Resources.Load<WordEnergyStyle>("WordEnergyStyle");
            if(trailPrefab!=null)
                for(int i=0;i<prewarmTrails;i++)trailPool.Push(CreateTrail());
        }
        SingleTrailEffect CreateTrail()
        {
            var effect=Instantiate(trailPrefab,runtimeRoot);
            effect.name="SingleTrail";
            effect.ResetForPool();effect.gameObject.SetActive(false);return effect;
        }

        /// <summary>Same entry point for physical key events and Unity integration checks.</summary>
        public void HandleKeyPressed(KeyCode key, Transform origin)
        {
            if(!Application.isPlaying || !isActiveAndEnabled)return;
            if(key==KeyCode.Return || key==KeyCode.KeypadEnter)
            { SubmitCurrentWord(); return; }
            if(key<KeyCode.A || key>KeyCode.Z)return; // Numbers, punctuation and modifiers do nothing here.
            if(origin==null || targetB==null || trailPrefab==null || !HasLetterAssets || impactPrefab==null)
            {
                if(!warningShown)Debug.LogWarning("[Single Trail] Assign Target B, trail, impact and 3D letter assets before typing.",this);
                warningShown=true;return;
            }
            warningShown=false;EnsureRuntime();
            char character=(char)('a'+((int)key-(int)KeyCode.A));
            if(current==null)
            {
                var root=new GameObject("Word_"+(++wordSerial)).transform;root.SetParent(runtimeRoot,false);
                root.position=targetB.position+wordOffset;
                if(displayCamera!=null)root.rotation=displayCamera.transform.rotation;
                current=new WordView {model=new TrailWordBatch(wordSerial),root=root,neutralPosition=root.position,neutralRotation=root.rotation};
                words.Add(current);root.gameObject.SetActive(words.Count==1);
            }
            if(letterCase==LetterCase.UpperCase || (letterCase==LetterCase.TitleCase&&current.model.Count==0))character=char.ToUpperInvariant(character);
            var view=current;
            if(!letterCatalog.TryGet(character,out var glyph))return;
            // 自然拼读规则色（无整词上下文时按单字母分类兜底）
            Color color=PhonicsColors.Letter(character);
            int index=AddLetter(view,glyph,color);
            Vector3 emissionPosition=origin.position;
            Action launch=()=>LaunchLetter(view,index,emissionPosition,color);
            if(words[0]==view)launch();else view.queuedLaunches.Add(launch);
        }
        // —— 自然拼读叠加层支持（WordTrailTyping 建 WordPhonicsOverlay 用）——
        // 字母本地区间与整体度量（均为 word root 局部空间，字母 localScale 已计入，root 缩放除外）
        public Transform WordRoot => current != null ? current.root : null;

        public bool TryGetWordLocalMetrics(out float minX, out float maxX, out float bottomY, out float letterH)
        {
            minX = maxX = bottomY = letterH = 0f;
            var v = current;
            if (v == null || v.letters.Count == 0) return false;
            float lo = float.PositiveInfinity, hi = float.NegativeInfinity, bot = float.PositiveInfinity, hMax = 0f;
            foreach (var l in v.letters)
            {
                var t = l.transform;
                var b = l.LocalBounds;
                lo = Mathf.Min(lo, t.localPosition.x + b.min.x * t.localScale.x);
                hi = Mathf.Max(hi, t.localPosition.x + b.max.x * t.localScale.x);
                bot = Mathf.Min(bot, t.localPosition.y + b.min.y * t.localScale.y);
                hMax = Mathf.Max(hMax, b.size.y * t.localScale.y);
            }
            if (hi <= lo) return false;
            minX = lo; maxX = hi; bottomY = bot; letterH = hMax;
            return true;
        }

        public bool TryGetLetterLocalXRange(int index, out float minX, out float maxX)
        {
            minX = maxX = 0f;
            var v = current;
            if (v == null || index < 0 || index >= v.letters.Count) return false;
            var t = v.letters[index].transform;
            var b = v.letters[index].LocalBounds;
            minX = t.localPosition.x + b.min.x * t.localScale.x;
            maxX = t.localPosition.x + b.max.x * t.localScale.x;
            return maxX > minX;
        }

        // —— 音节拆分双色（2026-11-09 用户要求：单词文本只按音节交替深藏蓝/橙两色，音节间圆点分隔）——
        // 两色取自参考截图（cos·tume）；圆点用程序生成贴图的 SpriteRenderer，挂在单词 root 下，
        // 自动跟随缩放/倾斜/退场销毁。不进 TrailWordBatch → 打字/下划线/音标索引完全不受影响。
        public static readonly Color SyllableColorA=new Color32(0,51,102,255);   // 深蓝 #003366（用户指定 2026-11-09）
        public static readonly Color SyllableColorB=new Color32(229,124,0,255);  // 橙 #E57C00（用户指定 2026-11-09）
        static readonly Color SyllableDotColor=new Color32(226,226,220,255);     // 分隔圆点：灰白（2026-11-09 用户要求）
        static Sprite syllableDotSprite;

        static Sprite SyllableDotSprite()
        {
            if(syllableDotSprite==null)
            {
                const int size=64;
                var tex=new Texture2D(size,size,TextureFormat.RGBA32,false);
                var px=new Color[size*size];
                var center=new Vector2((size-1)*0.5f,(size-1)*0.5f);
                float radius=size*0.5f-1.5f;
                for(int y=0;y<size;y++)
                    for(int x=0;x<size;x++)
                    {
                        float a=Mathf.Clamp01((radius-Vector2.Distance(new Vector2(x,y),center))/1.5f); // 边缘抗锯齿
                        px[y*size+x]=new Color(1f,1f,1f,a);
                    }
                tex.SetPixels(px);tex.Apply();
                syllableDotSprite=Sprite.Create(tex,new Rect(0,0,size,size),new Vector2(0.5f,0.5f),size); // 1 sprite 单位=1 世界单位
            }
            return syllableDotSprite;
        }

        /// <summary>音节拆分模式：当前单词文本改为按音节深藏蓝/橙交替，并在音节间画分隔圆点。
        /// starts = 各音节起始字母下标（升序，首元素须为 0）；段数 ≤1 时忽略。</summary>
        public void ApplySyllableSplit(List<int> starts)
        {
            if(current==null||!current.prepared)return;
            ApplySyllableSplit(current,starts);
        }

        /// <summary>退出音节拆分：恢复自然拼读规则色并移除分隔圆点。</summary>
        public void ClearSyllableSplit()
        {
            if(current==null||!current.prepared)return;
            ApplySyllableSplit(current,null);
        }

        void ApplySyllableSplit(WordView view,List<int> starts)
        {
            RestoreSyllableLayout(view); // 幂等：先还原上次拆分造成的加宽位移
            ClearSyllableDots(view);
            bool split=starts!=null&&starts.Count>1;
            view.syllableStarts=split?starts:null;
            if(!split)
            {
                var phonics=PhonicsColors.PerLetter(view.model.Text); // 恢复自然拼读规则色
                for(int i=0;i<view.letters.Count&&i<phonics.Length;i++)SetLetterColor(view,i,phonics[i]);
                return;
            }
            for(int i=0;i<view.letters.Count;i++)SetLetterColor(view,i,SyllableColor(starts,i));
            WidenSyllableGaps(view); // 音节组之间加宽（2026-11-09 用户要求：原间距太窄）
            SpawnSyllableDots(view);
        }

        // 音节组之间净增 extraGap（≈字高 32%）：边界后的字母整体右移，最后整词重新居中。
        // syllableBaseX 记录加宽前的基准 x → 还原/重排都从这里出发，不会累积漂移。
        void WidenSyllableGaps(WordView view)
        {
            if(view.syllableStarts==null||view.syllableStarts.Count<=1)return;
            float hMax=0f;
            foreach(var l in view.letters)hMax=Mathf.Max(hMax,l.LocalBounds.size.y*l.transform.localScale.y);
            if(hMax<=0f)return;
            view.syllableBaseX=new List<float>(view.letters.Count);
            foreach(var l in view.letters)view.syllableBaseX.Add(l.transform.localPosition.x);
            float extraGap=hMax*0.32f;
            float shift=0f;
            for(int i=0;i<view.letters.Count;i++)
            {
                if(i>0&&view.syllableStarts.Contains(i))shift+=extraGap;
                if(shift>0f)
                {
                    var p=view.letters[i].transform.localPosition;
                    p.x+=shift;
                    view.letters[i].transform.localPosition=p; // LandingPoint/Model 是字母子级，飞行终点随之移动
                }
            }
            float min=float.PositiveInfinity,max=float.NegativeInfinity;
            foreach(var l in view.letters)
            {
                var t=l.transform;var b=l.LocalBounds;
                min=Mathf.Min(min,t.localPosition.x+b.min.x*t.localScale.x);
                max=Mathf.Max(max,t.localPosition.x+b.max.x*t.localScale.x);
            }
            float center=(min+max)*0.5f;
            foreach(var l in view.letters)
            {
                var p=l.transform.localPosition;p.x-=center;l.transform.localPosition=p;
            }
            UpdateTiltPivot(view);
        }

        void RestoreSyllableLayout(WordView view)
        {
            if(view.syllableBaseX==null)return;
            for(int i=0;i<view.letters.Count&&i<view.syllableBaseX.Count;i++)
            {
                var t=view.letters[i].transform;
                var p=t.localPosition;p.x=view.syllableBaseX[i];t.localPosition=p;
            }
            view.syllableBaseX=null;
        }

        static Color SyllableColor(List<int> starts,int letterIndex)
        {
            int syllable=0;
            for(int i=0;i<starts.Count;i++)if(letterIndex>=starts[i])syllable=i;
            return syllable%2==0?SyllableColorA:SyllableColorB;
        }

        void SetLetterColor(WordView view,int index,Color color)
        {
            view.model.SetColor(index,color); // model 色是等待态/飞行/命中特效的唯一色源，必须同步
            var letter=view.letters[index];
            if(letter.Appearance!=null)letter.Appearance.SetMainColor(color); // 已点亮字母立即换色
            var activation=letter.Activation;
            if(activation!=null&&activation.IsWaiting)
            {
                Color c=color;c.a*=waitingPhonicsOpacity; // 等待态保持低不透明度
                activation.SetFeedback(c,Vector3.zero);
            }
        }

        void SpawnSyllableDots(WordView view)
        {
            float bot=float.PositiveInfinity,hMax=0f;
            foreach(var l in view.letters)
            {
                var t=l.transform;var b=l.LocalBounds;
                bot=Mathf.Min(bot,t.localPosition.y+b.min.y*t.localScale.y);
                hMax=Mathf.Max(hMax,b.size.y*t.localScale.y);
            }
            if(hMax<=0f)return;
            float dotDiameter=hMax*0.14f;
            for(int s=1;s<view.syllableStarts.Count;s++)
            {
                int prev=view.syllableStarts[s]-1,next=view.syllableStarts[s];
                if(prev<0||next>=view.letters.Count)continue;
                var tp=view.letters[prev].transform;var bp=view.letters[prev].LocalBounds;
                var tn=view.letters[next].transform;var bn=view.letters[next].LocalBounds;
                float gapMid=((tp.localPosition.x+bp.max.x*tp.localScale.x)+
                              (tn.localPosition.x+bn.min.x*tn.localScale.x))*0.5f;
                var dot=new GameObject("SyllableDot_"+s);
                dot.transform.SetParent(view.root,false);
                dot.transform.localPosition=new Vector3(gapMid,bot+hMax*0.45f,-0.01f);
                dot.transform.localScale=Vector3.one*dotDiameter;
                var sr=dot.AddComponent<SpriteRenderer>();
                sr.sprite=SyllableDotSprite();
                sr.color=SyllableDotColor; // 固定灰白（2026-11-09 用户要求），不随音节色
                view.syllableDots.Add(dot.transform);
            }
        }

        void ClearSyllableDots(WordView view)
        {
            foreach(var d in view.syllableDots)if(d!=null)Destroy(d.gameObject);
            view.syllableDots.Clear();
        }


        /// <summary>Allocate every glyph before input; hidden glyphs reserve stable, centered destinations.
        /// syllableStarts（可空）：各音节起始字母下标 → 单词文本按音节深藏蓝/橙交替 + 圆点分隔（2026-11-09）。</summary>
        public bool PrepareWord(string text,float viewportWidth,List<int> syllableStarts=null)
        {
            if(string.IsNullOrEmpty(text)||!HasLetterAssets||targetB==null||words.Count>0)return false;
            foreach(char c in text)if(!letterCatalog.TryGet(c,out _))return false;
            EnsureRuntime();
            var root=new GameObject("Word_"+(++wordSerial)).transform;root.SetParent(runtimeRoot,false);
            root.position=targetB.position+wordOffset;
            if(displayCamera!=null)root.rotation=displayCamera.transform.rotation;
            var view=new WordView{model=new TrailWordBatch(wordSerial),root=root,prepared=true,neutralPosition=root.position,neutralRotation=root.rotation};
            var phonicsColors=PhonicsColors.PerLetter(text); // 自然拼读规则色（整词分段）
            for(int i=0;i<text.Length;i++)
            {
                letterCatalog.TryGet(text[i],out var glyph);AddLetter(view,glyph,phonicsColors[i]);
            }
            float min=float.PositiveInfinity,max=float.NegativeInfinity;
            foreach(var letter in view.letters)
            {
                min=Mathf.Min(min,letter.transform.localPosition.x+letter.LocalBounds.min.x*letter.transform.localScale.x);
                max=Mathf.Max(max,letter.transform.localPosition.x+letter.LocalBounds.max.x*letter.transform.localScale.x);
            }
            float total=max-min,centerX=(min+max)*0.5f;
            foreach(var letter in view.letters)letter.transform.localPosition-=Vector3.right*centerX;
            if(displayCamera!=null&&total>0)
            {
                Vector3 center=displayCamera.WorldToViewportPoint(root.position);
                float usable=Mathf.Min(Mathf.Clamp(viewportWidth,0.1f,0.95f),2*Mathf.Max(0.01f,Mathf.Min(center.x,1-center.x)-screenMargin));
                float available=Vector3.Distance(displayCamera.ViewportToWorldPoint(new Vector3(0,center.y,center.z)),
                    displayCamera.ViewportToWorldPoint(new Vector3(usable,center.y,center.z)));
                // 本工程键盘尺度与原项目差 25 倍：允许放大到目标视口宽度（原版只缩不放），
                // 同时限制字母高度 ≤ 视口高度 14%（否则宽幅面下字母爆炸占满全屏）
                float frustumH=2f*center.z*Mathf.Tan(displayCamera.fieldOfView*0.5f*Mathf.Deg2Rad);
                root.localScale=Vector3.one*Mathf.Clamp(Mathf.Min(available/total,frustumH*0.14f/letterCapHeight),0.02f,50f);
                // 双保险：实测单词包围盒的视口高度，校正到目标 14%（几何/目录度量误差兜底）
                if(displayCamera!=null&&view.letters.Count>0)
                {
                    var bb=new Bounds(view.letters[0].transform.position,Vector3.zero);
                    foreach(var l in view.letters){var lr=l.GetComponent<Renderer>();if(lr!=null)bb.Encapsulate(lr.bounds);}
                    if(bb.size.y>0.0001f)
                    {
                        var c0=displayCamera.WorldToViewportPoint(bb.center+Vector3.up*bb.extents.y);
                        var c1=displayCamera.WorldToViewportPoint(bb.center-Vector3.up*bb.extents.y);
                        float vh=Mathf.Abs(c0.y-c1.y);
                        if(vh>0.0001f)root.localScale*=Mathf.Clamp(0.14f/vh,0.05f,20f);
                    }
                }
            }
            UpdateTiltPivot(view);
            // Resolve the initial pointer pose before any waiting glyph becomes visible.
            ReadMouseTilt(out var initialPointer,out var pointerInside);
            InitializeWordTilt(view,initialPointer,pointerInside);
            // 单词入场动画已禁用（用户要求：不要入场动效，直接静止显示）
            current=view;words.Add(view);
            if(syllableStarts!=null&&syllableStarts.Count>1)ApplySyllableSplit(view,syllableStarts); // 音节双色+圆点（须在等待态上色前改 model 色）
            for(int i=0;i<view.letters.Count;i++)
            {
                var letter=view.letters[i];
                var activation=letter.Activation??letter.gameObject.AddComponent<LetterActivation>();
                activation.ShowWaiting(letter,WaitingColor(view,i));letter.gameObject.SetActive(true); // 等待态：拼读色低不透明度
            }
            SetWaitingFeedback(0,false,Color.white,0);
            return true;
        }
        int AddLetter(WordView view,LetterCatalog.Entry glyph,Color color)
        {
            var letter=letterPool.Count>0?letterPool.Pop():Instantiate(letterViewPrefab);
            letter.gameObject.SetActive(false);letter.transform.SetParent(view.root,false);
            letter.transform.localRotation=Quaternion.identity;
            float scale=letterCapHeight/letterCatalog.CapHeight;letter.transform.localScale=Vector3.one*scale;
            letter.Bind(glyph,letterBodyMaterial,letterBevelMaterial,color);
            int index;float originX;
            if(letterCatalog.HasNativeSpacing)
            {
                if(view.letters.Count>0)view.fontPen+=view.previousAdvance+letterCatalog.GetKerning(view.previousCharacter,glyph.Character);
                originX=view.fontPen+glyph.fontOriginOffset;
                index=view.model.AddPositioned(glyph.Character,color,glyph.layoutWidth*scale,(originX+letter.LocalBounds.min.x)*scale);
                view.previousAdvance=glyph.fontAdvance;view.previousCharacter=glyph.Character;
            }
            else
            {
                index=view.model.Add(glyph.Character,color,glyph.layoutWidth*scale);
                originX=view.model.GetOffset(index)/scale-letter.LocalBounds.min.x;
            }
            // A common baseline preserves lowercase sizes and descenders; only shading uses per-glyph height.
            letter.transform.localPosition=new Vector3(originX*scale,-letterCapHeight*0.5f,0);
            letter.name="Letter_"+index+"_"+glyph.Character;
            view.letters.Add(letter);view.destinations.Add(letter.LandingPoint);view.revealAt.Add(float.PositiveInfinity);
            if(!view.prepared)UpdateTiltPivot(view);
            return index;
        }
        public bool EmitPreparedLetter(int index,Transform origin)
        {
            if(!CanEmitPreparedLetter||origin==null||index<0||index>=current.model.Count||!current.emitted.Add(index))return false;
            // Fast typing takes priority over the cosmetic entrance; never drop its first letter.
            current.promptEntrance?.Complete();
            SetWaitingFeedback(index,false,Color.white,0);
            LaunchLetter(current,index,origin.position,current.model.GetColor(index));return true;
        }
        void LaunchLetter(WordView view,int index,Vector3 emissionPosition,Color color)
        {
            Transform destination=view.destinations[index];
            var effect=trailPool.Count>0?trailPool.Pop():CreateTrail();
            // 原预制体按 letterCapHeight=0.02 的尺度制作（粒子尺寸/拖尾宽度是绝对值）；
            // 本工程字高放大 N 倍，飞行器与 EnhancedTrailGame 光头同步放大
            float trailScale=letterCapHeight/0.02f;
            effect.transform.localScale=Vector3.one*trailScale;
            activeTrails.Add(effect);effect.gameObject.SetActive(true);
            // 彗星外观=EnhancedTrailGame ribbon 分层拖尾（参考工程同款），
            // 由 LaunchLetter 统一按 trailScale 缩放；基础粒子仅在 EnhancedTrailGame
            // 资源缺失时作为兜底拖尾（SingleTrailEffect.Launch 内部切换）
            effect.EnhancedVisualEnabled=true;
            // 兜底粒子拖尾的宽度/亮度（仅在增强路径不可用时生效）
            var cometTrails=effect.GetComponent<ParticleSystem>().trails;
            cometTrails.widthOverTrailMultiplier=cometWidthFactor;
            rendererProperties.Clear();rendererProperties.SetFloat(IntensityId,brightness*cometGlowFactor);
            effect.GetComponent<ParticleSystemRenderer>().SetPropertyBlock(rendererProperties);
            // 0.1 秒内到达目标：按发射点到目标的实际距离动态换算巡航速度
            float launchSpeed=Vector3.Distance(emissionPosition,destination.position)/0.1f;
            effect.Launch(emissionPosition,upAxis,destination,color,launchSpeed,riseTime,turnSpeed,
                arrivalDistance,maxFlightTime,trailLifetime,
                arrived=>ResolveLetter(view,index,arrived),RecycleTrail,
                true,curveWiggleAmplitude,curveWiggleFrequency);
            if(effect.EnhancedVisual!=null)effect.EnhancedVisual.transform.localScale=Vector3.one*trailScale;
            effect.ConfigureBounds(upAxis,maxArcHeight,displayCamera,screenMargin);
        }

        public void SubmitCurrentWord()
        {
            if(current==null || current.model.Count==0)return;
            if(current.prepared&&current.emitted.Count!=current.model.Count)return;
            current.model.Submit();current=null;
        }
        void ResolveLetter(WordView word,int index,bool arrived)
        {
            if(word.root==null||!word.resolved.Add(index))return;
            if(arrived)
            {
                if(!word.anchorFixed)
                {
                    word.anchorFixed=true;
                }
                Vector3 landing=word.destinations[index].position;
                var impact=impactPool.Count>0?impactPool.Pop():Instantiate(impactPrefab,runtimeRoot);
                impact.gameObject.SetActive(true);
                float handoff=word.prepared?Mathf.Clamp(grayContractionDuration,0.02f,0.10f):letterRevealDelay;
                if(word.prepared)word.letters[index].Activation.BeginContraction(enableLetterPop?letterPopStartScale:1,handoff);
                impact.PlayForLetter(landing,word.model.GetColor(index),displayCamera,word.letters[index],handoff,RecycleImpact);
                ImpactPlayed?.Invoke(word.model.Id,index,landing,word.model.GetColor(index));
                word.revealAt[index]=Time.time+handoff;
            }
            else
            {
                word.model.MarkFailed(index);
                Debug.LogWarning("[Single Trail] A letter could not reach B (target lost or flight timed out). It will not block Enter.",this);
            }
        }
        void RecycleImpact(SingleTrailImpact impact)
        {
            impact.gameObject.SetActive(false);
            if(impactPool.Count<maxCachedTrails)impactPool.Push(impact);else Destroy(impact.gameObject);
        }
        void RecycleTrail(SingleTrailEffect effect)
        {
            activeTrails.Remove(effect);effect.ResetForPool();effect.gameObject.SetActive(false);
            if(trailPool.Count<maxCachedTrails)trailPool.Push(effect);else Destroy(effect.gameObject);
        }
        static void UpdateTiltPivot(WordView word)
        {
            bool any=false;Bounds bounds=new Bounds();
            foreach(var letter in word.letters)
            {
                var b=letter.LocalBounds;var t=letter.transform;
                var min=t.localPosition+Vector3.Scale(b.min,t.localScale);
                var max=t.localPosition+Vector3.Scale(b.max,t.localScale);
                if(!any){bounds=new Bounds(min,Vector3.zero);any=true;}
                bounds.Encapsulate(min);bounds.Encapsulate(max);
            }
            word.tiltPivot=any?bounds.center:Vector3.zero;
        }
        void InitializeWordTilt(WordView word,Vector2 pointer,bool pointerActive)
        {
            var tilt=word.mouseTilt.Initialize(pointer,pointerActive&&enableWordMouseTilt,mouseYawAngle,mousePitchAngle);
            WordMouseTilt.Apply(word.root,word.neutralPosition,word.neutralRotation,word.tiltPivot,tilt);
        }
        void UpdateWordTilt(WordView word,Vector2 pointer,bool pointerActive,float deltaTime)
        {
            // Enter freezes the exact pose; pending flights still track these unchanged endpoints.
            if(word.model.Submitted)return;
            if(!word.anchorFixed&&targetB!=null)word.neutralPosition=targetB.position+wordOffset;
            var tilt=word.mouseTilt.Step(pointer,pointerActive&&enableWordMouseTilt,deltaTime,mouseYawAngle,mousePitchAngle,mouseTiltSmoothTime);
            WordMouseTilt.Apply(word.root,word.neutralPosition,word.neutralRotation,word.tiltPivot,tilt);
        }
        void ReadMouseTilt(out Vector2 pointer,out bool inside)
        {
            Rect area=displayCamera!=null?displayCamera.pixelRect:new Rect(0,0,Screen.width,Screen.height);
            Vector2 position=Input.mousePosition;
            inside=Application.isFocused&&area.width>0&&area.height>0&&area.Contains(position);
            pointer=inside?new Vector2((position.x-area.xMin)/area.width*2-1,(position.y-area.yMin)/area.height*2-1):Vector2.zero;
        }
        void Update()
        {
            if(words.Count==0)return;
            ReadMouseTilt(out var pointer,out var pointerInside);
            foreach(var word in words)
            {
                word.promptEntrance?.Step(Time.deltaTime);
                UpdateWordTilt(word,pointer,pointerInside,Time.unscaledDeltaTime);
                foreach(var letter in word.letters)
                {
                    letter.Activation?.AdvanceContraction(Time.deltaTime);
                    if(letter.gameObject.activeSelf)letter.AdvanceEntrance(Time.deltaTime);
                }
                for(int i=0;i<word.revealAt.Count;i++)
                {
                    if(Time.time<word.revealAt[i])continue;
                    word.revealAt[i]=float.PositiveInfinity;
                    if(word.model.MarkArrived(i))
                    {
                        if(word.prepared)
                        {
                            // Commit both renderer states before any frame is rendered. No crossfade.
                            word.letters[i].Activation.AdvanceContraction(1);
                            word.letters[i].Activation.Reveal();
                            if(enableLetterPop)word.letters[i].BeginOpaqueEntrance(letterPopStartScale,letterPopOvershoot,letterPopDuration);
                        }
                        else if(enableLetterPop)word.letters[i].BeginEntrance(letterPopStartScale,letterPopOvershoot,letterPopDuration);
                        word.letters[i].gameObject.SetActive(true);
                        LetterRevealed?.Invoke(word.model.Id,i,word.destinations[i].position,word.model.GetColor(i));
                    }
                }
                // Newly revealed letters stay at the exact handoff scale until the next frame.
            }
            var front=words[0];
            if(!front.root.gameObject.activeSelf)front.root.gameObject.SetActive(true);
            if(front.queuedLaunches.Count>0)
            {
                foreach(var launch in front.queuedLaunches)launch();
                front.queuedLaunches.Clear();
            }
            bool animating=false;
            foreach(var letter in front.letters)if(letter.IsEntrancePlaying||(letter.Activation!=null&&letter.Activation.IsContracting)){animating=true;break;}
            // 完词脉冲：全部字母点亮（拼完、按 Enter 之前）立即开始——root 整组放大
            // wordPulseScale 再复原（sin 半波）。基准取摆位后的 root.localScale（L280/L291
            // 适配过视口），脉冲结束正好复原。Enter 后若脉冲未播完，退场等它结束再开始
            if(front.exit==null&&front.pulseT<0f&&front.model.AllResolved)
            {
                front.pulseT=0f;
                front.pulseBaseScale=front.root.localScale;
                WordPulseStarted?.Invoke(front.model.Id); // 拼完即触发：完词脉冲 + 单词发音
            }
            if(front.pulseT>=0f&&front.pulseT<1f)
            {
                front.pulseT=Mathf.Min(1f,front.pulseT+Time.deltaTime/wordPulseDuration);
                front.root.localScale=front.pulseBaseScale*(1f+(wordPulseScale-1f)*Mathf.Sin(Mathf.PI*front.pulseT));
            }
            // Snapshot the selected effect when it starts. Arrival/input and legacy fade remain unchanged.
            bool jelly=wordExitStyle!=null&&wordExitMaterial!=null&&displayCamera!=null&&
                wordExitStyle.mode!=WordExitStyle.ExitMode.OriginalFade;
            if(front.exit!=null)
            {
                front.exit.Step(Time.deltaTime);
                if(front.exit.IsComplete){front.exit.Dispose();front.model.CompleteExit();}
            }
            else if(jelly)
            {
                float exitHold=wordExitStyle.mode==WordExitStyle.ExitMode.FlyingLetters&&(wordExitStyle.quickJump||wordExitStyle.wholeWordJump)?0:completedWordHold;
                if(!animating)front.model.Advance(Time.deltaTime,exitHold,float.MaxValue);
                if(!animating&&front.pulseT>=1f&&front.model.IsReadyForExit(exitHold))
                {
                    front.exit=new WordExitSequence(front.letters,displayCamera,runtimeRoot,wordExitStyle,wordExitMaterial);
                    WordExitStarted?.Invoke(front.model.Id, front.exit.Duration); // 退场动画正式开始（含精确时长）
                }
            }
            else
            {
                if(!animating)front.model.Advance(Time.deltaTime,completedWordHold,wordFadeDuration);
                for(int i=0;i<front.letters.Count;i++)front.letters[i].SetOpacity(front.model.Alpha);
            }
            if(!front.model.IsComplete)return;
            foreach(var letter in front.letters)
            {
                letter.gameObject.SetActive(false);letter.transform.SetParent(runtimeRoot,false);
                if(letterPool.Count<maxCachedLetters)letterPool.Push(letter);else Destroy(letter.gameObject);
            }
            int completedId=front.model.Id;
            Destroy(front.root.gameObject);words.RemoveAt(0);
            WordCompleted?.Invoke(completedId);
        }
    }
}
