using System;
using System.Collections;
using System.IO;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._18_Ending.Pages
{
    [Serializable]
    public class EndingPage1Data
    {
        public TextSetting descriptionText; // 설명 텍스트 데이터
    }

    /// <summary> 엔딩 1페이지 컨트롤러 (타임랩스 영상 재생) </summary>
    public class EndingPage1Controller : GamePage<EndingPage1Data>
    {
        [Header("UI References")]
        [SerializeField] private RawImage videoDisplay; // 영상 출력용 RawImage
        [SerializeField] private VideoPlayer videoPlayer; // 비디오 재생 컴포넌트
        [SerializeField] private Text descriptionText; // 하단 설명 텍스트

        [Header("Settings")]
        [SerializeField] private string videoFolderName = "Timelapse"; // 영상 저장 폴더명
        [SerializeField] private string videoFileName = "Final_Timelapse.mp4"; // 재생할 파일명

        /// <summary> 데이터 설정 (텍스트 적용) </summary>
        protected override void SetupData(EndingPage1Data data)
        {
            if (descriptionText) 
            {
                UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
            }
        }

        /// <summary> 페이지 진입 (초기화 및 시퀀스 시작) </summary>
        public override void OnEnter()
        {
            base.OnEnter(); // 기본 활성화
            
            // UI 요소 투명화 (연출용)
            SetTextAlpha(descriptionText, 0f);
            SetImageAlpha(videoDisplay, 0f);

            // 재생 시퀀스 시작
            StartCoroutine(PresentationRoutine());
        }

        /// <summary> 페이지 퇴장 (코루틴 정지) </summary>
        public override void OnExit()
        {
            base.OnExit();
            StopAllCoroutines();
        }

        /// <summary> 영상 재생 및 연출 시퀀스 </summary>
        private IEnumerator PresentationRoutine()
        {   
            if (videoPlayer == null || videoDisplay == null)
            {
                Debug.LogError("[EndingPage1] VideoPlayer 또는 VideoDisplay 미할당");
                CompleteStep();
                yield break;
            }
            
            // 1. 설명 텍스트 페이드 인
            yield return StartCoroutine(FadeText(descriptionText, 0f, 1f, 1.0f));
            
            // 2. 영상 파일 경로 확인
            string filePath = GetVideoPath();
            
            // 2-1. FFmpeg 변환 대기 (레코더 연동)
            if (TimeLapseRecorder.Instance != null)
            {
                bool didWaitForProcessing = false;
                float processingTimeout = 60f;
                float processingWait = 0f;
                
                // 변환 중이면 대기
                while (TimeLapseRecorder.Instance.IsProcessing && processingWait < processingTimeout)
                {
                    didWaitForProcessing = true;
                    Debug.Log("[EndingPage1] FFmpeg 변환 중...");
                    yield return new WaitForSeconds(0.5f);
                    processingWait += 0.5f;
                }
                
                // 변환 실패 시 종료
                if (didWaitForProcessing && !TimeLapseRecorder.Instance.IsConversionSuccessful)
                {
                    Debug.LogError($"[EndingPage1] 변환 실패로 종료");
                    CompleteStep();
                    yield break; 
                }
            }

            // 2-2. 파일 생성 대기 (최대 5초)
            float waitTime = 0f;
            while (!File.Exists(filePath) && waitTime < 5.0f)
            {
                yield return new WaitForSeconds(0.2f);
                waitTime += 0.2f;
            }

            // 파일 없음 체크
            if (!File.Exists(filePath))
            {
                Debug.LogError($"[EndingPage1] 파일 없음: {filePath}");
                CompleteStep();
                yield break;
            }

            // 2-3. VideoPlayer 설정 및 준비
            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = new Uri(filePath).AbsoluteUri; 
            videoPlayer.renderMode = VideoRenderMode.APIOnly; 
            videoPlayer.isLooping = false; 
            videoPlayer.Prepare();

            // 준비 완료 대기 (최대 10초)
            float prepareWait = 0f;
            while (!videoPlayer.isPrepared && prepareWait < 10f)
            {
                yield return null;
                prepareWait += Time.deltaTime;
            }

            if (!videoPlayer.isPrepared)
            {
                Debug.LogError("[EndingPage1] VideoPlayer 준비 실패");
                CompleteStep();
                yield break;
            }

            // 텍스처 연결
            videoDisplay.texture = videoPlayer.texture;

            // 3. 재생 및 화면 페이드 인
            videoPlayer.Play();
            yield return StartCoroutine(FadeRawImage(videoDisplay, 0f, 1f, 1.0f));

            // 4. 영상 길이만큼 대기
            double duration = videoPlayer.length;
            if (duration > 0)
            {
                yield return new WaitForSeconds((float)duration);
            }
            
            // 재생 종료 확실히 대기
            float safetyWait = 0f;
            while (videoPlayer.isPlaying && safetyWait < 2.0f)
            {
                yield return null;
                safetyWait += Time.deltaTime;
            }
            
            CompleteStep(); // 단계 완료
        }

        /// <summary> 영상 파일 전체 경로 반환 </summary>
        private string GetVideoPath()
        {
            string dataPath = Application.dataPath;
            DirectoryInfo parentDir = Directory.GetParent(dataPath);
            string rootPath = (parentDir != null) ? parentDir.FullName : dataPath;
            return Path.Combine(rootPath, videoFolderName, videoFileName);
        }

        /// <summary> 텍스트 투명도 페이드 코루틴 </summary>
        private IEnumerator FadeText(Text target, float start, float end, float duration)
        {
            if (!target) yield break;
            float t = 0f;
            SetTextAlpha(target, start);
            while (t < duration)
            {
                t += Time.deltaTime;
                SetTextAlpha(target, Mathf.Lerp(start, end, t / duration));
                yield return null;
            }
            SetTextAlpha(target, end);
        }

        /// <summary> RawImage 투명도 페이드 코루틴 </summary>
        private IEnumerator FadeRawImage(RawImage target, float start, float end, float duration)
        {
            if (!target) yield break;
            float t = 0f;
            SetImageAlpha(target, start);
            while (t < duration)
            {
                t += Time.deltaTime;
                SetImageAlpha(target, Mathf.Lerp(start, end, t / duration));
                yield return null;
            }
            SetImageAlpha(target, end);
        }

        /// <summary> 페이지 전체 투명도 페이드 코루틴 </summary>
        private IEnumerator FadePageAlpha(float start, float end, float duration)
        {
            float t = 0f;
            SetAlpha(start);
            while (t < duration)
            {
                t += Time.deltaTime;
                SetAlpha(Mathf.Lerp(start, end, t / duration));
                yield return null;
            }
            SetAlpha(end);
        }

        /// <summary> 텍스트 알파값 즉시 설정 </summary>
        private void SetTextAlpha(Text t, float a)
        {
            if (t)
            {
                Color c = t.color;
                c.a = a;
                t.color = c;
            }
        }

        /// <summary> 이미지 알파값 즉시 설정 </summary>
        private void SetImageAlpha(RawImage i, float a)
        {
            if (i)
            {
                Color c = i.color;
                c.a = a;
                i.color = c;
            }
        }
    }
}