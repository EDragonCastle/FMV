using UnityEngine;

public class Setting : MonoBehaviour, IChannel
{
    public GameObject settingUI;

    private void OnEnable()
    {
        var eventManager = Locator<EventManager>.Get();
        eventManager.Subscription(ChannelInfo.Setting, HandleEvent);
    }

    private void OnDisable()
    {
        var eventManager = Locator<EventManager>.Get();
        eventManager.Unsubscription(ChannelInfo.Setting, HandleEvent);
    }

    public void HandleEvent(ChannelInfo channel, object information = null)
    {
        switch(channel)
        {
            case ChannelInfo.Setting:
                if (information is bool isActive)
                    settingUI.SetActive(isActive);
                break;
        }
    }

}
