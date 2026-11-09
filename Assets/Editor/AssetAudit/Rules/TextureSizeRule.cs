using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AssetAudit
{
    /// <summary>
    /// 贴图尺寸规范。核心不是判 2 的幂，而是判"同簇尺寸离散"——
    /// 后者才真正表达"混乱"，而且几乎不会误报。
    /// </summary>
    public class TextureSizeRule : IAuditRule
    {
        public string Id => "texture-size";
        public string DisplayName => "贴图尺寸规范";

        // 去掉末尾的序号、copy、@2x、(1) 等导出后缀，用于归簇
        static readonly Regex SuffixPattern = new Regex(
            @"([_\-\s]?(copy|@2x|@3x|\(\d+\)|\d+))+$", RegexOptions.IgnoreCase);

        public IEnumerable<AuditIssue> Check(AuditContext ctx)
        {
            var cfg = ctx.Config;
            var issues = new List<AuditIssue>();
            var ordered = ctx.Facts.Values.OrderBy(f => f.Path, StringComparer.Ordinal);

            foreach (var fact in ordered)
            {
                if (!fact.HasSourceSize) continue;

                // 1. 源图大于导入 Max Size：会被静默降采样，同时白白占仓库体积
                if (fact.SrcWidth > fact.MaxSize || fact.SrcHeight > fact.MaxSize)
                {
                    issues.Add(new AuditIssue
                    {
                        RuleId = Id,
                        Severity = Severity.Warning,
                        AssetPath = fact.Path,
                        Message = $"源图 {fact.SrcWidth}×{fact.SrcHeight} 大于导入 Max Size {fact.MaxSize}，导入时会被降采样",
                        FixHint = $"把 Max Size 提到 {NextPowerOfTwo(Mathf.Max(fact.SrcWidth, fact.SrcHeight))}，或把源图缩到 {fact.MaxSize} 以内"
                    });
                }
                // 2. 全局上限：整张图导入尺寸过大，多见于直接丢 4K 原图的场景
                else if (cfg.MaxSourceSize > 0 &&
                         (fact.SrcWidth > cfg.MaxSourceSize || fact.SrcHeight > cfg.MaxSourceSize))
                {
                    issues.Add(new AuditIssue
                    {
                        RuleId = Id,
                        Severity = Severity.Info,
                        AssetPath = fact.Path,
                        Message = $"导入尺寸 {fact.SrcWidth}×{fact.SrcHeight} 超过全局上限 {cfg.MaxSourceSize}",
                        FixHint = "确认是否真的需要这个分辨率；多数 UI 与场景贴图 1024 已足够"
                    });
                }

                // 3. 尺寸非 4 的倍数：BC / ETC 都是 4×4 块，非对齐会补齐并浪费显存
                if (cfg.RequireMultipleOf4 &&
                    (fact.SrcWidth > 64 || fact.SrcHeight > 64) &&
                    (fact.SrcWidth % 4 != 0 || fact.SrcHeight % 4 != 0))
                {
                    issues.Add(new AuditIssue
                    {
                        RuleId = Id,
                        Severity = Severity.Info,
                        AssetPath = fact.Path,
                        Message = $"尺寸 {fact.SrcWidth}×{fact.SrcHeight} 不是 4 的倍数，压缩块会补齐",
                        FixHint = "改为 4 的倍数，或确认该图使用未压缩格式"
                    });
                }

                // 4. 非 2 的幂 + 开启 mipmap：mip 链不整齐，远处容易出现闪烁
                if (cfg.WarnMipmapOnNonPot && fact.Mipmap && !IsPowerOfTwo(fact.SrcWidth))
                {
                    issues.Add(new AuditIssue
                    {
                        RuleId = Id,
                        Severity = Severity.Info,
                        AssetPath = fact.Path,
                        Message = $"非 2 的幂尺寸 {fact.SrcWidth}×{fact.SrcHeight} 开启了 mipmap",
                        FixHint = "改用 2 的幂尺寸，或关闭 mipmap"
                    });
                }

                // 5. 平台 override 缺失（默认关闭，项目依赖全局设置时开启会产生大量噪声）
                if (cfg.RequirePlatformOverride && !fact.AndroidOverridden)
                {
                    issues.Add(new AuditIssue
                    {
                        RuleId = Id,
                        Severity = Severity.Info,
                        AssetPath = fact.Path,
                        Message = "Android 平台未设置 override，将完全沿用全局默认",
                        FixHint = "确认全局设置已启用 ASTC；否则该图可能被压成 RGB 或未压缩"
                    });
                }
            }

            issues.AddRange(CheckClusters(ctx, cfg));
            return issues;
        }

        static IEnumerable<AuditIssue> CheckClusters(AuditContext ctx, AuditConfig cfg)
        {
            var clusters = new Dictionary<string, List<TextureFact>>();

            foreach (var fact in ctx.Facts.Values)
            {
                if (!fact.HasSourceSize) continue;
                var key = ClusterKey(fact.Path);
                if (!clusters.TryGetValue(key, out var list))
                    clusters[key] = list = new List<TextureFact>();
                list.Add(fact);
            }

            foreach (var cluster in clusters.OrderBy(c => c.Key, StringComparer.Ordinal))
            {
                var list = cluster.Value;
                if (list.Count < cfg.ClusterMinGroupSize) continue;

                var bySize = list
                    .GroupBy(f => (Width: f.SrcWidth, Height: f.SrcHeight))
                    .OrderByDescending(g => g.Count())
                    .ToList();

                if (bySize.Count < cfg.ClusterMinVariants) continue;

                var distribution = string.Join(" / ",
                    bySize.Select(g => $"{g.Key.Width}×{g.Key.Height} ×{g.Count()}"));

                var dominant = bySize[0];
                var outlierLayer = bySize[bySize.Count - 1];
                var outlier = outlierLayer.First();

                yield return new AuditIssue
                {
                    RuleId = "texture-size-cluster",
                    Severity = Severity.Warning,
                    AssetPath = outlier.Path,
                    Message = $"同簇尺寸不一致：{distribution}",
                    FixHint = $"主流尺寸是 {dominant.Key.Width}×{dominant.Key.Height}；" +
                              $"把这批偏离的图统一，或把它们移出该目录以免被误归簇"
                };
            }
        }

        static string ClusterKey(string assetPath)
        {
            var dir = Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ?? string.Empty;
            var ext = Path.GetExtension(assetPath).ToLowerInvariant();
            var name = SuffixPattern.Replace(Path.GetFileNameWithoutExtension(assetPath) ?? string.Empty, "");
            return $"{dir}|{name}|{ext}";
        }

        static bool IsPowerOfTwo(int value) => value > 0 && (value & (value - 1)) == 0;

        static int NextPowerOfTwo(int value)
        {
            var result = 1;
            while (result < value) result <<= 1;
            return result;
        }
    }
}
