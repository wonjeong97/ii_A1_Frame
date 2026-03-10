using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking; 
using My.Scripts.Global;      
using Cysharp.Threading.Tasks; // UniTask 활용을 위한 네임스페이스 추가

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
    /// 로컬 디스크에 저장하고 서버로 업로드하는 시퀀스를 관리합니다.
    /// </summary>
    public class PhotoCompositor : MonoBehaviour
    {
        [Header("Assets")]
        public Texture2D baseFrame; 

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
                rt = RenderTexture.GetTemporary(baseFrame.width, baseFrame.height, 0, RenderTextureFormat.ARGB32);
                RenderTexture prevActive = RenderTexture.active;
                RenderTexture.active = rt;

                GL.PushMatrix();
                GL.LoadPixelMatrix(0, baseFrame.width, baseFrame.height, 0);

                Graphics.DrawTexture(new Rect(0, 0, baseFrame.width, baseFrame.height), baseFrame);

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

                resultTex = new Texture2D(baseFrame.width, baseFrame.height, TextureFormat.RGB24, false);
                resultTex.ReadPixels(new Rect(0, 0, baseFrame.width, baseFrame.height), 0, 0);
                resultTex.Apply();

                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
                rt = null;

                // 인코딩 연산(CPU 집약적)을 스레드 풀로 넘겨 메인 스레드 프리징 차단
                byte[] jpgBytes = await UniTask.RunOnThreadPool(() => resultTex.EncodeToJPG(85));

                if (jpgBytes == null || jpgBytes.Length == 0)
                {
                    Debug.LogError("[PhotoCompositor] JPG 인코딩 실패");
                    return;
                }

                string finalFileName = $"{sanitizedName}_{outputFileName}.jpg";
                
                // 파일 쓰기도 비동기로 수행
                await File.WriteAllBytesAsync(Path.Combine(rootPath, finalFileName), jpgBytes);

                // 업로드 작업 시작
                await UploadImageAsync(jpgBytes, finalFileName);
            }
            catch (Exception e)
            {
                Debug.LogError($"[PhotoCompositor] 합성 중 예외: {e.Message}");
            }
            finally
            {
                if (resultTex) Destroy(resultTex);
                if (rt) RenderTexture.ReleaseTemporary(rt);
                IsProcessing = false; // 작업 성공 여부와 관계없이 플래그 해제 보장
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
            string url = $"{baseUrl}?idx_user={idxUser}&uid={encodedUid}&code={moduleCode}&type=jpg&count={safeUploadCount}";

            using (UnityWebRequest webRequest = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                // 메모리 복사 최소화를 위해 Raw 데이터를 직접 업로드 핸들러에 전달
                webRequest.uploadHandler = new UploadHandlerRaw(imageBytes);
                webRequest.uploadHandler.contentType = "image/jpeg"; 
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