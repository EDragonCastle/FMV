using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>메뉴 Tools > Video > Excel Importer</summary>
public class VideoImporterWindow : EditorWindow
{
    const string PrefPath = "VideoTool.XlsxPath";
    const int MaxIssuesShown = 200;

    string _path;
    ParseResult _last;
    string _summary;
    MessageType _summaryType = MessageType.Info;
    Vector2 _scroll;

    [MenuItem("Tools/Video/Excel Importer")]
    static void Open()
    {
        GetWindow<VideoImporterWindow>("Video Importer");
    }

    void OnEnable()
    {
        _path = EditorPrefs.GetString(PrefPath, "");
    }

    void OnGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("VideoSetting Excel (.xlsx)", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            _path = EditorGUILayout.TextField(_path);
            if (GUILayout.Button("찾기", GUILayout.Width(50)))
            {
                string dir = !string.IsNullOrEmpty(_path) && File.Exists(_path) ? Path.GetDirectoryName(_path) : "";
                string picked = EditorUtility.OpenFilePanel("VideoSetting Excel 선택", dir, "xlsx");
                if (!string.IsNullOrEmpty(picked))
                {
                    _path = picked;
                    GUI.FocusControl(null);
                }
            }
        }

        EditorGUILayout.HelpBox(
            "Excel이 원본입니다. 생성된 에셋(" + VideoAssetWriter.RootDir + ")은 직접 수정하지 마세요. 다음 임포트 때 덮어써집니다.\n" +
            "Excel로 관리하지 않는 예외적인 영상은 이 폴더 밖에 따로 VideoSetting 에셋을 만들어 관리하세요.",
            MessageType.None);

        bool canImport = !string.IsNullOrEmpty(_path) && File.Exists(_path);
        using (new EditorGUI.DisabledScope(!canImport))
        {
            if (GUILayout.Button("임포트", GUILayout.Height(30))) RunImport();
        }

        if (_summary != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(_summary, _summaryType);
        }

        if (_last != null && _last.Issues.Count > 0)
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            int shown = 0;
            foreach (var issue in _last.Issues)
            {
                if (shown++ >= MaxIssuesShown) break;
                EditorGUILayout.HelpBox(issue.ToString(), ToMessageType(issue.Severity));
            }
            if (_last.Issues.Count > MaxIssuesShown)
                EditorGUILayout.LabelField("… 외 " + (_last.Issues.Count - MaxIssuesShown) + "건 (에러부터 먼저 고치고 다시 임포트하세요)");
            EditorGUILayout.EndScrollView();
        }
    }

    void RunImport()
    {
        EditorPrefs.SetString(PrefPath, _path);

        _last = VideoImportParser.ParseFile(_path);
        int errors = _last.Count(IssueSeverity.Error);
        int warnings = _last.Count(IssueSeverity.Warning);

        if (errors > 0)
        {
            _summary = "에러 " + errors + "건, 경고 " + warnings + "건. 에셋은 생성/수정하지 않았습니다. Excel을 고치고 다시 임포트하세요.";
            _summaryType = MessageType.Error;
            Debug.LogError("[VideoTool] 임포트 실패: 에러 " + errors + "건");
            return;
        }

        var report = VideoAssetWriter.Write(_last);
        _summary = "완료: 영상 " + _last.Videos.Count + "개 (새로 " + report.Created + " / 갱신 " + report.Updated + "), 경고 " + warnings + "건.";
        if (report.Orphaned.Count > 0)
            _summary += "\nExcel에 없는 영상 에셋이 남아 있습니다 (삭제하지 않음): " + string.Join(", ", report.Orphaned.ToArray());
        _summaryType = warnings > 0 || report.Orphaned.Count > 0 ? MessageType.Warning : MessageType.Info;

        Debug.Log("[VideoTool] " + _summary);
    }

    static MessageType ToMessageType(IssueSeverity s)
    {
        switch (s)
        {
            case IssueSeverity.Error: return MessageType.Error;
            case IssueSeverity.Warning: return MessageType.Warning;
            default: return MessageType.Info;
        }
    }
}