using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking; 
using My.Scripts.Global;      

namespace My.Scripts.Utils
{
    [Serializable]
    public class CompositeSlot
    {
        public string fileSuffix; // 예: "_Q1"

        [Header("Position (Top-Left Pivot)")]
        [Tooltip("배경의 좌상단(0,0)을 기준으로, 사진의 좌상단이 위치할 좌표입니다.\n(X: 오른쪽으로 이동, Y: 아래로 이동)")]
        public Vector2 position; 
        
        [Header("Scale")]
        public Vector2 scale = Vector2.one; 
    }

    /// <summary> 사진 합성기 (좌상단 좌표계: Photoshop/UI 표준) </summary>
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

        [ContextMenu("Execute Composite Now")] 
        public void DebugProcessAndSave()
        {
            ProcessAndSave(debugBaseName);
        }

        public void ProcessAndSave(string baseName)
        {
            if (!baseFrame)
            {
                Debug.LogError("[PhotoCompositor] 배경 이미지 누락");
                return;
            }
            
            string safeBaseName = string.IsNullOrEmpty(baseName) ? "" : baseName;
            string clean = safeBaseName.Replace("\n", "").Replace("\r", "").Trim();
            string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);
            string sanitizedName = Regex.Replace(clean, invalidRegStr, "");
            
            if (string.IsNullOrWhiteSpace(sanitizedName))
            {
                sanitizedName = "UnknownPlayers";
            }

            // 1. 경로 설정 (날짜 폴더 포함)
            string rootPath = GetRootPath();
            if (!Directory.Exists(rootPath))
            {
                Debug.LogWarning($"[PhotoCompositor] 폴더가 존재하지 않아 합성할 수 없습니다: {rootPath}");
                return;
            }

            Debug.Log($"[PhotoCompositor] 합성 시작 (경로: {rootPath})");

            // 2. 렌더 텍스처 준비
            RenderTexture rt = RenderTexture.GetTemporary(baseFrame.width, baseFrame.height, 0, RenderTextureFormat.ARGB32);
            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = rt;

            // 3. GL 매트릭스 설정 (좌측 상단 0,0 기준)
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, baseFrame.width, baseFrame.height, 0);

            // 4. 배경 그리기
            Graphics.DrawTexture(new Rect(0, 0, baseFrame.width, baseFrame.height), baseFrame);

            // 5. 사진 합성 (해당 날짜 폴더에서 파일 로드, var 제거)
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

            // 6. 결과 텍스처 생성
            Texture2D resultTex = new Texture2D(baseFrame.width, baseFrame.height, TextureFormat.RGB24, false);
            resultTex.ReadPixels(new Rect(0, 0, baseFrame.width, baseFrame.height), 0, 0);
            resultTex.Apply();

            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);

            // 7. JPG 인코딩 (Quality 85)
            byte[] jpgBytes = resultTex.EncodeToJPG(85);
            if (jpgBytes == null || jpgBytes.Length == 0)
            {
                Debug.LogError("[PhotoCompositor] JPG 인코딩 실패로 저장/업로드를 중단합니다.");
                Destroy(resultTex);
                return;
            }
            string finalFileName = $"{sanitizedName}_{outputFileName}.jpg";

            // 8. 로컬 저장
            SaveToFile(jpgBytes, finalFileName, rootPath);

            // 9. 서버 업로드 진행
            StartCoroutine(UploadImageRoutine(jpgBytes, finalFileName));

            Destroy(resultTex);

            Debug.Log($"[PhotoCompositor] 합성 완료 및 로컬 저장됨: {Path.Combine(rootPath, finalFileName)}");
        }

        private IEnumerator UploadImageRoutine(byte[] imageBytes, string fileName)
        {
            int idxUser = 0;
            string uid = "";
            string baseUrl = "";
            string moduleCode = "a1"; 

            if (SessionManager.Instance)
            {
                idxUser = SessionManager.Instance.CurrentUserId;
                uid = SessionManager.Instance.PlayerAUid; 
                
                if (GameManager.Instance.ApiConfig != null)
                {
                    baseUrl = GameManager.Instance.ApiConfig.UploadFileUrl;
                }

                if (!string.IsNullOrEmpty(SessionManager.Instance.CurrentModuleCode))
                {
                    moduleCode = SessionManager.Instance.CurrentModuleCode.ToLower();
                }
            }

            if (string.IsNullOrEmpty(baseUrl))
            {
                Debug.LogWarning("[PhotoCompositor] API 설정(baseUrl)이 없어 업로드를 건너뜁니다.");
                yield break;
            }

            if (idxUser <= 0 || string.IsNullOrWhiteSpace(uid))
            {
                Debug.LogWarning("[PhotoCompositor] idx_user/uid가 유효하지 않아 업로드를 건너뜁니다.");
                yield break;
            }
            
            string encodedUid = UnityWebRequest.EscapeURL(uid);
            
            int safeUploadCount = Mathf.Max(1, uploadCount);
            string url = $"{baseUrl}?idx_user={idxUser}&uid={encodedUid}&code={moduleCode}&type=jpg&count={safeUploadCount}";
            Debug.Log($"[PhotoCompositor] 사진 업로드 시도 중...");

            using (UnityWebRequest webRequest = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                UploadHandlerRaw uploadHandler = new UploadHandlerRaw(imageBytes);
                uploadHandler.contentType = "image/jpeg"; 
                
                webRequest.uploadHandler = uploadHandler;
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.timeout = 10;

                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.ConnectionError || 
                    webRequest.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError($"[PhotoCompositor] 업로드 실패: {webRequest.error}");
                }
                else
                {
                    string responseJson = webRequest.downloadHandler.text;
                    Debug.Log($"[PhotoCompositor] 업로드 성공! status={webRequest.responseCode}");
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

        private void SaveToFile(byte[] bytes, string fileName, string rootPath)
        {
            if (!Directory.Exists(rootPath)) Directory.CreateDirectory(rootPath);

            string path = Path.Combine(rootPath, fileName);
            File.WriteAllBytes(path, bytes);
        }

        private string GetRootPath()
        {
            string dataPath = Application.dataPath;
            DirectoryInfo parentDir = Directory.GetParent(dataPath);
            string rootPath = parentDir != null ? parentDir.FullName : dataPath;
            
            string dateFolder = DateTime.Now.ToString("yyyy-MM-dd");
            
            return Path.Combine(rootPath, saveFolderName, dateFolder);
        }
    }
}