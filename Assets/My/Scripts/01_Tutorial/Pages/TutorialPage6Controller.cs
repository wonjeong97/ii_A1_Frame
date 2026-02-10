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
        [SerializeField] private Text descriptionText;
        [SerializeField] private Image imageFocus;

        [Header("Settings")]
        [SerializeField] private float stepDistance = 50f; // 휠 1클릭당 이동 거리
        [SerializeField] private float smoothTime = 0.1f;  // 부드러운 이동 시간
        [SerializeField] private float minX = -400;
        [SerializeField] private float maxX = 400f;
        [SerializeField] private float minY = -200f;
        [SerializeField] private float maxY = 250f;
        
        private readonly float fadeDuration = 0.1f;
        private readonly float centerMoveTime = 0.1f;

        private Vector2 _initialPos;
        private Vector2 _targetPos; // 목표 위치 
        private Vector2 _currentVelocity; // SmoothDamp용 속도 변수

        private bool _isInitialized;
        private bool _hasStarted;
        private bool _isInputBlocked;
        private int _currentStage; 

        private TextSetting _dataA_Info;
        private TextSetting _dataB_Start;
        private TextSetting _dataB_Info;
        
        private Coroutine _stageSequenceRoutine;

        // 휠 입력 추적 변수
        private int _lastP1Key = -1;
        private float _p1LastTime;
        private int _p1LastDir;

        private int _lastP2Key = -1;
        private float _p2LastTime;
        private int _p2LastDir;

        private const float FastInputThreshold = 0.2f; // 빠른 입력 임계값

        protected override void SetupData(TutorialPage6Data data)
        {
            if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, data.txtA_Start);
            _dataA_Info = data.txtA_Info;
            _dataB_Start = data.txtB_Start;
            _dataB_Info = data.txtB_Info;
            SetupPopupMessage(data.warningMessage, data.resetMessage);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            
            if (!_isInitialized && imageFocus)
            {
                _initialPos = imageFocus.rectTransform.anchoredPosition;
                _isInitialized = true;
            }

            _hasStarted = false;
            _isInputBlocked = false;
            _currentStage = 0; 
            _stageSequenceRoutine = null; 
            
            // 휠 상태 초기화
            _lastP1Key = -1; _p1LastDir = 0; _p1LastTime = 0f;
            _lastP2Key = -1; _p2LastDir = 0; _p2LastTime = 0f;
            
            ResetIdleState(true);
            
            if (imageFocus) 
            {
                imageFocus.rectTransform.anchoredPosition = _initialPos;
                _targetPos = _initialPos; // 목표 위치 초기화
            }
            SetAlpha(1f);
            SetTextAlpha(1f);
        }
        
        public override void OnExit()
        {
            if (_stageSequenceRoutine != null)
            {
                StopCoroutine(_stageSequenceRoutine);
                _stageSequenceRoutine = null;
            }
            base.OnExit();
        }
        
        private void Update()
        {
            if (!_isInputBlocked)
            {
                HandleWheelInput();
            }

            // 부드러운 이동 처리
            if (imageFocus)
            {
                imageFocus.rectTransform.anchoredPosition = Vector2.SmoothDamp(
                    imageFocus.rectTransform.anchoredPosition, 
                    _targetPos, 
                    ref _currentVelocity, 
                    smoothTime
                );
            }

            // 입력 감지 및 비활성 체크
            if (Input.anyKey || Input.touchCount > 0)
            {
                ResetIdleState(false);
            }
            else
            {
                UpdateInactivity(_isInputBlocked);
            }
        }

        /// <summary> 휠 시퀀스 입력 처리 (관성 보정 포함) </summary>
        private void HandleWheelInput()
        {
            if (imageFocus == null) return;

            int direction = 0; // 0:None, 1:Positive(Down/Right), -1:Negative(Up/Left)
            float now = Time.time;

            // Stage A: P1 (1~4) -> Vertical
            if (_currentStage == 0)
            {
                int currentKey = GetPressedKeyIndex(1, 4);
                if (currentKey != -1)
                {
                    if (_lastP1Key != -1)
                    {
                        int diff = (currentKey - _lastP1Key + 4) % 4;
                        int dir = 0;

                        if (diff == 1) dir = 1;       // CW (Down)
                        else if (diff == 3) dir = -1; // CCW (Up)

                        // [관성 보정]
                        if (now - _p1LastTime < FastInputThreshold && _p1LastDir != 0)
                        {
                            if (diff == 2 || (dir != 0 && dir != _p1LastDir))
                            {
                                dir = _p1LastDir;
                            }
                        }

                        if (dir != 0)
                        {
                            direction = dir;
                            _p1LastDir = dir;
                            _p1LastTime = now;
                        }
                    }
                    _lastP1Key = currentKey;
                }
            }
            // Stage B: P2 (5~8) -> Horizontal
            else
            {
                int currentKey = GetPressedKeyIndex(5, 8);
                if (currentKey != -1)
                {
                    if (_lastP2Key != -1)
                    {
                        // 5~8을 0~3으로 매핑
                        int currIdx = currentKey - 5;
                        int lastIdx = _lastP2Key - 5;
                        int diff = (currIdx - lastIdx + 4) % 4;
                        int dir = 0;
                        
                        if (diff == 1) dir = 1;       // CW (Right)
                        else if (diff == 3) dir = -1; // CCW (Left)

                        // [관성 보정]
                        if (now - _p2LastTime < FastInputThreshold && _p2LastDir != 0)
                        {
                            if (diff == 2 || (dir != 0 && dir != _p2LastDir))
                            {
                                dir = _p2LastDir;
                            }
                        }

                        if (dir != 0)
                        {
                            direction = dir;
                            _p2LastDir = dir;
                            _p2LastTime = now;
                        }
                    }
                    _lastP2Key = currentKey;
                }
            }

            // 이동 적용
            if (direction != 0)
            {
                if (!_hasStarted)
                {
                    _hasStarted = true;
                    _stageSequenceRoutine = StartCoroutine(ProcessStageSequence());
                }

                if (_currentStage == 0) // Vertical
                {
                    // 방향: 1(Down/Y-), -1(Up/Y+) -- 좌표계 주의
                    // CW(Down) -> y값 감소, CCW(Up) -> y값 증가
                    float moveY = (direction == 1) ? -stepDistance : stepDistance;
                    
                    _targetPos.y += moveY;
                    _targetPos.y = Mathf.Clamp(_targetPos.y, _initialPos.y + minY, _initialPos.y + maxY);
                    _targetPos.x = _initialPos.x; // 축 고정
                }
                else // Horizontal
                {
                    // CW(Right) -> x값 증가, CCW(Left) -> x값 감소
                    float moveX = (direction == 1) ? stepDistance : -stepDistance;
                    
                    _targetPos.x += moveX;
                    _targetPos.x = Mathf.Clamp(_targetPos.x, _initialPos.x + minX, _initialPos.x + maxX);
                    _targetPos.y = _initialPos.y; // 축 고정
                }
            }
        }

        /// <summary> 지정된 범위의 숫자 키 중 눌린 키 반환 (없으면 -1) </summary>
        private int GetPressedKeyIndex(int start, int end)
        {
            for (int i = start; i <= end; i++)
            {
                KeyCode key = (KeyCode)((int)KeyCode.Alpha0 + i);
                if (Input.GetKeyDown(key)) return i;
            }
            return -1;
        }

        /// <summary> 조작 후 대기 및 다음 단계 자동 전환 </summary>
        private IEnumerator ProcessStageSequence()
        {
            yield return CoroutineData.GetWaitForSeconds(5.0f); 

            _isInputBlocked = true; 
            
            // 코루틴 대신 메서드 직접 호출 (Update의 SmoothDamp가 이동 처리)
            MoveFocusToCenter(); 

            if (_currentStage == 0)
            {
                yield return StartCoroutine(TextChangeSequence(_dataA_Info));
                yield return CoroutineData.GetWaitForSeconds(4.0f);
                yield return StartCoroutine(TextChangeSequence(_dataB_Start));

                _currentStage = 1;
                _hasStarted = false;
                _isInputBlocked = false;
                _stageSequenceRoutine = null;
                
                // P2 입력 초기화
                _lastP2Key = -1;
                _p2LastDir = 0;
                _p2LastTime = 0f;
            }
            else
            {
                yield return StartCoroutine(TextChangeSequence(_dataB_Info));
                yield return CoroutineData.GetWaitForSeconds(4.0f);
                CompleteStep(); 
                _stageSequenceRoutine = null;
            }
        }

        // 직접 Lerp를 돌리지 않고 TargetPos만 설정하여 Update와 충돌 방지
        private void MoveFocusToCenter()
        {
            if (imageFocus == null) return;
            _targetPos = _initialPos; // 목표 위치를 중앙(초기 위치)으로 설정
        }
        
        private IEnumerator TextChangeSequence(TextSetting newTextData)
        {
            yield return StartCoroutine(FadeTextRoutine(1f, 0f));
            if (newTextData != null && descriptionText != null)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, newTextData);
            }
            yield return StartCoroutine(FadeTextRoutine(0f, 1f));
        }

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

        private void SetTextAlpha(float alpha)
        {
            if (descriptionText == null) return;
            Color c = descriptionText.color;
            c.a = alpha;
            descriptionText.color = c;
        }
    }
}