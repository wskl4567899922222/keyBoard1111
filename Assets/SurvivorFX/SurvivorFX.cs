using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace SurvivorFX
{
    /// <summary>
    /// 幸存者风格特效库（参考割草游戏视频）：放射激光束、彩虹羽刃、伤害飘字、
    /// 爆炸冲击环、烟花粒子、等级文字、角色光环、死亡爆裂。
    /// 纯运行时程序化生成（贴图/网格/材质），无外部资源依赖。
    ///
    /// 复用方式（如给 3D 键盘用）：直接调用静态方法即可，特效自建自毁——
    ///   SurvivorFX.FX.LaserBurst(keycapPos);
    ///   SurvivorFX.FX.DamageNumber(keycapPos + Vector3.up * 0.5f, 1234, true);
    ///
    /// WebGL 构建注意：运行时 Shader.Find 依赖 Assets/Settings/SurvivorFXAdditive.mat
    /// 占位材质（URP/Particles/Unlit 加法混合变体）才能在构建中存活（项目教训）。
    /// </summary>
    public static class FX
    {
        // ---------------- 调色板（视频配色，HDR 值交给 Bloom） ----------------
        public static readonly Color HotPink  = C(1f, 0.22f, 0.80f, 2.0f);
        public static readonly Color Magenta  = C(1f, 0.45f, 1f, 1.7f);
        public static readonly Color Violet   = C(0.60f, 0.32f, 1f, 1.9f);
        public static readonly Color Cyan     = C(0.30f, 1f, 1f, 1.8f);
        public static readonly Color IceWhite = C(1f, 1f, 1f, 1.6f);
        public static readonly Color SoftPink = C(1f, 0.55f, 0.95f, 1.7f);
        public static readonly Color Gold     = C(1f, 0.72f, 0.25f, 2.1f);
        public static readonly Color DmgRed   = C(1f, 0.18f, 0.15f, 2.2f);
        public static readonly Color EyeGreen = C(0.25f, 1f, 0.45f, 2.4f);

        public static readonly Color[] BurstPalette = { HotPink, Magenta, Violet, Cyan, IceWhite, SoftPink };

        static Color C(float r, float g, float b, float m) => new Color(r * m, g * m, b * m, 1f);

        // ---------------- 共享基础材质 / 贴图 / 网格 ----------------
        static Material _additiveBase;
        static Shader _particleShader;

        /// <summary>加法混合基础材质（URP/Particles/Unlit，状态与占位材质完全一致）。</summary>
        public static Material AdditiveBase()
        {
            if (_additiveBase != null) return _additiveBase;
            if (_particleShader == null) _particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (_particleShader == null) _particleShader = Shader.Find("Sprites/Default");
            var m = new Material(_particleShader);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 2f);          // Additive
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", 5f);    // SrcAlpha
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", 1f);    // One
            if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", 5f);
            if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", 1f);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            if (m.HasProperty("_ColorMode")) m.SetFloat("_ColorMode", 0f);  // Multiply（顶点色乘基色）
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            m.renderQueue = 3000;
            _additiveBase = m;
            return m;
        }

        /// <summary>实例化一份加法材质（可独立动画颜色/alpha）。</summary>
        public static Material AdditiveInstance(Color hdr, Texture2D map = null)
        {
            var m = new Material(AdditiveBase());
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", hdr);
            if (map != null && m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", map);
            return m;
        }

        static Texture2D _softDot, _strip, _ring, _blade;
        static Mesh _unitQuad, _bladeMesh;

        /// <summary>柔和光点（粒子/光晕）。</summary>
        public static Texture2D SoftDot()
        {
            if (_softDot != null) return _softDot;
            const int s = 64;
            var t = NewTex(s, s);
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f) / s * 2f - 1f, dy = (y + 0.5f) / s * 2f - 1f;
                    float d = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    float a = d * d * (3f - 2f * d);
                    px[y * s + x] = new Color(1f, 1f, 1f, a);
                }
            t.SetPixels(px); t.Apply(false, true);
            return _softDot = t;
        }

        /// <summary>光束横截面软边条（LineRenderer 用，v 方向=宽度方向）。</summary>
        public static Texture2D Strip()
        {
            if (_strip != null) return _strip;
            const int w = 32, h = 128;
            var t = NewTex(w, h);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h * 2f - 1f;
                float a = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(v) * 1.12f), 1.35f);
                for (int x = 0; x < w; x++) px[y * w + x] = new Color(1f, 1f, 1f, a);
            }
            t.SetPixels(px); t.Apply(false, true);
            return _strip = t;
        }

        /// <summary>薄圆环（冲击波）。</summary>
        public static Texture2D Ring()
        {
            if (_ring != null) return _ring;
            const int s = 256;
            var t = NewTex(s, s);
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f) / s * 2f - 1f, dy = (y + 0.5f) / s * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float band = Mathf.Exp(-Mathf.Pow((r - 0.82f) / 0.075f, 2f));
                    float glow = Mathf.Exp(-Mathf.Pow((r - 0.55f) / 0.28f, 2f)) * 0.16f;
                    px[y * s + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(band + glow));
                }
            t.SetPixels(px); t.Apply(false, true);
            return _ring = t;
        }

        /// <summary>羽刃形（u=横向宽度, v=纵向长度，尖端在 v=1）。</summary>
        public static Texture2D Blade()
        {
            if (_blade != null) return _blade;
            const int w = 128, h = 256;
            var t = NewTex(w, h);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                float shape = Mathf.Sin(Mathf.PI * Mathf.Pow(v, 0.62f));
                float width = Mathf.Lerp(0.16f, shape, Mathf.SmoothStep(0f, 0.22f, v));
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    float dx = Mathf.Abs(u - 0.5f) * 2f / Mathf.Max(0.03f, width * 0.95f);
                    float a = Mathf.Clamp01(1f - dx);
                    float core = Mathf.Clamp01(1f - dx * 1.7f);
                    byte c = (byte)(Mathf.Lerp(0.62f, 1f, core) * 255f);
                    px[y * w + x] = new Color32(c, c, c, (byte)(a * a * 255f));
                }
            }
            t.SetPixels(px); t.Apply(false, true);
            return _blade = t;
        }

        /// <summary>单位四边形（+Z 朝向）。</summary>
        public static Mesh UnitQuad()
        {
            if (_unitQuad != null) return _unitQuad;
            var m = new Mesh();
            m.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(.5f, .5f, 0), new Vector3(-.5f, .5f, 0) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateBounds();
            return _unitQuad = m;
        }

        /// <summary>共享羽刃网格：+Z 为长度方向，底在原点，轻微上拱。</summary>
        public static Mesh BladeMesh()
        {
            if (_bladeMesh != null) return _bladeMesh;
            float[] vs = { 0f, 0.12f, 0.32f, 0.58f, 0.82f, 1f };
            float[] ws = { 0.13f, 0.30f, 0.42f, 0.34f, 0.16f, 0.015f };
            int n = vs.Length;
            var verts = new Vector3[n * 2];
            var uvs = new Vector2[n * 2];
            for (int i = 0; i < n; i++)
            {
                float y = Mathf.Sin(Mathf.PI * vs[i]) * 0.1f;
                verts[i * 2]     = new Vector3(-ws[i] * 0.5f, y, vs[i]);
                verts[i * 2 + 1] = new Vector3(+ws[i] * 0.5f, y, vs[i]);
                uvs[i * 2]     = new Vector2(0f, vs[i]);
                uvs[i * 2 + 1] = new Vector2(1f, vs[i]);
            }
            var tris = new List<int>();
            for (int i = 0; i < n - 1; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                tris.AddRange(new[] { a, c, b, b, c, d });
            }
            var m = new Mesh { vertices = verts, uv = uvs, triangles = tris.ToArray() };
            m.RecalculateBounds();
            return _bladeMesh = m;
        }

        static Texture2D NewTex(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            return t;
        }

        // ---------------- 通用自毁动画器 ----------------
        public class FXAnim : MonoBehaviour
        {
            public float life = 1f;
            public float delay = 0f;
            public System.Action<float> tick;   // t: 0..1
            public System.Action end;
            float age;

            public static FXAnim Play(GameObject go, float life, System.Action<float> tick, System.Action end = null, float delay = 0f)
            {
                var a = go.AddComponent<FXAnim>();
                a.life = Mathf.Max(0.01f, life);
                a.tick = tick;
                a.end = end;
                a.delay = delay;
                a.age = -delay;
                return a;
            }

            void Update()
            {
                age += Time.deltaTime;
                if (age < 0f) return;
                float t = age / life;
                if (t >= 1f)
                {
                    if (tick != null) tick(1f);
                    if (end != null) end();
                    Destroy(gameObject);
                }
                else if (tick != null) tick(t);
            }
        }

        static float EaseOut(float t) => 1f - Mathf.Pow(1f - t, 3f);
        static float FadeLate(float t) => 1f - Mathf.SmoothStep(0.45f, 1f, t); // 后半段淡出

        static GameObject NewFX(string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            return go;
        }

        // ================= 特效 1：放射激光束爆发（视频核心特效） =================
        /// <param name="facingDeg">扇形中心朝向（度，0=+Z），360 = 全方向</param>
        /// <param name="spreadDeg">扇形张角</param>
        public static void LaserBurst(Vector3 pos, int count = 16, float facingDeg = 360f,
            float spreadDeg = 360f, float reach = 11f)
        {
            count = Mathf.Max(3, count);
            for (int i = 0; i < count; i++)
            {
                float off = spreadDeg >= 360f
                    ? (i / (float)count) * 360f + Random.Range(-8f, 8f)
                    : (count == 1 ? 0f : Mathf.Lerp(-spreadDeg * 0.5f, spreadDeg * 0.5f, i / (float)(count - 1)));
                SpawnBeam(pos, facingDeg >= 360f ? Random.Range(0f, 360f) : facingDeg,
                    off, BurstPalette[Random.Range(0, BurstPalette.Length)],
                    reach * Random.Range(0.75f, 1.15f), life: Random.Range(0.45f, 0.7f),
                    width: Random.Range(0.06f, 0.13f), bend: Random.Range(-1.2f, 1.2f));
            }
        }

        /// <param name="offDeg">相对朝向的偏移角，随爆发展开动画收拢→张开</param>
        static void SpawnBeam(Vector3 origin, float baseDeg, float offDeg, Color hdr,
            float reach, float life, float width, float bend)
        {
            var go = NewFX("fx_beam", origin);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = 14;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.numCapVertices = 2;
            lr.alignment = LineAlignment.View;
            lr.textureMode = LineTextureMode.Stretch;
            lr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.12f);
            lr.widthMultiplier = width;
            var mat = AdditiveInstance(hdr, Strip());
            lr.material = mat;
            float baseA = hdr.a;
            float y = Random.Range(0.06f, 0.22f);

            FXAnim.Play(go, life, t =>
            {
                // 展开：偏移角从收拢到张开；长度快速伸到满
                float ang = baseDeg + offDeg * EaseOut(Mathf.Min(1f, t * 2.6f));
                go.transform.rotation = Quaternion.Euler(0f, ang, 0f);
                float len = reach * EaseOut(Mathf.Min(1f, t * 2.2f));
                var pts = new Vector3[lr.positionCount];
                for (int i = 0; i < pts.Length; i++)
                {
                    float z = len * i / (pts.Length - 1);
                    float x = bend * Mathf.Sin((len <= 0.01f ? 0f : z / len) * Mathf.PI) * 0.6f;
                    pts[i] = new Vector3(x, y, z);
                }
                lr.SetPositions(pts);
                var c = hdr; c.a = baseA * FadeLate(t);
                mat.SetColor(PropColor, c);
            });
        }

        // ================= 特效 2：彩虹羽刃扇形扫射 =================
        public static void FanBlades(Vector3 pos, float facingDeg, int count = 12,
            float spreadDeg = 85f, float reach = 8f)
        {
            count = Mathf.Max(3, count);
            for (int i = 0; i < count; i++)
            {
                float off = count == 1 ? 0f : Mathf.Lerp(-spreadDeg * 0.5f, spreadDeg * 0.5f, i / (float)(count - 1));
                float hue = (i / (float)count + Random.Range(0f, 0.06f)) % 1f;
                var col = Color.HSVToRGB(hue, 0.85f, 1f) * 1.9f;
                SpawnBlade(pos, facingDeg + off, col, reach * Random.Range(0.8f, 1.1f),
                    width: Random.Range(0.55f, 1.05f), life: Random.Range(0.6f, 0.85f));
            }
        }

        static void SpawnBlade(Vector3 origin, float angleDeg, Color hdr, float reach, float width, float life)
        {
            var go = NewFX("fx_blade", origin);
            go.transform.rotation = Quaternion.Euler(0f, angleDeg, 0f);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = BladeMesh();
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var mat = AdditiveInstance(hdr, Blade());
            mr.material = mat;
            float baseA = hdr.a;

            FXAnim.Play(go, life, t =>
            {
                float g = EaseOut(Mathf.Min(1f, t * 2.4f));
                go.transform.localScale = new Vector3(width * (0.25f + 0.75f * g), 1f, reach * g);
                var c = hdr; c.a = baseA * FadeLate(t);
                mat.SetColor(PropColor, c);
            });
        }

        // ================= 特效 3：爆炸冲击环（地面扩散） =================
        public static void ExplosionRing(Vector3 pos, float maxRadius = 3.5f, Color? color = null, float delay = 0f)
        {
            var go = NewFX("fx_ring", pos);
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); // 平铺地面，法线朝上
            go.transform.position = pos + Vector3.up * 0.03f;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = UnitQuad();
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var hdr = color ?? SoftPink;
            var mat = AdditiveInstance(hdr, Ring());
            mr.material = mat;
            float baseA = hdr.a;

            FXAnim.Play(go, 0.5f, t =>
            {
                float s = Mathf.Lerp(0.6f, maxRadius * 2f, EaseOut(t));
                go.transform.localScale = new Vector3(s, s, 1f);
                var c = hdr; c.a = baseA * (1f - t);
                mat.SetColor(PropColor, c);
            }, null, delay);
        }

        // ================= 特效 4：烟花粒子四散 =================
        public static void Fireworks(Vector3 pos, int count = 70, Color[] palette = null, float speed = 9f)
        {
            var go = NewFX("fx_sparks", pos);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.24f);
            main.gravityModifier = 0.8f;
            main.maxParticles = 400;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.22f;
            shape.radiusThickness = 1f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;
            rend.material = AdditiveInstance(Color.white, SoftDot());

            var colors = palette ?? BurstPalette;
            int per = Mathf.Max(1, count / colors.Length);
            foreach (var c in colors)
            {
                main.startColor = c;
                ps.Emit(per);
            }
            float maxLife = 0.85f + 0.2f;
            FXAnim.Play(go, maxLife, null); // 只负责到时销毁
        }

        // ================= 特效 5：伤害飘字 =================
        /// <summary>带千分位的伤害数字，上浮 + 弹出缩放 + 淡出。暴击 = 金色放大。</summary>
        public static void DamageNumber(Vector3 pos, long amount, bool crit = false)
        {
            string text = amount.ToString("N0", CultureInfo.InvariantCulture);
            if (crit) text += "!";
            var hdr = crit ? Gold : (Random.value < 0.6f ? DmgRed : IceWhite);
            float height = crit ? 0.62f : 0.42f;
            SpawnText(pos, text, hdr, height, life: crit ? 1.1f : 0.9f,
                drift: new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(1.1f, 1.7f), 0f), pop: crit ? 1.25f : 1f);
        }

        /// <summary>通用文字特效（内置字体 + URP/Particles/Unlit，无 TMP 依赖）。</summary>
        public static void SpawnText(Vector3 pos, string text, Color hdr, float height,
            float life = 0.9f, Vector3? drift = null, float pop = 1f)
        {
            var go = NewFX("fx_text", pos);
            var d = drift ?? new Vector3(0f, 1.2f, 0f);
            Mesh mesh = FXText.Build(text, 48, FontStyle.Bold);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var mat = AdditiveInstance(hdr, GetFontTexture());
            mr.material = mat;
            float baseA = hdr.a;
            var b = mesh.bounds.size;
            float scale = height / Mathf.Max(0.01f, b.y);
            go.transform.localScale = new Vector3(scale * 0.4f, scale * 0.4f, 1f);
            if (Camera.main != null) go.transform.rotation = Camera.main.transform.rotation; // 面向相机
            Vector3 basePos = pos;

            FXAnim.Play(go, life, t =>
            {
                float s = scale * pop * Mathf.SmoothStep(0.4f, 1f, Mathf.Min(1f, t * 4f));
                go.transform.localScale = new Vector3(s, s, 1f);
                go.transform.position = basePos + d * t;
                var c = hdr; c.a = baseA * FadeLate(t);
                mat.SetColor(PropColor, c);
            });
        }

        // ================= 特效 6：等级文字（玩家头顶 Lv. N） =================
        /// <summary>一次性大等级文字（也可用 FXLevelText 做常驻跟随）。</summary>
        public static void LevelUpText(Vector3 pos, long lv)
        {
            SpawnText(pos + Vector3.up * 2.3f, "Lv. " + lv.ToString("N0", CultureInfo.InvariantCulture),
                IceWhite, 0.85f, life: 1.2f, drift: new Vector3(0f, 0.5f, 0f), pop: 1.35f);
        }

        // ================= 特效 7：角色流光光环（常驻，返回持有者便于清理） =================
        public static GameObject AttachAura(GameObject target, float radius = 0.5f)
        {
            var go = new GameObject("fx_aura");
            go.transform.SetParent(target.transform, false);
            go.transform.localPosition = Vector3.zero;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local; // 跟随宿主移动
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.17f);
            main.startColor = new ParticleSystem.MinMaxGradient(Magenta, Cyan);
            main.maxParticles = 80;
            main.loop = true;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            shape.radiusThickness = 0.6f;
            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.orbitalY = 3.2f;      // 绕 Y 轴环绕
            vol.radial = 0.25f;       // 缓慢外扩
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.3f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var em = ps.emission;
            em.rateOverTime = 24f;
            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;
            rend.material = AdditiveInstance(Color.white, SoftDot());
            return go;
        }

        /// <summary>角色脚下柔光圆盘（常驻）。</summary>
        public static GameObject AttachGlowDisc(GameObject target, float size = 1.7f, Color? color = null)
        {
            var go = new GameObject("fx_glowdisc");
            go.transform.SetParent(target.transform, false);
            go.transform.localPosition = Vector3.up * 0.03f;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = UnitQuad();
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var hdr = color ?? new Color(1f, 0.35f, 0.5f, 1f) * 1.4f;
            var mat = AdditiveInstance(hdr, SoftDot());
            mr.material = mat;
            float baseA = hdr.a;
            FXAnim.Play(go, 999f, t =>
            {
                var c = hdr; c.a = baseA * (0.55f + 0.3f * Mathf.Sin(Time.time * 4f));
                mat.SetColor(PropColor, c);
            });
            return go;
        }

        // ================= 特效 8：死亡爆裂（绿火花 + 金币散落 + 小环） =================
        public static void DeathBurst(Vector3 pos, Color? color = null)
        {
            var c = color ?? EyeGreen;
            Fireworks(pos, 18, new[] { c, Gold }, speed: 5f);
            ExplosionRing(pos, 0.9f, c);
        }

        /// <summary>周围命中辅助：对区域内的回调逐个触发（演示场景用）。</summary>
        public static float RandomRange(float a, float b) => Random.Range(a, b);
        public static bool Roll(float p) => Random.value < p;

        // ---------------- 内置字体（文本特效用，零资源依赖） ----------------
        static Font _font;
        public static Font GetFont()
        {
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _font;
        }
        public static Texture2D GetFontTexture() => GetFont().material != null ? GetFont().material.mainTexture as Texture2D : null;

        static readonly int PropColor = Shader.PropertyToID("_BaseColor");
    }

    /// <summary>运行时文字网格构建器：内置字体字形 → 网格（URP/Particles/Unlit 渲染）。</summary>
    public static class FXText
    {
        public static Mesh Build(string text, int size, FontStyle style)
        {
            var font = FX.GetFont();
            if (font == null) return new Mesh();
            font.RequestCharactersInTexture(text, size, style);
            var verts = new List<Vector3>(text.Length * 4);
            var uvs = new List<Vector2>(text.Length * 4);
            var tris = new List<int>(text.Length * 6);
            float pen = 0f;
            foreach (char ch in text)
            {
                if (ch == ' ') { pen += size * 0.3f; continue; }
                if (!font.GetCharacterInfo(ch, out var ci, size, style)) { pen += size * 0.3f; continue; }
                int i0 = verts.Count;
                verts.Add(new Vector3(pen + ci.minX, ci.minY, 0f));
                verts.Add(new Vector3(pen + ci.maxX, ci.minY, 0f));
                verts.Add(new Vector3(pen + ci.maxX, ci.maxY, 0f));
                verts.Add(new Vector3(pen + ci.minX, ci.maxY, 0f));
                uvs.Add(new Vector2(ci.uvBottomLeft.x, ci.uvBottomLeft.y));
                uvs.Add(new Vector2(ci.uvBottomRight.x, ci.uvBottomRight.y));
                uvs.Add(new Vector2(ci.uvTopRight.x, ci.uvTopRight.y));
                uvs.Add(new Vector2(ci.uvTopLeft.x, ci.uvTopLeft.y));
                tris.AddRange(new[] { i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 });
                pen += ci.advance;
            }
            var m = new Mesh { vertices = verts.ToArray(), uv = uvs.ToArray(), triangles = tris.ToArray() };
            m.RecalculateBounds();
            return m;
        }
    }

    /// <summary>常驻等级文字（跟随目标，可随时更新数值，更新时弹出动画）。</summary>
    public class FXLevelText : MonoBehaviour
    {
        MeshFilter _mf;
        MeshRenderer _mr;
        Material _mat;
        Transform _target;
        Vector3 _offset = new Vector3(0f, 2.3f, 0f);
        float _height = 0.8f;
        float _popT = 1f;
        long _lv;

        public static FXLevelText Create(Transform target, Vector3 offset, float height)
        {
            var go = new GameObject("fx_leveltext");
            var c = go.AddComponent<FXLevelText>();
            c._target = target;
            c._offset = offset;
            c._height = height;
            c._mf = go.AddComponent<MeshFilter>();
            c._mr = go.AddComponent<MeshRenderer>();
            c._mr.shadowCastingMode = ShadowCastingMode.Off;
            c._mr.receiveShadows = false;
            c._mat = FX.AdditiveInstance(FX.IceWhite, FX.GetFontTexture());
            c._mr.material = c._mat;
            if (Camera.main != null) go.transform.rotation = Camera.main.transform.rotation;
            c.Set(100, instant: true);
            return c;
        }

        public void Set(long lv, bool instant = false)
        {
            _lv = lv;
            var old = _mf.sharedMesh;
            _mf.sharedMesh = FXText.Build("Lv. " + lv.ToString("N0", CultureInfo.InvariantCulture), 48, FontStyle.Bold);
            if (old != null) Destroy(old);
            _popT = instant ? 1f : 0f;
        }

        void LateUpdate()
        {
            if (_target != null) transform.position = _target.position + _offset;
            if (_popT < 1f)
            {
                _popT = Mathf.Min(1f, _popT + Time.deltaTime * 2.5f);
                float s = _height / Mathf.Max(0.01f, _mf.sharedMesh != null ? _mf.sharedMesh.bounds.size.y : 1f);
                s *= 1f + 0.3f * Mathf.Exp(-5f * _popT) * Mathf.Sin(14f * _popT);
                transform.localScale = new Vector3(s, s, 1f);
            }
        }
    }
}
