using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core.Pages;
using My.Scripts.Global;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts._01_Tutorial.Pages
{
    /// <summary> 튜토리얼 6페이지용 데이터 구조체 </summary>
    [Serializable]
    public class TutorialPage6Data
    {
        [Header("Player A")]
        public TextSetting txtA_Start; 
        public TextSetting txtA_Info;  

        [Header("Player B")]
        public TextSetting txtB_Start; 
        public TextSetting txtB_Info;  
        
        public string warningMessage; 
        public string resetMessage;   
    }

    /// <summary>
    /// 튜토리얼 6페이지 컨트롤러.
    /// 플레이어 A(상하 이동)와 B(좌우 이동)가 순차적으로 다이얼을 조작하여 초점(Focus) 이미지를 움직여보는 협동 조작을 안내합니다.
    /// </summary>
    public class TutorialPage6Controller : PopupGamePage<TutorialPage6Data>
    {
        [Header("Page 6 UI")]
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text infoText; // 새로 추가된 안내 텍스트
        [SerializeField] private Image imageFocus;

        [Header("Settings")]
        [SerializeField] private float stepDistance = 50f; 
        [SerializeField] private float smoothTime = 0.1f;  
        [SerializeField] private float minX = -400;
        [SerializeField] private float maxX = 400f;
        [SerializeField] private float minY = -200f;
        [SerializeField] private float maxY = 250f;
        
        private readonly float fadeDuration = 0.5f;

        private Vector2 _initialPos;
        private Vector2 _targetPos; 
        private Vector2 _currentVelocity; 

        private bool _isInitialized;
        private bool _hasStarted;
        private bool _isInputBlocked;
        private int _currentStage; 

        private TutorialPage6Data _data; 
        private Coroutine _stageSequenceRoutine;

        private int _lastP1Key = -1;
        private int _p1StepCount; 
        private float _p1LastTime;
        private int _p1LastDir;

        private int _lastP2Key = -1;
        private int _p2StepCount; 
        private float _p2LastTime;
        private int _p2LastDir;

        private const int StepsForFullRotation = 3; 
        private const float FastInputThreshold = 0.2f; 

        /// <summary> JSON에서 로드한 각 플레이어별 안내 텍스트 및 경고 팝업 데이터 주입 </summary>
        protected override void SetupData(TutorialPage6Data data)
        {
            _data = data;
            if (_data == null)
            {
                Debug.LogError("[TutorialPage6] SetupData에 전달된 데이터가 null입니다.");
                return;
            }
            if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, _data.txtA_Start);
            if (infoText) UIManager.Instance.SetText(infoText.gameObject, _data.txtA_Info);
            
            SetupPopupMessage(_data.warningMessage, _data.resetMessage);
        }

        /// <summary> 페이지 진입 시 텍스트 상태, 초점 이미지의 초기 위치 및 입력 변수들을 초기화합니다. </summary>
        public override void OnEnter()
        {
            base.OnEnter();

            if (_data != null)
            {
                if (descriptionText)
                {
                    UIManager.Instance.SetText(descriptionText.gameObject, _data.txtA_Start);
                    ApplyDynamicNames(descriptionText);
                }
                if (infoText)
                {
                    UIManager.Instance.SetText(infoText.gameObject, _data.txtA_Info);
                    ApplyDynamicNames(infoText);
                }
            }
            
            // 첫 진입 시에만 초점 이미지의 기준 좌표를 기록하여 복귀 시 활용
            if (!_isInitialized && imageFocus)
            {
                _initialPos = imageFocus.rectTransform.anchoredPosition;
                _isInitialized = true;
            }

            _hasStarted = false;
            _isInputBlocked = false;
            _currentStage = 0; 
            _stageSequenceRoutine = null; 
            
            _lastP1Key = -1; _p1StepCount = 0; _p1LastDir = 0; _p1LastTime = 0f;
            _lastP2Key = -1; _p2StepCount = 0; _p2LastDir = 0; _p2LastTime = 0f;
            
            ResetIdleState(true);
            
            if (imageFocus) 
            {
                imageFocus.rectTransform.anchoredPosition = _initialPos;
                _targetPos = _initialPos; 
                _currentVelocity = Vector2.zero;
            }
            
            SetAlpha(1f);
            SetTextAlpha(1f);
        }

        /// <summary> 텍스트 내의 이름 플레이스홀더({nameA}, {nameB})를 세션의 실제 유저 이름으로 치환합니다. </summary>
        private void ApplyDynamicNames(Text txt)
        {
            if (txt && GameManager.Instance)
            {
                txt.text = txt.text.Replace("{nameA}", SessionManager.Instance.PlayerAFirstName)
                                   .Replace("{nameB}", SessionManager.Instance.PlayerBFirstName);
            }
        }
        
        /// <summary> 페이지 퇴장 시 진행 중인 연출 코루틴을 안전하게 중단합니다. </summary>
        public override void OnExit()
        {
            if (_stageSequenceRoutine != null)
            {
                StopCoroutine(_stageSequenceRoutine);
                _stageSequenceRoutine = null;
            }
            base.OnExit();
        }
        
        /// <summary> 매 프레임 입력 처리, 무응답 타임아웃 갱신 및 초점 이미지의 부드러운 위치 이동(SmoothDamp)을 수행합니다. </summary>
        private void Update()
        {
            if (!_isInputBlocked)
            {
                HandleWheelInput();
            }

            if (imageFocus)
            {
                imageFocus.rectTransform.anchoredPosition = Vector2.SmoothDamp(
                    imageFocus.rectTransform.anchoredPosition, 
                    _targetPos, 
                    ref _currentVelocity, 
                    smoothTime
                );
            }

            if (Input.anyKey || Input.touchCount > 0)
            {
                ResetIdleState(false);
            }
            else
            {
                UpdateInactivity(_isInputBlocked);
            }
        }

        /// <summary> 
        /// 스테이지에 따라 P1(상하) 또는 P2(좌우)의 다이얼 조작(키보드 1~8)을 감지하여 목표 좌표(targetPos)를 갱신합니다.
        /// 조작이 감지되면 다음 페이즈로 넘어가는 시퀀스 타이머가 시작됩니다.
        /// </summary>
        private void HandleWheelInput()
        {
            if (!imageFocus) return;

            int direction = 0; 
            float now = Time.time;

            if (_currentStage == 0) // P1 (상하 제어)
            {
                int currentKey = GetPressedKeyIndex(1, 4);
                if (currentKey != -1)
                {
                    if (_lastP1Key != -1)
                    {
                        int diff = (currentKey - _lastP1Key + 4) % 4;
                        int dir = 0;

                        if (diff == 1) dir = 1;       
                        else if (diff == 3) dir = -1; 

                        // 바운스 현상 등 비정상적인 빠른 입력 보정
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
                            
                            if (dir == _p1LastDir) _p1StepCount++;
                            else _p1StepCount = 1;

                            _p1LastDir = dir;
                            _p1LastTime = now;

                            if (_p1StepCount >= StepsForFullRotation)
                            {
                                SoundManager.Instance?.PlaySFX("카메라_1");
                                _p1StepCount = 0; 
                            }
                        }
                    }
                    _lastP1Key = currentKey;
                }
            }
            else // P2 (좌우 제어)
            {
                int currentKey = GetPressedKeyIndex(5, 8);
                if (currentKey != -1)
                {
                    if (_lastP2Key != -1)
                    {
                        int currIdx = currentKey - 5;
                        int lastIdx = _lastP2Key - 5;
                        int diff = (currIdx - lastIdx + 4) % 4;
                        int dir = 0;
                        
                        if (diff == 1) dir = 1;       
                        else if (diff == 3) dir = -1; 

                        // 바운스 현상 등 비정상적인 빠른 입력 보정
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

                            if (dir == _p2LastDir) _p2StepCount++;
                            else _p2StepCount = 1;

                            _p2LastDir = dir;
                            _p2LastTime = now;

                            if (_p2StepCount >= StepsForFullRotation)
                            {
                                SoundManager.Instance?.PlaySFX("카메라_1");
                                _p2StepCount = 0; 
                            }
                        }
                    }
                    _lastP2Key = currentKey;
                }
            }

            if (direction != 0)
            {
                if (!_hasStarted)
                {
                    _hasStarted = true;
                    // 조작이 감지되면 5초간 대기하는 시퀀스 시작
                    _stageSequenceRoutine = StartCoroutine(ProcessStageSequence());
                }

                // 스테이지에 따라 상하 또는 좌우로 목표 좌표 변경 (제한 범위 적용)
                if (_currentStage == 0) 
                {
                    float moveY = (direction == 1) ? -stepDistance : stepDistance;
                    _targetPos.y += moveY;
                    _targetPos.y = Mathf.Clamp(_targetPos.y, _initialPos.y + minY, _initialPos.y + maxY);
                    _targetPos.x = _initialPos.x; 
                }
                else 
                {
                    float moveX = (direction == 1) ? stepDistance : -stepDistance;
                    _targetPos.x += moveX;
                    _targetPos.x = Mathf.Clamp(_targetPos.x, _initialPos.x + minX, _initialPos.x + maxX);
                    _targetPos.y = _initialPos.y; 
                }
            }
        }

        /// <summary> 지정된 범위(start~end)의 숫자 키 입력을 감지하여 반환하는 헬퍼 함수 </summary>
        private int GetPressedKeyIndex(int start, int end)
        {
            for (int i = start; i <= end; i++)
            {
                KeyCode key = (KeyCode)((int)KeyCode.Alpha0 + i);
                if (Input.GetKeyDown(key)) return i;
            }
            return -1;
        }

        /// <summary> 
        /// 조작 감지 후 5초 대기, 초점 이미지 중앙 복귀, 안내 텍스트 동시 교체(P1 -> P2) 등 
        /// 변경된 기획에 맞춘 시퀀스를 처리합니다.
        /// </summary>
        private IEnumerator ProcessStageSequence()
        {
            if (_data == null)
            {
                Debug.LogError("[TutorialPage6] 데이터가 없습니다.");
                _stageSequenceRoutine = null;
                yield break;
            }
            
            // 유저가 마음껏 조작해 볼 수 있도록 5초간 대기
            yield return CoroutineData.GetWaitForSeconds(5.0f); 

            _isInputBlocked = true; 
            MoveFocusToCenter(); 

            if (_currentStage == 0)
            {
                // P1 종료. 중앙으로 이동하며 텍스트를 P2(txtB_Start, txtB_Info)로 동시 페이드 전환
                yield return StartCoroutine(TextChangeSequence(_data.txtB_Start, _data.txtB_Info));

                _currentStage = 1;
                _hasStarted = false; // B가 조작을 시작하면 다시 5초 타이머 시작
                _isInputBlocked = false;
                _stageSequenceRoutine = null;
                
                _lastP2Key = -1;
                _p2StepCount = 0;
                _p2LastDir = 0;
                _p2LastTime = 0f;
            }
            else
            {
                // P2 조작 5초 대기 종료 -> 중앙으로 복귀하는 모습을 1초간 보여준 후 완료
                yield return CoroutineData.GetWaitForSeconds(1.0f);
                CompleteStep(); 
                _stageSequenceRoutine = null;
            }
        }

        /// <summary> 스테이지 전환 시 초점 이미지를 초기 위치로 돌려보내기 위해 목표 좌표를 재설정합니다. </summary>
        private void MoveFocusToCenter()
        {
            if (!imageFocus) return;
            _targetPos = _initialPos; 
        }
        
        /// <summary> 페이드아웃 -> 메인 및 추가 텍스트 교체 -> 페이드인 순서로 텍스트를 부드럽게 동시 변경합니다. </summary>
        private IEnumerator TextChangeSequence(TextSetting newMainText, TextSetting newInfoText)
        {
            yield return StartCoroutine(FadeTextRoutine(1f, 0f));
            
            if (newMainText != null && descriptionText)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, newMainText);
                ApplyDynamicNames(descriptionText);
            }
            
            if (newInfoText != null && infoText)
            {
                UIManager.Instance.SetText(infoText.gameObject, newInfoText);
                ApplyDynamicNames(infoText);
            }
            
            yield return StartCoroutine(FadeTextRoutine(0f, 1f));
        }

        /// <summary> 텍스트의 투명도(Alpha)를 지정된 시간 동안 부드럽게 변경합니다. </summary>
        private IEnumerator FadeTextRoutine(float startAlpha, float endAlpha)
        {
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

        /// <summary> 모든 텍스트의 알파값을 즉시 갱신합니다. </summary>
        private void SetTextAlpha(float alpha)
        {
            if (descriptionText)
            {
                Color c = descriptionText.color;
                c.a = alpha;
                descriptionText.color = c;
            }
            
            if (infoText)
            {
                Color c = infoText.color;
                c.a = alpha;
                infoText.color = c;
            }
        }
    }
}