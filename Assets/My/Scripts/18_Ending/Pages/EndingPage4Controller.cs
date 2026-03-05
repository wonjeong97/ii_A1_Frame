using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core;
using My.Scripts.Global;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._18_Ending.Pages
{
    [Serializable]
    public class EndingPage4Data
    {
        public TextSetting descriptionText; // 일반 엔딩 텍스트
        public TextSetting allFinishedText; // 특별 엔딩 텍스트
    }

    /// <summary>
    /// 엔딩 씬의 마지막 페이지 컨트롤러.
    /// 카트리지 내 다른 콘텐츠의 완료 여부에 따라 특별 엔딩 분기 처리.
    /// </summary>
    public class EndingPage4Controller : GamePage<EndingPage4Data>
    {
        [Header("UI References")] 
        [SerializeField] private Text descriptionText;
        [SerializeField] private Image redLineImage;

        private bool _isAllFinished;
        private bool _hasSentEndTime;
        
        private EndingPage4Data _data;

        /// <summary>
        /// 페이지 데이터 캐싱.
        /// 백그라운드 API 통신 지연을 고려하여 텍스트 갱신은 OnEnter로 지연 처리함.
        /// </summary>
        /// <param name="data">설정할 페이지 데이터</param>
        protected override void SetupData(EndingPage4Data data)
        {
            _data = data;
        }

        /// <summary>
        /// 페이지 진입 시 연출 시작 및 최신 데이터 갱신.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            
            SetAlpha(0f);

            if (redLineImage)
            {
                redLineImage.type = Image.Type.Filled;
                redLineImage.fillAmount = 0f;
            }

            _isAllFinished = false;

            if (GameManager.Instance)
            {
                // 페이지 진입 시점의 최신 상태값을 평가
                _isAllFinished = GameManager.Instance.IsOtherCartridgeContentsCleared;
            }

            if (_data != null)
            {
                TextSetting textToUse = _data.descriptionText;

                if (_isAllFinished && _data.allFinishedText != null)
                {
                    textToUse = _data.allFinishedText;
                }

                if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, textToUse);
            }
            else
            {
                Debug.LogWarning("[EndingPage4Controller] _data 값이 null입니다.");
            }

            // 4페이지 진입 시 종료 시간 및 퇴장 업데이트
            if (!_hasSentEndTime && GameManager.Instance)
            {
                if (GameManager.Instance.CurrentUserId == 0)
                {
                    Debug.LogWarning("[EndingPage4] CurrentUserId가 없어 API 전송을 보류합니다.");
                }
                else
                {
                    Debug.Log("[EndingPage4] OnEnter: 종료(end) 시간 및 퇴장(exitRoom) 업데이트 호출");
                    GameManager.Instance.SendTimeUpdateAPI();
                    GameManager.Instance.SendExitRoomAPI();
                    _hasSentEndTime = true;
                }
            }

            StartCoroutine(SequenceRoutine());
        }

        /// <summary>
        /// 분기된 엔딩 타입에 따른 시퀀스 연출 루틴.
        /// </summary>
        private IEnumerator SequenceRoutine()
        {   
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            
            if (_isAllFinished && redLineImage)
            {
                yield return StartCoroutine(FillImageRoutine(redLineImage, 0f, 1f, 2.0f));
                if (SoundManager.Instance) SoundManager.Instance.FadeOutBGM(5.0f);
                yield return CoroutineData.GetWaitForSeconds(5.0f);
            }
            else
            {   
                yield return CoroutineData.GetWaitForSeconds(2.0f);
                if (SoundManager.Instance) SoundManager.Instance.FadeOutBGM(5.0f);
                yield return CoroutineData.GetWaitForSeconds(5.0f);
            }
            
            CompleteStep();
        }

        /// <summary>
        /// 이미지의 FillAmount를 조절하는 페이드 연출.
        /// </summary>
        /// <param name="t">대상 이미지</param>
        /// <param name="s">시작 값</param>
        /// <param name="e">목표 값</param>
        /// <param name="d">진행 시간</param>
        private IEnumerator FillImageRoutine(Image t, float s, float e, float d)
        {
            if (!t) yield break;
            float time = 0f;
            t.fillAmount = s;
            
            while (time < d)
            {
                time += Time.deltaTime;
                t.fillAmount = Mathf.Lerp(s, e, time / d);
                yield return null;
            }

            t.fillAmount = e;
        }
    }
}