using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace AssetAudit
{
    public class AuditContext
    {
        public string[] TexturePaths;
        public Dictionary<string, TextureFact> Facts;
        public Dictionary<string, List<string>> AtlasRefs;
        public AuditConfig Config;
    }

    /// <summary>
    /// 扫描调度器。分帧执行，每帧处理固定数量的资产，避免主线程长时间卡死。
    /// </summary>
    public class AuditRunner
    {
        const int FactBatchSize = 300;

        static readonly IAuditRule[] Rules =
        {
            new TextureSizeRule(),
            new AtlasOverlapRule()
        };

        enum Phase { Collect, Facts, Atlas, RunRules }

        public readonly List<AuditIssue> Issues = new List<AuditIssue>();
        public bool IsRunning { get; private set; }
        public float Progress { get; private set; }
        public string Status { get; private set; } = "空闲";
        public event Action Finished;

        AuditConfig _config;
        string[] _paths;
        int _cursor;
        Phase _phase;
        bool _cacheDirty;

        readonly Dictionary<string, TextureFact> _facts = new Dictionary<string, TextureFact>();
        readonly Dictionary<string, TextureFact> _cache = new Dictionary<string, TextureFact>();
        Dictionary<string, List<string>> _atlasRefs = new Dictionary<string, List<string>>();

        public void Start(AuditConfig config)
        {
            if (IsRunning || config == null) return;
            Reset();
            _config = config;
            LoadCache();
            IsRunning = true;
            EditorApplication.update += Tick;
            Status = "准备中…";
            RepaintSoon();
        }

        public void Cancel()
        {
            if (!IsRunning) return;
            EditorApplication.update -= Tick;
            IsRunning = false;
            Status = "已取消";
        }

        void Reset()
        {
            Issues.Clear();
            _facts.Clear();
            _cache.Clear();
            _atlasRefs = new Dictionary<string, List<string>>();
            _paths = null;
            _cursor = 0;
            _phase = Phase.Collect;
            Progress = 0f;
            _cacheDirty = false;
        }

        void Tick()
        {
            if (!IsRunning) return;
            switch (_phase)
            {
                case Phase.Collect: Collect(); break;
                case Phase.Facts: StepFacts(); break;
                case Phase.Atlas: BuildAtlasRefs(); _phase = Phase.RunRules; break;
                case Phase.RunRules: RunRules(); Finish(); break;
            }
        }

        void Collect()
        {
            Status = "收集贴图资产…";
            var guids = AssetDatabase.FindAssets("t:Texture2D", _config.SearchPaths);
            var list = new List<string>(guids.Length);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!_config.IsIgnored(path)) list.Add(path);
            }
            list.Sort(StringComparer.Ordinal);
            _paths = list.ToArray();
            _phase = Phase.Facts;
        }

        void StepFacts()
        {
            var end = Math.Min(_cursor + FactBatchSize, _paths.Length);
            for (var i = _cursor; i < end; i++)
            {
                var path = _paths[i];
                TextureFactBuilder.Stamp(path, out var mtime, out var metaMtime, out var size);

                if (_cache.TryGetValue(path, out var cached) &&
                    cached.MtimeTicks == mtime &&
                    cached.MetaMtimeTicks == metaMtime &&
                    cached.FileSize == size)
                {
                    _facts[path] = cached;
                    continue;
                }

                var fact = TextureFactBuilder.Build(path);
                _facts[path] = fact;
                _cache[path] = fact;
                _cacheDirty = true;
            }

            _cursor = end;
            var total = Math.Max(1, _paths.Length);
            Progress = (float)_cursor / total * 0.85f;
            Status = $"解析贴图 {_cursor}/{_paths.Length}";

            if (_cursor >= _paths.Length) _phase = Phase.Atlas;
        }

        void BuildAtlasRefs()
        {
            Status = "汇总图集引用…";
            _atlasRefs = new Dictionary<string, List<string>>();

            var guids = AssetDatabase.FindAssets("t:SpriteAtlas", _config.SearchPaths);
            foreach (var guid in guids)
            {
                var atlasPath = AssetDatabase.GUIDToAssetPath(guid);
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
                if (atlas == null) continue;

                foreach (var packable in SpriteAtlasExtensions.GetPackables(atlas))
                {
                    var path = AssetDatabase.GetAssetPath(packable);
                    if (string.IsNullOrEmpty(path)) continue;

                    // folder packable 需要递归展开，否则会漏掉整目录打包的情况
                    if (AssetDatabase.IsValidFolder(path)) CollectFolder(path, atlasPath);
                    else AddRef(path, atlasPath);
                }
            }

            Progress = 0.9f;
        }

        void CollectFolder(string folder, string atlasPath)
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            foreach (var guid in guids)
                AddRef(AssetDatabase.GUIDToAssetPath(guid), atlasPath);
        }

        void AddRef(string texturePath, string atlasPath)
        {
            if (!_atlasRefs.TryGetValue(texturePath, out var list))
                _atlasRefs[texturePath] = list = new List<string>();
            if (!list.Contains(atlasPath)) list.Add(atlasPath);
        }

        void RunRules()
        {
            Status = "执行规则…";
            var ctx = new AuditContext
            {
                TexturePaths = _paths,
                Facts = _facts,
                AtlasRefs = _atlasRefs,
                Config = _config
            };

            foreach (var rule in Rules)
                Issues.AddRange(rule.Check(ctx));

            Issues.Sort((a, b) =>
            {
                var bySeverity = b.Severity.CompareTo(a.Severity);
                return bySeverity != 0 ? bySeverity : string.CompareOrdinal(a.AssetPath, b.AssetPath);
            });

            Progress = 1f;
        }

        void Finish()
        {
            EditorApplication.update -= Tick;
            SaveCache();
            IsRunning = false;
            Status = $"完成：{Issues.Count} 条问题";
            Finished?.Invoke();
            RepaintSoon();
        }

        static void RepaintSoon() => EditorApplication.delayCall += () =>
        {
            foreach (var w in Resources.FindObjectsOfTypeAll<AuditWindow>()) w.Repaint();
        };

        void LoadCache()
        {
            try
            {
                if (!File.Exists(AuditPaths.CachePath)) return;
                var cache = JsonUtility.FromJson<FactCache>(File.ReadAllText(AuditPaths.CachePath));
                if (cache?.Entries == null) return;
                foreach (var entry in cache.Entries)
                {
                    if (!string.IsNullOrEmpty(entry.Path)) _cache[entry.Path] = entry;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AssetAudit] 缓存读取失败，将全量重扫：" + e.Message);
            }
        }

        void SaveCache()
        {
            if (!_cacheDirty) return;
            try
            {
                var dir = Path.GetDirectoryName(AuditPaths.CachePath);
                if (dir != null) Directory.CreateDirectory(dir);
                var cache = new FactCache { Entries = _cache.Values.ToList() };
                File.WriteAllText(AuditPaths.CachePath, JsonUtility.ToJson(cache));
                _cacheDirty = false;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AssetAudit] 缓存写入失败：" + e.Message);
            }
        }

        /// <summary>
        /// 同步全量扫描，供 CI 使用（不经 EditorApplication.update）。
        /// 执行方式：Unity -executeMethod AssetAudit.AuditRunner.RunBatch
        /// Error 数量大于 0 时以退出码 1 结束，可用于流水线卡口。
        /// </summary>
        public static void RunBatch()
        {
            var config = AuditConfig.LoadOrCreate();
            var runner = new AuditRunner();
            runner.RunSync(config);

            var payload = ReportWriter.Build(runner.Issues);
            ReportWriter.Write(payload, out var jsonPath, out var mdPath);

            Debug.Log($"[AssetAudit] Error {payload.ErrorCount} / Warning {payload.WarningCount} / " +
                      $"Info {payload.InfoCount}，报告：{jsonPath}");

            if (Application.isBatchMode)
                EditorApplication.Exit(payload.ErrorCount > 0 ? 1 : 0);
            else
                EditorUtility.RevealInFinder(mdPath);
        }

        public void RunSync(AuditConfig config)
        {
            Reset();
            _config = config;
            LoadCache();
            Collect();
            while (_phase == Phase.Facts) StepFacts();
            BuildAtlasRefs();
            RunRules();
            SaveCache();
            Status = $"完成：{Issues.Count} 条问题";
            Progress = 1f;
        }
    }
}
