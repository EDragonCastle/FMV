using UnityEngine;
using UnityEngine.UI;

public class VideoUI : MonoBehaviour, IChannel
{
    public RenderTexture texture1;
    public RenderTexture texture2;

    private RawImage rawImage;
    private bool isMain = true;

    private void Awake()
    {
        rawImage = this.GetComponent<RawImage>();
    }

    public void OnEnable()
    {
        var eventManager = Locator<EventManager>.Get();
        eventManager.Subscription(ChannelInfo.VideoTexture, HandleEvent);
    }

    private void OnDisable()
    {
        var eventManager = Locator<EventManager>.Get();
        eventManager.Unsubscription(ChannelInfo.VideoTexture, HandleEvent);
    }

    public void HandleEvent(ChannelInfo channel, object information = null)
    {
        switch(channel)
        {
            case ChannelInfo.VideoTexture:
                isMain = !isMain;
                if (isMain)
                    rawImage.texture = texture1;
                else
                    rawImage.texture = texture2;
                break;
        }
    }
}
