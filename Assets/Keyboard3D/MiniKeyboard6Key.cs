using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Keyboard3D
{
    /// <summary>
    /// 六键迷你键盘（视频复刻）：
    ///   银灰色圆角底座 + 两排共 6 颗高键帽（上窄下宽圆角方台、顶面浅凹），
    ///   5 颗深炭黑半哑光键帽（白色文字），左前一颗绿色 "Main" 键。
    /// 绿键交互（与视频一致）：
    ///   静止 → 绿色柔和常亮（顶亮、侧壁深绿、边缘透亮）+ 零星微尘；
    ///   按下 → 整颗键帽下沉、顶面跃升为高亮荧光绿、侧壁保留深绿层次、
    ///          周围出现柔和绿色辉光 + 细小灰绿微尘持续向上向外飘散；
    ///   松开 → 回升、亮度回落到常亮、残留粒子继续漂移后消散。
    /// 几何体 / 材质 / 灯光 / 粒子全部运行时程序化生成，不依赖外部模型。
    /// 交互：鼠标左键按住键帽，或按各键映射的键盘键（默认绿键 = A）。
    /// </summary>
    [AddComponentMenu("Keyboard3D/Mini Keyboard 6Key (六键键盘 视频复刻)")]
    public class MiniKeyboard6Key : MonoBehaviour
    {
        [Header("布局（两排各三键，mainRow/mainCol 指定绿键位置）")]
        public string[] backRowLabels = { "Caps", "Shift", "Enter" };
        public string[] frontRowLabels = { "Main", "Alt", "Ctrl" };
        public KeyCode[] backRowKeys = { KeyCode.Q, KeyCode.W, KeyCode.E };
        public KeyCode[] frontRowKeys = { KeyCode.A, KeyCode.S, KeyCode.D };
        [Tooltip("绿键所在排（0=后排 1=前排）")]
        public int mainRow = 1;
        [Tooltip("绿键所在列（0=左 1=中 2=右）")]
        public int mainCol = 0;

        [Header("尺寸")]
        [Tooltip("键距（相邻键中心距）")]
        public float keyPitch = 0.72f;
        [Tooltip("键帽底面宽/深")]
        public float keyWidth = 0.62f;
        [Tooltip("键帽高")]
        public float keyHeight = 0.30f;
        [Tooltip("顶面相对底面的缩放（<1 = 上窄下宽方台）")]
        public float topScale = 0.80f;
        [Tooltip("底面圆角半径")]
        public float cornerRadius = 0.09f;
        [Tooltip("顶面浅凹深度")]
        public float dishDepth = 0.014f;

        [Header("底座")]
        public float baseWidth = 2.4f;
        public float baseDepth = 1.8f;
        [Tooltip("底座总高（含抬起的边框）")]
        public float baseHeight = 0.15f;
        [Tooltip("底座内凹地面高度（键帽安装面）")]
        public float baseFloorHeight = 0.10f;
        [Tooltip("边框内缩（决定抬起的边框宽度）")]
        public float rimInset = 0.12f;
        public Color baseColor = new Color(0.62f, 0.64f, 0.66f);

        [Header("按压")]
        [Tooltip("按下时键帽下沉深度（相对键帽高度的比例）")]
        public float pressDepthRatio = 0.32f;
        [Tooltip("按下速度（0→1 每秒）")]
        public float pressSpeed = 14f;
        [Tooltip("回弹速度（0→1 每秒）；视频无明显回弹/过冲，用线性趋近")]
        public float releaseSpeed = 10f;
        [Tooltip("启用鼠标左键按住键帽（自动加碰撞器）")]
        public bool enableMouseClick = true;

        [Header("绿键发光")]
        [Tooltip("静止常亮发光色（较沉稳的翠绿）")]
        public Color idleGlowColor = new Color(0f, 0.85f, 0.30f);
        [Tooltip("按下发光色（荧光绿、略带黄感）")]
        public Color pressedGlowColor = new Color(0.55f, 1f, 0.25f);
        [Tooltip("静止发光强度（低亮度常亮）")]
        public float idleGlowIntensity = 0.65f;
        [Tooltip("按下发光强度（HDR 高亮，配合 Bloom 出辉光）")]
        public float pressedGlowIntensity = 3.4f;
        [Tooltip("按下时发光增强速度（0→1 每秒，视频为迅速增强）")]
        public float glowAttackSpeed = 18f;
        [Tooltip("松开后发光回落速度（0→1 每秒）")]
        public float glowReleaseSpeed = 8f;
        [Tooltip("绿键反照率（深绿实体外壳）")]
        public Color glowAlbedo = new Color(0f, 0.549f, 0.259f);
        [Tooltip("侧壁发光遮罩顶缘值（顶缘绿色透亮带）")]
        public float sideGlowTop = 0.4f;
        [Tooltip("侧壁发光遮罩底部值（往下沉黑）")]
        public float sideGlowBottom = 0.06f;
        [Tooltip("侧壁绿光衰减曲线指数：越大越集中在顶缘")]
        public float sideGlowCurve = 2f;
        [Tooltip("顶面发光径向衰减：越大边缘越暗（按下时保留侧壁深绿层次的关键）")]
        public float topGlowFalloff = 0.55f;
        [Tooltip("绿键键帽光滑度")]
        public float greenSmoothness = 0.7f;

        [Header("黑键配色")]
        public Color keycapBlack = new Color(0.063f, 0.094f, 0.114f);
        [Tooltip("黑键帽光滑度（半哑光塑料）")]
        public float blackSmoothness = 0.55f;

        [Header("周围辉光（点光，跟随绿键）")]
        public bool enableHaloLight = true;
        public Color haloColor = new Color(0.25f, 1f, 0.35f);
        [Tooltip("顶部点光强度（键帽上方，照出周围柔和绿辉光）")]
        public float haloIntensity = 2.2f;
        [Tooltip("低位溢光强度（键高中部，把绿光洒到邻键侧壁/底座）")]
        public float spillIntensity = 1.4f;

        [Header("粒子（细小灰绿微尘）")]
        public bool enableParticles = true;
        [Tooltip("静止每秒发射数（零星、很淡）")]
        public float idleParticleRate = 4f;
        [Tooltip("按下每秒发射数（明显增加、持续释放）")]
        public float pressedEmissionRate = 45f;
        [Tooltip("粒子最小/最大尺寸（远小于键帽文字）")]
        public float particleSizeMin = 0.006f;
        public float particleSizeMax = 0.016f;
        [Tooltip("粒子寿命范围（秒）")]
        public float lifetimeMin = 1.2f;
        public float lifetimeMax = 2.2f;
        [Tooltip("上升速度")]
        public float riseSpeed = 0.14f;

        readonly List<KeySlot> slots = new List<KeySlot>();
        readonly Dictionary<Transform, KeySlot> byRoot = new Dictionary<Transform, KeySlot>();
        KeySlot mouseSlot;

        static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
        static readonly int ColorID = Shader.PropertyToID("_Color");
        static readonly int EmissionID = Shader.PropertyToID("_EmissionColor");
        static readonly int EmissionMapID = Shader.PropertyToID("_EmissionMap");
        static readonly int SmoothnessID = Shader.PropertyToID("_Smoothness");
        static readonly int GlossID = Shader.PropertyToID("_Glossiness");
        static readonly int MetallicID = Shader.PropertyToID("_Metallic");

        class KeySlot
        {
            public Transform root;
            public Vector3 originLocal;
            public float depth;
            public KeyCode code;
            public bool isMain;
            public float amount;   // 按下量 0..1
            public float glow;     // 绿键发光量 0..1
            public bool mouseHeld;
            public bool pressed;   // 本帧最终按下状态（鼠标或键盘）
            public Material capMat;
            public Light halo;
            public Light spill;
            public ParticleSystem ps;
        }

        void Start()
        {
            BuildKeyboard();
            SetupUrpPostFx();
        }

        void Update()
        {
            ReadInput();
            float dt = Time.deltaTime;
            foreach (var s in slots) DriveSlot(s, dt);
        }

        // ─────────────────────────── 输入 ───────────────────────────

        void ReadInput()
        {
            if (enableMouseClick)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    var s = PickSlotUnderMouse();
                    if (s != null) { mouseSlot = s; s.mouseHeld = true; }
                }
                if (Input.GetMouseButtonUp(0) && mouseSlot != null)
                {
                    mouseSlot.mouseHeld = false;
                    mouseSlot = null;
                }
            }
            foreach (var s in slots)
                s.pressed = s.mouseHeld || (s.code != KeyCode.None && Input.GetKey(s.code));
        }

        KeySlot PickSlotUnderMouse()
        {
            var cam = Camera.main;
            if (cam == null) return null;
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 100f, ~0, QueryTriggerInteraction.Ignore))
                return byRoot.TryGetValue(hit.collider.transform, out var s) ? s : null;
            return null;
        }

        // ─────────────────────────── 驱动 ───────────────────────────

        void DriveSlot(KeySlot s, float dt)
        {
            // 按压：线性趋近目标（视频下压/回升短促直接，无回弹过冲）
            float target = s.pressed ? 1f : 0f;
            float speed = s.pressed ? pressSpeed : releaseSpeed;
            s.amount = Mathf.MoveTowards(s.amount, target, speed * dt);
            s.root.localPosition = s.originLocal + Vector3.down * (s.depth * s.amount);

            if (!s.isMain) return;

            // 发光：下压迅速增强、保持期间持续、松开回落到常亮
            s.glow = Mathf.MoveTowards(s.glow, s.pressed ? 1f : 0f,
                (s.pressed ? glowAttackSpeed : glowReleaseSpeed) * dt);
            ApplyGlow(s);
        }

        void ApplyGlow(KeySlot s)
        {
            if (s.capMat != null)
            {
                Color col = Color.Lerp(idleGlowColor, pressedGlowColor, s.glow);
                float inten = Mathf.Lerp(idleGlowIntensity, pressedGlowIntensity, s.glow);
                s.capMat.SetColor(EmissionID, col * inten);
            }
            if (s.halo != null) s.halo.intensity = s.glow * haloIntensity;
            if (s.spill != null) s.spill.intensity = s.glow * spillIntensity;
            if (s.ps != null)
            {
                var em = s.ps.emission;
                em.rateOverTime = Mathf.Lerp(idleParticleRate, pressedEmissionRate, s.glow);
            }
        }

        // ─────────────────────────── 场景构建 ───────────────────────────

        void BuildKeyboard()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) lit = Shader.Find("Standard");

            BuildBase(lit);
            BuildKeys(lit);
        }

        void BuildBase(Shader lit)
        {
            var root = new GameObject("Base");
            root.transform.SetParent(transform, false);

            // 内凹地面（键帽安装面）
            var floorGo = new GameObject("BaseFloor");
            floorGo.transform.SetParent(root.transform, false);
            var floorMf = floorGo.AddComponent<MeshFilter>();
            floorMf.sharedMesh = BuildSlabMesh(baseWidth - rimInset * 2f, baseDepth - rimInset * 2f, baseFloorHeight, 0.05f);
            var floorMr = floorGo.AddComponent<MeshRenderer>();
            floorMr.sharedMaterial = MakePlainMaterial(lit, baseColor * 0.92f, 0.4f, 0.55f);

            // 抬起的边框（环形，外沿=底座外沿，比内凹地面高）
            var rimGo = new GameObject("BaseRim");
            rimGo.transform.SetParent(root.transform, false);
            var rimMf = rimGo.AddComponent<MeshFilter>();
            rimMf.sharedMesh = BuildFrameMesh(baseWidth, baseDepth, 0.12f,
                baseWidth - rimInset * 2f, baseDepth - rimInset * 2f, 0.06f, baseHeight);
            var rimMr = rimGo.AddComponent<MeshRenderer>();
            rimMr.sharedMaterial = MakePlainMaterial(lit, baseColor, 0.75f, 0.45f);
        }

        void BuildKeys(Shader lit)
        {
            float floorY = baseFloorHeight;
            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    bool isMain = row == mainRow && col == mainCol;
                    string label = row == 0
                        ? (col < backRowLabels.Length ? backRowLabels[col] : "")
                        : (col < frontRowLabels.Length ? frontRowLabels[col] : "");
                    KeyCode code = row == 0
                        ? (col < backRowKeys.Length ? backRowKeys[col] : KeyCode.None)
                        : (col < frontRowKeys.Length ? frontRowKeys[col] : KeyCode.None);

                    var root = new GameObject($"Key_{row}_{col}_{label}");
                    root.transform.SetParent(transform, false);
                    root.transform.localPosition = new Vector3(
                        (col - 1) * keyPitch, floorY, (row == 0 ? -0.5f : 0.5f) * keyPitch);

                    // 键帽网格
                    var capGo = new GameObject("Cap");
                    capGo.transform.SetParent(root.transform, false);
                    var mf = capGo.AddComponent<MeshFilter>();
                    mf.sharedMesh = BuildKeycapMesh();
                    var mr = capGo.AddComponent<MeshRenderer>();
                    var mat = MakeCapMaterial(lit, isMain);
                    mr.sharedMaterial = mat;

                    // 文字
                    CreateLabel(root.transform, label);

                    // 碰撞器（鼠标拾取）
                    var bc = root.AddComponent<BoxCollider>();
                    bc.center = new Vector3(0f, keyHeight * 0.5f, 0f);
                    bc.size = new Vector3(keyWidth, keyHeight, keyWidth);

                    var slot = new KeySlot
                    {
                        root = root.transform,
                        originLocal = root.transform.localPosition,
                        depth = keyHeight * pressDepthRatio,
                        code = code,
                        isMain = isMain,
                        capMat = mat,
                    };

                    if (isMain)
                    {
                        if (enableHaloLight) CreateMainLights(slot);
                        if (enableParticles) slot.ps = CreateDustParticles(slot);
                        ApplyGlow(slot); // 初始常亮
                    }

                    byRoot[root.transform] = slot;
                    slots.Add(slot);
                }
            }
        }

        void CreateLabel(Transform parent, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            // 顶面浅凹中心略上方；Euler(-90,0,0)：文字正面朝上、字头朝 -Z（面向 +Z 方向的相机正读）
            go.transform.localPosition = new Vector3(0f, keyHeight - dishDepth + 0.003f, 0f);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            // 内置 TextMesh（不再依赖 TMP）：fontSize 64 × characterSize 0.1 ≈ 0.64 键宽级字号
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontStyle = FontStyle.Bold;
            tm.fontSize = 64;
            tm.characterSize = 0.1f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font;
            var rend = go.GetComponent<MeshRenderer>();
            rend.sharedMaterial = font.material;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }

        void CreateMainLights(KeySlot slot)
        {
            var haloGo = new GameObject("MainHaloLight");
            haloGo.transform.SetParent(slot.root, false);
            haloGo.transform.localPosition = new Vector3(0f, keyHeight + 0.22f, 0f);
            slot.halo = haloGo.AddComponent<Light>();
            slot.halo.type = LightType.Point;
            slot.halo.color = haloColor;
            slot.halo.range = 1.1f;
            slot.halo.intensity = 0f;
            slot.halo.shadows = LightShadows.None;

            var spillGo = new GameObject("MainSpillLight");
            spillGo.transform.SetParent(slot.root, false);
            spillGo.transform.localPosition = new Vector3(0f, keyHeight * 0.45f, 0f);
            slot.spill = spillGo.AddComponent<Light>();
            slot.spill.type = LightType.Point;
            slot.spill.color = haloColor;
            slot.spill.range = 1.7f;
            slot.spill.intensity = 0f;
            slot.spill.shadows = LightShadows.None;
        }

        // ─────────────────────────── 材质 ───────────────────────────

        Material MakePlainMaterial(Shader lit, Color color, float metallic, float smoothness)
        {
            var m = new Material(lit);
            if (m.HasProperty(BaseColorID)) m.SetColor(BaseColorID, color);
            else if (m.HasProperty(ColorID)) m.SetColor(ColorID, color);
            if (m.HasProperty(MetallicID)) m.SetFloat(MetallicID, metallic);
            if (m.HasProperty(SmoothnessID)) m.SetFloat(SmoothnessID, smoothness);
            return m;
        }

        Material MakeCapMaterial(Shader lit, bool isMain)
        {
            var m = MakePlainMaterial(lit, isMain ? glowAlbedo : keycapBlack, 0.05f,
                isMain ? greenSmoothness : blackSmoothness);
            if (!isMain) return m;

            // 绿键：自发光遮罩（顶面径向渐变 + 侧壁顶缘绿光条）→ 顶亮、侧壁深绿、边缘透亮
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetTexture(EmissionMapID, EmissionMask(sideGlowTop, sideGlowBottom, sideGlowCurve, topGlowFalloff));
            m.SetColor(EmissionID, idleGlowColor * idleGlowIntensity);
            return m;
        }

        // 自发光遮罩布局（与 KeyboardModelInput 同一套约定）：
        //   主体 = 径向渐变（顶面采样：中心亮、边缘按 topGlowFalloff 压暗）
        //   左侧竖条（u<0.06, v<0.7）= 侧面专用：顶缘 sideGlowTop → 底部 sideGlowBottom 幂次衰减
        static readonly Dictionary<(float, float, float, float), Texture2D> emissionMaskCache
            = new Dictionary<(float, float, float, float), Texture2D>();
        static Texture2D EmissionMask(float sideTop, float sideBottom, float curve, float topFalloff)
        {
            var key = (sideTop, sideBottom, curve, topFalloff);
            if (emissionMaskCache.TryGetValue(key, out var cached)) return cached;
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(u * u + v * v);
                    float k = Mathf.Clamp01(1f - d * topFalloff);
                    px[y * size + x] = new Color(k, k, k, 1f);
                }
            int stripW = (int)(size * 0.06f);
            int stripH = (int)(size * 0.70f);
            for (int y = 0; y < stripH; y++)
            {
                float t = (float)y / (stripH - 1); // 0=底 → 1=顶
                float k = Mathf.Lerp(sideBottom, sideTop, Mathf.Pow(t, curve));
                for (int x = 0; x < stripW; x++)
                    px[y * size + x] = new Color(k, k, k, 1f);
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            emissionMaskCache[key] = tex;
            return tex;
        }

        // 圆形软边粒子贴图（不贴图时 URP/Particles/Unlit 会画成实心方块）
        static Texture2D circleTex;
        static Texture2D CircleTex()
        {
            if (circleTex != null) return circleTex;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / (size * 0.5f);
                    float a = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((d - 0.7f) / 0.3f));
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply(false, true);
            circleTex = tex;
            return tex;
        }

        // ─────────────────────────── 粒子 ───────────────────────────

        ParticleSystem CreateDustParticles(KeySlot slot)
        {
            var go = new GameObject("DustParticles");
            go.transform.SetParent(slot.root, false);
            go.transform.localPosition = new Vector3(0f, keyHeight - dishDepth * 0.5f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            // 细小、低对比度：淡灰/灰绿，透明感强
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.85f, 0.88f, 0.85f, 0.28f),
                new Color(0.70f, 0.82f, 0.70f, 0.50f));
            main.startSize = new ParticleSystem.MinMaxCurve
            {
                mode = ParticleSystemCurveMode.TwoConstants,
                constantMin = particleSizeMin,
                constantMax = particleSizeMax,
            };
            main.startLifetime = new ParticleSystem.MinMaxCurve
            {
                mode = ParticleSystemCurveMode.TwoConstants,
                constantMin = lifetimeMin,
                constantMax = lifetimeMax,
            };
            main.startSpeed = new ParticleSystem.MinMaxCurve
            {
                mode = ParticleSystemCurveMode.TwoConstants,
                constantMin = 0.03f,
                constantMax = 0.12f,
            };
            main.gravityModifier = 0f;
            main.maxParticles = 400;
            main.simulationSpace = ParticleSystemSimulationSpace.World; // 键帽回升后残留粒子继续漂移

            // 半球体积：键帽上方生成，向外+向上散开，方向随机、幅度细碎
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.20f;
            shape.radiusThickness = 1f;

            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.y = riseSpeed;   // 整体上升趋势
            vol.x = 0f;
            vol.z = 0f;

            // 轻噪声 → 漂散的随机感（有的向左上、有的停留周边）
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.045f;
            noise.frequency = 0.4f;
            noise.scrollSpeed = 0.1f;

            // 生命周期：淡入快、淡出慢（松开后残留粒子逐渐变淡消失）
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                mode = GradientMode.Blend,
                colorKeys = new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f),
                },
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.1f),
                    new GradientAlphaKey(0.9f, 0.6f),
                    new GradientAlphaKey(0f, 1f),
                },
            });

            // 轻微缩小
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.5f));

            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = idleParticleRate; // 静止零星；按下时由 ApplyGlow 调高

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            Shader shader = GraphicsSettings.currentRenderPipeline != null
                ? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                : Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                var pm = new Material(shader);
                if (pm.HasProperty("_BaseColor")) pm.SetColor("_BaseColor", Color.white);
                if (pm.HasProperty("_TintColor")) pm.SetColor("_TintColor", Color.white);
                if (pm.HasProperty("_BaseMap")) pm.SetTexture("_BaseMap", CircleTex());
                else if (pm.HasProperty("_MainTex")) pm.SetTexture("_MainTex", CircleTex());
                // 显式透明 Alpha 混合（构建时保证透明变体进包）
                if (pm.HasProperty("_Surface")) pm.SetFloat("_Surface", 1f);
                pm.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                if (pm.HasProperty("_Blend")) pm.SetFloat("_Blend", 0f);
                if (pm.HasProperty("_SrcBlend")) pm.SetFloat("_SrcBlend", 5f);
                if (pm.HasProperty("_DstBlend")) pm.SetFloat("_DstBlend", 10f);
                if (pm.HasProperty("_ZWrite")) pm.SetFloat("_ZWrite", 0f);
                pm.renderQueue = 3000;
                rend.material = pm;
            }
            return ps;
        }

        // ─────────────────────────── URP 后处理 ───────────────────────────

        void SetupUrpPostFx()
        {
            if (GraphicsSettings.currentRenderPipeline == null) return;
            var cam = Camera.main;
            if (cam == null) return;
            cam.allowHDR = true;
            var extra = cam.GetComponent<UniversalAdditionalCameraData>();
            if (extra == null) extra = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            extra.renderPostProcessing = true;

            var vol = Object.FindObjectOfType<Volume>();
            if (vol == null)
            {
                var volGo = new GameObject("MiniKeyboardGlobalVolume");
                vol = volGo.AddComponent<Volume>();
                vol.isGlobal = true;
                vol.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            }
            var profile = vol.profile;
            Bloom bloom = null;
            Tonemapping tm = null;
            foreach (var c in profile.components)
            {
                if (c is Bloom bl) bloom = bl;
                else if (c is Tonemapping t2) tm = t2;
            }
            if (bloom == null) bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.45f);
            if (tm == null) tm = profile.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.ACES);

            // 浅灰摄影棚环境（视频背景）
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.60f, 0.61f, 0.63f);
        }

        // ─────────────────────────── 程序化网格 ───────────────────────────

        const int N = 48; // 轮廓点数（圆角方台平滑度）

        // 从中心沿 dir 方向与圆角矩形边界的交点（凸形 → 射线恒有一个交点）
        static Vector2 RoundedRectDir(Vector2 dir, float hw, float hd, float r)
        {
            float dx = dir.x, dy = dir.y;
            float tx = Mathf.Abs(dx) > 1e-5f ? hw / Mathf.Abs(dx) : float.MaxValue;
            float ty = Mathf.Abs(dy) > 1e-5f ? hd / Mathf.Abs(dy) : float.MaxValue;
            float t = Mathf.Min(tx, ty);
            Vector2 p = dir * t;
            float sx = hw - r, sy = hd - r;
            if (Mathf.Abs(p.x) > sx && Mathf.Abs(p.y) > sy) // 落在角部圆弧区
            {
                Vector2 c = new Vector2(Mathf.Sign(dx) * sx, Mathf.Sign(dy) * sy);
                float b = Vector2.Dot(c, dir);
                float disc = Mathf.Max(0f, b * b - (c.sqrMagnitude - r * r));
                p = dir * (-b + Mathf.Sqrt(disc));
            }
            return p;
        }

        static Vector2[] Contour(float hw, float hd, float r)
        {
            var pts = new Vector2[N];
            for (int i = 0; i < N; i++)
            {
                float a = i * Mathf.PI * 2f / N;
                pts[i] = RoundedRectDir(new Vector2(Mathf.Cos(a), Mathf.Sin(a)), hw, hd, r);
            }
            return pts;
        }

        // 键帽：上窄下宽圆角方台 + 顶面浅凹（平滑侧壁连续倒角，顶缘法线折断出高光带）
        Mesh BuildKeycapMesh()
        {
            var bottom = Contour(keyWidth * 0.5f, keyWidth * 0.5f, cornerRadius);
            float[] ts = { 0f, 0.18f, 0.36f, 0.55f, 0.72f, 0.86f, 1f };
            int M = ts.Length;

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // 侧壁：环向共享顶点 → 法线平滑，倒角处出连续反光带
            for (int j = 0; j < M; j++)
            {
                float t = ts[j];
                float s = Mathf.Lerp(1f, topScale, Mathf.SmoothStep(0f, 1f, t));
                float y = t * keyHeight;
                for (int i = 0; i < N; i++)
                {
                    verts.Add(new Vector3(bottom[i].x * s, y, bottom[i].y * s));
                    uvs.Add(new Vector2(0.01f, 0.05f + 0.62f * t)); // 采样发光遮罩左侧竖条
                }
            }
            for (int j = 0; j < M - 1; j++)
                for (int i = 0; i < N; i++)
                {
                    int i2 = (i + 1) % N;
                    int L0 = j * N + i, L1 = j * N + i2;
                    int U0 = (j + 1) * N + i, U1 = (j + 1) * N + i2;
                    tris.Add(L0); tris.Add(U0); tris.Add(L1);
                    tris.Add(L1); tris.Add(U0); tris.Add(U1);
                }

            // 顶面（浅凹）：rim → 内环1 → 内环2 → 中心，独立顶点（顶缘法线折断）
            int capStart = verts.Count;
            for (int i = 0; i < N; i++)
            {
                var p = bottom[i] * topScale;
                verts.Add(new Vector3(p.x, keyHeight, p.y));
                uvs.Add(TopUv(p));
            }
            float dishY1 = keyHeight - dishDepth * 0.45f;
            float dishY2 = keyHeight - dishDepth * 0.85f;
            for (int i = 0; i < N; i++)
            {
                var p = bottom[i] * (topScale * 0.62f);
                verts.Add(new Vector3(p.x, dishY1, p.y));
                uvs.Add(TopUv(p));
            }
            for (int i = 0; i < N; i++)
            {
                var p = bottom[i] * (topScale * 0.28f);
                verts.Add(new Vector3(p.x, dishY2, p.y));
                uvs.Add(TopUv(p));
            }
            int centerIdx = verts.Count;
            verts.Add(new Vector3(0f, keyHeight - dishDepth, 0f));
            uvs.Add(new Vector2(0.5f, 0.5f));

            int rim0 = capStart, ring1 = capStart + N, ring2 = capStart + 2 * N;
            for (int i = 0; i < N; i++)
            {
                int i2 = (i + 1) % N;
                // 内环 a → 外环 b，顶面法线 +Y 的绕序
                AddQuad(tris, ring1 + i, ring1 + i2, rim0 + i, rim0 + i2);
                AddQuad(tris, ring2 + i, ring2 + i2, ring1 + i, ring1 + i2);
                tris.Add(ring2 + i); tris.Add(ring2 + i2); tris.Add(centerIdx);
            }

            var mesh = new Mesh { name = "MiniKeycap" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        Vector2 TopUv(Vector2 p)
        {
            float u = 0.08f + 0.84f * (p.x / keyWidth + 0.5f);
            float v = 0.08f + 0.84f * (p.y / keyWidth + 0.5f);
            return new Vector2(u, v);
        }

        static void AddQuad(List<int> tris, int a0, int a1, int b0, int b1)
        {
            // a=内环、b=外环：两个三角形，顶面朝上绕序
            tris.Add(a0); tris.Add(a1); tris.Add(b0);
            tris.Add(a1); tris.Add(b1); tris.Add(b0);
        }

        // 实心圆角矩形板（底座内凹地面）
        Mesh BuildSlabMesh(float w, float d, float h, float r)
        {
            var c = Contour(w * 0.5f, d * 0.5f, r);
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (int i = 0; i < N; i++) { verts.Add(new Vector3(c[i].x, 0f, c[i].y)); uvs.Add(new Vector2(0.5f, 0f)); }
            for (int i = 0; i < N; i++) { verts.Add(new Vector3(c[i].x, h, c[i].y)); uvs.Add(new Vector2(0.5f, 1f)); }
            for (int i = 0; i < N; i++)
            {
                int i2 = (i + 1) % N;
                int L0 = i, L1 = i2, U0 = N + i, U1 = N + i2;
                tris.Add(L0); tris.Add(U0); tris.Add(L1);
                tris.Add(L1); tris.Add(U0); tris.Add(U1);
            }
            int centerIdx = verts.Count;
            verts.Add(new Vector3(0f, h, 0f)); uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i < N; i++) // 顶盖：+Y
            {
                int i2 = (i + 1) % N;
                tris.Add(centerIdx); tris.Add(N + i2); tris.Add(N + i);
            }
            centerIdx = verts.Count;
            verts.Add(new Vector3(0f, 0f, 0f)); uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i < N; i++) // 底盖：-Y
            {
                int i2 = (i + 1) % N;
                tris.Add(centerIdx); tris.Add(i); tris.Add(i2);
            }

            var mesh = new Mesh { name = "MiniBaseSlab" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // 圆角矩形框（底座抬起的边框：外轮廓 + 内孔，环形上下盖）
        Mesh BuildFrameMesh(float wOut, float dOut, float rOut, float wIn, float dIn, float rIn, float h)
        {
            var o = Contour(wOut * 0.5f, dOut * 0.5f, rOut);
            var ic = Contour(wIn * 0.5f, dIn * 0.5f, rIn);
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (int i = 0; i < N; i++) { verts.Add(new Vector3(o[i].x, 0f, o[i].y)); uvs.Add(new Vector2(0f, 0f)); }
            for (int i = 0; i < N; i++) { verts.Add(new Vector3(o[i].x, h, o[i].y)); uvs.Add(new Vector2(0f, 1f)); }
            for (int i = 0; i < N; i++) { verts.Add(new Vector3(ic[i].x, 0f, ic[i].y)); uvs.Add(new Vector2(1f, 0f)); }
            for (int i = 0; i < N; i++) { verts.Add(new Vector3(ic[i].x, h, ic[i].y)); uvs.Add(new Vector2(1f, 1f)); }

            int oL0 = 0, oU0 = N, iL0 = 2 * N, iU0 = 3 * N;
            for (int i = 0; i < N; i++)
            {
                int i2 = (i + 1) % N;
                int oL1 = i2, oU1 = N + i2, iL1 = 2 * N + i2, iU1 = 3 * N + i2;
                // 外壁：法线朝外
                tris.Add(oL0 + i); tris.Add(oU0 + i); tris.Add(oL0 + i2);
                tris.Add(oL0 + i2); tris.Add(oU0 + i); tris.Add(oU0 + i2);
                // 内壁：法线朝内（朝框中心）
                tris.Add(iL0 + i); tris.Add(iL0 + i2); tris.Add(iU0 + i);
                tris.Add(iL0 + i2); tris.Add(iU0 + i2); tris.Add(iU0 + i);
                // 顶环盖：+Y（内环 a、外环 b）
                tris.Add(iU0 + i); tris.Add(iU0 + i2); tris.Add(oU0 + i);
                tris.Add(iU0 + i2); tris.Add(oU0 + i2); tris.Add(oU0 + i);
                // 底环盖：-Y（反向）
                tris.Add(iL0 + i); tris.Add(oL0 + i); tris.Add(iL0 + i2);
                tris.Add(iL0 + i2); tris.Add(oL0 + i); tris.Add(oL0 + i2);
            }

            var mesh = new Mesh { name = "MiniBaseRim" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
