using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using My.Scripts.Global;
using Cysharp.Threading.Tasks;
using Cysharp.Text; 
using Microsoft.Extensions.Logging; 
using ZLogger; 
using VContainer; 

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

        private readonly static string InvalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
        private readonly static Regex InvalidFileRegex = new Regex($@"([{InvalidChars}]*\.+$)|([{InvalidChars}]+)", RegexOptions.Compiled);

        // --- 의존성 주입 (DI) 변수 ---
        private ILogger<PhotoCompositor> _logger;
        private SessionManager _sessionManager;
        private GameManager _gameManager;

        [Inject]
        public void Construct(
            ILogger<PhotoCompositor> logger,
            SessionManager sessionManager,
            GameManager gameManager)
        {
            _logger = logger;
            _sessionManager = sessionManager;
            _gameManager = gameManager;
        }

        [ContextMenu("Execute Composite Now")]
        public void DebugProcessAndSave()
        {
            ProcessAndSave(debugBaseName, true);
        }

        public void ProcessAndSave(string baseName, bool isDebug = false)
        {
            if (!baseFrame)
            {
                _logger.ZLogError($"[PhotoCompositor] 배경 이미지 누락");
                return;
            }

            IsProcessing = true;

            string safeBaseName = string.IsNullOrEmpty(baseName) ? "" : baseName;
            string clean = safeBaseName.Replace("\n", "").Replace("\r", "").Trim();
            
            string sanitizedName = InvalidFileRegex.Replace(clean, "");

            if (string.IsNullOrWhiteSpace(sanitizedName))
            {
                sanitizedName = "UnknownPlayers";
            }

            ExecuteCompositeAsync(sanitizedName, isDebug, this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTaskVoid ExecuteCompositeAsync(string sanitizedName, bool isDebug, CancellationToken ct)
        {
            Texture2D resultTex = null;

            try
            {
                string rootPath = GetRootPath();
                
                resultTex = CreateCompositeTexture(sanitizedName, rootPath);
                if (!resultTex) return;

                string finalFileName = ZString.Concat(sanitizedName, "_", outputFileName, ".png");
                string fullPath = Path.Combine(rootPath, finalFileName);

                byte[] pngBytes = await EncodeAndSaveTextureAsync(resultTex, fullPath, ct);

                if (!isDebug && pngBytes != null)
                {
                    await UploadImageAsync(pngBytes, finalFileName, ct);
                }
                else if (isDebug)
                {
                    _logger.ZLogInformation($"<color=cyan>[PhotoCompositor] 디버그 모드 완료: {finalFileName} 생성됨.</color>");
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                _logger.ZLogError(e, $"[PhotoCompositor] 합성 시퀀스 예외: {e.Message}");
            }
            finally
            {
                if (resultTex) Destroy(resultTex);
                IsProcessing = false;
            }
        }

        private Texture2D CreateCompositeTexture(string sanitizedName, string rootPath)
        {
            int targetWidth = Mathf.RoundToInt(baseFrame.width * baseFrameScale.x);
            int targetHeight = Mathf.RoundToInt(baseFrame.height * baseFrameScale.y);

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
            string targetPath = Path.Combine(rootPath, ZString.Concat(sanitizedName, slot.fileSuffix, ".png"));
            if (!File.Exists(targetPath)) return;

            Texture2D photoTex = LoadTextureFromFile(targetPath);
            if (!photoTex) return;

            float w = photoTex.width * slot.scale.x;
            float h = photoTex.height * slot.scale.y;
            Rect drawRect = new Rect(slot.position.x, -slot.position.y, w, h);

            Graphics.DrawTexture(drawRect, photoTex);
            Destroy(photoTex);
        }

        private async UniTask<byte[]> EncodeAndSaveTextureAsync(Texture2D tex, string savePath, CancellationToken ct)
        {
            byte[] rawData = tex.GetRawTextureData();
            uint width = (uint)tex.width;
            uint height = (uint)tex.height;
            UnityEngine.Experimental.Rendering.GraphicsFormat format = tex.graphicsFormat;

            await UniTask.SwitchToThreadPool();

            byte[] pngBytes = null;
            try
            {
                pngBytes = ImageConversion.EncodeArrayToPNG(rawData, format, width, height);

                if (pngBytes != null && pngBytes.Length > 0)
                {
                    string directory = Path.GetDirectoryName(savePath);
                    if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                    await File.WriteAllBytesAsync(savePath, pngBytes, ct);
                }
            }
            finally
            {
                await UniTask.SwitchToMainThread();
            }

            if (pngBytes == null || pngBytes.Length == 0)
            {
                _logger.ZLogError($"[PhotoCompositor] PNG 인코딩 실패");
                return null;
            }

            return pngBytes;
        }

        private async UniTask UploadImageAsync(byte[] imageBytes, string fileName, CancellationToken ct)
        {
            string url = ConstructUploadUrl();
            if (string.IsNullOrEmpty(url)) return;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                bool success = await ExecuteSingleUpload(url, imageBytes, ct);
                if (success) return;

                if (attempt < maxRetries - 1)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(retryDelay), cancellationToken: ct);
                }
            }
        }

        private string ConstructUploadUrl()
        {
            if (!_sessionManager || !_gameManager || _gameManager.ApiConfig == null)
                return null;

            int idxUser = _sessionManager.CurrentUserId;
            string uid = _sessionManager.PlayerAUid;
            string baseUrl = _gameManager.ApiConfig.UploadFileUrl;
            
            string moduleCode = string.IsNullOrEmpty(_sessionManager.CurrentModuleCode)
                ? GameConstants.Module.Code
                : _sessionManager.CurrentModuleCode.ToUpper();

            if (idxUser <= 0 || string.IsNullOrWhiteSpace(uid)) return null;

            string encodedUid = UnityWebRequest.EscapeURL(uid);
            int safeUploadCount = Mathf.Max(1, uploadCount);

            return ZString.Format("{0}?idx_user={1}&uid={2}&code={3}&type=png&count={4}",
                baseUrl, idxUser, encodedUid, moduleCode, safeUploadCount);
        }

        private async UniTask<bool> ExecuteSingleUpload(string url, byte[] imageBytes, CancellationToken ct)
        {
            using (UnityWebRequest webRequest = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                webRequest.uploadHandler = new UploadHandlerRaw(imageBytes) { contentType = "image/png" };
                webRequest.timeout = 15;

                try
                {
                    await webRequest.SendWebRequest().ToUniTask(cancellationToken: ct);
                    return webRequest.result == UnityWebRequest.Result.Success;
                }
                catch (OperationCanceledException) { return false; }
                catch (Exception e)
                {
                    _logger.ZLogWarning($"[PhotoCompositor] 업로드 통신 에러: {e.Message}");
                    return false;
                }
            }
        }

        private Texture2D LoadTextureFromFile(string path)
        {
            Texture2D tex = null;
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                
                tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(bytes);
                return tex;
            }
            catch (Exception e)
            {
                if (tex != null) Destroy(tex);
                _logger.ZLogWarning($"이미지 로드 실패 ({path}): {e.Message}");
                return null;
            }
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