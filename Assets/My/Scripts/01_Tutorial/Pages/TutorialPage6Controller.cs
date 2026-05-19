using System;
using System.Threading;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using My.Scripts.Core;
using My.Scripts.Global;
using My.Scripts.Utils;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._01_Tutorial.Pages
{
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
    /// 정문화된 WheelInputUtility 방향 데이터와 UI 이동 좌표계 축 방향성이 완벽하게 동기화되었습니다.
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

        private Vector2 _initialPos;
        private Vector2 _targetPos;
        private Vector2 _currentVelocity;

        private bool _isInitialized;
        private bool _hasStarted;
        private bool _isInputBlocked;
        private int _currentStage;

        private TutorialPage6Data _data;

        private PlayerWheelState _p1State;
        private PlayerWheelState _p2State;

        private const int StepsForFullRotation = 3;
        private CancellationTokenSource _sequenceCts;

        // --- 의존성 주입 (DI) 변수 ---
        private SessionManager _sessionManager;
        private new ILogger<TutorialPage6Controller> _logger;

        [Inject]
        public void Construct(SessionManager sessionManager, SoundManager soundManager,
            ILogger<TutorialPage6Controller> logger)
        {
            _sessionManager = sessionManager;
            _soundManager = soundManager;
            _logger = logger;
        }

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
            SetupPopupMessage(data?.warningMessage ?? string.Empty, data?.resetMessage ?? string.Empty);
        }

        public override void OnEnter()
        {
            base.OnEnter();

            if (_sequenceCts != null)
            {
                _sequenceCts.Cancel();
                _sequenceCts.Dispose();
                _sequenceCts = null;
            }

            _sequenceCts = new CancellationTokenSource();

            if (_data != null && _uiManager)
            {
                if (descriptionText)
                {
                    _uiManager.SetText(descriptionText.gameObject, _data.txtA_Start);
                    ApplyDynamicNames(descriptionText);
                }

                if (infoText)
                {
                    _uiManager.SetText(infoText.gameObject, _data.txtA_Info);
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
            if (descriptionText) descriptionText.SetAlpha(1f);
            if (infoText) infoText.SetAlpha(1f);
        }

        private void ApplyDynamicNames(Text txt)
        {
            if (!txt || !_sessionManager) return;

            using (Utf16ValueStringBuilder sb = ZString.CreateStringBuilder())
            {
                sb.Append(txt.text);
                sb.Replace("{nameA}", _sessionManager.PlayerAFirstName ?? string.Empty);
                sb.Replace("{nameB}", _sessionManager.PlayerBFirstName ?? string.Empty);
                txt.text = sb.ToString();
            }
        }

        public override void OnExit()
        {
            if (_sequenceCts != null)
            {
                _sequenceCts.Cancel();
                _sequenceCts.Dispose();
                _sequenceCts = null;
            }

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

        private void UpdateFocusMovement()
        {
            if (!imageFocus) return;

            imageFocus.rectTransform.anchoredPosition = Vector2.SmoothDamp(
                imageFocus.rectTransform.anchoredPosition,
                _targetPos,
                ref _currentVelocity,
                smoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime
            );
        }

        private void HandleWheelInput()
        {
            if (!imageFocus) return;

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

        private void ApplyMovement(int direction)
        {
            if (!_hasStarted)
            {
                _hasStarted = true;

                if (_sequenceCts != null)
                {
                    _sequenceCts.Cancel();
                    _sequenceCts.Dispose();
                }

                _sequenceCts = new CancellationTokenSource();

                ProcessStageSequenceAsync(_sequenceCts.Token).Forget();
            }

            // 정회전(1)일 때 아래(-Y)로, 역회전(-1)일 때 위(+Y)로 이동합니다.
            if (_currentStage == 0) // P1 (상하 조작)
            {
                float moveY = -direction * stepDistance;
                _targetPos.y = Mathf.Clamp(_targetPos.y + moveY, _initialPos.y + minY, _initialPos.y + maxY);
                _targetPos.x = _initialPos.x;
            }
            else // P2 (좌우 조작)
            {
                float moveX = direction * stepDistance; // 1이면 오른쪽(+X), -1이면 왼쪽(-X)
                _targetPos.x = Mathf.Clamp(_targetPos.x + moveX, _initialPos.x + minX, _initialPos.x + maxX);
                _targetPos.y = _initialPos.y;
            }
        }

        private async UniTaskVoid ProcessStageSequenceAsync(CancellationToken token)
        {
            try
            {
                if (_data == null) return;

                await UniTask.Delay(5000, ignoreTimeScale: true, cancellationToken: token);

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
                    await UniTask.Delay(1000, ignoreTimeScale: true, cancellationToken: token);
                    CompleteStep();
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async UniTask TextChangeSequenceAsync(TextSetting newMainText, TextSetting newInfoText,
            CancellationToken token)
        {
            await FadeTextAsync(1f, 0f, token);

            if (_uiManager)
            {
                if (newMainText != null && descriptionText)
                {
                    _uiManager.SetText(descriptionText.gameObject, newMainText);
                    ApplyDynamicNames(descriptionText);
                }

                if (newInfoText != null && infoText)
                {
                    _uiManager.SetText(infoText.gameObject, newInfoText);
                    ApplyDynamicNames(infoText);
                }
            }

            await FadeTextAsync(0f, 1f, token);
        }

        private async UniTask FadeTextAsync(float startAlpha, float endAlpha, CancellationToken token)
        {
            var task1 = descriptionText
                ? descriptionText.FadeAsync(startAlpha, endAlpha, fadeDuration, token)
                : UniTask.CompletedTask;
            var task2 = infoText
                ? infoText.FadeAsync(startAlpha, endAlpha, fadeDuration, token)
                : UniTask.CompletedTask;

            await UniTask.WhenAll(task1, task2);
        }

        private int ProcessPlayerWheel(ref PlayerWheelState state, int start, int end)
        {
            int currentKey = WheelInputUtility.GetPressedKeyIndex(start, end);
            if (currentKey == -1) return 0;

            int previousDir = state.lastDir;
            int dir = WheelInputUtility.ResolveDirection(currentKey, 4, ref state);

            if (dir != 0)
            {
                state.stepCount = (dir == previousDir) ? state.stepCount + 1 : 1;

                if (state.stepCount >= StepsForFullRotation)
                {
                    if (_soundManager)
                    {
                        _soundManager.PlaySFX("카메라_1");
                    }

                    state.stepCount = 0;
                }
            }

            return dir;
        }
    }
}