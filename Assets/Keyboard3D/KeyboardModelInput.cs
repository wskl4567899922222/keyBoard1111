using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Keyboard3D
{
    /// <summary>
    /// 挂在键盘模型（Keyboard68.fbx 实例）根物体上。
    /// 自动按空间位置识别 67 个键帽并映射到物理键盘 KeyCode：
    /// 按下按键 → 对应键帽下沉；松开 → 弹回。也支持鼠标点击键帽。
    /// </summary>
    [AddComponentMenu("Keyboard3D/Keyboard Model Input (模型键盘按键输入)")]
    public class KeyboardModelInput : MonoBehaviour
    {
        [Tooltip("按下时键帽下沉深度（相对键帽高度的比例）")]
        public float pressDepthRatio = 0.35f;
        [Tooltip("按下速度（0→1 每秒）")]
        public float pressSpeed = 20f;
        [Tooltip("回弹速度（0→1 每秒）")]
        public float releaseSpeed = 12f;
        [Tooltip("启用鼠标点击键帽（自动加碰撞器）")]
        public bool enableMouseClick = true;

        [Header("发光")]
        public bool enableGlow = true;
        [Tooltip("发光颜色（要纯绿：红/蓝通道接近 0，否则会偏青发白）")]
        public Color glowColor = new Color(0f, 1f, 0.12f);
        [Tooltip("发光强度（无 Bloom 时建议 1.2~1.6，过高会过曝发白）")]
        public float glowIntensity = 1.5f;

        [Header("目标键点光源（照明周围键帽）")]
        [Tooltip("启用目标键点光源（全场景只有一个，跟随目标键移动）")]
        public bool enablePointLight = true;
        [Tooltip("点光源颜色（与发光同色系的绿）")]
        public Color pointLightColor = new Color(0.2f, 1f, 0.3f);
        [Tooltip("点光源强度")]
        public float pointLightIntensity = 2.4f;
        [Tooltip("点光源照射范围（1.5~2.5 能照亮相邻键帽，太大整块键盘都变绿）")]
        public float pointLightRange = 1.8f;
        [Tooltip("点光源在键帽上方的高度（倍数 × 键帽高）")]
        public float pointLightHeight = 1.4f;
        [Tooltip("聚光灯锥角（度）：罩住键帽即可，水平方向无光，邻键/底板缝隙不被照亮")]
        public float pointLightSpotAngle = 118f;
        [Tooltip("宽键（空格等）两端补光聚光灯强度")]
        public float pointLightFlankIntensity = 12f;
        [Tooltip("周围泛光点光强度（邻键侧面+底板染绿）")]
        public float haloLightIntensity = 3.5f;
        [Tooltip("顶灯仅在按压时点亮（true=初始态顶面只留自发光径向渐变，中心亮、边缘暗；false=旧行为，目标键顶灯恒亮）")]
        public bool lightOnPressOnly = false;
        [Tooltip("lightOnPressOnly 模式下按压时顶灯的常亮底率（0=完全跟手，0.4=按下即有 40% 亮度）")]
        public float lightPressBase = 0.4f;
        [Tooltip("lightOnPressOnly 模式下按到底时顶灯的峰值倍率（默认 1.6=旧行为；顶面全亮冲爆就调低，如 1.0）")]
        public float lightPressPeak = 1.6f;
        [Tooltip("lightOnPressOnly 模式下按到底时泛光灯的恢复比例（默认 1=旧行为；泛光贴键顶极近，按下全亮主因之一，如 0.45）")]
        public float haloPressPeak = 1f;

        [Header("补光（参考图双高光：主光+补光在键帽圆角两侧各切出一条竖向高光）")]
        [Tooltip("启用第二盏方向补光（false=旧行为单光源，只有一条高光）")]
        public bool enableFillLight = false;
        [Tooltip("补光强度（0.3~0.6：够切出第二条高光但不洗白暗部）")]
        public float fillLightIntensity = 0.45f;
        [Tooltip("补光颜色（默认偏冷白，与暖主光形成层次）")]
        public Color fillLightColor = new Color(0.9f, 0.95f, 1f);
        [Tooltip("补光方向欧拉角：主光约 Euler(40,-30,0) 从右上打，补光默认从前左打，在左前圆角切出第二条高光")]
        public Vector3 fillLightAngles = new Vector3(35f, 55f, 0f);

        [Tooltip("邻键溢光（参考图：发光键把绿光洒到邻键侧壁）：低位点光，低于键帽顶面 → 照不到自家顶面")]
        public bool enableSpillLight = true;
        [Tooltip("邻键溢光强度（1~2：太亮会让邻键像发光键）")]
        public float spillLightIntensity = 1.6f;
        [Tooltip("邻键溢光范围（2 左右：罩住相邻一圈键）")]
        public float spillLightRange = 2.2f;

        [Header("粒子尘埃（辅助反馈）")]
        public bool enableParticles = true;
        [Tooltip("松开按键后，旧目标键粒子保留漂浮的秒数（0=立即开始渐隐）")]
        public float particleFadeDelay = 0f;
        [Tooltip("粒子渐隐时长：保留期结束后材质 alpha 从 1 渐降到 0 的秒数（渐变消失而非瞬间清除）")]
        public float particleFadeDuration = 1.0f;
        [Tooltip("平时每秒发射数（≈26/s × 0.23s 寿命 ≈ 同屏稳定 6 颗）")]
        public float idleEmissionRate = 26f;
        [Tooltip("按住时每秒发射数（更密集）")]
        public float pressedEmissionRate = 150f;
        [Tooltip("按下瞬间的爆发粒子数")]
        public int pressedBurst = 20;
        [Tooltip("粒子生成球壳半径")]
        public float particleRadius = 0.5f;
        [Tooltip("粒子向汇聚点收拢的速度")]
        public float convergeSpeed = 2.2f;
        [Tooltip("粒子最大尺寸（随机）")]
        public float particleSize = 0.12f;
        [Tooltip("按下时粒子云半径（大范围光影云，约 2 个键距）")]
        public float pressedCloudRadius = 2.4f;

        [Header("键帽配色覆盖（参考图黑键白字 / 糖果色）")]
        [Tooltip("所有键帽改黑色、字符改白色（参考图配色，绿光对比最好）；关掉 = 保留 FBX 原糖果色")]
        public bool blackKeycaps = true;
        [Tooltip("糖果配色（2026-10-15 版 68 键参考图）：蓝/粉/绿/淡紫/深紫/白/奶油 7 色逐键映射 + 白色字符。开启时优先于 blackKeycaps。Awake 会按场景/对象名自动判断：名字含 Candy 才启用")]
        public bool candyKeycaps = false;
        [Tooltip("黑键帽颜色（URP Lit 深灰黑 #10181D）")]
        public Color keycapBlack = new Color(0f, 0f, 0f);

        [Header("拖动旋转 / 缩放")]
        [Tooltip("旋转灵敏度（度/像素）")]
        public float rotateSpeed = 0.25f;
        [Tooltip("俯仰角限制（度），180 = 可以完全上下颠倒")]
        public float maxPitch = 180f;
        [Tooltip("滚轮缩放速度")]
        public float zoomSpeed = 4f;
        [Tooltip("启用拖动旋转/滚轮缩放（场景里有多个键盘实例时只给一个开，避免转速/缩放速度翻倍；按压与发光不受影响）")]
        public bool enableOrbit = true;

        [Header("侧壁压暗（高光泽键帽防侧壁反光发灰）")]
        [Tooltip("侧壁反照率倍率（AlbedoMask 侧壁竖条；按下时反照率向亮绿插值，此值决定侧壁还透出多少键帽本来的黑色）")]
        public float sideAlbedoDark = 0.35f;
        [Tooltip("键帽塑料光滑度（-1=保持模型原值；参考图塑料质感建议 0.5~0.6，高光宽而柔不锐利）")]
        public float keycapSmoothness = -1f;
        [Tooltip("底座（非键帽/字母网格）光滑度：哑光塑料。底座原继承 0.8 高光，点光在低模硬边法线上碎成锯齿状白斑")]
        public float chassisSmoothness = 0.15f;
        [Tooltip("底座法线平滑（按位置合并硬边法线）。实测副作用大：低角度下底座坡面法线偏上，掠射菲涅尔把环境反射成大片灰白/蓝色。默认关闭")]
        public bool chassisSmoothNormals = false;
        [Tooltip("侧壁光滑度遮罩：-1=关闭（侧壁与顶面共用材质光滑度标量）；0~1=启用反照率贴图 alpha 控制光滑度——顶面=材质原光滑度，侧壁=此值。钢琴黑键帽必开：0.9+ 的光滑度会让侧壁强烈反射环境光发灰")]
        public float sideSmoothnessMask = -1f;
        [Header("侧壁绿光（参考图：顶缘一道绿光往下沉黑）")]
        [Tooltip("侧壁自发光竖条顶部值，×发光强度 glowIntensity 后生效；越大顶缘绿光越亮")]
        public float sideGlowTop = 0.16f;
        [Tooltip("侧壁自发光竖条底部值；越大底部残留的绿光越多")]
        public float sideGlowBottom = 0.03f;
        [Tooltip("侧壁绿光衰减曲线指数：1=线性渐变，2=绿光集中顶缘、往下迅速沉黑")]
        public float sideGlowCurve = 2f;
        [Tooltip("顶面发光遮罩径向衰减：边缘值 = 1-falloff，越大边缘越暗、绿光越集中中心（默认 0.75=边缘 0.25）")]
        public float topGlowFalloff = 0.75f;
        [Tooltip("初始光斑收紧指数：对径向渐变做幂次收紧，1=旧行为；2.5 ≈ 初始亮区半径缩到约一半")]
        public float idleGlowTightness = 1f;
        [Tooltip("初始态顶面发光整体亮度比例（参考图无白色爆点 → 0.25；只作用于顶面径向，侧壁绿光条不变）")]
        public float idleTopScale = 1f;
        [Tooltip("调试：强制指定目标键（名称/键码包含即命中，如 Space/Shift/A）。留空 = 纯随机")]
        public string debugTargetKey = "";
        [Tooltip("lightOnPressOnly 模式下初始态泛光灯的压暗比例（0=全灭，0.3=保留 30% 邻键微光）")]
        public float haloPressDim = 0.3f;
        [Tooltip("宽键初始长条光斑侧灯强度倍率（×haloLightIntensity×haloPressDim；0=关闭）")]
        public float haloSideIntensity = 0f;
        [Tooltip("宽键初始态自发光增强倍率（随按下淡出）：把近均匀的长条遮罩推过 Bloom 阈值，形成连续发光长条（点光做不出均匀长条）")]
        public float wideIdleStripBoost = 2.9f;
        [Tooltip("宽键初始态中间泛光灯压暗比例（长条亮度交给自发光，灯只留点缀光源感；按下恢复 100%）")]
        public float wideHaloIdleDim = 0.45f;
        [Tooltip("宽键顶面短轴采样压缩（<1 = 发光长条在短轴方向加宽，1=不加宽；普通键不受影响）")]
        public float wideStripWidth = 0.72f;
        [Tooltip("按下时顶面中心亮斑强度（0=关闭=旧行为；1=参考图 Main 键按下时的中心泛白光核）")]
        public float pressGlowCore = 0f;
        [Tooltip("中心亮斑半径（顶面 UV 半径比例，0.35≈顶面三分之一）")]
        public float pressGlowCoreSize = 0.35f;
        [Tooltip("按压发光加成：按下时发光强度额外倍率（默认 0.15=旧行为；0.5~0.7 让中心 HDR 冲高、ACES 泛白成亮斑）")]
        public float pressGlowBoost = 0.15f;
        [Tooltip("按下亮斑的外围保留亮度（0~1，越小中环越暗、中心光斑对比越强；默认 0.45）")]
        public float pressGlowEdgeKeep = 0.45f;
        [Tooltip("发光时键帽向 GlowAlbedo 染色的最大权重（0.95=染成深绿；调低让侧壁保持黑塑料）")]
        public float glowAlbedoLerp = 0.95f;
        [Tooltip("键帽顶面法线判定阈值：法线 y 分量 >= 此值才算顶面（其余归侧面暗区）。默认 0.707≈45°；高光泽倒角键帽建议 0.82，让倒角肩部归侧面避免亮灰边")]
        public float capTopNormalMinY = 0.707f;

        [Tooltip("按下时侧壁绿光压暗比例（参考图按下=顶亮侧暗；1=旧行为不压暗，0.55≈侧壁绿光压到 55%）")]
        public float sideGlowPressDim = 1f;
        [Tooltip("粒子汇聚点下沉量（键高比例）：顶面是凹面，汇聚点要从顶缘平面下沉到凹面中心（对比键盘凹面深 6%）")]
        public float convergeSink = 0.06f;

        class KeySlot
        {
            public Transform root;
            public Vector3 originLocal;
            public float height;    // 键帽高度（世界单位）
            public KeyCode code;
            public float amount;    // 当前按下量 0..1
            public float glowAmount; // 发光量 0..1（按下瞬间点亮）
            public bool driven;      // 本帧是否被输入驱动
            public Material glowMat;    // 键帽材质实例（发光用）
            public Texture2D dynMask;   // 按下亮斑动态遮罩（懒创建，仅发光中的键更新）
            public float lastCoreBlend = -1f; // 上次写入动态遮罩的亮斑混合量（避免每帧重传贴图）
            public int colorID;         // 该材质的 albedo 颜色属性 ID（Standard=_Color，URP=_BaseColor）
            public Color baseColor;     // 键帽本色（发光时压暗用）
            public ParticleSystem ps;   // 尘埃粒子
            public Material psMat;      // 粒子渲染材质实例（每键独立）——渐隐时改 _BaseColor alpha
            public bool pressConsumed;  // 本次按下是否已触发过目标切换
            public bool wide;           // 是否宽键（AttachTargetLight 时判定）——宽键初始态走"自发光长条"路径
            public bool cloudActive;    // 当前是否处于"按下大范围粒子云"形态
            public float releaseTime = -1f; // 停止成为目标的时刻（-1=不在待清空状态），用于粒子延迟消散
            public float baseSmoothness; // 键帽原始光滑度（发光时临时调高，松开恢复）
            public int smoothID;        // 光滑度属性 ID（Standard=_Glossiness，URP=_Smoothness；0=不支持）
            public bool smoothLocked;   // 光滑度遮罩模式（alpha 通道分区）：标量已置 1，禁止发光逻辑改写
            public Material charMat;    // 字母网格材质实例（发光时提亮字母保证可读）
            public int charColorID;     // 字母颜色属性 ID
            public Color charBaseColor; // 字母原色
        }

        readonly List<KeySlot> slots = new List<KeySlot>();
        int capMismatchCount; // 键帽网格名未匹配 "Cube" 的键数（构建诊断用，浏览器 F12 看 Console）
        readonly Dictionary<KeyCode, KeySlot> byCode = new Dictionary<KeyCode, KeySlot>();
        readonly Dictionary<Transform, KeySlot> byRoot = new Dictionary<Transform, KeySlot>();
        KeySlot mouseSlot;
        bool inputUnavailable;
        KeySlot targetSlot; // 当前目标键：唯一发光、唯一有粒子、唯一挂点光源
        Light targetLight;  // 顶部聚光灯：照出键帽顶亮侧暗的光影渐变
        Light haloLight;    // 小范围点光：悬在键帽上方，给邻键侧面+底板染绿（用户要求的"周围泛绿"层）
        Light spillLight;   // 邻键溢光：低位点光（键高中部），只照邻键侧壁、不冲自家顶面
        // 注：曾尝试沿长轴再加两盏低位侧溢光（宽键邻键覆盖），但灯离自家键帽端部仅 0.1~0.3
        // 单位，无阴影 + 1/d² 让自照压倒照邻键（几百倍）→ 自家端部被照出亮团。已删除，
        // 改为宽键时中间溢光扩大 range + 增强（见 Update）。
        readonly List<Light> flankLights = new List<Light>(); // 宽键两端补光聚光灯
        readonly List<Light> haloSideLights = new List<Light>(); // 宽键初始长条光斑：沿长轴分布的两盏泛光点光（仅初始态，按下淡出）
        Vector3 lightBaseLocal; // 顶灯挂载时的本地位置
        Vector3 lightSinkLocal; // 键按下下沉一个完整行程的本地向量（灯反向补偿用）
        bool flankersActive; // 当前目标键是否为宽键（启用两端补光）
        int pressAxis = 1; // 按压轴：0=右 1=上 2=前（模型最薄方向，即垂直底座）
        int pressSign = -1; // 沿按压轴的方向（-1 = 从键帽指向底座）

        // 拖动旋转 / 缩放状态
        Quaternion baseRot = Quaternion.identity;
        float yaw, pitch;
        float zoomMin, zoomMax = -1f;
        bool leftRotating;
        Vector3 leftDownPos;

        Vector3 PressAxisWorld =>
            pressAxis == 0 ? transform.right : pressAxis == 1 ? transform.up : transform.forward;

        // 轴对齐包围盒沿世界方向 d 的跨度
        static float Extent(Vector3 d, Vector3 size) =>
            Mathf.Abs(d.x) * size.x + Mathf.Abs(d.y) * size.y + Mathf.Abs(d.z) * size.z;

        // 目标键的灯光组：顶部聚光灯（顶面光斑渐变）+ 宽键两端补光聚光灯。
        // 都只在目标键身上，切换目标时一起搬走
        void CreateTargetLight()
        {
            if (!enablePointLight) return;
            var top = new GameObject("TargetSpotLight_Top");
            targetLight = top.AddComponent<Light>();
            targetLight.type = LightType.Spot;      // 聚光灯：锥角朝下罩住键帽，
            targetLight.spotAngle = pointLightSpotAngle; // 水平方向完全无光 → 邻键/底板缝隙不会有圆形光斑
            targetLight.color = pointLightColor;
            targetLight.range = pointLightRange;
            targetLight.intensity = 0f;
            targetLight.shadows = LightShadows.None;

            // 周围泛绿层：小范围点光悬在键帽上方，只染绿邻键侧面和底板
            // （点光在键帽顶面高度 → 邻键顶面只能接到掠射光，顶面不受影响）
            var haloGo = new GameObject("TargetHaloLight_Point");
            haloLight = haloGo.AddComponent<Light>();
            haloLight.type = LightType.Point;
            haloLight.color = pointLightColor;
            haloLight.range = 1.2f;
            haloLight.intensity = 0f;
            haloLight.shadows = LightShadows.None;

            // 宽键初始长条光斑（用户方案）：单盏泛光是"中心圆形亮斑"的物理来源，
            // 宽键时沿长轴再布两盏同色点光 → 初始态拼成连续长条受光区。
            // 强度在 Update 里随按下 (1-amount) 淡出、窄键恒灭 → 按下单光斑不受影响
            for (int i = 0; i < 2; i++)
            {
                var sideGo = new GameObject("TargetHaloSideLight_" + i);
                var sl = sideGo.AddComponent<Light>();
                sl.type = LightType.Point;
                sl.color = pointLightColor;
                sl.range = 1.2f;
                sl.intensity = 0f;
                sl.shadows = LightShadows.None;
                haloSideLights.Add(sl);
            }

            // 邻键溢光：低位点光（挂载时置于键高中部，低于键帽顶面）
            // → 自家顶面法线朝上、灯在其下方，漫反射无贡献；只把绿光洒到邻键侧壁/底板
            if (enableSpillLight)
            {
                var spillGo = new GameObject("TargetSpillLight_Point");
                spillLight = spillGo.AddComponent<Light>();
                spillLight.type = LightType.Point;
                spillLight.color = pointLightColor;
                spillLight.range = spillLightRange;
                spillLight.intensity = 0f;
                spillLight.shadows = LightShadows.None;
            }

            // 宽键（空格等）两端补光：单盏聚光灯照不满超宽键帽
            for (int i = 0; i < 2; i++)
            {
                var fl = new GameObject("TargetFlankLight_" + i);
                var l = fl.AddComponent<Light>();
                l.type = LightType.Spot;
                l.color = pointLightColor;
                l.range = 2.2f;
                l.intensity = 0f;
                l.shadows = LightShadows.None;
                l.renderMode = LightRenderMode.ForcePixel;
                flankLights.Add(l);
            }
        }

        // 把两盏灯挂到指定键上：作为键根的子物体，跟着下沉动画一起动
        void AttachTargetLight(KeySlot s)
        {
            if (s == null) return;
            Vector3 upDir = -pressSign * PressAxisWorld;

            // 键帽整体包围盒（含字母，都在键帽体积内）——顶灯/侧灯/补光灯共用
            Bounds b = new Bounds(s.root.position, Vector3.zero);
            bool first = true;
            foreach (var r in s.root.GetComponentsInChildren<Renderer>())
            {
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            Vector3 center = b.center;

            // 宽键判定：水平方向最大半宽 > 1（普通键 ~0.55，空格 ~2.75）
            float halfWX = Extent(s.root.right, b.size) * 0.5f;
            float halfWZ = Extent(s.root.forward, b.size) * 0.5f;
            float halfW = Mathf.Max(halfWX, halfWZ);
            bool wide = halfW > 1.0f;
            flankersActive = wide;
            s.wide = wide; // 记到槽位上：发光更新按宽键走自发光长条路径

            if (targetLight != null)
            {
                if (wide) targetLight.spotAngle = 110f; // 宽键主锥扩到 110° 罩住中段
                // 顶灯（聚光）：先设世界位置和朝向（灯头垂直指向键帽），再挂到键根下并保持世界变换
                Vector3 topWorld = ParticleOrigin(s.root, s.height) + upDir * (s.height * (pointLightHeight - 0.5f));
                targetLight.transform.position = topWorld;
                // 注意：upDir 常正好是世界正上方，与 LookRotation 默认 up 向量反向共线会得到无效旋转，
                // 必须显式给一个不平行的 up 参考轴
                Vector3 spotFwd = -upDir;
                Vector3 spotUp = Mathf.Abs(spotFwd.y) > 0.9f ? Vector3.forward : Vector3.up;
                targetLight.transform.rotation = Quaternion.LookRotation(spotFwd, spotUp);
                targetLight.transform.SetParent(s.root, true); // true = 保留世界变换，之后跟随键根移动/旋转
                // 记录挂载位置和"按下整个下沉行程"的本地向量：
                // 按键时灯跟着键根下沉会让光锥压向底板、光溢出到键帽外，
                // 每帧按下沉量反向抬高灯，保持灯的世界高度恒定 → 按下光锥几何与平时一致
                lightBaseLocal = targetLight.transform.localPosition;
                Vector3 worldDown = pressSign * PressAxisWorld;
                lightSinkLocal = s.root.InverseTransformDirection(worldDown) * (pressDepthRatio * s.height);
            }

            // 周围泛光点光：贴着键帽顶面上方 0.35 倍键高。
            // 注意不做下沉补偿：它没有可见光锥、跟着键帽走，距离恒定 →
            // 按下时键帽顶面亮度不因 1/d² 衰减而变暗（做过补偿反而导致按下顶面变暗）
            if (haloLight != null)
            {
                haloLight.transform.position = ParticleOrigin(s.root, s.height) + upDir * (s.height * 0.35f);
                haloLight.transform.SetParent(s.root, true);
            }

            // 邻键溢光：键高中部（bounds 中心），低于键帽顶面 → 自家顶面不受影响
            if (spillLight != null)
            {
                spillLight.transform.position = center + upDir * (s.height * 0.05f);
                spillLight.transform.SetParent(s.root, true);
            }

            // 宽键两端补光：主锥罩不住的部分由两盏侧翼聚光灯负责
            Vector3 longDir = halfWX >= halfWZ ? s.root.right : s.root.forward;
            longDir.Normalize();
            foreach (var l in flankLights)
            {
                if (l == null) continue;
                if (!wide) continue; // 窄键不启用（强度在 Update 里保持 0）
                int sign = (l == flankLights[0]) ? 1 : -1;
                l.transform.position = center + longDir * (sign * halfW * 0.6f) + upDir * (1.0f * s.height);
                l.transform.rotation = LookAtDown(upDir);
                l.spotAngle = 140f;
                l.transform.SetParent(s.root, true);
            }

            // 宽键长条光斑侧灯：沿长轴 ±0.55 半宽分布，与中间泛光点光同高（贴键帽顶面上方 0.35 键高）。
            // 挂到键根下跟随下沉；窄键位置无意义（Update 里恒灭）
            foreach (var l in haloSideLights)
            {
                if (l == null) continue;
                int sign = (l == haloSideLights[0]) ? 1 : -1;
                l.transform.position = ParticleOrigin(s.root, s.height)
                    + upDir * (s.height * 0.35f) + longDir * (sign * halfW * 0.6f);
                l.transform.SetParent(s.root, true);
            }
        }

        // 竖直向下的 LookRotation：fwd 与默认 up 共线时必须显式给参考轴
        Quaternion LookAtDown(Vector3 upDir)
        {
            Vector3 fwd = -upDir;
            Vector3 up = Mathf.Abs(fwd.y) > 0.9f ? Vector3.forward : Vector3.up;
            return Quaternion.LookRotation(fwd, up);
        }

        void ComputePressAxis()
        {
            // 把每个网格的包围盒变换到根节点局部空间再取总包围盒——
            // 局部尺寸不随模型旋转/倾斜变化，按压轴判定永远正确
            var rootMat = transform.worldToLocalMatrix;
            Bounds lb = new Bounds();
            bool first = true;
            foreach (var mf in GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var m = rootMat * mf.transform.localToWorldMatrix;
                var b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3(
                        (i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var lp = m.MultiplyPoint3x4(corner);
                    if (first) { lb = new Bounds(lp, Vector3.zero); first = false; }
                    else lb.Encapsulate(lp);
                }
            }
            if (first) return;
            pressAxis = lb.size.x <= lb.size.y && lb.size.x <= lb.size.z ? 0
                      : lb.size.y <= lb.size.z ? 1 : 2;

            // 方向：键帽平均位置 → 模型包围盒中心 = 朝底座方向
            float keyMean = 0f;
            foreach (var s in slots) keyMean += PressAxisCoord(s.root.position);
            keyMean = slots.Count > 0 ? keyMean / slots.Count : 0f;
            float center = pressAxis == 0 ? lb.center.x : pressAxis == 1 ? lb.center.y : lb.center.z;
            pressSign = center < keyMean ? -1 : 1;
            Debug.Log($"[模型键盘] 按压轴 = 局部{new[] { "X", "Y", "Z" }[pressAxis]} 轴，方向 {pressSign}，模型局部尺寸 {lb.size}");
        }

        float PressAxisCoord(Vector3 worldPos)
        {
            Vector3 lp = transform.InverseTransformPoint(worldPos);
            return pressAxis == 0 ? lp.x : pressAxis == 1 ? lp.y : lp.z;
        }

        // 67 键布局，行序 = 从后(数字行)到前(底行)，列序 = 从左到右
        static readonly KeyCode[][] Layout =
        {
            new[] { KeyCode.Escape, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
                    KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0,
                    KeyCode.Minus, KeyCode.Equals, KeyCode.Backspace, KeyCode.BackQuote },
            new[] { KeyCode.Tab, KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R, KeyCode.T, KeyCode.Y, KeyCode.U,
                    KeyCode.I, KeyCode.O, KeyCode.P, KeyCode.LeftBracket, KeyCode.RightBracket,
                    KeyCode.Backslash, KeyCode.Delete },
            new[] { KeyCode.CapsLock, KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.F, KeyCode.G, KeyCode.H, KeyCode.J,
                    KeyCode.K, KeyCode.L, KeyCode.Semicolon, KeyCode.Quote, KeyCode.Return, KeyCode.Insert },
            new[] { KeyCode.LeftShift, KeyCode.Z, KeyCode.X, KeyCode.C, KeyCode.V, KeyCode.B, KeyCode.N, KeyCode.M,
                    KeyCode.Comma, KeyCode.Period, KeyCode.Slash, KeyCode.RightShift, KeyCode.UpArrow, KeyCode.RightControl },
            new[] { KeyCode.LeftControl, KeyCode.LeftWindows, KeyCode.LeftAlt, KeyCode.Space, KeyCode.RightAlt,
                    KeyCode.Menu, KeyCode.LeftArrow, KeyCode.DownArrow, KeyCode.RightArrow },
        };

        void Awake()
        {
            baseRot = transform.rotation;
            NormalizeShaders();         // URP 下把键盘所有材质规范成 URP/Lit（Built-in 管线跳过）
            // 点光源参数（覆盖组件上旧的序列化值）：
            // 参考图观感 = 键帽像自体发光的亮绿（顶面高亮 + 光斑渐变）
            pointLightHeight = 1.5f;    // 聚光灯架高：灯到键帽顶 1.0 倍键高
            pointLightRange = 1.7f;     // 从灯到键帽顶 ~0.37 + 底板余量
            pointLightIntensity = 12f;  // 锥角收窄后补偿（URP 聚光按坎德拉计，不随锥角自动增亮）
            pointLightSpotAngle = 66f;  // 半角 33°：自家键帽远缘偏角 ~31° 在锥内全覆盖，
                                        // 邻键帽顶面 31°~49° 几乎全在锥外 → 顶面不再泛绿（只剩侧面受光）
            pointLightColor = new Color(0f, 1f, 0f); // 纯绿：灯里的红/蓝成分会把顶面染成薄荷色
            haloLightIntensity = 3.5f;  // 周围泛光：能看出邻键侧面/底板泛绿但不泛滥
            glowIntensity = 1.5f;       // 发光强度定稿值（2026-10-19 参考图调优）。旧值 2.6 会在 Play 时
                                        // 冲掉"应用样式"菜单序列化的 1.5，导致侧壁整面亮绿+中心过曝
            blackKeycaps = true;        // 强制黑键白字（用户指定，不再依赖 Inspector 勾选）
            keycapBlack = new Color(0f, 0f, 0f); // 纯黑键帽（用户 10-22 反馈深灰不够黑；强制覆盖场景序列化旧值）
            // 糖果配色自动开关：仅 Candy 场景（或键盘对象名含 "Candy"）启用，主场景保持黑键纯黑
            candyKeycaps = gameObject.name.Contains("Candy")
                || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.Contains("Candy");
            glowColor = new Color(0f, 1f, 0.12f); // 强制纯绿（场景里序列化的是带蓝浅绿，会偏薄荷色）
            // Candy 场景糖果分支：发光全链路换黄（用户 10-22 要求，仅糖果模式生效）。
            // 注意：本块位于全局参数赋值之前，数值会被下方黑键默认值覆盖（10-22 16:00 前的原始状态，
            // 当时的"顶面太亮/侧壁太暗"即由此而来；如需糖果值生效须把本块移到全局赋值之后）
            if (candyKeycaps)
            {
                keycapSmoothness = 0.62f;   // 糖果键帽恢复半哑光塑料质感（0.35 哑光是黑键纯黑专用）
                sideSmoothnessMask = 0.62f; // 侧壁同步恢复
                glowColor = new Color(1f, 0.88f, 0f);      // 自发光金黄（键帽顶面/侧壁的亮光）
                pointLightColor = new Color(1f, 0.9f, 0f); // 顶灯/泛光/溢光灯同步黄
                GlowAlbedo = new Color(0.55f, 0.45f, 0f);  // 发光时键帽染色深黄（对应黑键模式的深绿）
                idleTopScale = 0f;          // 顶面不自发光（顶面太亮/字符糊多轮收敛）
                sideGlowTop = 1.0f;         // 侧壁顶缘黄光拉满
                sideGlowBottom = 0.3f;      // 侧壁黄光柱占比最大化
                sideGlowCurve = 1.2f;       // 侧壁缓渐变
                sideGlowPressDim = 1.0f;    // 按下时侧壁不压暗（糖果光柱）
                pressGlowBoost = 0.15f;     // 按压顶面增亮收敛
                pressGlowCore = 0.3f;       // 按压中心亮斑收敛
                glowAlbedoLerp = 0.1f;      // 发光时键帽保持糖果本色
                lightPressBase = 0.12f;     // 按压顶灯浅色键帽只给一点暖光
                lightPressPeak = 0.3f;      // 按到底顶灯峰值收敛
                haloPressDim = 0f;          // 初始泛光熄灭（浅色键帽成片亮黄真凶）
                haloPressPeak = 0.15f;      // 按到底泛光只恢复 15%
                spillLightIntensity = 0.5f; // 溢光减弱：防邻键互射糊字符
            }
            // —— 参考图样式定稿参数（2026-10-19，代码级强制）——
            // 场景序列化的旧值会盖掉脚本字段默认值，这里统一强制，Play 即生效；
            // "应用样式"菜单/对比键盘的赋值与之相同，无冲突
            keycapSmoothness = 0.45f;   // 顶面适度反射（用户 10-22 反馈顶面太暗）：0.45 让顶面有天空盒反光的层次感，
                                        // 不会像 0.62 那样成片灰调；侧壁由 sideSmoothnessMask 单独压黑
            sideSmoothnessMask = 0.2f;  // 侧壁强吸光哑光（保持沉黑，参考图同款）
            chassisSmoothness = 0.15f;  // 底座近全哑光：0.35 下掠射菲涅尔仍把环境反成白点带（用户复测仍有），0.15 摊薄
            chassisSmoothNormals = false; // 法线平滑实测副作用大（低角度掠射反射发灰蓝），默认关闭
            sideAlbedoDark = 0.15f;     // 侧壁漫反射沉黑
            sideGlowTop = 0.55f;        // 侧壁顶缘绿线 0.40→0.55：低视角下端部可见的是弧面侧壁，提亮它（仅目标键生效）
            sideGlowBottom = 0.03f;     // 侧壁底部近黑（用户 10-22 反馈侧壁绿光占比偏大，0.05→0.03）
            sideGlowCurve = 3.2f;       // 绿光更集中顶缘（2.0→3.2：衰减更陡，下沉范围缩小）
            topGlowFalloff = 0.8f;      // 顶面径向衰减：0.8 让初始亮区更宽（用户反馈"初始光斑不够大"）
            idleGlowTightness = 0.45f;  // 平顶化+全椭圆UV：端部自发光达中心 93%，过 Bloom 阈值成均匀长条，边缘仍归零
            idleTopScale = 0.4f;        // 初始顶面发光提亮（0.25→0.4，用户 10-22 反馈顶面暗）
            glowAlbedoLerp = 0.42f;     // 顶面染色权重
            pressGlowCore = 0.5f;       // 按下中心亮斑开关/大小
            pressGlowCoreSize = 0.22f;
            pressGlowBoost = 0.3f;      // 按压发光加成
            pressGlowEdgeKeep = 0.45f;  // 按下基底=未收紧径向 × 0.45 → 顶面均匀亮绿
            sideGlowPressDim = 0.55f;   // 按下时侧壁绿光压到 55%（顶亮侧暗）
            lightOnPressOnly = true;    // 顶灯按压驱动：初始态顶灯熄灭
            lightPressBase = 0.4f;      // 按下即有 40% 顶灯亮度
            lightPressPeak = 1.0f;      // 按到底顶灯峰值（1.6 会冲爆顶面）
            haloPressPeak = 0.3f;       // 按到底泛光恢复 30%（泛光贴键顶，过高照亮侧壁）
            haloPressDim = 0.35f;       // 初始泛光压到 35%
            haloSideIntensity = 0f;     // 用户要求去掉两盏侧灯：光斑感全消，亮度由自发光承担
            wideIdleStripBoost = 2.9f;  // 宽键初始自发光增强：中心 1.44/端部 1.36，整条过阈值且保玉绿（3.2 会偏白）
            wideStripWidth = 0.72f;     // 宽键短轴采样压缩：长条短轴方向加宽（用户要求按下长条更宽）
            wideHaloIdleDim = 0.45f;    // 宽键初始中间泛光压到 45%：减小中心与两端的对比，按下恢复
            enableFillLight = true;     // 补光：圆角两侧双高光
            fillLightIntensity = 0.45f;
            fillLightAngles = new Vector3(35f, 55f, 0f);
            enableSpillLight = true;    // 邻键溢光：发光键把绿光洒到邻键侧壁
            spillLightIntensity = 1.6f;
            // 粒子（2026-10-20 用户反馈：尺寸小一点，初始范围大一点，按下范围小一点）
            particleSize = 0.035f;      // 0.05 → 0.035：用户反馈可以再小一点
            particleRadius = 0.8f;      // 初始汇聚范围 0.5 → 0.8：尘埃绕得更开
            particleFadeDelay = 0f;     // 松开立即开始渐隐：从松开到完全消失总共 1 秒（用户要求）
            particleFadeDuration = 1.0f; // 渐隐时长 1 秒：材质 alpha 线性降到 0（用户要求平滑过渡消失）
            pressedCloudRadius = 1.4f;  // 按下光影云范围 2.4 → 1.4：更收敛
            BuildKeyMap();
            ComputePressAxis();
            CreateTargetLight();
            // 补光：第二盏无阴影方向光，让键帽圆角两侧各出现一条竖向高光（参考图双光线）
            if (enableFillLight)
            {
                var fillGo = new GameObject("FillLight");
                fillGo.transform.SetParent(transform, false);
                fillGo.transform.localRotation = Quaternion.Euler(fillLightAngles);
                var fill = fillGo.AddComponent<Light>();
                fill.type = LightType.Directional;
                fill.intensity = fillLightIntensity;
                fill.color = fillLightColor;
                fill.shadows = LightShadows.None;
            }
            // 按压轴检测后，用正确方向重新摆放粒子发射器
            foreach (var s in slots)
            {
                if (s.ps == null) continue;
                s.ps.transform.rotation = Quaternion.LookRotation(-pressSign * PressAxisWorld);
                s.ps.transform.position = ParticleOrigin(s.root, s.height);
            }
            SetupUrpPostFx();           // 相机 HDR+后处理、全局 Bloom、刷新反射探针
            // 运行时诊断（WebGL 排查用）：材质关键字/光滑度通道/网格可读性/后处理状态
            try
            {
                var mf0 = GetComponentInChildren<MeshFilter>();
                if (mf0 != null && mf0.sharedMesh != null)
                    Debug.LogWarning("[Keyboard3D] Diag mesh=" + mf0.sharedMesh.name + " readable=" + mf0.sharedMesh.isReadable
                        + " uv=" + mf0.sharedMesh.uv.Length);
                var mr0 = GetComponentInChildren<MeshRenderer>();
                if (mr0 != null && mr0.sharedMaterial != null)
                {
                    var sm0 = mr0.sharedMaterial;
                    Debug.LogWarning("[Keyboard3D] Diag mat=" + sm0.name + " shader=" + sm0.shader.name
                        + " kwSmooth=" + sm0.IsKeywordEnabled("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A")
                        + " stc=" + (sm0.HasProperty("_SmoothnessTextureChannel") ? sm0.GetFloat("_SmoothnessTextureChannel").ToString() : "-")
                        + " smooth=" + (sm0.HasProperty("_Smoothness") ? sm0.GetFloat("_Smoothness").ToString("F2") : "-")
                        + " kwEmi=" + sm0.IsKeywordEnabled("_EMISSION")
                        + " base=" + (sm0.HasProperty("_BaseColor") ? sm0.GetColor("_BaseColor").ToString("F3") : "-"));
                }
                var ps0 = GetComponentInChildren<ParticleSystemRenderer>();
                if (ps0 != null && ps0.material != null)
                    Debug.LogWarning("[Keyboard3D] Diag particle shader=" + ps0.material.shader.name
                        + " map=" + (ps0.material.HasProperty("_BaseMap") && ps0.material.GetTexture("_BaseMap") != null ? "YES" : "NO")
                        + " surface=" + (ps0.material.HasProperty("_Surface") ? ps0.material.GetFloat("_Surface").ToString() : "-"));
            }
            catch (System.Exception ex) { Debug.LogWarning("[Keyboard3D] Diag fail: " + ex.Message); }
            try { Input.GetKey(KeyCode.A); }
            catch { inputUnavailable = true; Debug.LogWarning("[模型键盘] 新 Input System 下旧 Input 不可用，物理键盘输入已禁用（鼠标点击仍有效）。可在 Player Settings 把 Active Input Handling 改为 Both。"); }
        }

        // URP 迁移：把键盘下所有内置管线材质替换为 URP/Lit 实例（保留颜色/贴图）
        // 覆盖键帽、字母和底座——FBX 内嵌材质在 URP 下会是粉色，这里统一纠正
        void NormalizeShaders()
        {
            if (GraphicsSettings.currentRenderPipeline == null) return; // 内置管线无需处理
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return;
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var sm = mats[i];
                    if (sm == null || sm.shader == lit || sm.shader.name.Contains("Universal Render Pipeline"))
                        continue;
                    var nm = new Material(lit);
                    Color c = Color.white;
                    if (sm.HasProperty("_BaseColor")) c = sm.GetColor("_BaseColor");
                    else if (sm.HasProperty("_Color")) c = sm.color;
                    if (nm.HasProperty("_BaseColor")) nm.SetColor("_BaseColor", c);
                    if (sm.HasProperty("_MainTex") && sm.mainTexture != null) nm.mainTexture = sm.mainTexture;
                    // 规格起始值：金属度 0~0.15、光滑度 0.75~0.9（塑料键帽质感）
                    if (nm.HasProperty("_Metallic")) nm.SetFloat("_Metallic", 0.05f);
                    if (nm.HasProperty("_Smoothness")) nm.SetFloat("_Smoothness", 0.8f);
                    mats[i] = nm;
                    changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        // URP 后处理：相机 HDR + 后处理开关、全局 Bloom（克制参数）+ ACES 色调映射、刷新反射探针
        void SetupUrpPostFx()
        {
            if (GraphicsSettings.currentRenderPipeline == null) return;
            var cam = Camera.main;
            if (cam != null)
            {
                cam.allowHDR = true;
                var extra = cam.GetComponent<UniversalAdditionalCameraData>();
                if (extra == null) extra = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                extra.renderPostProcessing = true;
                Volume vol = Object.FindObjectOfType<Volume>();
                if (vol == null)
                {
                    var volGo = new GameObject("KeyboardGlobalVolume_Runtime");
                    vol = volGo.AddComponent<Volume>();
                    vol.isGlobal = true;
                    vol.profile = ScriptableObject.CreateInstance<VolumeProfile>();
                }
                // 无论新建还是复用已有 Volume，都把参数刷成当前规格（旧 profile 可能是旧数值）。
                // 注意复用已有组件：场景里有多个键盘实例时共享同一个 profile，
                // 第二个实例再 Add 同类型组件会抛 InvalidOperationException
                var profile = vol.profile;
                Bloom bloom = null;
                Tonemapping tm = null;
                foreach (var c in profile.components)
                {
                    if (c is Bloom bl) bloom = bl;
                    else if (c is Tonemapping t2) tm = t2;
                }
                if (bloom == null) bloom = profile.Add<Bloom>(true);
                bloom.threshold.Override(1.1f);
                bloom.intensity.Override(0.3f);
                bloom.scatter.Override(0.4f);
                if (tm == null) tm = profile.Add<Tonemapping>(true);
                tm.mode.Override(TonemappingMode.ACES);

                // —— 参考图白色摄影棚环境 ——
                // 侧面光泽反光带的本质：明亮环境在光滑塑料上的镜面反射。
                // 暗环境里无论怎么调材质都出不来，所以把整个环境调成摄影棚风格。
                // 幂等：两个键盘实例/多次 Play 重复调用安全。
                cam.clearFlags = CameraClearFlags.Skybox; // 用户指定 Background Type=Skybox（天空盒用项目默认，不另设贴图）
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.55f, 0.57f, 0.60f); // 柔和棚顶漫射
                var studioProbe = GameObject.Find("KeyboardStudioProbe");
                if (studioProbe == null)
                {
                    studioProbe = new GameObject("KeyboardStudioProbe");
                    var probe = studioProbe.AddComponent<ReflectionProbe>();
                    probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Custom;
                    probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
                    probe.size = new Vector3(200f, 200f, 200f); // 覆盖两块键盘（以原点为中心）
                    probe.intensity = 1.3f; // 暗环境+亮顶的结构化反射：亮带要够亮才出质感
                    probe.customBakedTexture = GetStudioCubemap(); // 亮灰环境贴图（顶亮/四壁白/地面暗）
                }
                Debug.LogWarning("[Keyboard3D] PostFx pipeline=" + (GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : "NULL")
                    + " bloom=" + (bloom != null) + " tm=" + (tm != null)
                    + " clear=" + cam.clearFlags + " hdr=" + cam.allowHDR
                    + " post=" + (cam.GetComponent<UniversalAdditionalCameraData>() != null && cam.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing)
                    + " probe=" + (GameObject.Find("KeyboardStudioProbe") != null)
                    + " ambient=" + RenderSettings.ambientLight.ToString("F2")
                    + " skybox=" + (RenderSettings.skybox != null ? RenderSettings.skybox.name : "NULL"));
            }
            // 刷新反射探针：子级探针统一改 Custom + 摄影棚贴图——
            // 若保留 Realtime 模式拍摄真实场景（暗背景），会以距离优势盖过全局摄影棚探针，
            // 侧壁反光带又会被吃掉
            foreach (var p in GetComponentsInChildren<ReflectionProbe>())
            {
                if (p.mode != UnityEngine.Rendering.ReflectionProbeMode.Custom)
                    p.mode = UnityEngine.Rendering.ReflectionProbeMode.Custom;
                if (p.customBakedTexture == null)
                    p.customBakedTexture = GetStudioCubemap();
            }
        }

        // 粒子汇聚点：键帽网格（Cube）包围盒中心正上方，贴着键帽顶面（0.5 倍键帽高）。
        // 基准用网格包围盒中心（键根节点原点可能偏离视觉中心，宽键尤其明显）；
        // 悬浮不能太高——斜视角下悬空点投影会偏离键帽中心（视差），贴面则正好落在中心
        Vector3 ParticleOrigin(Transform root, float keyHeight)
        {
            Vector3 upDir = -pressSign * PressAxisWorld;
            Vector3 center = root.position;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null || !mf.sharedMesh.name.StartsWith("Cube")) continue;
                var r = mf.GetComponent<Renderer>();
                if (r != null) center = r.bounds.center;
                break;
            }
            // 0.5 倍键高 = 顶缘平面；再下沉 convergeSink（顶面是凹面，汇聚点取凹面中心）
            return center + upDir * (keyHeight * (0.5f - convergeSink));
        }

        // 键帽 UV 重写：顶面（法线沿高度轴分量 >= topNormalMinY，含看不见的底面）映射到遮罩的径向渐变区，
        // 其余（侧面+倒角肩部）映射到左下角深灰区 → 顶面"中央亮、边缘稍暗"，侧面自发光只有 ~15%。
        // 高度轴自动检测（bounds 跨度最小的轴）：FBX 网格局部朝向随导出管线不同
        // （Blender 导出是 Z-up），硬编码 Y 会把顶面/侧面分区整个颠倒——
        // 症状：顶面哑光发暗、前后侧面反而高光发亮
        // topNormalMinY：顶面判定阈值。默认 0.707（≈45°，原键盘行为不变）；
        // 高光泽倒角键帽应调高（如 0.82≈35°）——倒角肩部法线从 0°~90° 过渡，
        // 阈值低时大半肩部被当"顶面"（白反照率+0.92 镜面）→ 亮一圈灰边，调高后肩部归侧面变哑光黑
        static void RewriteCapUv(MeshFilter mf, float topNormalMinY = 0.707f, float shortSqueeze = 1f)
        {
            var src = mf.sharedMesh;
            if (src == null || src.name.EndsWith("_GlowUV")) return;
            // 构建里 FBX 未开 Read/Write 时读网格会失败（编辑器不强制）——
            // 跳过并提示，避免刷屏报错；FBX meta 已开 isReadable，正常都会走下面
            if (!src.isReadable)
            {
                Debug.LogWarning($"[Keyboard3D] 网格 {src.name} 未开启 Read/Write，跳过 UV 重写（发光渐变/侧面压暗会失效）");
                return;
            }
            var mesh = Object.Instantiate(src);
            mesh.name = src.name + "_GlowUV";
            var verts = mesh.vertices;
            var norms = mesh.normals;
            var uvs = new Vector2[verts.Length];
            var b = mesh.bounds;
            var size = b.size;
            // 高度轴 = 跨度最小的轴（键帽总是扁平的）
            int up = 0;
            if (size.y <= size.x && size.y <= size.z) up = 1;
            else if (size.z <= size.x && size.z <= size.y) up = 2;
            int a0 = (up + 1) % 3, a1 = (up + 2) % 3; // 两个水平轴
            for (int i = 0; i < verts.Length; i++)
            {
                var n = norms[i];
                float nUp = up == 0 ? n.x : up == 1 ? n.y : n.z;
                if (Mathf.Abs(nUp) >= topNormalMinY)
                {
                    // 顶/底面：面上位置归一化到 0~1，再缩进 0.08~0.92 避开深灰角
                    float u = (AxisOf(verts[i], a0) - AxisOf(b.min, a0)) / Mathf.Max(1e-5f, AxisOf(size, a0));
                    float w = (AxisOf(verts[i], a1) - AxisOf(b.min, a1)) / Mathf.Max(1e-5f, AxisOf(size, a1));
                    // 椭圆适配：发光遮罩的径向渐变是圆形，宽键（空格等）长轴两端在遮罩里
                    // 已是"边缘"→ 几乎不发光（用户反馈"空格键只有中间亮"）。
                    // 把长轴方向的采样范围向中心压缩，让圆形渐变变成内切椭圆铺满整面；
                    // 短轴不动。1u 方键 aspect≈1 无变化。
                    // 压缩系数 1.0 = 完全内切椭圆：长轴端部 kIdle 达中心的 91%，
                    // 配合宽键初始自发光增强形成均匀长条（0.7 部分压缩时端部只有 80%，
                    // bloom 只从中心过阈值 → 又变回中心热点）
                    float span0 = AxisOf(size, a0), span1 = AxisOf(size, a1);
                    float aspect = Mathf.Min(span0, span1) / Mathf.Max(1e-5f, Mathf.Max(span0, span1));
                    float squeeze = aspect;
                    // 宽键短轴加宽（用户要求按下长条更宽）：短轴也向中心压缩采样，
                    // 边缘 r 从 1.0 降到 shortSqueeze → kIdle 抬到渐变中段 → 长条短轴加宽。
                    // 仅 aspect<0.5 的宽键生效（窄键 aspect≈1 不受影响）
                    if (span0 > span1)
                    {
                        u = 0.5f + (u - 0.5f) * squeeze;
                        if (aspect < 0.5f) w = 0.5f + (w - 0.5f) * shortSqueeze;
                    }
                    else
                    {
                        w = 0.5f + (w - 0.5f) * squeeze;
                        if (aspect < 0.5f) u = 0.5f + (u - 0.5f) * shortSqueeze;
                    }
                    uvs[i] = new Vector2(0.08f + 0.84f * u, 0.08f + 0.84f * w);
                }
                else
                {
                    // 侧面：u 固定 0.01（遮罩左侧竖条），v 按键帽高度 0.05→0.67 渐变
                    // → 侧面绿光"上亮下暗"（参考图效果），不再是均匀一块
                    float h = (AxisOf(verts[i], up) - AxisOf(b.min, up)) / Mathf.Max(1e-5f, AxisOf(size, up));
                    uvs[i] = new Vector2(0.01f, 0.05f + 0.62f * h);
                }
            }
            mesh.uv = uvs;
            mf.mesh = mesh;
        }
        static float AxisOf(Vector3 v, int i) => i == 0 ? v.x : i == 1 ? v.y : v.z;

        // 摄影棚环境贴图：暗四壁 + 暗棚顶 + 弱柔光箱——
        // 参考图的黑键更黑、反光更少：整体压暗（2026-10-19 用户反馈"环境光没有那么亮"）
        static Cubemap studioCubemap;
        static Cubemap GetStudioCubemap()
        {
            if (studioCubemap != null) return studioCubemap;
            var cm = new Cubemap(16, TextureFormat.RGBA32, false);
            FillFace(cm, CubemapFace.PositiveX, new Color(0.16f, 0.17f, 0.19f)); // 暗墙
            FillFace(cm, CubemapFace.NegativeX, new Color(0.16f, 0.17f, 0.19f));
            FillFace(cm, CubemapFace.PositiveZ, new Color(0.28f, 0.29f, 0.31f)); // 柔光箱（前）——
            FillFace(cm, CubemapFace.NegativeZ, new Color(0.28f, 0.29f, 0.31f)); // 前后两块在长条凹面（空格）
                                                                                 // 会反射成两个对称亮团（用户反馈），
                                                                                 // 压暗让长条亮度由发光遮罩主导
            FillFace(cm, CubemapFace.PositiveY, new Color(0.55f, 0.56f, 0.58f)); // 棚顶主光
            FillFace(cm, CubemapFace.NegativeY, new Color(0.12f, 0.13f, 0.14f)); // 地面
            cm.Apply();
            studioCubemap = cm;
            return cm;
        }
        static void FillFace(Cubemap cm, CubemapFace face, Color c)
        {
            var cols = new Color[16 * 16];
            for (int i = 0; i < cols.Length; i++) cols[i] = c;
            cm.SetPixels(cols, face);
        }

        // 自发光遮罩布局：
        //   主体 = 径向渐变（顶面采样，中心 1.0 → 边缘 ~0.3）
        //   左侧竖条（u<0.06, v<0.7）= 侧面专用：顶缘 sideGlowTop → 底部 sideGlowBottom 的
        //   sideGlowCurve 次幂渐变——参考图侧壁是"顶缘一道绿光、往下迅速沉到键帽本来的黑色"
        // 参数化 + 缓存：侧面绿光浓度是 Inspector 可调参数，不同键盘实例可用不同值
        static readonly Dictionary<(float, float, float, float, float, float, float), Texture2D> emissionMaskCache
            = new Dictionary<(float, float, float, float, float, float, float), Texture2D>();
        static Texture2D EmissionMask(float sideTop, float sideBottom, float curve, float topFalloff = 0.75f, float idleTightness = 1f, float sidePressDim = 1f, float idleTopScale = 1f)
        {
            var key = (sideTop, sideBottom, curve, topFalloff, idleTightness, sidePressDim, idleTopScale);
            if (emissionMaskCache.TryGetValue(key, out var cached)) return cached;
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            FillEmissionPixels(px, size, sideTop, sideBottom, curve, topFalloff, 0f, 0.35f, 0.45f, idleTightness, sidePressDim, idleTopScale);
            tex.SetPixels(px);
            tex.Apply(false, true);
            emissionMaskCache[key] = tex;
            return tex;
        }

        // 像素填充（静态遮罩与按下亮斑动态遮罩共用）：
        //   初始形态 = 基础径向的 idleTightness 次幂（收紧亮斑）；
        //   按下形态 = max(中心高斯亮斑, 未收紧径向 × edgeKeep)——
        //   基底故意用"未收紧"的径向：参考图 Main 键按下时顶面是整体均匀亮绿（不是亮斑+黑边），
        //   收紧只属于初始态形态；左侧竖条 = 侧壁渐变（不受亮斑影响）
        static void FillEmissionPixels(Color[] px, int size, float sideTop, float sideBottom, float curve,
            float topFalloff, float coreBlend, float coreSize, float edgeKeep = 0.45f, float idleTightness = 1f, float sidePressDim = 1f, float idleTopScale = 1f)
        {
            float sigma2 = 2f * coreSize * coreSize;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float d2 = u * u + v * v;
                    float d = Mathf.Sqrt(d2); // 0=中心 → ~1.41=角
                    float kIdle = Mathf.Clamp01(1f - d * topFalloff);
                    float k = kIdle;
                    // 幂次整形：>1 收紧亮斑、<1 抬高中段（平顶化，横向拉宽光斑）；=1 线性
                    if (Mathf.Abs(idleTightness - 1f) > 0.001f) k = Mathf.Pow(kIdle, idleTightness);
                    k *= idleTopScale;                          // 初始顶面整体亮度比例（去白色爆点）
                    if (coreBlend > 0f)
                    {
                        float core = Mathf.Exp(-d2 / sigma2);       // 中心高斯亮斑（d=0 → 1.0）
                        float pressed = Mathf.Max(core, kIdle * edgeKeep); // 按下基底=未收紧径向 × edgeKeep
                        k = Mathf.Lerp(k, pressed, coreBlend);
                    }
                    px[y * size + x] = new Color(k, k, k, 1f);
                }
            // 侧面竖条：u < 0.06（顶面采样从 0.08 起，互不干扰），v < 0.7
            // 按下时按 sideGlowPressDim 压暗（参考图按下=顶亮侧暗，随 coreBlend 平滑过渡）
            float sideDim = Mathf.Lerp(1f, sidePressDim, coreBlend);
            int stripW = (int)(size * 0.06f);
            int stripH = (int)(size * 0.70f);
            for (int y = 0; y < stripH; y++)
            {
                float t = (float)y / (stripH - 1);          // 0=底 → 1=顶（遮罩 v 坐标；侧壁实际采样 v=0.05~0.67）
                float k = Mathf.Lerp(sideBottom, sideTop, Mathf.Pow(t, curve)) * sideDim;
                for (int x = 0; x < stripW; x++)
                    px[y * size + x] = new Color(k, k, k, 1f);
            }
        }

        // 按下亮斑动态遮罩：64×64（渐变足够平滑），只有发光中的键每帧更新，开销可忽略
        static Color[] dynMaskPx;
        static Texture2D GetOrCreateDynMask(ref Texture2D tex)
        {
            if (tex == null)
                tex = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            if (dynMaskPx == null) dynMaskPx = new Color[64 * 64];
            return tex;
        }

        // 反照率遮罩：全白（顶面保持原色），左侧竖条（u<0.06, v<0.7）= sideDark 供侧面采样——
        // 侧面漫反射乘 sideDark 压暗；alpha 通道可选作光滑度遮罩（顶面=topA，侧壁=sideA），
        // 配合 _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A 关键字实现"顶面镜面、侧壁哑光"的分区光滑度。
        // 参数化 + 缓存：不同键盘实例可用不同的侧壁参数互不干扰
        static readonly Dictionary<(float, float, float), Texture2D> albedoMaskCache
            = new Dictionary<(float, float, float), Texture2D>();
        static Texture2D AlbedoMask(float sideDark, float topA, float sideA)
        {
            var key = (sideDark, topA, sideA);
            if (albedoMaskCache.TryGetValue(key, out var cached)) return cached;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            float topAlpha = topA < 0f ? 1f : Mathf.Clamp01(topA);
            float sideAlpha = sideA < 0f ? 1f : Mathf.Clamp01(sideA);
            for (int i = 0; i < px.Length; i++) px[i] = new Color(1f, 1f, 1f, topAlpha);
            int stripW = (int)(size * 0.06f);
            int stripH = (int)(size * 0.70f);
            for (int y = 0; y < stripH; y++)
                for (int x = 0; x < stripW; x++)
                    px[y * size + x] = new Color(sideDark, sideDark, sideDark, sideAlpha);
            tex.SetPixels(px);
            tex.Apply(false, true);
            albedoMaskCache[key] = tex;
            return tex;
        }

        // 圆形软边粒子贴图：中心不透明 → 边缘 0.7 半径处开始羽化到 1.0 全透明
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
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / (size * 0.5f); // 0=圆心 → 1=外接圆
                    float a = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((d - 0.7f) / 0.3f));
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            tex.Apply(false, true);
            circleTex = tex;
            return tex;
        }

        void BuildKeyMap()
        {
            // 1. 找到所有键帽主体网格（名字以 Cube 开头），键的根节点 = Cap 的父节点
            var roots = new HashSet<Transform>();
            var rootPos = new Dictionary<Transform, Vector3>();
            foreach (var mf in GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null || !mf.sharedMesh.name.StartsWith("Cube")) continue;
                Transform t = mf.transform;
                if (t.parent != null && t.parent.name.StartsWith("Cap") && t.parent.parent != null)
                    t = t.parent.parent;
                else if (t.parent != null)
                    t = t.parent;
                if (roots.Add(t))
                    rootPos[t] = t.position;
            }

            if (roots.Count == 0)
            {
                Debug.LogError("[模型键盘] 没找到键帽（Cube 网格）。请确认脚本挂在 Keyboard68 模型实例上。");
                return;
            }

            // 2. 深度轴 = 两个水平轴中跨度较小的那个（键盘宽度 > 进深）
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var p in rootPos.Values)
            {
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z);
            }
            bool depthIsZ = (maxX - minX) >= (maxZ - minZ);
            float dMin = depthIsZ ? minZ : minX, dMax = depthIsZ ? maxZ : minX;
            float tol = (dMax - dMin) * 0.1f;

            // 3. 按深度聚类成行
            var posList = new List<KeyValuePair<Transform, Vector3>>(rootPos);
            posList.Sort((a, b) =>
                (depthIsZ ? a.Value.z : a.Value.x).CompareTo(depthIsZ ? b.Value.z : b.Value.x));
            var rows = new List<List<KeyValuePair<Transform, Vector3>>>();
            foreach (var kv in posList)
            {
                float d = depthIsZ ? kv.Value.z : kv.Value.x;
                if (rows.Count > 0)
                {
                    var prev = rows[rows.Count - 1];
                    float pd = depthIsZ ? prev[0].Value.z : prev[0].Value.x;
                    if (Mathf.Abs(d - pd) < tol) { prev.Add(kv); continue; }
                }
                rows.Add(new List<KeyValuePair<Transform, Vector3>> { kv });
            }

            // 4. 行序校正：数字行（15 键）在后，底行（9 键）在前
            if (rows.Count >= 2 && rows[0].Count < rows[rows.Count - 1].Count)
                rows.Reverse();

            var counts = new int[rows.Count];
            for (int i = 0; i < rows.Count; i++) counts[i] = rows[i].Count;
            if (rows.Count != Layout.Length || !CountsMatch(counts))
            {
                Debug.LogError($"[模型键盘] 行聚类结果 {string.Join(",", counts)} 与布局 {Layout.Length} 行不符，物理键盘映射已跳过（鼠标点击仍可用）。");
                CreateSlots(rows, null);
                return;
            }

            // 5. 行内列方向自动校正：FBX 坐标转换可能翻转 X 轴导致左右镜像。
            //    底行最宽的键必是空格，而空格在布局左半边（col 3）——据此自动判定列方向。
            Vector3 colAxis = depthIsZ ? transform.right : transform.forward;
            float ColVal(Vector3 p) => depthIsZ ? p.x : p.z;
            float KeyWidth(Transform root)
            {
                var rs = root.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) return 0f;
                var b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                return Extent(colAxis, b.size);
            }

            int colSign = 1; // 1=升序(左→右), -1=降序
            var bottomRow = rows.Find(r => r.Count == 9);
            if (bottomRow != null)
            {
                bottomRow.Sort((a, b) => ColVal(a.Value).CompareTo(ColVal(b.Value)));
                int widestIdx = 0; float widest = -1f;
                for (int i = 0; i < bottomRow.Count; i++)
                {
                    float w = KeyWidth(bottomRow[i].Key);
                    if (w > widest) { widest = w; widestIdx = i; }
                }
                if (widestIdx > bottomRow.Count / 2) colSign = -1;
            }

            for (int r = 0; r < rows.Count; r++)
            {
                if (colSign > 0) rows[r].Sort((a, b) => ColVal(a.Value).CompareTo(ColVal(b.Value)));
                else rows[r].Sort((a, b) => ColVal(b.Value).CompareTo(ColVal(a.Value)));
                var layout = Layout[r];
                for (int c = 0; c < rows[r].Count; c++)
                    rowColOf[rows[r][c].Key] = (r, c);
            }
            Debug.Log($"[模型键盘] 列方向 colSign={colSign}（1=正常, -1=已自动翻转镜像）。");
            CreateSlots(rows, Layout);
            Debug.Log($"[模型键盘] 已映射 {slots.Count} 个键帽 → KeyCode（{rows.Count} 行 {string.Join(",", counts)}）。");
        }

        readonly Dictionary<Transform, (int r, int c)> rowColOf = new Dictionary<Transform, (int, int)>();

        ParticleSystem CreateConvergeParticles(Transform root, float keyHeight)
        {
            var go = new GameObject("ConvergeParticles");
            go.transform.SetParent(root, false);
            // 发射器 = 字符中心，+Z 沿"键帽上方"（底座反方向）
            go.transform.rotation = Quaternion.LookRotation(-pressSign * PressAxisWorld);
            go.transform.position = ParticleOrigin(root, keyHeight);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            float k = Mathf.Max(1f, keyHeight);
            // 圆形微尘：透明度在 40%~70% 之间随机（每颗粒子固定一个值）
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.9f, 0.92f, 0.95f, 0.4f),
                new Color(0.9f, 0.92f, 0.95f, 0.7f));
            main.startSize = new ParticleSystem.MinMaxCurve
            {
                mode = ParticleSystemCurveMode.TwoConstants,
                constantMin = 0.045f * k,
                constantMax = 0.155f * k
            };
            // 寿命 = 恰好从球壳汇聚到中心点的时间：粒子在键帽上方中心处消失
            main.startLifetime = Mathf.Max(0.3f, particleRadius / Mathf.Max(0.5f, convergeSpeed));
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            main.maxParticles = 300;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var shape = ps.shape;
            // 半球壳（开口朝上）：粒子在汇聚点上方生成、向下飘落到顶面中心——
            // 用户指定方向"从上往下"
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = particleRadius;
            shape.radiusThickness = 0.05f;

            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.radial = -convergeSpeed * 0.75f; // 稍缓的汇聚，飘得更柔和

            // 加一点噪声扰动，让微尘有漂浮感、轨迹不那么机械
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.08f;
            noise.frequency = 0.6f;

            var em = ps.emission;
            em.enabled = false; // 开关和发射率由目标键逻辑控制
            em.rateOverTime = idleEmissionRate; // 连续发射，同屏稳定约 5~6 颗

            // 透明度固定为 startColor 的随机值（40%~70%），不再随生命周期渐变——
            // 若开启渐变会把最终 alpha 相乘压到远低于用户要求的区间
            var col = ps.colorOverLifetime;
            col.enabled = false;

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            // 浅色微尘用普通 alpha 混合（加法混合在浅色背景上会看不见）
            bool urp = GraphicsSettings.currentRenderPipeline != null;
            Shader shader = urp
                ? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                : Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default"); // 兜底：普通混合、吃顶点色
            if (shader == null)
                Debug.LogWarning("[Keyboard3D] 粒子 shader 未打包（构建裁剪）：需在 Assets/Settings 放 UrpParticlesPlaceholder.mat 强制进包");
            if (shader != null)
            {
                var pm = new Material(shader);
                if (pm.HasProperty("_BaseColor")) pm.SetColor("_BaseColor", Color.white);
                if (pm.HasProperty("_Color")) pm.SetColor("_Color", Color.white);
                if (pm.HasProperty("_TintColor")) pm.SetColor("_TintColor", Color.white);
                // 圆形软边贴图：不贴图时 URP/Particles/Unlit 会把粒子画成实心方块
                if (pm.HasProperty("_BaseMap")) pm.SetTexture("_BaseMap", CircleTex());
                else if (pm.HasProperty("_MainTex")) pm.SetTexture("_MainTex", CircleTex());
                // 关键字/混合状态必须与 UrpParticlesPlaceholder.mat 完全一致：
                // 占位材质只把"透明变体"带进构建，运行时默认的不透明变体会被裁剪
                // → 粒子在 WebGL 不可见/变洋红。这里显式切成透明 Alpha 混合。
                if (pm.HasProperty("_Surface")) pm.SetFloat("_Surface", 1f);
                pm.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                if (pm.HasProperty("_Blend")) pm.SetFloat("_Blend", 0f);      // Alpha 混合
                if (pm.HasProperty("_SrcBlend")) pm.SetFloat("_SrcBlend", 5f);
                if (pm.HasProperty("_DstBlend")) pm.SetFloat("_DstBlend", 10f);
                if (pm.HasProperty("_ZWrite")) pm.SetFloat("_ZWrite", 0f);
                pm.renderQueue = 3000;
                rend.material = pm;
            }
            return ps;
        }

        // 粒子渐隐：改渲染材质的透明度（粒子颜色在生成时固化，改 startColor 不影响已有粒子；
        // 材质为每键独立实例，互不影响）。属性名因 shader 而异，逐个尝试
        static readonly int PPID_BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int PPID_Color = Shader.PropertyToID("_Color");
        static readonly int PPID_TintColor = Shader.PropertyToID("_TintColor");
        static void SetParticleAlpha(Material m, float a)
        {
            if (m == null) return;
            if (m.HasProperty(PPID_BaseColor)) { var c = m.GetColor(PPID_BaseColor); c.a = a; m.SetColor(PPID_BaseColor, c); }
            else if (m.HasProperty(PPID_Color)) { var c = m.GetColor(PPID_Color); c.a = a; m.SetColor(PPID_Color, c); }
            if (m.HasProperty(PPID_TintColor)) { var c = m.GetColor(PPID_TintColor); c.a = a; m.SetColor(PPID_TintColor, c); }
        }

        // 粒子双形态切换：
        //   平时 = 小范围球壳向键帽上方中心汇聚（聚焦、克制）
        //   按下 = 大范围半球体积内的松散光尘云（参考图：铺满键帽上方一大片，缓缓上飘）
        void ConfigureParticleCloud(ParticleSystem ps, bool pressed)
        {
            if (ps == null) return;
            var main = ps.main;
            var shape = ps.shape;
            var vol = ps.velocityOverLifetime;
            if (pressed)
            {
                // 大范围但同样向中心汇聚：半球体积内生成，全部收向键帽上方中心
                main.startLifetime = pressedCloudRadius / Mathf.Max(0.5f, convergeSpeed); // 恰好在汇聚点消失
                shape.shapeType = ParticleSystemShapeType.Hemisphere; // 半球体积，铺在键帽上方
                shape.radius = pressedCloudRadius;
                shape.radiusThickness = 1f;                    // 体积内随机分布（不是薄壳）
                vol.radial = -convergeSpeed;                   // 向心汇聚
                vol.z = 0f;
            }
            else
            {
                main.startLifetime = Mathf.Max(0.3f, particleRadius / Mathf.Max(0.5f, convergeSpeed));
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = particleRadius;
                shape.radiusThickness = 0.05f;                 // 球壳
                vol.radial = -convergeSpeed * 0.75f;           // 向中心汇聚
                vol.z = 0f;
            }
        }

        static bool CountsMatch(int[] counts)
        {
            if (counts.Length != 5) return false;
            int[] expect = { 15, 15, 14, 14, 9 };
            for (int i = 0; i < 5; i++) if (counts[i] != expect[i]) return false;
            return true;
        }

        void CreateSlots(List<List<KeyValuePair<Transform, Vector3>>> rows, KeyCode[][] layout)
        {
            foreach (var row in rows)
            {
                foreach (var kv in row)
                {
                    var root = kv.Key;
                    var renderers = root.GetComponentsInChildren<Renderer>();
                    if (renderers.Length == 0) continue;
                    var b = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

                    var slot = new KeySlot
                    {
                        root = root,
                        originLocal = root.localPosition,
                        height = Mathf.Max(0.01f, Extent(PressAxisWorld, b.size)),
                        code = KeyCode.None,
                    };

                    // 键帽主体（Cube）+ 字母网格分别建材质实例；
                    // 黑键白字无条件应用（不依赖 enableGlow / 网格名分支，
                    // WebGL 构建里若分支没走键帽会保持 FBX 原色）
                    bool capMatched = false;
                    foreach (var r in renderers)
                    {
                        var mf = r.GetComponent<MeshFilter>();
                        if (mf == null || mf.sharedMesh == null) continue;
                        bool isCap = mf.sharedMesh.name.StartsWith("Cube", System.StringComparison.OrdinalIgnoreCase);
                        if (isCap)
                        {
                            capMatched = true;
                            RewriteCapUv(mf, capTopNormalMinY, wideStripWidth); // 顶面采样径向渐变、侧面+倒角肩部采样深灰角
                            var m = new Material(r.sharedMaterial);
                            if (enableGlow)
                            {
                                m.EnableKeyword("_EMISSION");
                                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                                m.SetColor(EmissionID, Color.black);
                                // 渐变自发光遮罩：顶面中央亮绿边缘稍暗，侧面顶缘绿光往下沉黑（四层结构第 1/2 层）
                                m.SetTexture(EmissionMapID, EmissionMask(sideGlowTop, sideGlowBottom, sideGlowCurve, topGlowFalloff, idleGlowTightness, sideGlowPressDim, idleTopScale));
                            }
                            // 记录原始光滑度（发光时调高制造高光"光源感"，松开恢复）；
                            // 也作为光滑度遮罩的顶面 alpha 值
                            // keycapSmoothness>=0 时强制覆盖：塑料质感（高光宽而柔，不像金属/玻璃的锐利白斑）
                            if (keycapSmoothness >= 0f)
                            {
                                if (m.HasProperty(GlossID)) m.SetFloat(GlossID, keycapSmoothness);
                                if (m.HasProperty(SmoothnessID)) m.SetFloat(SmoothnessID, keycapSmoothness);
                            }
                            if (m.HasProperty(GlossID)) { slot.smoothID = GlossID; slot.baseSmoothness = m.GetFloat(GlossID); }
                            else if (m.HasProperty(SmoothnessID)) { slot.smoothID = SmoothnessID; slot.baseSmoothness = m.GetFloat(SmoothnessID); }
                            // 反照率遮罩：顶面采样白色（原色不变），侧面采样 sideAlbedoDark 压暗侧壁；
                            // alpha 通道可选作光滑度遮罩（sideSmoothnessMask>=0 时启用）：
                            // 顶面 alpha=材质原光滑度、侧壁 alpha=sideSmoothnessMask，
                            // 配合 _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A——钢琴黑 0.92 侧壁镜面反射
                            // 环境光是"侧壁发灰"的主因，压到 0.2 后侧壁哑光近黑
                            bool maskSmooth = sideSmoothnessMask >= 0f;
                            // 糖果色模式：侧壁不压暗（sideDark=1 保持糖果色），黑键模式沿用 sideAlbedoDark 沉黑
                            float capSideDark = candyKeycaps ? 1f : sideAlbedoDark;
                            var albedo = AlbedoMask(capSideDark,
                                maskSmooth ? slot.baseSmoothness : -1f,
                                maskSmooth ? sideSmoothnessMask : -1f);
                            if (m.HasProperty(BaseMapID)) m.SetTexture(BaseMapID, albedo);
                            else if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", albedo);
                            if (maskSmooth)
                            {
                                m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
                                if (m.HasProperty("_SmoothnessTextureChannel")) m.SetFloat("_SmoothnessTextureChannel", 1f);
                                if (slot.smoothID != 0) m.SetFloat(slot.smoothID, 1f); // 标量置 1，分区值全由 alpha 提供
                                slot.smoothLocked = true; // 发光提光滑度逻辑对遮罩工作流失效，锁定
                            }
                            r.material = m;
                            if (enableGlow) slot.glowMat = m;
                            // 黑键白字配色覆盖（参考图）
                            slot.colorID = m.HasProperty("_BaseColor") ? BaseColorID : ColorID;
                            if (candyKeycaps)
                            {
                                // 糖果配色（10-15 版参考图 7 色逐键映射，按行列查表）
                                var cc = CandyColorFor(root);
                                m.SetColor(slot.colorID, cc);
                            }
                            else if (blackKeycaps) m.SetColor(slot.colorID, keycapBlack);
                            slot.baseColor = m.GetColor(slot.colorID);
                        }
                        else if (slot.charMat == null)
                        {
                            // 字母网格：单独实例化，发光时提亮成近白保证可读
                            var m = new Material(r.sharedMaterial);
                            r.material = m;
                            if (enableGlow) slot.charMat = m;
                            slot.charColorID = m.HasProperty("_BaseColor") ? BaseColorID : ColorID;
                            if (candyKeycaps) m.SetColor(slot.charColorID, CandyTextColor); // 糖果色配深灰字符（原参考图）
                            else if (blackKeycaps) m.SetColor(slot.charColorID, Color.white); // 黑键配白字
                            slot.charBaseColor = m.GetColor(slot.charColorID);
                        }
                    }
                    if (!capMatched) capMismatchCount++;

                    if (enableParticles)
                    {
                        slot.ps = CreateConvergeParticles(root, slot.height);
                        slot.psMat = slot.ps.GetComponent<ParticleSystemRenderer>().material; // 独立实例，可安全逐键渐隐
                    }
                    if (layout != null && rowColOf.TryGetValue(root, out var rc))
                    {
                        slot.code = layout[rc.r][rc.c];
                        byCode[slot.code] = slot;
                    }
                    if (enableMouseClick)
                    {
                        var bc = root.gameObject.AddComponent<BoxCollider>();
                        bc.center = root.InverseTransformPoint(b.center);
                        Vector3 ls = root.InverseTransformVector(b.size);
                        bc.size = new Vector3(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z));
                    }
                    byRoot[root] = slot;
                    slots.Add(slot);
                }
            }

            // 构建诊断：WebGL 按 F12 看 Console。若键帽没变黑/没有发光，先看这里的匹配数——
            // 未匹配说明 FBX 网格命名变了（不再以 "Cube" 开头），材质分支全部没走
            Debug.Log($"[Keyboard3D] CreateSlots 完成：共 {slots.Count} 键，键帽网格匹配 {slots.Count - capMismatchCount}，未匹配 {capMismatchCount}；enableGlow={enableGlow}, blackKeycaps={blackKeycaps}");
            MatteChassis(); // 底座哑光+法线平滑：消除点光在底座上的锯齿状高光斑
        }

        // 底座处理（用户反馈底座上出现锯齿状光影）：
        // 1) 哑光化——NormalizeShaders 给所有网格统一了 0.8 高光滑度，键帽后来调成 0.62，
        //    但底座（不在键 root 下）一直是 0.8：点光在其上产生锐利镜面白斑；
        // 2) 法线平滑——底座低模是硬边法线（每面恒定），无阴影点光的光斑边界沿三角面
        //    碎裂成锯齿。按顶点位置合并平均法线后，光斑边界连续渐变。
        // 只处理键帽/字母层级之外的网格，键帽的精心调校不受影响。
        void MatteChassis()
        {
            if (chassisSmoothness < 0f || slots.Count == 0) return;
            int matCount = 0, meshCount = 0;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                bool inKey = false;
                foreach (var s in slots)
                    if (s.root != null && r.transform.IsChildOf(s.root)) { inKey = true; break; }
                if (inKey) continue;

                var mats = r.materials; // 访问 .materials 即实例化，可安全改
                bool matChanged = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    // 面板 albedo 强制近黑：Rectangle 面板原 albedo 非黑，靠 0.8 镜面"显得黑"，
                    // 点光高光一穿帮就露馅；纯黑 albedo + 哑光 = 稳定的黑塑料，漫反射≈0
                    if (m.HasProperty(BaseColorID)) { m.SetColor(BaseColorID, new Color(0.02f, 0.02f, 0.025f, 1f)); matChanged = true; }
                    else if (m.HasProperty(ColorID)) { m.SetColor(ColorID, new Color(0.02f, 0.02f, 0.025f, 1f)); matChanged = true; }
                    if (m.HasProperty(SmoothnessID)) { m.SetFloat(SmoothnessID, chassisSmoothness); matChanged = true; }
                    if (m.HasProperty(GlossID)) { m.SetFloat(GlossID, chassisSmoothness); matChanged = true; }
                    if (m.HasProperty("_Metallic")) { m.SetFloat("_Metallic", 0f); matChanged = true; }
                }
                if (matChanged) { r.materials = mats; matCount++; }
                Debug.Log($"[Keyboard3D] MatteChassis 处理 renderer={r.name} path={GetPath(r.transform)}");

                if (!chassisSmoothNormals) continue;
                var mf = r.GetComponent<MeshFilter>();
                var src = mf != null ? mf.sharedMesh : null;
                if (src == null || !src.isReadable || src.name.EndsWith("_SmoothN")) continue;
                var mesh = Object.Instantiate(src);
                mesh.name = src.name + "_SmoothN";
                var verts = mesh.vertices;
                var norms = mesh.normals;
                // 按位置（量化到 0.1mm）合并平均法线：硬边顶点共享平滑法线
                var acc = new Dictionary<long, Vector3>(verts.Length);
                var keyOf = new long[verts.Length];
                for (int i = 0; i < verts.Length; i++)
                {
                    var p = verts[i];
                    long k = ((long)Mathf.RoundToInt(p.x * 10000f) << 42)
                           ^ ((long)Mathf.RoundToInt(p.y * 10000f) << 21)
                           ^ (long)Mathf.RoundToInt(p.z * 10000f);
                    keyOf[i] = k;
                    if (acc.TryGetValue(k, out var v)) acc[k] = v + norms[i];
                    else acc[k] = norms[i];
                }
                for (int i = 0; i < verts.Length; i++)
                    norms[i] = acc[keyOf[i]].normalized;
                mesh.normals = norms;
                mf.mesh = mesh;
                meshCount++;
            }
            Debug.Log($"[Keyboard3D] 底座处理完成：哑光材质 {matCount} 个 renderer，法线平滑 {meshCount} 个 mesh（chassisSmoothness={chassisSmoothness}）");
        }

        static string GetPath(Transform t)
        {
            var sb = new System.Text.StringBuilder(t.name);
            while (t.parent != null) { t = t.parent; sb.Insert(0, t.name + "/"); }
            return sb.ToString();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            foreach (var s in slots) s.driven = false;

            // 物理键盘
            if (!inputUnavailable)
            {
                foreach (var kv in byCode)
                {
                    bool down = false;
                    try { down = Input.GetKey(kv.Key); }
                    catch { inputUnavailable = true; break; }
                    DriveSlot(kv.Value, down, dt);
                }
            }
            if (inputUnavailable)
                foreach (var s in slots) DriveSlot(s, false, dt);

            // 鼠标：点击键帽=按压；左键拖动空白处=旋转；右键拖动=旋转；滚轮=缩放
            try
            {
                if (Input.GetMouseButtonDown(0) && Camera.main != null)
                {
                    leftDownPos = Input.mousePosition;
                    leftRotating = false;
                    // 按在键帽上 → 按压该键；按在空白处 → 准备旋转
                    mouseSlot = TryRaycastKey(out var slot) ? slot : null;
                }
                if (Input.GetMouseButton(0))
                {
                    if (!leftRotating && (Input.mousePosition - leftDownPos).magnitude > 6f)
                    {
                        leftRotating = true;
                        mouseSlot = null; // 拖动开始后取消按压
                    }
                    if (leftRotating && enableOrbit)
                        ApplyRotate(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
                }
                if (Input.GetMouseButtonUp(0))
                {
                    mouseSlot = null;
                    leftRotating = false;
                }

                // 右键拖动：任意位置旋转
                if (Input.GetMouseButton(1) && enableOrbit)
                    ApplyRotate(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

                // 滚轮缩放（推近/拉远相机，距离限制在初始值的 0.35~2.5 倍）
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(scroll) > 0.0001f && Camera.main != null && enableOrbit)
                {
                    var camT = Camera.main.transform;
                    Vector3 offset = camT.position - transform.position;
                    float dist = offset.magnitude;
                    if (zoomMax <= 0f) { zoomMin = dist * 0.35f; zoomMax = dist * 2.5f; }
                    float nd = Mathf.Clamp(dist - scroll * zoomSpeed, zoomMin, zoomMax);
                    camT.position = transform.position + offset.normalized * nd;
                }
            }
            catch { /* 新 Input System 下不可用 */ }
            if (mouseSlot != null) DriveSlot(mouseSlot, true, dt);

            // 本帧未被任何输入驱动的键（如鼠标专用键）弹回
            foreach (var s in slots)
                if (!s.driven) DriveSlot(s, false, dt);

            // 目标键：开局随机选一个，被按下后自动换下一个
            if (targetSlot == null && slots.Count > 0) PickTarget();

            // 应用效果
            Vector3 worldDown = pressSign * PressAxisWorld;
            foreach (var s in slots)
            {
                float depth = pressDepthRatio * s.height;
                Vector3 localDown = s.root.parent != null
                    ? s.root.parent.InverseTransformDirection(worldDown) * depth
                    : Vector3.down * depth;
                s.root.localPosition = s.originLocal + localDown * s.amount;

                // 发光：只有目标键亮，按下时明显变亮，其余自然衰减
                if (s != targetSlot)
                    s.glowAmount = Mathf.MoveTowards(s.glowAmount, 0f, releaseSpeed * dt);
                if (s.glowMat != null)
                {
                    // 按下提亮 Emission 但必须克制：加成过高时遮罩边缘也被推过 1.0，
                    // ACES 削顶 + Bloom 让光晕逸散到邻键（WebGL 截图的教训）。
                    // 0.15 → 按下 3.0，顶缘 0.38×3.0≈1.14 刚过阈值一线，光晕贴边不外溢
                    float glow = s.glowAmount * glowIntensity * (1f + s.amount * pressGlowBoost);
                    // 宽键初始长条：自发光遮罩经完全椭圆 UV 后接近均匀（端部 91%），
                    // 初始态整体增强推过 Bloom 阈值 → 连续发光长条（点光 1/d² 做不出均匀长条）；
                    // 随按下淡出（按下形态由 coreBlend 遮罩接管）
                    if (s.wide)
                        glow *= 1f + wideIdleStripBoost * (1f - s.amount) * s.glowAmount;
                    var e = glowColor * glow;
                    // URP HDR：发光值直接交给 Bloom（阈值 1.1），不封顶；
                    // 绿通道 1.8~2.2 处于 HDR 1~3 区间，光晕克制
                    s.glowMat.SetColor(EmissionID, e);
                    // 按下中心亮斑（参考图 Main 键按下：顶面中心泛白光核、边缘仍绿）：
                    // 发光遮罩随按压量向"高斯亮斑"形态过渡；配合按压加成，中心先过
                    // Bloom 阈值 + ACES 泛白形成光斑。pressGlowCore=0 时完全走旧行为
                    if (pressGlowCore > 0f && s.glowMat.HasProperty(EmissionMapID))
                    {
                        // 过渡系数完全跟手（pressGlowCore 只作开关与核心大小）：
                        // 按到底必须完整到达"均匀亮绿"形态，半途过渡会残留暗边
                        float coreBlend = Mathf.Clamp01(s.amount);
                        if (coreBlend > 0.001f || s.lastCoreBlend > 0.001f)
                        {
                            GetOrCreateDynMask(ref s.dynMask);
                            if (Mathf.Abs(coreBlend - s.lastCoreBlend) > 0.02f)
                            {
                                FillEmissionPixels(dynMaskPx, 64, sideGlowTop, sideGlowBottom, sideGlowCurve,
                                    topGlowFalloff, coreBlend, pressGlowCoreSize, pressGlowEdgeKeep, idleGlowTightness, sideGlowPressDim, idleTopScale);
                                s.dynMask.SetPixels(dynMaskPx);
                                s.dynMask.Apply(false, false);
                                s.lastCoreBlend = coreBlend;
                            }
                            s.glowMat.SetTexture(EmissionMapID,
                                coreBlend > 0.001f ? s.dynMask : EmissionMask(sideGlowTop, sideGlowBottom, sideGlowCurve, topGlowFalloff, idleGlowTightness, sideGlowPressDim, idleTopScale));
                            if (coreBlend <= 0.001f) s.lastCoreBlend = 0f; // 已切回静态遮罩，下次按下从干净状态起步
                        }
                    }
                    // 键帽染色：权重 glowAlbedoLerp（默认 0.95 深绿）；调低可让侧壁保持
                    // 黑塑料质感、顶面亮度交给自发光的径向渐变（参考图"中心光晕"观感）
                    var albedo = Color.Lerp(s.baseColor, GlowAlbedo, s.glowAmount * glowAlbedoLerp);
                    s.glowMat.SetColor(s.colorID, albedo);
                    // 发光时光滑度拉到 0.8（规格 0.75~0.9）：顶灯镜面高光让键帽看起来真的在"发光"
                    // （光滑度遮罩模式下标量已锁定为 1，分区值由遮罩 alpha 提供，跳过）
                    if (s.smoothID != 0 && !s.smoothLocked)
                        s.glowMat.SetFloat(s.smoothID, Mathf.Lerp(s.baseSmoothness, 0.8f, s.glowAmount));
                    // 字母提亮成近白：深绿底 + 白字，参考图同款的可读性；
                    // 糖果模式例外：字符保持黑色不提亮（发光键帽是亮绿，黑字对比反而最强，提白会看不见）
                    if (s.charMat != null)
                        s.charMat.SetColor(s.charColorID,
                            candyKeycaps
                                ? s.charBaseColor
                                : Color.Lerp(s.charBaseColor, new Color(0.95f, 1f, 0.95f), s.glowAmount));
                }

                // 粒子：只有目标键有尘埃；平时小范围汇聚，按下切大范围光影云
                if (s.ps != null)
                {
                    // 松开即提交：releaseTime 满 particleFadeDelay 秒后开始渐隐——
                    // 材质 alpha 在 particleFadeDuration 秒内线性降到 0（用户要求渐变消失，
                    // 不是瞬间 Clear；粒子颜色生成时已固化，渐隐只能动渲染材质）
                    if (s.releaseTime >= 0f)
                    {
                        float fadeT = (Time.time - s.releaseTime - particleFadeDelay)
                            / Mathf.Max(0.05f, particleFadeDuration);
                        if (fadeT >= 1f)
                        {
                            s.ps.Clear(true);
                            SetParticleAlpha(s.psMat, 1f); // 复位，下次发射恢复不透明
                            s.cloudActive = false;
                            s.releaseTime = -1f;
                        }
                        else if (fadeT > 0f)
                            SetParticleAlpha(s.psMat, 1f - fadeT);
                        else
                            SetParticleAlpha(s.psMat, 1f); // 保留期内确保不透明
                    }
                    var em = s.ps.emission;
                    if (s == targetSlot)
                    {
                        // 随机重新选中同一键 = 新目标：取消渐隐流程，恢复正常透明度
                        if (s.releaseTime >= 0f)
                        {
                            s.releaseTime = -1f;
                            SetParticleAlpha(s.psMat, 1f);
                        }
                        em.enabled = true;
                        bool pressed = s.amount > 0.5f;
                        // 平时连续发射（同屏稳定 5~6 颗），按住时大范围云 + 高密度
                        em.rateOverTime = pressed ? pressedEmissionRate : idleEmissionRate;
                        if (s.cloudActive != pressed)
                        {
                            s.cloudActive = pressed;
                            ConfigureParticleCloud(s.ps, pressed);
                        }
                    }
                    else
                    {
                        em.enabled = false;
                        // 不再是目标键：立即停止发射，粒子再漂浮 particleFadeDelay 秒后
                        // 开始渐隐（总消失 = delay + duration）
                        if (s.releaseTime < 0f && (s.ps.particleCount > 0 || s.cloudActive))
                            s.releaseTime = Time.time;
                    }
                }

                // 按过的键完全松开后：若是目标键，此时才切换到下一个目标
                if (s.pressConsumed && s.amount < 0.2f)
                {
                    s.pressConsumed = false;
                    if (s == targetSlot) PickTarget();
                }
            }

            // 目标键恒定发光（不呼吸脉动）；按下瞬间粒子爆发一批（切换在松开时发生）
            if (targetSlot != null)
            {
                targetSlot.glowAmount = 1f;
                if (targetSlot.amount > 0.5f && !targetSlot.pressConsumed)
                {
                    targetSlot.pressConsumed = true;
                    if (targetSlot.ps != null)
                        targetSlot.ps.Emit(pressedBurst);
                }
            }

            // 两盏点光源强度跟随目标键的呼吸脉动 + 按压增强（与自发光同步）
            // lightOnPressOnly：顶灯随按压量点亮（初始态顶灯熄灭 → 顶面只剩自发光径向渐变，暗翡翠感）；
            // 按到底到 lightPressPeak（默认 1.6×=旧按下亮度，可调低避免顶面冲爆）
            float lightFactor = targetSlot == null ? 0f
                : lightOnPressOnly
                    ? targetSlot.amount * (lightPressBase + (lightPressPeak - lightPressBase) * targetSlot.glowAmount)
                    : targetSlot.glowAmount * (1f + targetSlot.amount * 0.6f); // 按下顶灯 1.6 倍：够亮但
                                                                              // 不把顶面推过 Bloom 阈值太多（×3 时 Bloom 会把光晕逸散到邻键）
            if (targetLight != null)
                targetLight.intensity = pointLightIntensity * lightFactor;
            // 按压下沉补偿：键根下沉多少，灯就相对键根抬高多少，世界高度恒定
            if (targetLight != null && targetSlot != null)
                targetLight.transform.localPosition =
                    lightBaseLocal - lightSinkLocal * targetSlot.amount;
            // 周围泛光点光：随发光出现，按下明显增强（跟随键帽下沉，无补偿 → 自家顶面亮度恒定）。
            // lightOnPressOnly 时初始压暗到 haloPressDim、按到底恢复到 haloPressPeak（可调低，
            // 泛光贴键顶极近、1/d² 照度极高，按下 100% 会把顶面冲成全亮）。
            // 宽键初始态额外压到 wideHaloIdleDim：长条亮度由自发光承担，单盏点光必然是圆形热点
            float wideHaloMul = targetSlot != null && targetSlot.wide
                ? Mathf.Lerp(wideHaloIdleDim, 1f, targetSlot.amount) : 1f;
            if (haloLight != null)
                haloLight.intensity = targetSlot != null
                    ? haloLightIntensity * targetSlot.glowAmount * (1f + targetSlot.amount * 0.3f)
                        * (lightOnPressOnly ? Mathf.Lerp(haloPressDim, haloPressPeak, targetSlot.amount) : 1f)
                        * wideHaloMul
                    : 0f;
            // 宽键初始长条光斑：两盏沿长轴分布的泛光点光，只在初始态亮——
            // 随按下 (1-amount) 淡出（保证"按下单光斑"），且仅宽键启用（窄键恒灭）。
            // 初始亮度与中间泛光的初始值一致（lightOnPressOnly 时 = haloPressDim 档）
            float sideHaloI = targetSlot != null && flankersActive
                ? haloLightIntensity * targetSlot.glowAmount * haloSideIntensity
                    * (lightOnPressOnly ? haloPressDim : 1f)
                    * (1f - targetSlot.amount)
                : 0f;
            foreach (var l in haloSideLights)
                if (l != null) l.intensity = sideHaloI;
            // 零强度灯直接关闭：URP 逐物体附加光名额（m_AdditionalLightsPerObjectLimit）
            // 按"enabled 且在范围内"计数，强度 0 也占位 → 不关闭会挤掉有效光源
            if (targetLight != null) targetLight.enabled = targetLight.intensity > 0.001f;
            if (haloLight != null) haloLight.enabled = haloLight.intensity > 0.001f;
            if (spillLight != null) spillLight.enabled = spillLight.intensity > 0.001f;
            foreach (var fl in flankLights)
                if (fl != null) fl.enabled = fl.intensity > 0.001f;
            foreach (var l in haloSideLights)
                if (l != null) l.enabled = l.intensity > 0.001f;
            // 邻键溢光：跟随目标键发光恒亮（低位，不冲自家顶面）。
            // 宽键（空格等）时扩大 range + 增强：单盏灯覆盖不到长条两端的邻键
            // （侧溢光方案因无阴影+近距离自照产生亮团已废弃），扩 range 即可沿条覆盖
            if (spillLight != null)
            {
                bool wide = targetSlot != null && flankersActive;
                spillLight.range = wide ? spillLightRange * 1.5f : spillLightRange;
                spillLight.intensity = targetSlot != null
                    ? spillLightIntensity * targetSlot.glowAmount * (wide ? 1.35f : 1f)
                    : 0f;
            }
            // 宽键两端补光灯（窄键自动熄灭）
            // lightOnPressOnly 模式停用：两盏端部补光会把按下的宽键打成"两个光斑"
            // （用户反馈"按下应该是一个光斑"），宽键照明由主锥 110° + 均匀 emission 承担
            float flankI = (flankersActive && !lightOnPressOnly ? pointLightFlankIntensity : 0f) * lightFactor;
            foreach (var fl in flankLights)
                if (fl != null) fl.intensity = flankI;
        }

        void DriveSlot(KeySlot s, bool down, float dt)
        {
            s.driven = true;
            if (down)
                s.amount = Mathf.MoveTowards(s.amount, 1f, pressSpeed * dt);
            else
                s.amount = Mathf.MoveTowards(s.amount, 0f, releaseSpeed * dt);
        }

        // 拖动旋转（世界轴）：
        //   水平拖动 = 绕世界竖直轴"转身"（180° 时从另一侧看，仍是键顶朝上）
        //   垂直拖动 = 绕屏幕水平轴前后翻转
        // 用世界轴叠加而不是模型局部轴——模型基准旋转不为零时局部轴会导致
        // 水平拖动变成侧滚，做不出"转身"效果
        void ApplyRotate(float dx, float dy)
        {
            if (Mathf.Abs(dx) < 0.0001f && Mathf.Abs(dy) < 0.0001f) return;
            yaw += dx * rotateSpeed;
            pitch = Mathf.Clamp(pitch - dy * rotateSpeed, -maxPitch, maxPitch);
            transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up)
                               * Quaternion.AngleAxis(pitch, Vector3.right)
                               * baseRot;
        }

        bool TryRaycastKey(out KeySlot slot)
        {
            slot = null;
            if (Camera.main == null) return false;
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 100f))
            {
                var t = hit.transform;
                while (t != null && !byRoot.ContainsKey(t)) t = t.parent;
                if (t != null) { slot = byRoot[t]; return true; }
            }
            return false;
        }

        void PickTarget()
        {
            var pool = slots.FindAll(s => s != targetSlot && s.code != KeyCode.None);
            if (pool.Count == 0) pool = slots.FindAll(s => s != targetSlot);
            if (pool.Count == 0) return;
            // 调试：指定目标键（名称/键码包含即命中，如 "Space"），留空则纯随机
            if (!string.IsNullOrEmpty(debugTargetKey))
            {
                var dbg = slots.Find(s => s.code != KeyCode.None &&
                    (s.root.name.Contains(debugTargetKey) || s.code.ToString().Contains(debugTargetKey)));
                if (dbg != null)
                {
                    targetSlot = dbg;
                    AttachTargetLight(targetSlot);
                    return;
                }
            }
            targetSlot = pool[Random.Range(0, pool.Count)];
            AttachTargetLight(targetSlot); // 点光源"卸载旧目标 → 挂载新目标"
        }

        static readonly int EmissionID = Shader.PropertyToID("_EmissionColor");
        static readonly int EmissionMapID = Shader.PropertyToID("_EmissionMap");
        // ---- 糖果配色（10-15 版参考图，7 色逐键）----
        // 行序与 rows 一致（rows[0]=数字行 15 键 … rows[4]=底行 9 键），
        // 列序已由 MapKeys 的 colSign 校正为屏幕左→右
        static readonly string CandyTextColorHex = "000000"; // 黑色字符（用户 10-22 要求；糖果浅色键帽上黑字对比清晰）
        static readonly string[][] CandyRowColors =
        {
            // row0 数字行: Esc 1 2 3 4 5 6 7 8 9 0 - = Bksp `
            new[]{ "white","white","pink","green","lavender","lavender","purple","purple","green","pink","blue","blue","blue","white","blue" },
            // row1: Tab Q W E R T Y U I O P [ ] \ Del
            new[]{ "white","blue","pink","green","lavender","lavender","purple","purple","green","pink","blue","blue","blue","blue","blue" },
            // row2: Caps A S D F G H J K L ; ' Enter Ins
            new[]{ "blue","blue","pink","green","lavender","lavender","purple","purple","green","pink","blue","blue","white","blue" },
            // row3: LShift Z X C V B N M , . / RShift ^ RCtrl
            new[]{ "blue","blue","pink","green","lavender","lavender","purple","purple","green","pink","blue","blue","white","blue" },
            // row4: Ctrl Win Alt Space Alt Opt < v >
            new[]{ "blue","blue","blue","cream","blue","blue","white","white","white" },
        };
        static Color CandyHex(string hex) =>
            ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.white;
        static Color CandyTextColor => CandyHex(CandyTextColorHex);
        Color CandyColorFor(Transform root)
        {
            // 色名 → hex（10-15 版参考图取色）
            if (!rowColOf.TryGetValue(root, out var rc)) return CandyHex("E2E1E6");
            var row = rc.r >= 0 && rc.r < CandyRowColors.Length ? CandyRowColors[rc.r] : null;
            int c = row != null ? Mathf.Clamp(rc.c, 0, row.Length - 1) : 0;
            string name = row != null ? row[c] : "white";
            string hex = name switch
            {
                "blue" => "A6CDF8",
                "pink" => "FCCFDE",
                "green" => "B6DAB2",
                "lavender" => "E5C4EC",
                "purple" => "BBAAFE",
                "cream" => "F4E2C8",
                _ => "E2E1E6", // white
            };
            return CandyHex(hex);
        }

        static readonly int BaseMapID = Shader.PropertyToID("_BaseMap");
        static readonly int ColorID = Shader.PropertyToID("_Color");
        static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
        // 发光染色目标绿（URP Lit 深绿 #008C42，染 95%）
        Color GlowAlbedo = new Color(0f, 0.549f, 0.259f); // 发光键帽染色（黑键模式深绿；Candy 模式 Awake 换深黄）
        static readonly int GlossID = Shader.PropertyToID("_Glossiness");      // Standard
        static readonly int SmoothnessID = Shader.PropertyToID("_Smoothness"); // URP
    }
}
