using UnityEngine;

public class Factory : FactoryMethod<StorySelectButton>
{
    public Factory(StorySelectButton prefab, int poolSize = 30, Transform parent = null) : base(prefab, poolSize, parent)
    {

    }
}
