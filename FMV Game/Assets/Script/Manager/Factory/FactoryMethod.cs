using UnityEngine;
using System.Collections.Generic;

public abstract class FactoryMethod<T> where T : Component, IEntity
{
    protected ObjectPool<T> pool;
    private List<T> activeEntitys = new();

    protected FactoryMethod(T prefab, int poolSize = 30, Transform _parent = null)
    {
        pool = new ObjectPool<T>(prefab, poolSize, _parent);
    }

    public T Spawn(Vector3 position, Quaternion rotation, float scaleMultiplier = 1, Transform parent = null)
    {
        T entity = CreateEntity();
        entity.SetTransform(position, rotation, scaleMultiplier, parent);
        entity.gameObject.SetActive(true);
        entity.OnSpawn();

        activeEntitys.Add(entity);
        return entity;
    }

    public void Despawn(T entity)
    {
        entity.OnDespawn();
        entity.gameObject.SetActive(false);
        pool.Return(entity);

        activeEntitys.Remove(entity);
    }

    public void DespawnAll()
    {
        var snapShot = new List<T>(activeEntitys);
        foreach (var entity in snapShot)
            Despawn(entity);
    }

    // Factory Method - GOF의 디자인 패턴에 나오는 Hook 구현 (필요에 따라 Create 다른 방식으로 구현하고 싶다면 override해서 구현한다)
    protected virtual T CreateEntity() => pool.Get();
}



/// 추상 팩토리가 아닌 팩토리 메서드로 만든 이유
/// 추상 팩토리는 이미 만들 오브젝트를 미리 알고 있어야 하는 상태이지만, 범용적인 Factory를 제작할 때는 무엇을 만들지 알 수 없다.
/// 그래서 팩토리 메서드로 만든 것이다.