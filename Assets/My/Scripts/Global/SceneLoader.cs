using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace My.Scripts.Global
{
    /// <summary>
    /// Addressables 기반의 씬 비동기 로드 유틸리티.
    /// 에러나 취소 시 발생하는 에셋번들 메모리 누수를 안전하게 차단합니다.
    /// </summary>
    public static class SceneLoader
    {
        public static async UniTask LoadAsync(
            string sceneName, 
            LoadSceneMode loadMode = LoadSceneMode.Single, 
            CancellationToken cancellationToken = default)
        {
            AsyncOperationHandle<SceneInstance> handle = default;

            try
            {
                handle = Addressables.LoadSceneAsync(sceneName, loadMode);
                await handle.ToUniTask(cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // 로드 중 강제 취소 시 메모리 누수 방지
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
                throw;
            }
            catch (Exception e)
            {
                // 로드 실패(오타, 파일 유실 등) 시 메모리 누수 방지
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
                
                Debug.LogError($"[SceneLoader] 씬 로드 실패 ({sceneName}): {e.Message}");
                throw;
            }
        }
    }
}