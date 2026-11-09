using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AssetAudit
{
    public enum Severity { Info = 0, Warning = 1, Error = 2 }

    [System.Serializable]
    public class AuditIssue
    {
        public string RuleId;
        public Severity Severity;
        public string AssetPath;
        public string Message;
        public string FixHint;
    }

    public interface IAuditRule
    {
        string Id { get; }
        string DisplayName { get; }
        IEnumerable<AuditIssue> Check(AuditContext ctx);
    }

    [System.Serializable]
    public class ReportPayload
    {
        public List<AuditIssue> Issues = new List<AuditIssue>();
        public int ErrorCount;
        public int WarningCount;
        public int InfoCount;
    }

    public static class AuditPaths
    {
        public static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
        public static string CachePath => Path.Combine(ProjectRoot, "Library", "AssetAuditCache.json");
        public static string ReportDir => Path.Combine(ProjectRoot, "Library", "AssetAudit");
    }
}
