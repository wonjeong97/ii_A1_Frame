using System;
using System.Collections;
using System.Text.RegularExpressions;
using My.Scripts.Core;
using My.Scripts.Global; 
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._18_Ending.Pages
{
    [Serializable]
    public class EndingPage3Data
    {
        public TextSetting descriptionText1; 
        public TextSetting descriptionText2; 
    }

    public class EndingPage3Controller : GamePage<EndingPage3Data>
    {
        [Header("UI References")]
        [SerializeField] private Text text1; 
        [SerializeField] private Text text2; 
        [SerializeField] private CanvasGroup imageCanvasGroup;
        [SerializeField] private CanvasGroup textCanvasGroup;

        // 나중에 OnEnter에서 텍스트를 교체하기 위해 데이터를 들고 있습니다.
        private EndingPage3Data _data; 
        
        // 중복 전송 방지용 가드 변수
        private bool _hasSentPieceUpdate;

        protected override void SetupData(EndingPage3Data data)
        {
            _data = data;
            
            // 이 시점에서는 폰트, 크기, 색상 등의 '스타일'만 미리 입혀둡니다.
            if (text1) UIManager.Instance.SetText(text1.gameObject, data.descriptionText1);
            if (text2) UIManager.Instance.SetText(text2.gameObject, data.descriptionText2);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            
            if (textCanvasGroup) textCanvasGroup.alpha = 0;
            if (imageCanvasGroup) imageCanvasGroup.alpha = 0f;

            // 3페이지가 화면에 등장할 때 조각 개수를 계산합니다! (API 로딩 대기 완료)
            UpdateTotalPiecesText();

            // [수정] _hasSentPieceUpdate가 false일 때만 서버 업데이트 호출
            if (GameManager.Instance && !_hasSentPieceUpdate)
            {
                GameManager.Instance.SendPieceUpdateAPI(5);
                _hasSentPieceUpdate = true; // 호출 후 true로 변경하여 중복 방지
            }
            
            StartCoroutine(SequenceRoutine());
        }

        /// <summary>
        /// API 통신이 끝난 시점에서 최종 마음 조각 개수를 계산하여 화면 텍스트에 적용합니다.
        /// </summary>
        private void UpdateTotalPiecesText()
        {
            if (text2 && _data?.descriptionText2 != null)
            {
                if (!GameManager.Instance) return;
                int existingPieces = GameManager.Instance.TotalPieces;
                int totalPieces = existingPieces + 5; // 기존 조각 + 이번 판 5개
                Debug.Log($"[EndingPage3] 텍스트 갱신 완료! (API 로드: {existingPieces}개 + 추가 5개 = 총 {totalPieces}개)");

                string originalText = _data.descriptionText2.text;
                if (!string.IsNullOrEmpty(originalText))
                {
                    text2.text = originalText.Replace("{0}", totalPieces.ToString());
                }
            }
        }

        private IEnumerator SequenceRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(0.5f);
            SoundManager.Instance?.PlaySFX("공통_6");
            yield return StartCoroutine(FadeCanvasGroup(imageCanvasGroup, 0f, 1f, 1.0f));
            yield return CoroutineData.GetWaitForSeconds(2.0f);
            yield return StartCoroutine(FadeCanvasGroup(textCanvasGroup, 0f, 1f, 1.0f));
            yield return CoroutineData.GetWaitForSeconds(3.0f);
            
            CompleteStep();
        }

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