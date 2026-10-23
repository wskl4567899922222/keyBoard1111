using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SurvivorFX
{
    /// <summary>
    /// 幸存者特效演示场景导演（对应参考视频的完整画面）：
    /// 运行时自建 ground/玩家/敌人海/后处理，按视频节奏循环播放全部特效——
    /// 放射激光爆发、彩虹羽刃扇射、伤害飘字、爆炸冲击环、烟花粒子、等级文字。
    /// 点击地面可在该处触发小型爆发。
    /// 所有特效来自 SurvivorFX.FX 静态 API（可被 3D 键盘直接复用）。
    /// </summary>
    public class SurvivorFXDemo : MonoBehaviour
    {
        [Header("演出节奏")]
        public float comboIntervalMin = 0.9f;
        public float comboIntervalMax = 1.5f;
        [Header("敌人海")]
        public int enemyTarget = 24;
        public float spawnRingMin = 6f;
        public float spawnRingMax = 11f;

        Transform _player;
        FXLevelText _lvText;
        long _lv = 100;
        float _comboT, _nextCombo = 1.2f, _idleT;
        int _comboCount;

        void Start()
        {
            SetupCameraAndPost();
            BuildGround();
            BuildPlayer();
        }

        void LateUpdate()
        {
            if (_player == null || Camera.main == null) return;
            var want = _player.position + new Vector3(0f, 12.5f, -6.2f);
            var camT = Camera.main.transform;
            float k = 1f - Mathf.Exp(-5f * Time.deltaTime);
            camT.position = Vector3.Lerp(camT.position, want, k);
        }

        void SetupCameraAndPost()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                cam.allowHDR = true;
                cam.backgroundColor = new Color(0.004f, 0.012f, 0.01f, 1f);
                var extra = cam.GetComponent<UniversalAdditionalCameraData>();
                if (extra == null) extra = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                extra.renderPostProcessing = true;
            }
            if (Object.FindObjectOfType<Volume>() == null)
            {
                var volGo = new GameObject("FXGlobalVolume_Runtime");
                var vol = volGo.AddComponent<Volume>();
                vol.isGlobal = true;
                vol.profile = ScriptableObject.CreateInstance<VolumeProfile>();
                var bloom = vol.profile.Add<Bloom>(true);
                bloom.threshold.Override(1.05f);
                bloom.intensity.Override(0.9f);
                bloom.scatter.Override(0.55f);
                var tm = vol.profile.Add<Tonemapping>(true);
                tm.mode.Override(TonemappingMode.ACES);
                var vg = vol.profile.Add<Vignette>(true);
                vg.intensity.Override(0.32f);
                vg.smoothness.Override(0.4f);
            }
        }

        // ---------------- 地面 + 战场绿色光点碎屑 ----------------
        void BuildGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(7f, 1f, 7f); // 70×70
            var mr = ground.GetComponent<MeshRenderer>();
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.EnableKeyword("_EMISSION"); // 与 UrpLitPlaceholder 占位材质变体一致（WebGL 构建）
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.02f, 0.045f, 0.028f));
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", new Color(0.006f, 0.02f, 0.012f));
            mr.sharedMaterial = m;

            // 战场碎屑：绿色/金色光点合并成单网格（一次绘制、无碰撞体）
            int n = 150;
            var verts = new List<Vector3>(n * 4);
            var colors = new List<Color>(n * 4);
            var tris = new List<int>(n * 6);
            for (int i = 0; i < n; i++)
            {
                float r = Mathf.Sqrt(Random.Range(1.5f, 22f) / 22f) * 22f;
                float a = Random.Range(0f, Mathf.PI * 2f);
                Vector3 c = new Vector3(Mathf.Cos(a) * r, 0.012f, Mathf.Sin(a) * r);
                float s = Random.Range(0.05f, 0.16f);
                var col = Random.value < 0.2f
                    ? new Color(1f, 0.75f, 0.3f, Random.Range(0.25f, 0.55f))
                    : new Color(0.25f, 1f, 0.45f, Random.Range(0.2f, 0.5f));
                int i0 = verts.Count;
                verts.Add(c + new Vector3(-s, 0, -s)); verts.Add(c + new Vector3(s, 0, -s));
                verts.Add(c + new Vector3(s, 0, s));  verts.Add(c + new Vector3(-s, 0, s));
                colors.Add(col); colors.Add(col); colors.Add(col); colors.Add(col);
                tris.AddRange(new[] { i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 });
            }
            var mesh = new Mesh { vertices = verts.ToArray(), colors = colors.ToArray(), triangles = tris.ToArray() };
            mesh.RecalculateBounds();
            var go = new GameObject("GroundSpecks");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var smr = go.AddComponent<MeshRenderer>();
            smr.shadowCastingMode = ShadowCastingMode.Off;
            smr.receiveShadows = false;
            smr.material = FX.AdditiveInstance(Color.white * 1.2f);
        }

        // ---------------- 玩家（简单小人 + 光环 + 等级文字） ----------------
        void BuildPlayer()
        {
            var root = new GameObject("Player");
            root.transform.position = Vector3.zero;
            _player = root.transform;
            FXEnemy.Player = root.transform;

            var bodyMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            bodyMat.EnableKeyword("_EMISSION");
            if (bodyMat.HasProperty("_BaseColor")) bodyMat.SetColor("_BaseColor", new Color(0.07f, 0.08f, 0.1f));
            if (bodyMat.HasProperty("_EmissionColor")) bodyMat.SetColor("_EmissionColor", new Color(0.05f, 0.03f, 0.03f));
            if (bodyMat.HasProperty("_Smoothness")) bodyMat.SetFloat("_Smoothness", 0.6f);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            Object.Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.5f, 0.28f, 0.4f);
            body.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            body.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            Object.Destroy(head.GetComponent<Collider>());
            head.transform.SetParent(root.transform, false);
            head.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            head.transform.localPosition = new Vector3(0f, 1.02f, 0f);
            head.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;

            FX.AttachAura(root, 0.5f);
            FX.AttachGlowDisc(root, 1.8f);

            _lvText = FXLevelText.Create(root.transform, new Vector3(0f, 2.4f, 0f), 0.8f);
        }

        // ---------------- 演出主循环 ----------------
        void Update()
        {
            SpawnTick();
            ComboTick();
            IdleDamageTick();
            ClickTick();
        }

        void SpawnTick()
        {
            if (FXEnemy.All.Count < enemyTarget)
            {
                float r = Random.Range(spawnRingMin, spawnRingMax);
                float a = Random.Range(0f, Mathf.PI * 2f);
                Vector3 pos = _player.position + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                FXEnemy.Spawn(pos);
            }
            foreach (var e in FXEnemy.All)
                if ((e.transform.position - _player.position).sqrMagnitude > 16f * 16f)
                    e.Recycle(_player.position, spawnRingMin, spawnRingMax);
        }

        void ComboTick()
        {
            _comboT += Time.deltaTime;
            if (_comboT < _nextCombo) return;
            _comboT = 0f;
            _nextCombo = Random.Range(comboIntervalMin, comboIntervalMax);
            _comboCount++;
            var p = _player.position;
            bool mega = _comboCount % 5 == 0;

            if (mega)
            {
                FX.LaserBurst(p, 28, 360f, 360f, 13f);
                FX.LaserBurst(p, 14, Random.Range(0f, 360f), 150f, 11f);
                FX.ExplosionRing(p, 4.8f);
                FX.ExplosionRing(p, 3.4f, FX.IceWhite, 0.12f);
                FX.Fireworks(p, 130, FX.BurstPalette, 11f);
                FX.FanBlades(p, Random.Range(0f, 360f), 10, 85f, 8.5f);
                _lv += Random.Range(900, 1500);
                FX.LevelUpText(p, _lv);
                HitAround(p, 12f, 40, 1200);
            }
            else
            {
                int roll = Random.Range(0, 3);
                if (roll == 0)
                {
                    // 全方向放射爆发（视频帧 1/5/15 同款）
                    FX.LaserBurst(p, Random.Range(16, 25), 360f, 360f, Random.Range(9f, 12f));
                    FX.ExplosionRing(p, 3.2f);
                    FX.Fireworks(p, 70, FX.BurstPalette, 9f);
                    HitAround(p, 10f, 18, 900);
                }
                else if (roll == 1)
                {
                    // 定向扇形激光（视频帧 3 同款）
                    float facing = Random.Range(0f, 360f);
                    FX.LaserBurst(p, Random.Range(12, 17), facing, 130f, Random.Range(10f, 13f));
                    FX.ExplosionRing(p, 2.6f, FX.HotPink);
                    FX.Fireworks(p, 50, new[] { FX.Gold, FX.HotPink, FX.SoftPink }, 8f);
                    HitAround(p, 8f, 14, 800);
                }
                else
                {
                    // 彩虹羽刃（视频帧 13/17 同款）
                    FX.FanBlades(p, Random.Range(0f, 360f), Random.Range(10, 15), 85f, Random.Range(7f, 9.5f));
                    FX.ExplosionRing(p, 2.4f, FX.Cyan);
                    FX.Fireworks(p, 60, new[] { FX.Magenta, FX.Cyan, FX.Violet }, 8f);
                    HitAround(p, 7f, 12, 700);
                }
                _lv += Random.Range(80, 900);
            }
            if (_lvText != null) _lvText.Set(_lv);
        }

        void IdleDamageTick()
        {
            _idleT += Time.deltaTime;
            if (_idleT < 0.14f) return;
            _idleT = 0f;
            for (int i = 0; i < 2; i++)
            {
                var e = FXEnemy.PickNear(_player.position, 8f);
                if (e == null) return;
                e.TakeDamage(Random.Range(60, 650), FX.Roll(0.08f));
            }
        }

        void HitAround(Vector3 pos, float radius, int count, float power)
        {
            var hits = FXEnemy.NearSorted(pos, radius, count);
            foreach (var e in hits)
            {
                bool crit = FX.Roll(0.2f);
                long amt = (long)Random.Range(power * 0.22f, power) * (crit ? 3 : 1);
                e.TakeDamage(amt, crit);
            }
        }

        void ClickTick()
        {
            try
            {
                if (!Input.GetMouseButtonDown(0) || Camera.main == null) return;
                var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (!Physics.Raycast(ray, out var hit, 100f)) return;
                Vector3 p = hit.point;
                FX.LaserBurst(p, 12, 360f, 360f, 8f);
                FX.ExplosionRing(p, 2.4f);
                FX.Fireworks(p, 45, FX.BurstPalette, 7f);
                HitAround(p, 2.5f, 12, 700);
                _lv += Random.Range(30, 300);
                if (_lvText != null) _lvText.Set(_lv);
            }
            catch { /* 新 Input System 下不可用则忽略 */ }
        }

        // ---------------- 敌人（黑色小球 + 绿眼 + 血条） ----------------
        public class FXEnemy : MonoBehaviour
        {
            public static readonly List<FXEnemy> All = new List<FXEnemy>();
            public static Transform Player;

            static Material _bodyMat, _eyeMat, _barMat;
            static Quaternion _camRot = Quaternion.Euler(62f, 0f, 0f);

            long _hp = 1500;
            float _speed, _baseScale, _barW = 0.62f, _bumpT = 1f;
            GameObject _barRoot, _barQuad;
            Transform _barQuadT;

            public static void Spawn(Vector3 pos)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Enemy";
                Object.Destroy(go.GetComponent<Collider>()); // 避免干扰点击射线
                go.transform.position = pos;

                var e = go.AddComponent<FXEnemy>();
                e._baseScale = Random.Range(0.35f, 0.62f);
                go.transform.localScale = Vector3.one * e._baseScale;
                e._speed = Random.Range(0.8f, 1.8f);
                e._hp = Random.Range(900, 2200);

                if (_bodyMat == null)
                {
                    _bodyMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    _bodyMat.EnableKeyword("_EMISSION");
                    if (_bodyMat.HasProperty("_BaseColor")) _bodyMat.SetColor("_BaseColor", new Color(0.03f, 0.05f, 0.04f));
                    if (_bodyMat.HasProperty("_EmissionColor")) _bodyMat.SetColor("_EmissionColor", new Color(0.02f, 0.05f, 0.03f));
                    _eyeMat = FX.AdditiveInstance(FX.EyeGreen, FX.SoftDot());
                    _barMat = FX.AdditiveInstance(new Color(1f, 0.16f, 0.12f, 0.95f) * 1.6f);
                }
                go.GetComponent<MeshRenderer>().sharedMaterial = _bodyMat;

                // 双绿眼（加法光点，面向相机）
                var rot = _camRot;
                for (int i = 0; i < 2; i++)
                {
                    var eye = new GameObject("eye" + i);
                    eye.transform.SetParent(go.transform, false);
                    eye.transform.localPosition = new Vector3(i == 0 ? -0.26f : 0.26f, 0.3f, 0.5f);
                    eye.transform.localRotation = rot;
                    eye.AddComponent<MeshFilter>().sharedMesh = FX.UnitQuad();
                    var er = eye.AddComponent<MeshRenderer>();
                    er.shadowCastingMode = ShadowCastingMode.Off;
                    er.receiveShadows = false;
                    er.sharedMaterial = _eyeMat;
                    eye.transform.localScale = new Vector3(0.16f, 0.16f, 1f);
                }

                // 世界空间血条（红色，左端锚定）
                e._barRoot = new GameObject("hpbar");
                var quad = new GameObject("fill");
                quad.transform.SetParent(e._barRoot.transform, false);
                quad.AddComponent<MeshFilter>().sharedMesh = FX.UnitQuad();
                var qr = quad.AddComponent<MeshRenderer>();
                qr.shadowCastingMode = ShadowCastingMode.Off;
                qr.receiveShadows = false;
                qr.sharedMaterial = _barMat;
                e._barQuadT = quad.transform;

                All.Add(e);
            }

            public static FXEnemy PickNear(Vector3 pos, float radius)
            {
                FXEnemy best = null; float bd = radius * radius;
                foreach (var e in All)
                {
                    float d = (e.transform.position - pos).sqrMagnitude;
                    if (d < bd) { bd = d; best = e; }
                }
                return best;
            }

            public static List<FXEnemy> NearSorted(Vector3 pos, float radius, int count)
            {
                var list = new List<FXEnemy>();
                float r2 = radius * radius;
                foreach (var e in All)
                    if ((e.transform.position - pos).sqrMagnitude <= r2) list.Add(e);
                list.Sort((a, b) =>
                    (a.transform.position - pos).sqrMagnitude.CompareTo((b.transform.position - pos).sqrMagnitude));
                if (count >= 0 && list.Count > count) list.RemoveRange(count, list.Count - count);
                return list;
            }

            public void TakeDamage(long amt, bool crit)
            {
                if (_hp <= 0) return;
                _hp -= amt;
                _bumpT = 0f;
                var p = transform.position;
                FX.DamageNumber(p + new Vector3(Random.Range(-0.4f, 0.4f), 0.9f * _baseScale + 0.5f, Random.Range(-0.2f, 0.2f)), amt, crit);
                if (_hp <= 0) Die();
            }

            void Die()
            {
                FX.DeathBurst(transform.position, FX.EyeGreen);
                if (_barRoot != null) Destroy(_barRoot);
                All.Remove(this);
                Destroy(gameObject);
            }

            public void Recycle(Vector3 around, float rMin, float rMax)
            {
                float r = Random.Range(rMin, rMax);
                float a = Random.Range(0f, Mathf.PI * 2f);
                transform.position = around + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            }

            void Start()
            {
                if (Player == null) { var pl = GameObject.Find("Player"); if (pl != null) Player = pl.transform; }
            }

            void Update()
            {
                if (Player == null) return;
                var to = Player.position - transform.position;
                to.y = 0f;
                float len = to.magnitude;
                if (len > 1.0f)
                {
                    var dir = to / Mathf.Max(0.001f, len);
                    transform.position += dir * (_speed * Time.deltaTime);
                }
                // 受击闪缩放
                _bumpT = Mathf.Min(1f, _bumpT + Time.deltaTime * 5f);
                float bump = 1f + 0.35f * (1f - _bumpT);
                transform.localScale = Vector3.one * (_baseScale * bump);
                // 血条跟随
                if (_barRoot != null)
                {
                    float hp01 = Mathf.Clamp01((float)_hp / 2200f);
                    _barRoot.transform.position = transform.position
                        + Vector3.up * (0.95f * _baseScale + 0.35f)
                        - CameraRotRight() * (_barW * 0.5f);
                    _barRoot.transform.rotation = _camRot;
                    _barQuadT.localScale = new Vector3(_barW * hp01, 0.05f, 1f);
                    _barQuadT.localPosition = new Vector3(_barW * hp01 * 0.5f, 0f, 0f);
                }
            }

            static Vector3 CameraRotRight()
            {
                var fwd = _camRot * Vector3.forward; fwd.y = 0f;
                return new Vector3(fwd.z, 0f, -fwd.x).normalized; // 相机右方向在地面上的投影
            }
        }
    }
}
