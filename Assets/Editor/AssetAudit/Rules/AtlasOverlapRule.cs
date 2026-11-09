using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AssetAudit
{
    /// <summary>
    /// 同一张源图被多个图集打包。这是包体重复最常见的来源，也最容易被忽略，
    /// 因为视觉上完全看不出问题——只有在打 AssetBundle 时才翻倍。
    /// </summary>
    public class AtlasOverlapRule : IAuditRule
    {
        public string Id => "atlas-overlap";
        public string DisplayName => "图集重复引用";

        public IEnumerable<AuditIssue> Check(AuditContext ctx)
        {
            if (!ctx.Config.ReportAtlasOverlap) yield break;

            foreach (var entry in ctx.AtlasRefs.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                if (entry.Value.Count < 2) continue;

                var atlasNames = entry.Value
                    .Select(p => Path.GetFileNameWithoutExtension(p))
                    .OrderBy(n => n, StringComparer.Ordinal);

                yield return new AuditIssue
                {
                    RuleId = Id,
                    Severity = Severity.Warning,
                    AssetPath = entry.Key,
                    Message = $"被 {entry.Value.Count} 个图集引用：{string.Join(" / ", atlasNames)}",
                    FixHint = "同一源图进入多个图集会让包体重复；确认是否应该只保留一个图集，" +
                               "若两个图集分属不同 bundle，包体会实打实翻倍"
                };
            }
        }
    }
}
