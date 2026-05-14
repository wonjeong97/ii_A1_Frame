using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking; 
using My.Scripts.Global;      
using Cysharp.Threading.Tasks;

namespace My.Scripts.Utils
{
    [Serializable]
    public class CompositeSlot
    {
        public string fileSuffix; 

        [Header("Position (Top-Left Pivot)")]
        [Tooltip("배경의 좌상단(0,0)을 기준으로, 사진의 좌상단이 위치할 좌표입니다.")]
        public Vector2 position; 
        
        [Header("Scale")]
        public Vector2 scale = Vector2.one; 
    }

    /// <summary> 
    /// 저장된 개별 플레이어 사진들을 지정된 프레임(틀) 이미지 위에 합성한 뒤,
    /// 로컬 디스크에 PNG로 저장하고 서버로 업로드하는 시퀀스를 관리합니다.
    /// </summary>
    public class PhotoCompositor : MonoBehaviour
    {
        [Header("Assets")]
        public Texture2D baseFrame; 
        
        [Tooltip("배경 이미지(출력 캔버스)의 스케일입니다. 화질을 높이려면 값을 키워 해상도를 증가시킬 수 있습니다.")]
        public Vector2 baseFrameScale = Vector2.one; 

        [Header("Config")]
        public string saveFolderName = "Pictures";
        public string outputFileName = "Composite";
        
        [Tooltip("서버 업로드 시 구분용 카운트 번호")]
        [Min(1)]
        public int uploadCount = 1;
        
        [Header("API Retry Settings")]
        [SerializeField] private int maxRetries = 10;
        [SerializeField] private float retryDelay = 1.0f;

        [Header("Layout")]
        public List<CompositeSlot> slots;

        [Header("Debug")]
        public string debugBaseName = "PlayerAPlayerB";

        public bool IsProcessing { get; private set; }

        [ContextMenu("Execute Composite Now")] 
        public void DebugProcessAndSave()
        {
            ProcessAndSave(debugBaseName, true);
        }

        public void ProcessAndSave(string baseName, bool isDebug = false)
        {
            if (!baseFrame)
            {
                Debug.LogError("[PhotoCompositor] 배경 이미지 누락");
                return;
            }

            IsProcessing = true;
            
            string safeBaseName = string.IsNullOrEmpty(baseName) ? "" : baseName;
            string clean = safeBaseName.Replace("\n", "").Replace("\r", "").Trim();
            string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);
            string sanitizedName = Regex.Replace(clean, invalidRegStr, "");
            
            if (string.IsNullOrWhiteSpace(sanitizedName))
            {
                sanitizedName = "UnknownPlayers";
            }

            ExecuteCompositeAsync(sanitizedName, isDebug).Forget();
        }

        /// <summary> 
        /// 실제 렌더링 및 비동기 파일 처리 시퀀스입니다. 
        /// </summary>
        private async UniTaskVoid ExecuteCompositeAsync(string sanitizedName, bool isDebug)
        {
            Texture2D resultTex = null;

            try
            {
                resultTex = CreateCompositeTexture(sanitizedName);
                if (!resultTex) return;

                string finalFileName = string.Concat(sanitizedName, "_", outputFileName, ".png");
                string fullPath = Path.Combine(GetRootPath(), finalFileName);
                
                byte[] pngBytes = await EncodeAndSaveTextureAsync(resultTex, fullPath);

                if (!isDebug && pngBytes != null)
                {
                    await UploadImageAsync(pngBytes, finalFileName);
                }
                else if (isDebug)
                {
                    Debug.Log($"<color=cyan>[PhotoCompositor] 디버그 모드 완료: {finalFileName} 생성됨.</color>");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[PhotoCompositor] 합성 시퀀스 예외: {e.Message}");
            }
            finally
            {
                if (resultTex) Destroy(resultTex);
                IsProcessing = false; 
            }
        }
        
        /// <summary>
        /// RenderTexture와 GL 명령을 사용하여 배경 위에 개별 사진들을 합성한 Texture2D를 반환함.
        /// </summary>
        private Texture2D CreateCompositeTexture(string sanitizedName)
        {
            int targetWidth = Mathf.RoundToInt(baseFrame.width * baseFrameScale.x);
            int targetHeight = Mathf.RoundToInt(baseFrame.height * baseFrameScale.y);
            string rootPath = GetRootPath();

            RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32);
            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = rt;

            GL.PushMatrix();
            GL.LoadPixelMatrix(0, targetWidth, targetHeight, 0);

            Graphics.DrawTexture(new Rect(0, 0, targetWidth, targetHeight), baseFrame);

            foreach (CompositeSlot slot in slots)
            {
                DrawSlotPhoto(rootPath, sanitizedName, slot);
            }

            GL.PopMatrix();

            Texture2D tex = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            tex.Apply();

            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);

