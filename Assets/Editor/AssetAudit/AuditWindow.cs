using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AssetAudit
{
    public class AuditWindow : EditorWindow
    {
        const int DisplayCap = 500;

        AuditConfig _config;
        readonly AuditRunner _runner = new AuditRunner();
        Vector2 _scroll;
        Severity _minSeverity = Severity.Info;
        string _search = "";

        [MenuItem("Tools/资产审计检查器")]
        static void Open() => GetWindow<AuditWindow>("资产审计");

        void OnEnable()
        {
            if (_config == null) _config = AuditConfig.LoadOrCreate();
            _runner.Finished += Repaint;
        }

        void OnDisable()
        {
            _runner.Finished -= Repaint;
            _runner.Cancel();
        }

        void OnGUI()
        {
            DrawToolbar();
            DrawProgress();
            DrawFilters();

            if (_config == null)
            {
                EditorGUILayout.HelpBox("请先在工具栏指定 AuditConfig，或创建默认配置。", MessageType.Info);
                return;
            }

            DrawResults();
        }

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _config = (AuditConfig)EditorGUILayout.ObjectField(
                    _config, typeof(AuditConfig), false, GUILayout.Width(220));

                using (new EditorGUI.DisabledScope(_runner.IsRunning || _config == null))
                {
                    if (GUILayout.Button("运行", EditorStyles.toolbarButton, GUILayout.Width(56)))
                        _runner.Start(_config);
                }

                using (new EditorGUI.DisabledScope(!_runner.IsRunning))
                {
                    if (GUILayout.Button("取消", EditorStyles.toolbarButton, GUILayout.Width(56)))
                        _runner.Cancel();
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("导出报告", EditorStyles.toolbarButton, GUILayout.Width(76)))
                    Export();
            }
        }

        void DrawProgress()
        {
            EditorGUILayout.Space();

            var rect = GUILayoutUtility.GetRect(1f, 18f, GUILayout.ExpandWidth(true));
            EditorGUI.ProgressBar(rect, _runner.Progress, _runner.Status);

            var counts = CountBySeverity();
            EditorGUILayout.LabelField(
                $"Error {counts.error}    Warning {counts.warning}    Info {counts.info}",
                EditorStyles.miniLabel);

            EditorGUILayout.Space();
        }

        void DrawFilters()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("最低级别", GUILayout.Width(64));
                _minSeverity = (Severity)EditorGUILayout.EnumPopup(_minSeverity, GUILayout.Width(90));
                GUILayout.Space(8);
                _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);
            }
        }

        void DrawResults()
        {
            var filtered = Filtered().ToList();
            EditorGUILayout.LabelField($"筛选后 {filtered.Count} 条", EditorStyles.miniLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var issue in filtered.Take(DisplayCap))
                DrawRow(issue);

            if (filtered.Count > DisplayCap)
            {
                EditorGUILayout.HelpBox(
                    $"仅显示前 {DisplayCap} 条，共 {filtered.Count} 条。请收窄筛选或导出报告查看全量。",
                    MessageType.Info);
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawRow(AuditIssue issue)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            using (new EditorGUILayout.HorizontalScope())
            {
                var tagWidth = 70f;
                EditorGUILayout.LabelField(issue.Severity.ToString(), EditorStyles.boldLabel,
                    GUILayout.Width(tagWidth));

                if (GUILayout.Button(issue.AssetPath, EditorStyles.linkLabel))
                    Ping(issue.AssetPath);
            }

            EditorGUILayout.LabelField(issue.Message, EditorStyles.wordWrappedMiniLabel);
            if (!string.IsNullOrEmpty(issue.FixHint))
                EditorGUILayout.LabelField("建议：" + issue.FixHint, EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.EndVertical();
        }

        IEnumerable<AuditIssue> Filtered()
        {
            var keyword = (_search ?? string.Empty).Trim();
            foreach (var issue in _runner.Issues)
            {
                if (issue.Severity < _minSeverity) continue;
                if (keyword.Length > 0 &&
                    !issue.AssetPath.Contains(keyword) &&
                    !issue.Message.Contains(keyword))
                    continue;
                yield return issue;
            }
        }

        (int error, int warning, int info) CountBySeverity()
        {
            var error = 0;
            var warning = 0;
            var info = 0;
            foreach (var issue in _runner.Issues)
            {
                switch (issue.Severity)
                {
                    case Severity.Error: error++; break;
                    case Severity.Warning: warning++; break;
                    default: info++; break;
                }
            }
            return (error, warning, info);
        }

        void Export()
        {
            if (_runner.Issues.Count == 0)
            {
                EditorUtility.DisplayDialog("资产审计", "没有可导出的结果，请先运行扫描。", "好");
                return;
            }

            var payload = ReportWriter.Build(_runner.Issues);
            ReportWriter.Write(payload, out var jsonPath, out _);
            Debug.Log("[AssetAudit] 报告已导出：" + jsonPath);
            EditorUtility.RevealInFinder(jsonPath);
        }

        static void Ping(string assetPath)
        {
            var obj = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (obj == null) return;
            EditorGUIUtility.PingObject(obj);
            Selection.activeObject = obj;
        }
    }
}
