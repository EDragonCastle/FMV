using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class WriteReport
{
    public int Created;
    public int Updated;
    public List<string> Orphaned = new List<string>();   // Excel에서 사라졌지만 에셋은 남아 있는 VideoUID
}

/// <summary>
/// 검증을 통과한 ParseResult로 VideoSetting 에셋을 만든다.
/// 에셋을 지우고 다시 만들지 않고 "찾아서 내용만 덮어쓴다" → GUID가 유지되어 다른 곳의 참조가 끊기지 않는다.
/// </summary>
public static class VideoAssetWriter
{
    public const string RootDir = "Assets/Generated/Video";

    public static WriteReport Write(ParseResult result)
    {
        var report = new WriteReport();
        EnsureFolder(RootDir);

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var pv in result.Videos)
            {
                string path = RootDir + "/" + pv.VideoUID + ".asset";
                var asset = AssetDatabase.LoadAssetAtPath<VideoSetting>(path);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<VideoSetting>();
                    AssetDatabase.CreateAsset(asset, path);
                    report.Created++;
                }
                else report.Updated++;

                asset.videoUID = pv.VideoUID;
                asset.clipKey = pv.ClipKey;
                asset.loopStartTime = pv.LoopStartTime;
                asset.branchTimer = pv.BranchTimer;
                asset.branchType = pv.BranchType;
                asset.scriptList = new List<Script>(pv.Scripts);
                asset.branchOptions = new List<BranchOption>(pv.BranchOptions);
                asset.qteSetting = pv.QteSetting;
                asset.nextVideoUID = pv.NextVideoUID;
                asset.endingId = pv.EndingId;
                EditorUtility.SetDirty(asset);
            }

            var current = new HashSet<string>(result.Videos.Select(v => v.VideoUID));
            foreach (string guid in AssetDatabase.FindAssets("t:VideoSetting", new[] { RootDir }))
            {
                var existing = AssetDatabase.LoadAssetAtPath<VideoSetting>(AssetDatabase.GUIDToAssetPath(guid));
                if (existing != null && !current.Contains(existing.videoUID))
                    report.Orphaned.Add(existing.videoUID);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return report;
    }

    static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}