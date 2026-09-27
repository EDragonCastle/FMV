using System.Collections.Generic;

public enum BranchType
{
    Select,
    QTE,
    Continue,
    Ending,
}

[System.Serializable]
public class Script
{
    public float startTime;     // 대화가 나올 시간
    public float duration;      // 대화 지속시간
    public string scriptText;   // 대화 내용
}

[System.Serializable]
public class BranchOption
{
    public string branchText;       // 버튼 문구
    public string targetVideoUID;   // 다음 영상으로 갈 비디오 UID
}

public enum QTEPassCondition
{
    Sequence,
    AnyStep,
}

[System.Serializable]
public class QTEStep
{
    public List<string> acceptedKeys = new();
    public float timeLimit;
}

[System.Serializable]
public class QTESetting
{
    public QTEPassCondition passCondition;
    public List<QTEStep> steps = new();
    public string successTargetVideoUID;
    public string failTargetVideoUID;
}