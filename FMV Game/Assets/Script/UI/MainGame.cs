using UnityEngine;


public class MainGame : MonoBehaviour, IChannel
{
    public GameObject mainScene;

    private void Awake()
    {
        mainScene.SetActive(false);
    }

    private void OnEnable()
    {
        var eventManager = Locator<EventManager>.Get();
        eventManager.Subscription(ChannelInfo.MainGame, HandleEvent);
    }

    private void OnDisable()
    {
        var eventManager = Locator<EventManager>.Get();
        eventManager.Unsubscription(ChannelInfo.MainGame, HandleEvent);
    }

    public void HandleEvent(ChannelInfo channel, object information = null)
    {
        switch(channel)
        {
            case ChannelInfo.MainGame:
                if (information is bool isActive)
                    mainScene.SetActive(isActive);
                break;
        }
    }
}
