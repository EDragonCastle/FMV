using UnityEngine;
using TMPro;

public class StoryText : MonoBehaviour, IChannel
{
    private TextMeshProUGUI textUI;
    private bool isWriting = false;
    private void Awake()
    {
        textUI = this.GetComponent<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        var eventManager = Locator<EventManager>.Get();
        eventManager.Subscription(ChannelInfo.StroyText, HandleEvent);
    }

    private void OnDisable()
    {
        var eventManager = Locator<EventManager>.Get();
        eventManager.Unsubscription(ChannelInfo.StroyText, HandleEvent);
    }

    public void HandleEvent(ChannelInfo channel, object information = null)
    {
        switch(channel)
        {
            case ChannelInfo.StroyText:
                StoryTextSetting storyText = information as StoryTextSetting;
                if(storyText != null)
                {
                    WritingText(storyText);
                    isWriting = true;
                }
                else
                {
                    Debug.LogError("지금 Event로 들어온 Script는 StoryTextSetting Type이 아닙니다.");
                }
                break;
        }
    }

    private void WritingText(StoryTextSetting stroySetting)
    {
        CancelInvoke(nameof(EmptyText));
        textUI.text = stroySetting.text;

        if(stroySetting.duration != 0)
            Invoke(nameof(EmptyText), stroySetting.duration);
    }

    private void EmptyText()
    {
        textUI.text = "";
    }
}


public class StoryTextSetting
{
    public string text;
    public float duration;
}