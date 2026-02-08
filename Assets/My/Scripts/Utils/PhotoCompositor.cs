using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

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

            // 1. 경로 설정 (날짜 폴더 포함) - 한 번만 계산하여 로드/저장에 동일하게 사용
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
                // rootPath에 이미 날짜 폴더가 포함되어 있음
                string targetPath = Path.Combine(rootPath, $"{baseName}{slot.fileSuffix}.png");
                
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
                else
                {
                    // Debug.Log($"[PhotoCompositor] 파일 없음(건너뜀): {targetPath}");
                }
            }

            GL.PopMatrix();

            // 6. 결과 텍스처 생성
            Texture2D resultTex = new Texture2D(baseFrame.width, baseFrame.height, TextureFormat.RGB24, false);
            resultTex.ReadPixels(new Rect(0, 0, baseFrame.width, baseFrame.height), 0, 0);
            resultTex.Apply();

            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);

            // 7. 저장 (동일한 rootPath 사용)
            // [수정] rootPath를 인자로 전달하여 경로 불일치 방지
            SaveToFile(resultTex, $"{baseName}_{outputFileName}.png", rootPath);
            Destroy(resultTex);

            Debug.Log($"[PhotoCompositor] 합성 완료 및 저장됨: {Path.Combine(rootPath, $"{baseName}_{outputFileName}.png")}");
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

        // rootPath를 인자로 받도록 시그니처 변경 및 내부 GetRootPath 호출 제거
        private void SaveToFile(Texture2D tex, string fileName, string rootPath)
        {
            // 폴더가 없으면 생성
            if (!Directory.Exists(rootPath)) Directory.CreateDirectory(rootPath);

            string path = Path.Combine(rootPath, fileName);
            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
        }

        /// <summary> 루트 경로 반환 (날짜 폴더 포함) </summary>
        private string GetRootPath()
        {
            string dataPath = Application.dataPath;
            DirectoryInfo parentDir = Directory.GetParent(dataPath);
            string rootPath = (parentDir != null) ? parentDir.FullName : dataPath;
            
            // 날짜 폴더 추가 (예: Pictures/2026-02-06)
            string dateFolder = DateTime.Now.ToString("yyyy-MM-dd");
            
            return Path.Combine(rootPath, saveFolderName, dateFolder);
        }
    }
}