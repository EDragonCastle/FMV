using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.Video;

public class Title : MonoBehaviour, IChannel
{
    public GameObject title;
    public AudioSource titleSound;

    private void OnEnable()
    {
        var eventManager = Locator<EventManager>.Get();
        eventManager.Subscription(ChannelInfo.Title, HandleEvent);
    }

    private void OnDisable()
    {
        var eventManager = Locator<EventManager>.Get();
        eventManager.Unsubscription(ChannelInfo.Title, HandleEvent);
    }

    public async void GameStart()
    {
        await NewGame();
    }
    
    private async UniTask NewGame()
    {
        // 게임 시작
        var eventManager = Locator<EventManager>.Get();
        var resourceManager = Locator<ResourceManager>.Get();

        var newGameVideoSetting = await resourceManager.Get<VideoSetting>("V001");
        var video = await resourceManager.Get<VideoClip>(newGameVideoSetting.clipKey);
       
        StoryVideoSetting storySetting = new();
        storySetting.setting = newGameVideoSetting;
        storySetting.clip = video;

        eventManager.Notify(ChannelInfo.PlayVideo, storySetting);
        eventManager.Notify(ChannelInfo.MainGame, true);
        title.SetActive(false);
    }


    public void GameExit()
    {
        #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
        #else
                Application.Quit();
        #endif
    }

    public void Setting()
    {

    }

    private void ActiveTitle(bool isActive)
    {
        title.SetActive(isActive);
        if(isActive) {
            titleSound.Play();
        }
    }

    public void HandleEvent(ChannelInfo channel, object information = null)
    {
        switch(channel)
        {
            case ChannelInfo.Title:
                if (information is bool active)
                    ActiveTitle(active);
                break;
        }
    }
}
