using System;
using System.Collections;
using My.Scripts.Core;
using My.Scripts.Global; 
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._18_Ending.Pages
{
    /// <summary> 엔딩 4페이지용 데이터 구조체 </summary>
    [Serializable]
    public class EndingPage4Data
    {
        public TextSetting descriptionText1; 
        public TextSetting descriptionText2; 
    }

    /// <summary>
    /// 엔딩 4페이지 컨트롤러.
    /// 플레이 보상(마음 조각)을 서버에 갱신하고, 누적된 전체 조각 개수를 시각적으로 보여줍니다.
    /// </summary>
    public class EndingPage4Controller : GamePage<EndingPage4Data>
    {
        [Header("UI References")]
        [SerializeField] private Text text1; 
        [SerializeField] private Text text2; 
        [SerializeField] private CanvasGroup imageCanvasGroup;
        [SerializeField] private CanvasGroup textCanvasGroup;
        
        private const int PagePieceReward = 5; // 엔딩 도달 시 부여하는 고정 보상
        private EndingPage4Data _data; 
        private bool _hasSentPieceUpdate;

        /// <summary> JSON 설정 로드 및 UI 텍스트 초기화 </summary>
        protected override void SetupData(EndingPage4Data data)
        {
            _data = data;
            if (text1 && UIManager.Instance) UIManager.Instance.SetText(text1.gameObject, data.descriptionText1);
            if (text2 && UIManager.Instance) UIManager.Instance.SetText(text2.gameObject, data.descriptionText2);
        }

        /// <summary> 페이지 진입 시 보상 API 전송, 텍스트 갱신 및 페이드 시퀀스 시작 </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            
            // 연출 전 UI 숨김 처리
            if (textCanvasGroup) textCanvasGroup.alpha = 0;
            if (imageCanvasGroup) imageCanvasGroup.alpha = 0f;
            
            // 중복 지급 방지 및 유효 세션 확인 후 보상 데이터 서버 전송
            if (GameManager.Instance && SessionManager.Instance && SessionManager.Instance.CurrentUserId != 0 && !_hasSentPieceUpdate)
            {
                GameManager.Instance.SendPieceUpdateAPI(PagePieceReward);
                _hasSentPieceUpdate = true; 
            }

            UpdateTotalPiecesText();
            
            StartCoroutine(SequenceRoutine());
        }

        /// <summary> 기존 보유량과 현재 보상을 합산하여 UI에 표시 </summary>
        private void UpdateTotalPiecesText()
        {
            if (text2 && _data?.descriptionText2 != null)
            {
                if (!GameManager.Instance || !SessionManager.Instance) return;
                
                int existingPieces = SessionManager.Instance.TotalPieces;
                int pendingReward = _hasSentPieceUpdate ? PagePieceReward : 0;
                
                // 기존 10개 + 보상 5개 = 결과 15
                int totalPieces = existingPieces + pendingReward;

                string originalText = _data.descriptionText2.text;
                if (!string.IsNullOrEmpty(originalText))
                {
                    // 템플릿의 {0} 위치를 계산된 총합으로 치환
                    text2.text = originalText.Replace("{0}", totalPieces.ToString());
                }
            }
        }

        /// <summary> 사운드 출력 및 이미지 -> 텍스트 순서의 페이드인 연출 제어 </summary>
        private IEnumerator SequenceRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(0.5f);
            
            SoundManager.Instance?.PlaySFX("공통_6"); // 획득 강조 효과음
            
            // 이미지 그룹 등장
            yield return StartCoroutine(FadeCanvasGroup(imageCanvasGroup, 0f, 1f, 1.0f));
            yield return CoroutineData.GetWaitForSeconds(2.0f);
            
            // 텍스트 그룹 등장
            yield return StartCoroutine(FadeCanvasGroup(textCanvasGroup, 0f, 1f, 1.0f));
            yield return CoroutineData.GetWaitForSeconds(3.0f);

            CompleteStep();
        }

        /// <summary> CanvasGroup의 알파값을 선형 보간하여 시각적 투명도 조절 </summary>
        private IEnumerator FadeCanvasGroup(CanvasGroup cg, float s, float e, float d)
        {
            if (!cg) yield break;
            float time = 0f;
            cg.alpha = s;
            
            while(time < d) 
            { 
                time += Time.deltaTime; 
                cg.alpha = Mathf.Lerp(s, e, time/d); 
                yield return null; 
            }
            cg.alpha = e;
        }
    }
}