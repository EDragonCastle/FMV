using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName ="Story/VideoSetting")]
public class VideoSetting : ScriptableObject
{
    public string videoUID;     // 현재 Video가 실행되고 있는 이름
    public string clipKey;      // 플레이할 영상

    public float loopStartTime;     // 선택지를 선택하기 전까지 Loop를 계속 진행할 Timer다.

    public List<Script> scriptList = new(); // 대화가 나올 시간과 Script는 공통으로 이뤄질 수 있다.

    public float branchTimer;   // 분기 선택지 시스템이 나올 시간때
    public BranchType branchType;     // 이벤트인지 선택인지 BranchType을 알아야 한다.

    public List<BranchOption> branchOptions = new();   // 성공/실패 시 다음으로 이동할 VideoUID
    public QTESetting qteSetting;   // QTE 전용

    public string nextVideoUID;
    public string endingId;
}