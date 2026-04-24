using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

namespace My.Scripts.Global
{
    public static class SceneLoader
    {
        public static async UniTask LoadAsync(string sceneName)
        {
            var handle = Addressables.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            await handle.ToUniTask();
        }
    }
}
