using UnityEngine;
using TMPro;
using Cysharp.Threading.Tasks;
using UnityEngine.Video;

public class StorySelectButton : MonoBehaviour, IEntity
{
    public TextMeshProUGUI storyButtonText;
    private string videoIndex;
    
    public void Initalize(string storySelectText, string _videoIndex)
    {
        storyButtonText.text = storySelectText;
        videoIndex = _videoIndex;
    }

    public void ApplyPrototype(Prototype data)
    {

    }

    public void OnDespawn()
    {

    }

    public void OnSpawn()
    {

    }

    public void SetTransform(Vector3 position, Quaternion rotation, float multiplier = 1, Transform parent = null)
    {
        this.transform.localPosition = position;
        this.transform.localRotation = rotation;

        if (parent != null)
            this.transform.SetParent(parent);
    }


    public async void SelectButton()
    {
        // 버튼을 누르면 없애야 한다.
        var eventManager = Locator<EventManager>.Get();
        var resourceManager = Locator<ResourceManager>.Get();

        var videoSetting = await resourceManager.Get<VideoSetting>(videoIndex);
        var videoClip = await resourceManager.Get<VideoClip>(videoSetting.clipKey);

        StoryVideoSetting storySetting = new();
        storySetting.clip = videoClip;
        storySetting.setting = videoSetting;

        eventManager.Notify(ChannelInfo.Select);
        eventManager.Notify(ChannelInfo.PlayVideo, storySetting);
    }
}
