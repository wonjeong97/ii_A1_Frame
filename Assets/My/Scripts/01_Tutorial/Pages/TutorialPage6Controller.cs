using System;
using System.Collections;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Global;
using My.Scripts.Utils;
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
    /// 플레이어 A(상하)와 B(좌우)가 순차적으로 다이얼을 조작하여 초점을 맞추는 협동 조작 컨트롤러.
    /// </summary>
    public class TutorialPage6Controller : PopupGamePage<TutorialPage6Data>
    {
        [Header("Page 6 UI")]
        [SerializeField] private Text descriptionText;

        [SerializeField] private Text infoText;
        [SerializeField] private Image imageFocus;

        [Header("Settings")]
        [SerializeField] private float stepDistance = 50f;

        [SerializeField] private float smoothTime = 0.1f;
        [SerializeField] private float minX = -400f;
        [SerializeField] private float maxX = 400f;
        [SerializeField] private float minY = -200f;
        [SerializeField] private float maxY = 250f;

        private readonly float fadeDuration = 0.5f;
        private readonly static StringBuilder StringBuilder = new StringBuilder(256);

        private Vector2 _initialPos;
        private Vector2 _targetPos;
        private Vector2 _currentVelocity;

        private bool _isInitialized;
        private bool _hasStarted;
        private bool _isInputBlocked;
        private int _currentStage;

        private TutorialPage6Data _data;
        private Coroutine _stageSequenceRoutine;

        private PlayerWheelState _p1State;
        private PlayerWheelState _p2State;

        private const int StepsForFullRotation = 3;
        private const float FastInputThreshold = 0.2f;
        private CancellationTokenSource _sequenceCts;

        public override object ExtractCurrentData()
        {
            TextSetting txtA_Start, txtA_Info, txtB_Start, txtB_Info;
            if (_currentStage == 0)
            {
                txtA_Start =
                    TutorialPageUtils.BuildTextSetting(descriptionText, _data?.txtA_Start, _data?.txtA_Start?.text);
                txtA_Info = TutorialPageUtils.BuildTextSetting(infoText, _data?.txtA_Info, _data?.txtA_Info?.text);
                txtB_Start = _data?.txtB_Start;
                txtB_Info = _data?.txtB_Info;
            }
            else
            {
                txtA_Start = _data?.txtA_Start;
                txtA_Info = _data?.txtA_Info;
                txtB_Start =
                    TutorialPageUtils.BuildTextSetting(descriptionText, _data?.txtB_Start, _data?.txtB_Start?.text);
                txtB_Info = TutorialPageUtils.BuildTextSetting(infoText, _data?.txtB_Info, _data?.txtB_Info?.text);
            }

            return new TutorialPage6Data
            {
                txtA_Start = txtA_Start,
                txtA_Info = txtA_Info,
                txtB_Start = txtB_Start,
                txtB_Info = txtB_Info,
                warningMessage = _data?.warningMessage ?? string.Empty,
                resetMessage = _data?.resetMessage ?? string.Empty,
            };
        }

        protected override void SetupData(TutorialPage6Data data)
        {
            _data = data;
            if (descriptionText)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, _data.txtA_Start);
            }

            if (infoText)
            {
                UIManager.Instance.SetText(infoText.gameObject, _data.txtA_Info);
            }

            SetupPopupMessage(_data.warningMessage, _data.resetMessage);
        }

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

            if (!_isInitialized && imageFocus)
            {
                _initialPos = imageFocus.rectTransform.anchoredPosition;
                _isInitialized = true;
            }

            _hasStarted = false;
            _isInputBlocked = false;
            _currentStage = 0;
            _stageSequenceRoutine = null;

            _p1State = PlayerWheelState.Default;
            _p2State = PlayerWheelState.Default;

            ResetIdleState(true);

            if (imageFocus)
            {
                imageFocus.rectTransform.anchoredPosition = _initialPos;
                _targetPos = _initialPos;
                _currentVelocity = Vector2.zero;
            }

            SetAlpha(1f);
            UIFadeUtility.SetAlpha(descriptionText, 1f);
            UIFadeUtility.SetAlpha(infoText, 1f);
        }

        /// <summary> StringBuilder를 사용하여 가비지 생성 없이 닉네임 플레이스홀더를 치환함. </summary>
        private void ApplyDynamicNames(Text txt)
        {
            if (!txt || !SessionManager.Instance)
            {
                return;
            }

            StringBuilder.Clear();
            StringBuilder.Append(txt.text);
            StringBuilder.Replace("{nameA}", SessionManager.Instance.PlayerAFirstName);
            StringBuilder.Replace("{nameB}", SessionManager.Instance.PlayerBFirstName);
            txt.text = StringBuilder.ToString();
        }

        public override void OnExit()
        {
            _sequenceCts?.Cancel();
            _sequenceCts?.Dispose();
            _sequenceCts = null;

            base.OnExit();
        }

        private void Update()
        {
            if (!_isInputBlocked)
            {
                HandleWheelInput();
            }

            UpdateFocusMovement();

            if (Input.anyKey || Input.touchCount > 0)
            {
                ResetIdleState(false);
            }
            else
            {
                UpdateInactivity(_isInputBlocked);
            }
        }

        /// <summary> 초점 이미지의 부드러운 위치 이동을 제어함. </summary>
        private void UpdateFocusMovement()
        {
            if (!imageFocus)
            {
                return;
            }

            imageFocus.rectTransform.anchoredPosition = Vector2.SmoothDamp(
                imageFocus.rectTransform.anchoredPosition,
                _targetPos,
                ref _currentVelocity,
                smoothTime
            );
        }

        /// <summary> 
        /// 현재 스테이지에 따라 대상 플레이어의 입력을 처리하고 목표 좌표를 갱신함.
        /// </summary>
        private void HandleWheelInput()
        {
            if (!imageFocus)
            {
                return;
            }

            int direction = 0;
            if (_currentStage == 0)
            {
                direction = ProcessPlayerWheel(ref _p1State, 1, 4);
            }
            else
            {
                direction = ProcessPlayerWheel(ref _p2State, 5, 8);
            }

            if (direction != 0)
            {
                ApplyMovement(direction);
            }
        }

        /// <summary> 입력된 방향을 기반으로 스테이지별 이동 제한 구역을 고려하여 좌표를 이동함. </summary>
        private void ApplyMovement(int direction)
        {
            if (!_hasStarted)
            {
                _hasStarted = true;
                
                // 기존 코루틴 대신 토큰 소스를 갱신하고 UniTask 실행
                _sequenceCts?.Cancel();
                _sequenceCts?.Dispose();
                _sequenceCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
                
                ProcessStageSequenceAsync(_sequenceCts.Token).Forget();
            }

            if (_currentStage == 0) // P1 (상하)
            {
                float moveY = (direction == 1) ? -stepDistance : stepDistance;
                _targetPos.y = Mathf.Clamp(_targetPos.y + moveY, _initialPos.y + minY, _initialPos.y + maxY);
                _targetPos.x = _initialPos.x;
            }
            else // P2 (좌우)
            {
                float moveX = (direction == 1) ? stepDistance : -stepDistance;
                _targetPos.x = Mathf.Clamp(_targetPos.x + moveX, _initialPos.x + minX, _initialPos.x + maxX);
                _targetPos.y = _initialPos.y;
            }
        }
        
        // <summary> 일정 시간 후 텍스트를 교체하고 플레이어 조작 권한을 넘김. </summary>
        private async UniTaskVoid ProcessStageSequenceAsync(CancellationToken token)
        {
            if (_data == null) return;
            
            await UniTask.Delay(TimeSpan.FromSeconds(5.0), cancellationToken: token); 

            _isInputBlocked = true; 
            _targetPos = _initialPos; 

            if (_currentStage == 0)
            {
                await TextChangeSequenceAsync(_data.txtB_Start, _data.txtB_Info, token);

                _currentStage = 1;
                _hasStarted = false; 
                _isInputBlocked = false;
                _p2State = PlayerWheelState.Default;
            }
            else
            {
                await UniTask.Delay(TimeSpan.FromSeconds(1.0), cancellationToken: token);
                CompleteStep(); 
            }
        }
        
        /// <summary> 안내 텍스트 페이드아웃 -> 텍스트 교체 -> 페이드인을 순차적으로 비동기 대기함. </summary>
        private async UniTask TextChangeSequenceAsync(TextSetting newMainText, TextSetting newInfoText, CancellationToken token)
        {
            await FadeTextAsync(1f, 0f, token);
            
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
            
            await FadeTextAsync(0f, 1f, token);
        }
        
        /// <summary> 설명 텍스트와 안내 텍스트를 동시에 페이드 처리함. </summary>
        private async UniTask FadeTextAsync(float startAlpha, float endAlpha, CancellationToken token)
        {
            var task1 = UIFadeUtility.FadeGraphicAsync(descriptionText, startAlpha, endAlpha, fadeDuration, token);
            var task2 = UIFadeUtility.FadeGraphicAsync(infoText, startAlpha, endAlpha, fadeDuration, token);
            
            await UniTask.WhenAll(task1, task2);
        }

        /// <summary>
        /// 특정 플레이어의 물리 입력값을 분석하여 정규화된 방향(-1, 0, 1)을 반환함.
        /// WheelInputUtility를 사용하여 방향 보정 및 튕김 현상 필터링 수행.
        /// </summary>
        private int ProcessPlayerWheel(ref PlayerWheelState state, int start, int end)
        {
            int currentKey = WheelInputUtility.GetPressedKeyIndex(start, end);
            if (currentKey == -1)
            {
                return 0;
            }

            if (state.lastKey == -1)
            {
                state.lastKey = currentKey;
                return 0;
            }

            float now = Time.time;
            int diff = (currentKey - state.lastKey + 4) % 4;
            int dir = WheelInputUtility.ResolveDirection(diff, now, ref state);

            if (dir != 0)
            {
                state.stepCount = (dir == state.lastDir) ? state.stepCount + 1 : 1;
                state.lastDir = dir;
                state.lastTime = now;

                if (state.stepCount >= StepsForFullRotation)
                {
                    if (SoundManager.Instance)
                    {
                        SoundManager.Instance.PlaySFX("카메라_1");
                    }

                    state.stepCount = 0;
                }
            }

            state.lastKey = currentKey;
            return dir;
        }
    }
}