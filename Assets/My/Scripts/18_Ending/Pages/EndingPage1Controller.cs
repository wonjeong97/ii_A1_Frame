using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using My.Scripts.Core;    
using My.Scripts.Global;  
using Wonjeong.Data;
using Wonjeong.UI;   

namespace My.Scripts._18_Ending.Pages
{
    [Serializable]
    public class EndingPage1Data
    {
        public TextSetting descriptionText;
    }

    public class EndingPage1Controller : GamePage
    {
        [Header("UI References")]
        [SerializeField] private RawImage videoDisplay; 
        [SerializeField] private VideoPlayer videoPlayer; 
        [SerializeField] private Text descriptionText;

        [Header("Settings")]
        [SerializeField] private string videoFolderName = "Timelapse"; 
        [SerializeField] private string videoFileName = "Final_Timelapse.mp4";

        public override void SetupData(object data) 
        {
            var pageData = data as EndingPage1Data;
            if (pageData == null) return;

            if (descriptionText) 
            {
                UIManager.Instance.SetText(descriptionText.gameObject, pageData.descriptionText);
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            
            StartCoroutine(PlayVideoRoutine());
        }

        public override void OnExit()
        {
            base.OnExit();
            StopAllCoroutines();
        }

        private IEnumerator PlayVideoRoutine()
        {
            // 1. 경로 설정
            string dataPath = Application.dataPath;
            DirectoryInfo parentDir = Directory.GetParent(dataPath);
            string rootPath = (parentDir != null) ? parentDir.FullName : dataPath;
            
            string folderPath = Path.Combine(rootPath, videoFolderName);
            string filePath = Path.Combine(folderPath, videoFileName);

            // 2. FFmpeg 변환 상태 확인
            if (TimeLapseRecorder.Instance != null)
            {
                // 변환 작업 중이면 대기
                while (TimeLapseRecorder.Instance.IsProcessing)
                {
                    Debug.Log("[EndingPage1] FFmpeg 변환 중... 잠시만 기다려주세요.");
                    yield return new WaitForSeconds(0.5f);
                }

                // 변환 실패 시 중단
                if (!TimeLapseRecorder.Instance.IsConversionSuccessful)
                {
                    Debug.LogError($"[EndingPage1] FFmpeg 변환 실패 (Code: {TimeLapseRecorder.Instance.LastExitCode})");
                    yield break; 
                }
            }
            else
            {
                Debug.LogWarning("[EndingPage1] TimeLapseRecorder가 없습니다. 파일 유무만 체크합니다.");
            }

            // 3. 파일 시스템 동기화 대기 (최대 5초)
            // 프로세스는 끝났어도 파일이 OS 상에서 바로 인식되지 않을 수 있음
            float waitTime = 0f;
            while (!File.Exists(filePath) && waitTime < 5.0f)
            {
                Debug.Log($"[EndingPage1] 파일 시스템 동기화 대기 중...");
                yield return new WaitForSeconds(0.2f);
                waitTime += 0.2f;
            }

            // 4. 영상 재생
            if (File.Exists(filePath))
            {
                Debug.Log($"[EndingPage1] 영상 로드 시작: {filePath}");

                videoPlayer.source = VideoSource.Url;
                videoPlayer.url = "file://" + filePath; 
                videoPlayer.renderMode = VideoRenderMode.APIOnly; 
                videoPlayer.Prepare();

                // 비디오 준비 대기
                while (!videoPlayer.isPrepared)
                {
                    yield return null;
                }

                // 텍스처 연결 및 재생
                if (videoPlayer.texture != null)
                {
                    videoDisplay.texture = videoPlayer.texture;
                    videoPlayer.Play();
                }
            }
            else
            {
                Debug.LogError($"[EndingPage1] 영상 파일을 찾을 수 없습니다: {filePath}");
            }
        }
        
        // 다시 하기 버튼 연결용
        public void OnRestartBtnClick()
        {
            if(GameManager.Instance != null)
            {
                GameManager.Instance.ChangeScene(GameConstants.Scene.Title);
            }
        }
    }
}