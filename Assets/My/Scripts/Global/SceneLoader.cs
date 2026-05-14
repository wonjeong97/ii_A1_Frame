using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace My.Scripts.Global
{
    public static class SceneLoader
    {
        public static async UniTask LoadAsync(string sceneName)
        {
            AsyncOperationHandle<SceneInstance> handle = Addressables.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            await handle.ToUniTask();
        }
    }
}
