using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

public class ResourceManager
{
    private Dictionary<string, (AsyncOperationHandle handle, int refCount)> handle;

    public ResourceManager()
    {
        handle = new Dictionary<string, (AsyncOperationHandle, int)>();
    }

    public void Release(string assetName)
    {
        if(handle.TryGetValue(assetName, out var _handle))
        {
            int newCount = _handle.refCount - 1;

            if(newCount <= 0)
            {
                Addressables.Release(_handle.handle);
                handle.Remove(assetName);
            }
            else
            {
                handle[assetName] = (_handle.handle, newCount);
            }

        }
    }


    public async UniTask<T> Get<T>(string assetName, System.Threading.CancellationToken token = default) where T : Object
    {
        if(handle.TryGetValue(assetName, out var _handle))
        {
            if (!_handle.handle.IsDone)
            {
                await _handle.handle.Convert<T>().ToUniTask(cancellationToken: token);
            }
            

            if(handle.TryGetValue(assetName, out var current))
            {
                handle[assetName] = (current.handle, current.refCount + 1);
                return (T)_handle.handle.Result;
            }
        }

        var loadHandle = Addressables.LoadAssetAsync<T>(assetName);

        try
        {
            T result = await loadHandle.ToUniTask(cancellationToken: token);

            if(result != null)
            {
                handle[assetName] = (loadHandle, 1);
            }
            return result;
        }
        catch(System.OperationCanceledException)
        {
            throw;
        }
        catch (System.Exception e)
        {
            if (loadHandle.IsValid())
                Addressables.Release(loadHandle);
            Debug.LogError($"Resource Manager에서 {assetName}를 실패했습니다. : {e.Message}");
            return null;
        }
    }
}
