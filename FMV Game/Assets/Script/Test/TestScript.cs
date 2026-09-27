using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Cysharp.Threading.Tasks;

public class TestScript : MonoBehaviour
{
    public RenderTexture mainTexture;
    public RenderTexture subTexture;

    public RawImage mainScene;

    public VideoClip clip1;
    public VideoClip clip2;
    public VideoClip clip3;

    public VideoPlayer player1;

    private bool isPause;

    async void Update()
    {

    }

    private async UniTask TestVedio(int index)
    {
        var resourceManager = Locator<ResourceManager>.Get();

        switch(index)
        {
            case 0:
                player1.clip = await resourceManager.Get<VideoClip>("Test1");
                break;
            case 1:
                player1.clip = await resourceManager.Get<VideoClip>("Test2");
                break;
            case 2:
                player1.clip = await resourceManager.Get<VideoClip>("Test3");
                break;
        }

        player1.Play();
    }

}
