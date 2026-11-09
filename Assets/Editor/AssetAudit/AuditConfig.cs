using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace AssetAudit
{
    [CreateAssetMenu(menuName = "Asset Audit/Config", fileName = "AuditConfig")]
    public class AuditConfig : ScriptableObject
    {
        public const string DefaultAssetPath = "Assets/Editor/AssetAudit/AuditConfig.asset";

        [Header("扫描范围")]
        public string[] SearchPaths = { "Assets" };

        // 用正则排除第三方、临时目录。误报是这类工具最大的死因，白名单必须先行。
        public string[] IgnorePathPatterns =
        {
            @"^Assets/.*/ThirdParty",
            @"^Assets/.*/_Temp"
        };

        [Header("尺寸")]
        public int MaxSourceSize = 2048;
        public bool RequireMultipleOf4 = true;
        public bool WarnMipmapOnNonPot = false;
        public int ClusterMinGroupSize = 4;
        public int ClusterMinVariants = 3;

        [Header("平台设置")]
        public bool RequirePlatformOverride = false;

        [Header("图集")]
        public bool ReportAtlasOverlap = true;

        public bool IsIgnored(string path)
        {
            foreach (var pattern in IgnorePathPatterns)
            {
                if (!string.IsNullOrEmpty(pattern) &&
                    Regex.IsMatch(path, pattern, RegexOptions.IgnoreCase))
                    return true;
            }
            return false;
        }

        public static AuditConfig LoadOrCreate()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<AuditConfig>(DefaultAssetPath);
            return cfg != null ? cfg : CreateInstance<AuditConfig>();
        }
    }
}
