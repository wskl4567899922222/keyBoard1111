using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace AssetAudit
{
    public static class ReportWriter
    {
        public static ReportPayload Build(IEnumerable<AuditIssue> issues)
        {
            var list = issues.ToList();
            return new ReportPayload
            {
                Issues = list,
                ErrorCount = list.Count(i => i.Severity == Severity.Error),
                WarningCount = list.Count(i => i.Severity == Severity.Warning),
                InfoCount = list.Count(i => i.Severity == Severity.Info)
            };
        }

        public static void Write(ReportPayload payload, out string jsonPath, out string mdPath)
        {
            Directory.CreateDirectory(AuditPaths.ReportDir);
            jsonPath = Path.Combine(AuditPaths.ReportDir, "report.json");
            mdPath = Path.Combine(AuditPaths.ReportDir, "report.md");
            File.WriteAllText(jsonPath, JsonUtility.ToJson(payload, true), Encoding.UTF8);
            File.WriteAllText(mdPath, ToMarkdown(payload), Encoding.UTF8);
        }

        public static string ToMarkdown(ReportPayload payload, int rowCap = 200)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# 资产审计报告");
            sb.AppendLine();
            sb.AppendLine($"生成时间：{DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine();
            sb.AppendLine($"- Error：{payload.ErrorCount}");
            sb.AppendLine($"- Warning：{payload.WarningCount}");
            sb.AppendLine($"- Info：{payload.InfoCount}");
            sb.AppendLine();
            sb.AppendLine("| 级别 | 资产 | 规则 | 问题 | 建议 |");
            sb.AppendLine("|---|---|---|---|---|");

            foreach (var issue in payload.Issues.Take(rowCap))
            {
                sb.AppendLine(
                    $"| {issue.Severity} | `{issue.AssetPath}` | {issue.RuleId} | {issue.Message} | {issue.FixHint} |");
            }

            if (payload.Issues.Count > rowCap)
                sb.AppendLine($"\n> 仅列出前 {rowCap} 条，共 {payload.Issues.Count} 条，完整数据见 report.json。");

            return sb.ToString();
        }
    }
}
