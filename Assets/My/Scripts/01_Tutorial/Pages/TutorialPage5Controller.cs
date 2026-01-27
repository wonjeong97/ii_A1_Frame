using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._01_Tutorial.Pages
{
    [Serializable]
    public class TutorialPage5Data
    {
        [Header("Player A")]
        public TextSetting txtA_Start;
        public TextSetting txtA_Info;

        [Header("Player B")]
        public TextSetting txtB_Start;
        public TextSetting txtB_Info;
    }

    /// <summary> 튜토리얼 5페이지 컨트롤러 (방향키 조작 및 단계별 진행) </summary>
    public class TutorialPage5Controller : GamePage<TutorialPage5Data>
    {
        [Header("Page 5 UI")]
        [SerializeField] private Text descriptionText; // 설명 텍스트
        [SerializeField] private Image imageFocus; // 조작 대상 이미지

        [Header("Settings")]
        [SerializeField] private float moveSpeed = 500f; // 이동 속도
        [SerializeField] private float minX = -400; // X축 최소 범위
        [SerializeField] private float maxX = 400f; // X축 최대 범위
        [SerializeField] private float minY = -200f; // Y축 최소 범위
        [SerializeField] private float maxY = 250f; // Y축 최대 범위
        
        [SerializeField] private float fadeDuration = 0.5f; // 페이드 시간
        [SerializeField] private float centerMoveTime = 0.5f; // 중앙 복귀 시간

        private Vector2 _initialPos; // 초기 위치 저장
        private bool _isInitialized; // 초기화 여부
        private bool _hasStarted; // 조작 시작 여부
        private bool _isInputBlocked; // 입력 차단 여부
        private int _currentStage; // 현재 단계 (0: A, 1: B)

        private TextSetting _dataA_Info;
        private TextSetting _dataB_Start;
        private TextSetting _dataB_Info;

        /// <summary> 데이터 설정 (텍스트 데이터 캐싱) </summary>
        protected override void SetupData(TutorialPage5Data data)
        {
            if (descriptionText) 
                UIManager.Instance.SetText(descriptionText.gameObject, data.txtA_Start);
            
            _dataA_Info = data.txtA_Info;
            _dataB_Start = data.txtB_Start;
            _dataB_Info = data.txtB_Info;
        }

        /// <summary> 페이지 진입 (상태 초기화) </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            
            // 초기 위치 저장 (최초 1회)
            if (!_isInitialized && imageFocus != null)
            {
                _initialPos = imageFocus.rectTransform.anchoredPosition;
                _isInitialized = true;
            }

            // 상태 리셋
            _hasStarted = false;
            _isInputBlocked = false;
            _currentStage = 0; 
            
            if (imageFocus) imageFocus.rectTransform.anchoredPosition = _initialPos;
            
            SetAlpha(1f);
            SetTextAlpha(1f);
        }

        /// <summary> 입력 및 상태 갱신 </summary>
        private void Update()
        {
            if (_isInputBlocked) return;
            HandleInputByStage();
        }

        /// <summary> 단계별 입력 처리 (A: 상하, B: 좌우) </summary>
        private void HandleInputByStage()
        {
            if (imageFocus == null) return;

            Vector2 moveDir = Vector2.zero;
            
            // 단계별 허용 키 확인
            if (_currentStage == 0) // A: 상하
            {
                if (Input.GetKey(KeyCode.UpArrow)) moveDir.y = 1;
                else if (Input.GetKey(KeyCode.DownArrow)) moveDir.y = -1;
            }
            else // B: 좌우
            {
                if (Input.GetKey(KeyCode.RightArrow)) moveDir.x = 1;
                else if (Input.GetKey(KeyCode.LeftArrow)) moveDir.x = -1;
            }

            // 이동 입력 발생 시
            if (moveDir != Vector2.zero)
            {
                // 첫 조작 시 시퀀스 시작
                if (!_hasStarted)
                {
                    _hasStarted = true;
                    StartCoroutine(ProcessStageSequence());
                }

                // 이동 처리 (범위 제한 포함)
                Vector2 currentPos = imageFocus.rectTransform.anchoredPosition;
                Vector2 nextPos = currentPos + (moveDir * (moveSpeed * Time.deltaTime));

                if (_currentStage == 0)
                {
                    nextPos.x = _initialPos.x;
                    nextPos.y = Mathf.Clamp(nextPos.y, _initialPos.y + minY, _initialPos.y + maxY);
                }
                else
                {
                    nextPos.y = _initialPos.y;
                    nextPos.x = Mathf.Clamp(nextPos.x, _initialPos.x + minX, _initialPos.x + maxX);
                }
                imageFocus.rectTransform.anchoredPosition = nextPos;
            }
        }

        /// <summary> 조작 후 대기 및 다음 단계 전환 </summary>
        private IEnumerator ProcessStageSequence()
        {
            yield return new WaitForSeconds(5.0f); // 5초간 자유 조작

            _isInputBlocked = true; // 입력 차단
            StartCoroutine(MoveFocusToCenter()); // 중앙 복귀

            if (_currentStage == 0)
            {
                // Stage 0 -> 1 전환
                yield return StartCoroutine(TextChangeSequence(_dataA_Info));
                yield return new WaitForSeconds(4.0f);
                yield return StartCoroutine(TextChangeSequence(_dataB_Start));

                _currentStage = 1;
                _hasStarted = false;
                _isInputBlocked = false; // 입력 재개
            }
            else
            {
                // Stage 1 -> 완료
                yield return StartCoroutine(TextChangeSequence(_dataB_Info));
                yield return new WaitForSeconds(4.0f);
                CompleteStep(); // 단계 완료
            }
        }

        /// <summary> 이미지를 중앙으로 복귀 </summary>
        private IEnumerator MoveFocusToCenter()
        {
            if (imageFocus == null) yield break;
            float timer = 0f;
            Vector2 startPos = imageFocus.rectTransform.anchoredPosition;
            
            while (timer < centerMoveTime)
            {
                timer += Time.deltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, timer / centerMoveTime);
                imageFocus.rectTransform.anchoredPosition = Vector2.Lerp(startPos, _initialPos, progress);
                yield return null;
            }
            imageFocus.rectTransform.anchoredPosition = _initialPos;
        }

        /// <summary> 텍스트 교체 연출 (페이드) </summary>
        private IEnumerator TextChangeSequence(TextSetting newTextData)
        {
            yield return StartCoroutine(FadeTextRoutine(1f, 0f)); // 페이드 아웃
            if (newTextData != null && descriptionText != null)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, newTextData);
            }
            yield return StartCoroutine(FadeTextRoutine(0f, 1f)); // 페이드 인
        }

        /// <summary> 텍스트 투명도 조절 코루틴 </summary>
        private IEnumerator FadeTextRoutine(float startAlpha, float endAlpha)
        {
            if (descriptionText == null) yield break;
            float timer = 0f;
            SetTextAlpha(startAlpha);
            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                float progress = timer / fadeDuration;
                SetTextAlpha(Mathf.Lerp(startAlpha, endAlpha, progress));
                yield return null;
            }
            SetTextAlpha(endAlpha);
        }

        /// <summary> 텍스트 투명도 설정 </summary>
        private void SetTextAlpha(float alpha)
        {
            if (descriptionText == null) return;
            Color c = descriptionText.color;
            c.a = alpha;
            descriptionText.color = c;
        }
    }
}