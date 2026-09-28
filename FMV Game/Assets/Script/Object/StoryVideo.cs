using UnityEngine;
using UnityEngine.Video;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public class StoryVideo : MonoBehaviour, IChannel
{
    public VideoPlayer player;
    public VideoPlayer player2;

    private const int frameCount = 1;

    private EventManager eventManager;
    private bool isPlaying = false;
    private VideoSetting videoSetting;
    private int scriptIndex = 0;
    private bool isBranch = false;
    private bool isMain = true;


    private void Awake()
    {
        eventManager = Locator<EventManager>.Get();
        player.Pause();
        player2.Pause();
    }

    private void OnEnable()
    {
        eventManager.Subscription(ChannelInfo.PlayVideo, HandleEvent);
    }

    private void OnDisable()
    {
        eventManager.Unsubscription(ChannelInfo.PlayVideo, HandleEvent);
    }

    private void Update()
    {
        if(isPlaying)
            PlayingStory();
    }


    public void HandleEvent(ChannelInfo channel, object information = null)
    {
        switch(channel)
        {
            case ChannelInfo.PlayVideo:
                StoryVideoSetting setting = information as StoryVideoSetting;

                if(setting != null)
                {
                    videoSetting = setting.setting;
                    long targetFrame = (long)(videoSetting.loopStartTime * player.frameRate);

                    if (isMain)
                    {
                        player.clip = setting.clip;
                        player.frame = 0;
                        player.time = 0;
                        player.Play();

                        player2.clip = setting.clip;
                        player2.frame = targetFrame;
                        player2.Pause();
                    }
                    else
                    {
                        player2.clip = setting.clip;
                        player2.time = 0;
                        player2.frame = 0;
                        player2.Play();

                        player.clip = setting.clip;
                        player.frame = targetFrame;
                        player.Pause();
                    }

                    scriptIndex = 0;
                    isBranch = false;
                }
                else
                {
                    Debug.LogError("잘못된 Data가 들어왔다.");
                    return;
                }

                isPlaying = true;
                break;
        }
    }


    private void PlayingStory()
    {
        if(isMain)
        {
            // clip의 time을 토대로 Script가 실행이 된다.
            if (scriptIndex < videoSetting.scriptList.Count) {
                if(player.time >= videoSetting.scriptList[scriptIndex].startTime) {
                    StoryTextSetting storyTextSetting = new();
                    storyTextSetting.text = videoSetting.scriptList[scriptIndex].scriptText;
                    storyTextSetting.duration = videoSetting.scriptList[scriptIndex].duration;
                    eventManager.Notify(ChannelInfo.StroyText, storyTextSetting);
                    scriptIndex++;
                }
            }

            if (!isBranch && (float)player.time >= videoSetting.branchTimer)
            {
                Branch(videoSetting.branchType);
            }

            if (player.frame >= (long)player.frameCount - frameCount)
            {
                player.frame = player2.frame;
                eventManager.Notify(ChannelInfo.VideoTexture);
                player.Pause();
                isMain = !isMain;
                player2.Play();
            }
        }
        else
        {
            // clip의 time을 토대로 Script가 실행이 된다.
            if (scriptIndex < videoSetting.scriptList.Count) {
                if (player2.time >= videoSetting.scriptList[scriptIndex].startTime)
                {
                    StoryTextSetting storyTextSetting = new();
                    storyTextSetting.text = videoSetting.scriptList[scriptIndex].scriptText;
                    storyTextSetting.duration = videoSetting.scriptList[scriptIndex].duration;
                    eventManager.Notify(ChannelInfo.StroyText, storyTextSetting);
                    scriptIndex++;
                }
            }

            if (!isBranch && (float)player2.time >= videoSetting.branchTimer)
            {
                Branch(videoSetting.branchType);
            }

            if (player2.frame >= (long)player2.frameCount - frameCount)
            {
                player2.frame = player.frame;
                eventManager.Notify(ChannelInfo.VideoTexture);
                player2.Pause();
                isMain = !isMain;
                player.Play();
            }
        }
    }


    private void Branch(BranchType type)
    {
        switch (type)
        {
            case BranchType.Select:
                // videoSetting.branchOptions 을 사용해서 무엇을 한다.
                SelectSetting selectSetting = new();
                selectSetting.branchOptions = videoSetting.branchOptions;
                selectSetting.selectType = SettingType.Constant;
                eventManager.Notify(ChannelInfo.SelectSetting, selectSetting);
                break;
            case BranchType.QTE:
                // videoSetting.qteSetting을 사용해서 무엇을 한다.
                break;
            case BranchType.Continue:
                // 다음 비디오가 실행된다.
                break;
            case BranchType.Ending:
                // 엔딩은 어떻게 할 것인지 생각해야 한다.
                eventManager.Notify(ChannelInfo.MainGame, false);
                eventManager.Notify(ChannelInfo.Title, true);

                isPlaying = false;
             

                EndingSetting().Forget();
                break;
        }

        isBranch = true;
    }


    private async UniTask EndingSetting()
    {
        var resourceManager = Locator<ResourceManager>.Get();

        var newGameVideoSetting = await resourceManager.Get<VideoSetting>("V001");
        var video = await resourceManager.Get<VideoClip>(newGameVideoSetting.clipKey);

        StoryVideoSetting storySetting = new();
        storySetting.setting = newGameVideoSetting;
        storySetting.clip = video;

        player.clip = storySetting.clip;
        player.frame = 0;
        player.time = 0;
        player.Pause();

        player2.clip = storySetting.clip;
        player2.frame = 0;
        player2.time = 0;
        player2.Pause();
    }
}

public class StoryVideoSetting
{
    public VideoSetting setting;
    public VideoClip clip;
}