            return tex;
        }
        
        private void DrawSlotPhoto(string rootPath, string sanitizedName, CompositeSlot slot)
        {
            string targetPath = Path.Combine(rootPath, string.Concat(sanitizedName, slot.fileSuffix, ".png"));
            if (!File.Exists(targetPath)) return;

            Texture2D photoTex = LoadTextureFromFile(targetPath);
            if (!photoTex) return;

            float w = photoTex.width * slot.scale.x;
            float h = photoTex.height * slot.scale.y;
            Rect drawRect = new Rect(slot.position.x, -slot.position.y, w, h);
            
            Graphics.DrawTexture(drawRect, photoTex);
            Destroy(photoTex);
        }
        
        /// <summary>
        /// 텍스처 데이터를 백그라운드에서 PNG로 인코딩하고 지정된 경로에 비동기로 저장함.
        /// </summary>
        private async UniTask<byte[]> EncodeAndSaveTextureAsync(Texture2D tex, string savePath)
        {
            byte[] rawData = tex.GetRawTextureData();
            uint width = (uint)tex.width;
            uint height = (uint)tex.height;
            UnityEngine.Experimental.Rendering.GraphicsFormat format = tex.graphicsFormat;

            await UniTask.SwitchToThreadPool();

            byte[] pngBytes = ImageConversion.EncodeArrayToPNG(rawData, format, width, height);

            if (pngBytes == null || pngBytes.Length == 0)
            {
                await UniTask.SwitchToMainThread();
                Debug.LogError("[PhotoCompositor] PNG 인코딩 실패");
                return null;
            }

            await File.WriteAllBytesAsync(savePath, pngBytes);
            
            // 안전하게 메인 스레드로 복귀
            await UniTask.SwitchToMainThread();
            return pngBytes;
        }

        private async UniTask UploadImageAsync(byte[] imageBytes, string fileName)
        {
            string url = ConstructUploadUrl();
            if (string.IsNullOrEmpty(url)) return;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                bool success = await ExecuteSingleUpload(url, imageBytes);
                if (success) return;

                if (attempt < maxRetries - 1)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(retryDelay));
                }
            }
        }
        
        private string ConstructUploadUrl()
        {
            if (!SessionManager.Instance || !GameManager.Instance || GameManager.Instance.ApiConfig == null) return null;

            int idxUser = SessionManager.Instance.CurrentUserId;
            string uid = SessionManager.Instance.PlayerAUid;
            string baseUrl = GameManager.Instance.ApiConfig.UploadFileUrl;
            string moduleCode = string.IsNullOrEmpty(SessionManager.Instance.CurrentModuleCode) 
                ? "A1" 
                : SessionManager.Instance.CurrentModuleCode.ToUpper();

            if (idxUser <= 0 || string.IsNullOrWhiteSpace(uid)) return null;

            string encodedUid = UnityWebRequest.EscapeURL(uid);
            int safeUploadCount = Mathf.Max(1, uploadCount);

            return $"{baseUrl}?idx_user={idxUser.ToString()}&uid={encodedUid}&code={moduleCode}&type=png&count={safeUploadCount.ToString()}";
        }

        private async UniTask<bool> ExecuteSingleUpload(string url, byte[] imageBytes)
        {
            using (UnityWebRequest webRequest = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                webRequest.uploadHandler = new UploadHandlerRaw(imageBytes) { contentType = "image/png" };
                webRequest.timeout = 15;

                try
                {
                    await webRequest.SendWebRequest().ToUniTask();
                    return webRequest.result == UnityWebRequest.Result.Success;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[PhotoCompositor] 업로드 통신 에러: {e.Message}");
                    return false;
                }
            }
        }

        private Texture2D LoadTextureFromFile(string path)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                Texture2D tex = new Texture2D(2, 2);
                tex.LoadImage(bytes);
                return tex;
            }
            catch { return null; }
        }

        private string GetRootPath()
        {
            string dataPath = Application.dataPath;
            DirectoryInfo parentDir = Directory.GetParent(dataPath);
            string rootPath = parentDir != null ? parentDir.FullName : dataPath;
            return Path.Combine(rootPath, saveFolderName, DateTime.Now.ToString("yyyy-MM-dd"));
        }
    }
}