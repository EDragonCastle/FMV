using UnityEditor;

/// <summary>자동 생성 에셋을 인스펙터에서 읽기 전용으로 보여준다. (실수로 손대는 것 방지)</summary>
[CustomEditor(typeof(VideoSetting))]
public class VideoSettingInspector : Editor
{
    public override void OnInspectorGUI()
    {
        var t = (VideoSetting)target;
        bool generated = UnityEditor.AssetDatabase.GetAssetPath(t).StartsWith(VideoAssetWriter.RootDir);
        if (generated)
            EditorGUILayout.HelpBox(
                "Excel 임포터가 만든 에셋입니다. 여기서 수정하지 마세요. (다음 임포트 때 덮어써집니다)",
                MessageType.Info);

        using (new EditorGUI.DisabledScope(generated))
            DrawDefaultInspector();
    }
}