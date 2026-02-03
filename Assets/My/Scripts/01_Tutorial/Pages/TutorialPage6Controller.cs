using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core.Pages;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._01_Tutorial.Pages
{
    /// <summary>
    /// 튜토리얼 6페이지 데이터 클래스
    /// </summary>
    [Serializable]
    public class TutorialPage6Data
    {
        [Header("Player A")]
        public TextSetting txtA_Start; // A 시작 텍스트
        public TextSetting txtA_Info;  // A 정보 텍스트

        [Header("Player B")]
        public TextSetting txtB_Start; // B 시작 텍스트
        public TextSetting txtB_Info;  // B 정보 텍스트
        
        public string warningMessage; // 1차 경고 메시지
        public string resetMessage;   // 2차 초기화 메시지
    }

    /// <summary> 튜토리얼 6페이지 컨트롤러 </summary>
    public class TutorialPage6Controller : PopupGamePage<TutorialPage6Data>
    {
        [Header("Page 6 UI")]
        [SerializeField] private Text descriptionText; // 설명 텍스트 UI
        [SerializeField] private Image imageFocus; // 조작 대상 이미지

        [Header("Settings")]
        [SerializeField] private float moveSpeed = 500f; // 이동 속도
        [SerializeField] private float minX = -400; // X축 최소 범위
        [SerializeField] private float maxX = 400f; // X축 최대 범위
        [SerializeField] private float minY = -200f; // Y축 최소 범위
        [SerializeField] private float maxY = 250f; // Y축 최대 범위
        
        [SerializeField] private float fadeDuration = 0.5f; // 텍스트 페이드 시간
        [SerializeField] private float centerMoveTime = 0.5f; // 중앙 복귀 연출 시간

        // 내부 로직 변수
        private Vector2 _initialPos; // 초기 위치 저장
        private bool _isInitialized; // 초기화 여부
        private bool _hasStarted; // 조작 시작 여부
        private bool _isInputBlocked; // 입력 차단 여부 (연출 중)
        private int _currentStage; // 현재 단계 (0: A, 1: B)

        private TextSetting _dataA_Info;
        private TextSetting _dataB_Start;
        private TextSetting _dataB_Info;
        
        private Coroutine _stageSequenceRoutine; // 시퀀스 코루틴

        /// <summary> 데이터 설정: 텍스트 데이터 캐싱 및 팝업 메시지 설정 </summary>
        protected override void SetupData(TutorialPage6Data data)
        {
            // 첫 텍스트 적용
            if (descriptionText) 
                UIManager.Instance.SetText(descriptionText.gameObject, data.txtA_Start);
            
            // 이후 사용될 텍스트 데이터 캐싱
            _dataA_Info = data.txtA_Info;
            _dataB_Start = data.txtB_Start;
            _dataB_Info = data.txtB_Info;

            // 팝업 메시지 설정
            SetupPopupMessage(data.warningMessage, data.resetMessage);
        }

        /// <summary>  페이지 진입: 상태 및 위치 초기화 </summary>
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
            _stageSequenceRoutine = null; 
            
            // 팝업 즉시 끄기 및 타이머 초기화
            ResetIdleState(true);
            
            // UI 초기화
            if (imageFocus) imageFocus.rectTransform.anchoredPosition = _initialPos;
            SetAlpha(1f);
            SetTextAlpha(1f);
        }

        // OnExit은 부모 클래스에서 리셋 처리를 하므로, 고유 로직만 추가 처리
        public override void OnExit()
        {
            // 실행 중인 시퀀스 코루틴 정지
            if (_stageSequenceRoutine != null)
            {
                StopCoroutine(_stageSequenceRoutine);
                _stageSequenceRoutine = null;
            }
            StopAllCoroutines();

            base.OnExit(); // 부모의 StopResetSequence 호출
        }

        /// <summary>  매 프레임 업데이트: 입력 감지 및 비활성 체크 </summary>
        private void Update()
        {
            // 1. 입력 감지
            if (Input.anyKey || Input.touchCount > 0)
            {
                // 리셋 취소 (부드럽게)
                ResetIdleState(false);

                // 연출 중이 아닐 때만 조작 허용
                if (!_isInputBlocked)
                {
                    HandleInputByStage();
                }
            }
            else
            {
                // 2. 비활성 시간 누적 (부모 메서드)
                // 연출 중(_isInputBlocked)일 때는 시간을 누적하지 않음
                UpdateInactivity(_isInputBlocked);
            }
        }

        /// <summary>  단계별(A/B) 입력 처리 및 이동 로직 </summary>
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
                    _stageSequenceRoutine = StartCoroutine(ProcessStageSequence());
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

        /// <summary> 조작 후 대기 및 다음 단계 자동 전환 </summary>
        private IEnumerator ProcessStageSequence()
        {
            yield return CoroutineData.GetWaitForSeconds(5.0f); // 5초간 자유 조작

            _isInputBlocked = true; // 입력 차단 (이때부터 비활성 타이머도 멈춤)
            StartCoroutine(MoveFocusToCenter()); // 중앙 복귀 연출

            if (_currentStage == 0)
            {
                // Stage A -> B 전환
                yield return StartCoroutine(TextChangeSequence(_dataA_Info));
                yield return CoroutineData.GetWaitForSeconds(4.0f);
                yield return StartCoroutine(TextChangeSequence(_dataB_Start));

                _currentStage = 1;
                _hasStarted = false;
                _isInputBlocked = false; // 입력 재개 (비활성 타이머 다시 작동)
                _stageSequenceRoutine = null;
            }
            else
            {
                // Stage B -> 완료
                yield return StartCoroutine(TextChangeSequence(_dataB_Info));
                yield return CoroutineData.GetWaitForSeconds(4.0f);
                CompleteStep(); 
                _stageSequenceRoutine = null;
            }
        }

        /// <summary>  이미지를 중앙으로 부드럽게 복귀시킴 </summary>
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

        /// <summary>  텍스트 교체 연출 (페이드 아웃 -> 교체 -> 페이드 인) </summary>
        private IEnumerator TextChangeSequence(TextSetting newTextData)
        {
            yield return StartCoroutine(FadeTextRoutine(1f, 0f));
            if (newTextData != null && descriptionText != null)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, newTextData);
            }
            yield return StartCoroutine(FadeTextRoutine(0f, 1f));
        }

        /// <summary>  텍스트 알파값 조절 코루틴 </summary>
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

        /// <summary>  텍스트 알파값 즉시 설정 </summary>
        private void SetTextAlpha(float alpha)
        {
            if (descriptionText == null) return;
            Color c = descriptionText.color;
            c.a = alpha;
            descriptionText.color = c;
        }
    }
}