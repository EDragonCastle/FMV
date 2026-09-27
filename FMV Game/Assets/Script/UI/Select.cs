using UnityEngine;
using System.Collections.Generic;

public class Select : MonoBehaviour, IChannel
{
    public GameObject constantParent;
    public GameObject freeParent;

    private void OnEnable()
    {
        var eventManager = Locator<EventManager>.Get();
        eventManager.Subscription(ChannelInfo.Select, HandleEvent);
        eventManager.Subscription(ChannelInfo.SelectSetting, HandleEvent);
    }

    private void OnDisable()
    {
        var eventManager = Locator<EventManager>.Get();
        eventManager.Unsubscription(ChannelInfo.Select, HandleEvent);
        eventManager.Unsubscription(ChannelInfo.SelectSetting, HandleEvent);
    }

    public void HandleEvent(ChannelInfo channel, object information = null)
    {
        var factory = Locator<Factory>.Get();
        switch(channel)
        {
            case ChannelInfo.SelectSetting:
                SelectSetting selectSetting = information as SelectSetting;
                
                if(selectSetting != null) {
                    if(selectSetting.selectType == SettingType.Constant) {
                        foreach(var selectObject in selectSetting.branchOptions) {
                            var select = factory.Spawn(Vector3.zero, Quaternion.identity, parent: constantParent.transform);
                            select.Initalize(selectObject.branchText, selectObject.targetVideoUID);
                        }
                    }
                    else if(selectSetting.selectType == SettingType.Free) {
                        foreach(var selectObject in selectSetting.branchOptions) {
                            var select = factory.Spawn(Vector3.zero, Quaternion.identity, parent:   freeParent.transform);
                            select.Initalize(selectObject.branchText, selectObject.targetVideoUID);
                        }
                    }
                }
                else
                {
                    Debug.LogError("EventManager에 들어올 Data가 SelectSetting Type이 아닙니다.");
                    return;
                }
                break;
            case ChannelInfo.Select:
                foreach(Transform child in constantParent.transform)
                {
                    var button = child.GetComponent<StorySelectButton>();
                    factory.Despawn(button);
                }

                foreach(Transform child in freeParent.transform)
                {
                    var button = child.GetComponent<StorySelectButton>();
                    factory.Despawn(button);
                }

                break;
        }
    }

}


public class SelectSetting
{
    public List<BranchOption> branchOptions;
    public SettingType selectType;
}

public enum SettingType
{
    Constant,
    Free,
}

// select 개수
// 이게 고정된 Select인지 자유형 Select인지에 따라 다르게 설정해야 한다.