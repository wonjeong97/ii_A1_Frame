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

        [Header("Layout")]
        public List<CompositeSlot> slots;

        [Header("Debug")]
        public string debugBaseName = "PlayerAPlayerB";

        public bool IsProcessing { get; private set; }

        [ContextMenu("Execute Composite Now")] 
        public void DebugProcessAndSave()
        {
            ProcessAndSave(debugBaseName);
        }

        /// <summary> 
        /// 합성 로직을 실행합니다. 무거운 인코딩과 업로드는 비동기로 처리하여 프리징을 방지합니다.
        /// </summary>
        public void ProcessAndSave(string baseName)
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

            ExecuteCompositeAsync(sanitizedName).Forget();
        }

        /// <summary> 
        /// 실제 렌더링 및 비동기 파일 처리 시퀀스입니다. 
        /// </summary>
        private async UniTaskVoid ExecuteCompositeAsync(string sanitizedName)
        {
            string rootPath = GetRootPath();
            RenderTexture rt = null;
            Texture2D resultTex = null;

            try
            {
                // 스케일이 적용된 최종 캔버스(해상도) 크기 계산
                int targetWidth = Mathf.RoundToInt(baseFrame.width * baseFrameScale.x);
                int targetHeight = Mathf.RoundToInt(baseFrame.height * baseFrameScale.y);

                rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32);
                RenderTexture prevActive = RenderTexture.active;
                RenderTexture.active = rt;

                GL.PushMatrix();
                GL.LoadPixelMatrix(0, targetWidth, targetHeight, 0);

                // 스케일이 적용된 크기만큼 배경 텍스처 그리기
                Graphics.DrawTexture(new Rect(0, 0, targetWidth, targetHeight), baseFrame);

                foreach (CompositeSlot slot in slots)
                {
                    string targetPath = Path.Combine(rootPath, $"{sanitizedName}{slot.fileSuffix}.png");
                    if (File.Exists(targetPath))
                    {
                        Texture2D photoTex = LoadTextureFromFile(targetPath);
                        if (photoTex)
                        {
                            float w = photoTex.width * slot.scale.x;
                            float h = photoTex.height * slot.scale.y;
                            Rect drawRect = new Rect(slot.position.x, -slot.position.y, w, h);
                            Graphics.DrawTexture(drawRect, photoTex);
                            Destroy(photoTex); 
                        }
                    }
                }

                GL.PopMatrix();

                // 스케일이 적용된 크기만큼 결과 텍스처 생성
                resultTex = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
                resultTex.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
                resultTex.Apply();

                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
                rt = null;

                byte[] rawData = resultTex.GetRawTextureData();
                int texWidth = resultTex.width;
                int texHeight = resultTex.height;
                UnityEngine.Experimental.Rendering.GraphicsFormat format = resultTex.graphicsFormat;

                // 백그라운드 스레드에서 PNG 인코딩 수행 (무손실)
                byte[] pngBytes = await UniTask.RunOnThreadPool(() => 
                {
                    return ImageConversion.EncodeArrayToPNG(rawData, format, (uint)texWidth, (uint)texHeight);
                });

                if (pngBytes == null || pngBytes.Length == 0)
                {
                    Debug.LogError("[PhotoCompositor] PNG 인코딩 실패");
                    return;
                }

                string finalFileName = $"{sanitizedName}_{outputFileName}.png";
                
                // 파일 쓰기도 비동기로 수행
                await File.WriteAllBytesAsync(Path.Combine(rootPath, finalFileName), pngBytes);

                // 업로드 작업 시작
                await UploadImageAsync(pngBytes, finalFileName);
            }
            catch (Exception e)
            {
                Debug.LogError($"[PhotoCompositor] 합성 중 예외: {e.Message}");
            }
            finally
            {
                if (resultTex) Destroy(resultTex);
                if (rt) RenderTexture.ReleaseTemporary(rt);
                IsProcessing = false; 
            }
        }

        /// <summary> 
        /// 서버 업로드를 UniTask 기반으로 처리하여 try-finally 관리를 용이하게 합니다. 
        /// </summary>
        private async UniTask UploadImageAsync(byte[] imageBytes, string fileName)
        {
            int idxUser = 0;
            string uid = "";
            string baseUrl = "";
            string moduleCode = "a1"; 

            if (SessionManager.Instance)
            {
                idxUser = SessionManager.Instance.CurrentUserId;
                uid = SessionManager.Instance.PlayerAUid; 
                if (GameManager.Instance && GameManager.Instance.ApiConfig != null)
                    baseUrl = GameManager.Instance.ApiConfig.UploadFileUrl;
                if (!string.IsNullOrEmpty(SessionManager.Instance.CurrentModuleCode))
                    moduleCode = SessionManager.Instance.CurrentModuleCode.ToLower();
            }

            if (string.IsNullOrEmpty(baseUrl) || idxUser <= 0 || string.IsNullOrWhiteSpace(uid)) return;
            
            string encodedUid = UnityWebRequest.EscapeURL(uid);
            int safeUploadCount = Mathf.Max(1, uploadCount);
            
            // 파라미터 type=png 로 수정
            string url = $"{baseUrl}?idx_user={idxUser}&uid={encodedUid}&code={moduleCode}&type=png&count={safeUploadCount}";

            using (UnityWebRequest webRequest = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                webRequest.uploadHandler = new UploadHandlerRaw(imageBytes);
                // 헤더를 image/png 로 수정
                webRequest.uploadHandler.contentType = "image/png"; 
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.timeout = 15;

                await webRequest.SendWebRequest().ToUniTask();

                if (webRequest.result != UnityWebRequest.Result.Success)
                    Debug.LogError($"[PhotoCompositor] 업로드 실패: {webRequest.error}");
                else
                    Debug.Log($"[PhotoCompositor] 업로드 성공: {webRequest.responseCode}");
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