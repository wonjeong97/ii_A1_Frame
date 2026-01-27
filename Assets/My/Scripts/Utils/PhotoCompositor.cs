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
        public string outputFileName = "Final_Composite";

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

            Debug.Log($"[PhotoCompositor] 합성 시작 (좌상단 앵커 & 피벗)...");

            // 1. 렌더 텍스처 준비
            RenderTexture rt = RenderTexture.GetTemporary(baseFrame.width, baseFrame.height, 0, RenderTextureFormat.ARGB32);
            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = rt;

            // 2. GL 매트릭스 설정 (좌측 상단 0,0 기준)
            GL.PushMatrix();
            // LoadPixelMatrix(left, right, bottom, top)
            // (0, width, height, 0) -> 0이 Top, height가 Bottom이 되므로 Y축이 아래로 증가함
            GL.LoadPixelMatrix(0, baseFrame.width, baseFrame.height, 0);

            // 3. 배경 그리기
            Graphics.DrawTexture(new Rect(0, 0, baseFrame.width, baseFrame.height), baseFrame);

            // 4. 사진 합성
            string rootPath = GetRootPath();
            foreach (var slot in slots)
            {
                string targetPath = Path.Combine(rootPath, $"{baseName}{slot.fileSuffix}.png");
                
                if (File.Exists(targetPath))
                {
                    Texture2D photoTex = LoadTextureFromFile(targetPath);
                    if (photoTex != null)
                    {
                        // 크기 계산
                        float w = photoTex.width * slot.scale.x;
                        float h = photoTex.height * slot.scale.y;

                        // [핵심] 위치 계산 (좌상단 피벗)
                        // GL 좌표계가 이미 좌상단(0,0)이므로,
                        // Rect의 시작점(x,y)가 곧 이미지의 좌상단 모서리(Pivot)가 됩니다.
                        Rect drawRect = new Rect(slot.position.x, -slot.position.y, w, h);
                        
                        Graphics.DrawTexture(drawRect, photoTex);
                        
                        Destroy(photoTex);
                    }
                }
            }

            GL.PopMatrix();

            // 5. 저장
            Texture2D resultTex = new Texture2D(baseFrame.width, baseFrame.height, TextureFormat.RGB24, false);
            resultTex.ReadPixels(new Rect(0, 0, baseFrame.width, baseFrame.height), 0, 0);
            resultTex.Apply();

            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);

            SaveToFile(resultTex, $"{baseName}_{outputFileName}.png");
            Destroy(resultTex);
            
            Debug.Log("[PhotoCompositor] 저장 완료");
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

        private void SaveToFile(Texture2D tex, string fileName)
        {
            string folder = GetRootPath();
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            string path = Path.Combine(folder, fileName);
            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
        }

        private string GetRootPath()
        {
            string dataPath = Application.dataPath;
            DirectoryInfo parentDir = Directory.GetParent(dataPath);
            string rootPath = (parentDir != null) ? parentDir.FullName : dataPath;
            return Path.Combine(rootPath, saveFolderName);
        }
    }
}