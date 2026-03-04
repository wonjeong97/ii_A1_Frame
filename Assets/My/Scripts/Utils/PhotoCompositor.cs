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
            if (baseFrame == null)
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

            // 5. 사진 합성 (해당 날짜 폴더에서 파일 로드)
            foreach (var slot in slots)
            {
                // 원본 소스 파일은 PNG이므로 확장자 유지
                string targetPath = Path.Combine(rootPath, $"{sanitizedName}{slot.fileSuffix}.png");
                
                if (File.Exists(targetPath))
                {
                    Texture2D photoTex = LoadTextureFromFile(targetPath);
                    if (photoTex != null)
                    {
                        // 크기 계산
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

            // 7. JPG 인코딩 (Quality 85) - 로컬 저장과 업로드에 공통 사용
            byte[] jpgBytes = resultTex.EncodeToJPG(85);
            string finalFileName = $"{sanitizedName}_{outputFileName}.jpg";

            // 8. 로컬 저장 (JPG 원본 보존)
            SaveToFile(jpgBytes, finalFileName, rootPath);

            // 9. 서버 업로드 진행
            StartCoroutine(UploadImageRoutine(jpgBytes, finalFileName));

            // 결과 텍스처 메모리 해제
            Destroy(resultTex);

            Debug.Log($"[PhotoCompositor] 합성 완료 및 로컬 저장됨: {Path.Combine(rootPath, finalFileName)}");
        }

        /// <summary>
        /// 합성된 사진 데이터를 UploadHandlerRaw를 사용하여 서버로 전송하는 코루틴
        /// </summary>
        private IEnumerator UploadImageRoutine(byte[] imageBytes, string fileName)
        {
            int idxUser = 0;
            string uid = "";
            string baseUrl = "";

            if (GameManager.Instance)
            {
                idxUser = GameManager.Instance.CurrentUserId;
                uid = GameManager.Instance.PlayerAUid; 
                
                if (GameManager.Instance.ApiConfig != null)
                {
                    baseUrl = GameManager.Instance.ApiConfig.UploadFileUrl;
                }
            }

            // API.json 설정을 불러오지 못한 경우 방어 로직
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
            
            string url = $"{baseUrl}?idx_user={idxUser}&uid={uid}&code=a1&type=jpg";
            Debug.Log("[PhotoCompositor] 사진 업로드 시도 중...");

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
                    Debug.Log($"[PhotoCompositor] 업로드 성공! 서버 응답: {responseJson}");
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