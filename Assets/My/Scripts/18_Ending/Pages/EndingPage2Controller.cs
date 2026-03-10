using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core;
using My.Scripts.Timelapse;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._18_Ending.Pages
{   
    /// <summary> 엔딩 2페이지용 데이터 구조체 </summary>
    [Serializable]
    public class EndingPage2Data
    {
        public TextSetting descriptionText; 
    }
    
    public class EndingPage2Controller : GamePage<EndingPage2Data>
    {
        [Header("UI References")]
        [SerializeField] private Text descriptionText; 
        [SerializeField] private Image loadingFillImage; 

        [Header("Sound Settings")]
        [SerializeField] private float loadingSoundInterval = 7.0f;
        [Header("Timeout Settings")]
        [SerializeField] private float conversionTimeout = 40.0f;
        
        private Coroutine _loadingSoundRoutine;

        protected override void SetupData(EndingPage2Data data)
        {
            if (descriptionText) 
            {
                UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
                SetTextAlpha(0f); 
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            if (loadingFillImage) loadingFillImage.fillAmount = 0f;
            StartCoroutine(FadeText(0f, 1f, 1.0f));
            StartCoroutine(ProcessRealtimeVideoRoutine());
        }

        private IEnumerator ProcessRealtimeVideoRoutine()
        {
            if (!TimeLapseRecorder.Instance)
            {
                if (loadingFillImage) loadingFillImage.fillAmount = 1f;
                yield return CoroutineData.GetWaitForSeconds(2.0f);
                CompleteStep();
                yield break;
            }

            yield return CoroutineData.GetWaitForSeconds(0.5f); 

            if (!TimeLapseRecorder.Instance.IsRealtimeProcessing && string.IsNullOrEmpty(TimeLapseRecorder.Instance.LastRealtimeVideoPath))
                TimeLapseRecorder.Instance.ConvertToRealtimeVideo();

            if (_loadingSoundRoutine != null) StopCoroutine(_loadingSoundRoutine);
            _loadingSoundRoutine = StartCoroutine(LoadingSoundLoopRoutine());

            float startWaitTime = Time.time;
            while (TimeLapseRecorder.Instance.IsRealtimeProcessing)
            {
                if (Time.time - startWaitTime > conversionTimeout) break;
                if (loadingFillImage)
                    loadingFillImage.fillAmount = Mathf.Lerp(loadingFillImage.fillAmount, TimeLapseRecorder.Instance.RealtimeProgress, Time.deltaTime * 5f);
                yield return null;
            }

            if (_loadingSoundRoutine != null) { StopCoroutine(_loadingSoundRoutine); _loadingSoundRoutine = null; }
            if (loadingFillImage) loadingFillImage.fillAmount = 1f;
            yield return CoroutineData.GetWaitForSeconds(1.5f);
            CompleteStep();
        }
        
        private IEnumerator LoadingSoundLoopRoutine()
        {
            while (true)
            {
                if (SoundManager.Instance) SoundManager.Instance.PlaySFX("키오스크_3");
                yield return CoroutineData.GetWaitForSeconds(loadingSoundInterval);
            }
        }

        public override void OnExit()
        {
            if (_loadingSoundRoutine != null) { StopCoroutine(_loadingSoundRoutine); _loadingSoundRoutine = null; }
            base.OnExit();
            
            // # FIX: 리얼타임 변환이 끝난 경우에만 타임랩스 변환 시작
            if (TimeLapseRecorder.Instance && !TimeLapseRecorder.Instance.IsRealtimeProcessing)
            {
                TimeLapseRecorder.Instance.ConvertToVideo();
            }
        }

        private IEnumerator FadeText(float start, float end, float duration)
        {
            if (!descriptionText) yield break;
            float t = 0f;
            SetTextAlpha(start);
            while (t < duration)
            {
                t += Time.deltaTime;
                SetTextAlpha(Mathf.Lerp(start, end, t / duration));
                yield return null;
            }
            SetTextAlpha(end);
        }

        private void SetTextAlpha(float alpha)
        {
            if (descriptionText)
            {
                Color c = descriptionText.color;
                c.a = alpha;
                descriptionText.color = c;
            }
        }
    }
}