using UnityEngine;
using Cysharp.Threading.Tasks;

[DefaultExecutionOrder(-9999)]
public class GameManager : MonoBehaviour
{
    public Transform pooingObjectParent;

    void Awake()
    {
        // 의존성 주입으로 Lazy Initalize 해결
        EventManager eventManager = new EventManager();
        Locator<EventManager>.Provide(eventManager);

        ResourceManager resourceManager = new ResourceManager();
        Locator<ResourceManager>.Provide(resourceManager);

        Initalize().Forget();
    }

    private async UniTask Initalize()
    {
        var resourceManager = Locator<ResourceManager>.Get();
        var storySelectButtonObject = await resourceManager.Get<GameObject>("StoryButton");
        var storySelectButtonComponent = storySelectButtonObject.GetComponent<StorySelectButton>();

        Factory selectButtonFactory = new Factory(storySelectButtonComponent, parent: pooingObjectParent);
        Locator<Factory>.Provide(selectButtonFactory);
    }
}
