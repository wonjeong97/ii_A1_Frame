using System.Collections;
using My.Scripts.Core.Data;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts.Core.Pages
{
    /// <summary>  질문 및 답변 선택 페이지 컨트롤러 </summary>
    public class Page_QnA : PopupGamePage<QnAPageData>
    {
        [Header("UI References")] 
        [SerializeField] private Text descriptionText; // 설명 텍스트
        [SerializeField] private Text questionText; // 질문 텍스트
        [SerializeField] private Text[] answerTexts; // 답변 텍스트 배열

        [Header("Canvas Groups")] 
        [SerializeField] private CanvasGroup descriptionGroup; // 설명 그룹
        [SerializeField] private CanvasGroup questionGroup; // 질문 그룹
        [SerializeField] private CanvasGroup answerGroup; // 답변 그룹

        private Coroutine _sequenceRoutine; // 등장 연출 코루틴
        private bool _isCompleted; // 단계 완료 여부
        private bool _isInputEnabled; // 입력 허용 여부

        /// <summary>  데이터 설정: 텍스트 UI 적용 및 팝업 메시지 설정 </summary>
        protected override void SetupData(QnAPageData data)
        {
            if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
            if (questionText) UIManager.Instance.SetText(questionText.gameObject, data.questionText);

            if (answerTexts != null)
            {
                for (int i = 0; i < answerTexts.Length; i++)
                {
                    if (!answerTexts[i]) continue;
                    if (data.answerTexts != null && i < data.answerTexts.Length)
                    {
                        UIManager.Instance.SetText(answerTexts[i].gameObject, data.answerTexts[i]);
                        answerTexts[i].gameObject.SetActive(true);
                    }
                    else answerTexts[i].gameObject.SetActive(false);
                }
            }
            
            // 팝업 메시지 설정
            SetupPopupMessage(data.warningMessage, data.resetMessage);
        }

        /// <summary> 페이지 진입: 상태 초기화 및 등장 연출 시작 </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;
            _isInputEnabled = false;
            
            // 팝업 즉시 끄기 및 타이머 초기화
            ResetIdleState(true);

            // 그룹 투명도 초기화
            SetGroupAlpha(questionGroup, 0f);
            SetGroupAlpha(answerGroup, 0f);
            SetGroupAlpha(descriptionGroup, 0f);

            if (_sequenceRoutine != null) StopCoroutine(_sequenceRoutine);
            _sequenceRoutine = StartCoroutine(ShowSequence());
        }

        /// <summary>  매 프레임 업데이트: 입력 감지 및 비활성 체크 </summary>
        private void Update()
        {
            if (_isCompleted) return;

            // 1. 입력 감지
            if (Input.anyKey || Input.touchCount > 0)
            {
                // 입력 시 부드럽게 리셋 취소
                ResetIdleState(false);
                
                // 정답 선택 로직
                if (_isInputEnabled)
                {
                    HandleSelectionInput();
                }
            }
            else
            {
                // 2. 비활성 시간 누적 (부모 메서드)
                // 입력이 허용되지 않은 상태(_isInputEnabled == false)라면 타이머를 차단함
                UpdateInactivity(!_isInputEnabled);
            }
        }

        /// <summary>  플레이어의 키 입력(숫자키)에 따라 답변을 선택 처리 </summary>
        private void HandleSelectionInput()
        {
            // Player A (1~5)
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Alpha2) ||
                Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Alpha4) ||
                Input.GetKeyDown(KeyCode.Alpha5))
            {
                _isCompleted = true;
                CompleteStep(1); // 1: Player A 완료 정보
            }

            // Player B (6~0)
            if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Alpha7) ||
                Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Alpha9) ||
                Input.GetKeyDown(KeyCode.Alpha0))
            {
                _isCompleted = true;
                CompleteStep(2); // 2: Player B 완료 정보
            }
        }

        /// <summary>  페이지 진입 시 UI 요소들을 순차적으로 페이드 인 </summary>
        private IEnumerator ShowSequence()
        {
            // 페이지 전체 페이드 완료 대기
            if (canvasGroup) yield return new WaitUntil(() => canvasGroup.alpha >= 0.9f);
            
            SoundManager.Instance?.PlaySFX("공통_8");
            
            // 순차 등장 (FadeContent 사용)
            yield return StartCoroutine(FadeContent(questionGroup, 0f, 1f, 1f));
            yield return StartCoroutine(FadeContent(answerGroup, 0f, 1f, 1f));
            yield return StartCoroutine(FadeContent(descriptionGroup, 0f, 1f, 1f));
            
            _isInputEnabled = true;
        }

        /// <summary> 콘텐츠 CanvasGroup의 투명도를 조절하는 유틸리티 코루틴 </summary>
        private IEnumerator FadeContent(CanvasGroup cg, float start, float end, float duration)
        {
            if (!cg) yield break;
            if (end > 0f) cg.gameObject.SetActive(true);

            float t = 0f;
            cg.alpha = start;
            while (t < duration)
            {
                t += Time.deltaTime;
                cg.alpha = Mathf.Lerp(start, end, t / duration);
                yield return null;
            }
            cg.alpha = end;
            
            if (end <= 0f) cg.gameObject.SetActive(false);
        }

        /// <summary>  CanvasGroup의 투명도를 즉시 설정 </summary>
        private void SetGroupAlpha(CanvasGroup cg, float alpha)
        {
            if (cg)
            {
                cg.alpha = alpha;
                cg.gameObject.SetActive(alpha > 0f);
            }
        }
    }
}