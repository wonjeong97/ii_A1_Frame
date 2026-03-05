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
    /// 엔딩 씬의 마지막 페이지 컨트롤러입니다.
    /// 게임 초반에 조회해 둔 카트리지 완료 상태를 확인하고 연출을 분기합니다.
    /// </summary>
    public class EndingPage4Controller : GamePage<EndingPage4Data>
    {
        [Header("UI References")] 
        [SerializeField] private Text descriptionText;
        [SerializeField] private Image redLineImage;

        private bool _isAllFinished;
        private bool _hasSentEndTime; // 중복 호출 방지 플래그
        
        protected override void SetupData(EndingPage4Data data)
        {
            _isAllFinished = false;

            if (GameManager.Instance)
            {
                // 게임 시작 시 백그라운드로 조회해둔 값을 사용
                _isAllFinished = GameManager.Instance.IsOtherCartridgeContentsCleared;
            }

            TextSetting textToUse = data.descriptionText;

            // 모두 클리어했다면 특별 엔딩 텍스트 적용
            if (_isAllFinished && data.allFinishedText != null)
            {
                textToUse = data.allFinishedText;
            }

            if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, textToUse);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            
            SetAlpha(0f);

            if (redLineImage)
            {
                redLineImage.type = Image.Type.Filled;
                redLineImage.fillAmount = 0f;
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

        private IEnumerator SequenceRoutine()
        {   
            yield return CoroutineData.GetWaitForSeconds(1.0f);
            
            if (_isAllFinished && redLineImage)
            {
                yield return StartCoroutine(FillImageRoutine(redLineImage, 0f, 1f, 2.0f));
                SoundManager.Instance?.FadeOutBGM(5.0f);
                yield return CoroutineData.GetWaitForSeconds(5.0f);
            }
            else
            {   
                yield return CoroutineData.GetWaitForSeconds(2.0f);
                SoundManager.Instance?.FadeOutBGM(5.0f);
                yield return CoroutineData.GetWaitForSeconds(5.0f);
            }
            
            CompleteStep();
        }

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