using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using My.Scripts.Core.Data;
using My.Scripts.Global;
using My.Scripts.Hardware;
using My.Scripts.Utils;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Wonjeong.Data;
using Wonjeong.UI;
using ZLogger;

namespace My.Scripts.Core.Pages
{
    /// <summary>
    /// 양측 플레이어의 하드웨어 입력을 받아 질문에 대한 답변을 처리하고 API를 동기화하는 페이지.
    /// 싱글톤을 완전히 배제하고 부모 클래스들의 주입 필드를 상속받아 무결성 구동을 보장합니다.
    /// </summary>
    public class Page_QnA : PopupGamePage<QnAPageData>
    {
        [Header("UI References")] 
        [SerializeField] private Text descriptionText; 
        [SerializeField] private Text questionText; 
        [SerializeField] private Text[] answerTexts; 
        
        [Header("Check UI References")]
        [SerializeField] private Text nicknameA; 
        [SerializeField] private Text nicknameB; 
        [SerializeField] private CanvasGroup cgLightA; 
        [SerializeField] private Image imgLightA; 
        [SerializeField] private CanvasGroup cgLightB; 
        [SerializeField] private Image imgLightB; 

        [Header("Canvas Groups")] 
        [SerializeField] private CanvasGroup contentsGroup;
        
        private CancellationTokenSource _sequenceCts;
        
        private bool _isAnsweredA;
        private bool _isAnsweredB;
        private bool _completionStarted; 
        private bool _isInputEnabled;
        
        private const string SideLeft = "left";
        private const string SideRight = "right";

        // --- 의존성 주입 (DI) 변수 ---
        private SessionManager _sessionManager;
        private LevelManager _levelManager;

        /// <summary> 부모들의 수령 체인 외에 QnA 자체적으로 필요한 고유 세션 데이터 주입 </summary>
        [Inject]
        public void ConstructQnA(SessionManager sessionManager, LevelManager levelManager)
        {
            _sessionManager = sessionManager;
            _levelManager = levelManager;
        }

        protected override void SetupData(QnAPageData data)
        {
            if (data == null) return;
            if (_levelManager && _levelManager.CurrentQuestionNumber > 0 && data.questionText != null)
            {
                _logger?.ZLogInformation($"[Page_QnA] {_levelManager.CurrentQuestionNumber}번 문항 질문 로드 완료: {data.questionText.text}");
            }

            ApplyTextSetting(descriptionText, data.descriptionText, "descriptionText");
            ApplyTextSetting(questionText, data.questionText, "questionText");
            ApplyTextSetting(nicknameA, data.nicknamePlayerA, "nicknamePlayerA");
            ApplyTextSetting(nicknameB, data.nicknamePlayerB, "nicknamePlayerB");

            SetupAnswerTexts(data.answerTexts);
        }
        
        private void ApplyTextSetting(Text uiText, TextSetting setting, string fieldName)
        {
            if (setting != null && !string.IsNullOrEmpty(setting.text))
            {
                if (uiText && _uiManager != null) _uiManager.SetText(uiText.gameObject, setting);
            }
            else
            {
                Debug.LogWarning($"{fieldName} 누락됨.");
            }
        }
        
        private void SetupAnswerTexts(TextSetting[] providedAnswers)
        {
            if (answerTexts == null) return;

            if (providedAnswers == null)
            {
                Debug.LogWarning("answerTexts 배열 데이터 누락됨.");
                return;
            }

            int providedCount = providedAnswers.Length;

            for (int i = 0; i < answerTexts.Length; i++)
            {
                Text txt = answerTexts[i];
                if (txt == null) continue;

                bool hasData = i < providedCount && providedAnswers[i] != null;
                txt.gameObject.SetActive(hasData);

                if (hasData && _uiManager != null)
                {
                    _uiManager.SetText(txt.gameObject, providedAnswers[i]);
                }
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _isAnsweredA = false;
            _isAnsweredB = false;
            _completionStarted = false;
            _isInputEnabled = false;
            
            ResetIdleState(true);
            SetGroupAlpha(contentsGroup, 0f);
            
            if (cgLightA) 
            { 
                cgLightA.alpha = 0f; 
                cgLightA.gameObject.SetActive(false); 
            }
            if (cgLightB) 
            { 
                cgLightB.alpha = 0f; 
                cgLightB.gameObject.SetActive(false); 
            }

            if (_sessionManager && _gameManager)
            {
                Sprite spriteA = _gameManager.GetColorSprite(_sessionManager.PlayerAColor);
                if (imgLightA) imgLightA.sprite = spriteA;

                Sprite spriteB = _gameManager.GetColorSprite(_sessionManager.PlayerBColor);
                if (imgLightB) imgLightB.sprite = spriteB;
            }

            _sequenceCts?.Cancel();
            _sequenceCts?.Dispose();
            _sequenceCts = new CancellationTokenSource();
            
            ShowSequenceAsync(_sequenceCts.Token).Forget();
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
            if (_completionStarted) return;

            bool inputDetected = false;
            if (_isInputEnabled) inputDetected = ProcessCommonKeyboardInput();

            if (inputDetected || Input.anyKey || Input.touchCount > 0) ResetIdleState(false);
            else UpdateInactivity(!_isInputEnabled);
        }

        protected override void OnHardwareInput(string input, bool isLeft)
        {
            if (_completionStarted || !_isInputEnabled) return;

            int selectedValue = 0;
            string side = isLeft ? SideLeft : SideRight;

            switch (input)
            {
                case GameConstants.Hardware.Input1On: selectedValue = 1; break;
                case GameConstants.Hardware.Input2On: selectedValue = 2; break;
                case GameConstants.Hardware.Input3On: selectedValue = 3; break;
                case GameConstants.Hardware.Input4On: selectedValue = 4; break;
                case GameConstants.Hardware.Input5On: selectedValue = 5; break;
            }

            if (selectedValue != 0) ProcessInput(selectedValue, side);
        }

        private void ProcessInput(int selectedValue, string side)
        {
            bool isPlayerA = side == SideLeft;

            if (HasAlreadyAnswered(isPlayerA)) return;

            ResetIdleState(false);
            MarkAsAnswered(isPlayerA);
            TurnOffHardwareLed(isPlayerA);
            
            if (_sessionManager && _levelManager)
            {
                string playerName = isPlayerA ? _sessionManager.PlayerAFirstName : _sessionManager.PlayerBFirstName;
                int qNo = _levelManager.CurrentQuestionNumber;
                _logger?.ZLogInformation($"[Page_QnA] {playerName}이(가) {qNo}번 질문에서 {selectedValue}번 답변을 선택함.");
            }
            
            SendApiUpdate(side, selectedValue);
            ShowLightUI(isPlayerA);
            
            CheckCompletion();
        }
        
        private bool HasAlreadyAnswered(bool isPlayerA)
        {
            return isPlayerA ? _isAnsweredA : _isAnsweredB;
        }

        private void MarkAsAnswered(bool isPlayerA)
        {
            if (isPlayerA) _isAnsweredA = true;
            else _isAnsweredB = true;
        }

        private void TurnOffHardwareLed(bool isPlayerA)
        {
            if (!_arduinoManager) return;
            
            if (isPlayerA) _arduinoManager.SendCommandToLeft(GameConstants.Hardware.CmdLedAllOff);
            else _arduinoManager.SendCommandToRight(GameConstants.Hardware.CmdLedAllOff);
        }

        private void SendApiUpdate(string side, int selectedValue)
        {
            if (!_gameManager || !_levelManager) return;

            int qNo = _levelManager.CurrentQuestionNumber;
            if (qNo > 0)
            {
                _gameManager.SendValueUpdateAPI(qNo, side, selectedValue);
            }
        }

        private void ShowLightUI(bool isPlayerA)
        {
            CanvasGroup targetCg = isPlayerA ? cgLightA : cgLightB;
            if (targetCg)
            {
                targetCg.gameObject.SetActive(true);
                targetCg.FadeAsync(0f, 1f, 1.0f, this.GetCancellationTokenOnDestroy()).Forget();
            }
        }

        private void CheckCompletion()
        {   
            if (_isAnsweredA && _isAnsweredB && !_completionStarted)
            {
                _completionStarted = true;
                CompleteAsync(this.GetCancellationTokenOnDestroy()).Forget();
            }
        }
        
        private async UniTaskVoid CompleteAsync(CancellationToken token)
        {   
            if (_soundManager) _soundManager.PlaySFX("공통_22");
            await UniTask.Delay(TimeSpan.FromSeconds(1.0), ignoreTimeScale: true, cancellationToken: token);
            CompleteStep();
        }
        
        private async UniTaskVoid ShowSequenceAsync(CancellationToken token)
        {
            if (canvasGroup) await UniTask.WaitUntil(canvasGroup, cg => cg.alpha >= 0.9f, PlayerLoopTiming.Update, token);
            
            if (_soundManager) _soundManager.PlaySFX("공통_8");
            
            if (contentsGroup)
            {
                contentsGroup.gameObject.SetActive(true);
                await contentsGroup.FadeAsync(0f, 1f, 0.5f, token);
            }
            
            if (_arduinoManager)
            {
                _arduinoManager.SendCommandToBoth(GameConstants.Hardware.CmdSoundOn);
                await UniTask.Delay(TimeSpan.FromSeconds(0.1), ignoreTimeScale: true, cancellationToken: token);
                _arduinoManager.SendCommandToBoth(GameConstants.Hardware.CmdLedAllOn);
            }
            
            bool isTutorial = _levelManager && _levelManager.CurrentQuestionNumber == 0;
            if (!isTutorial)
            {
                _isInputEnabled = true;
                return;
            }

            _isInputEnabled = false; 
            await UniTask.Delay(TimeSpan.FromSeconds(3.5), ignoreTimeScale: true, cancellationToken: token);
            
            if (_arduinoManager)
            {
                _arduinoManager.SendCommandToBoth(GameConstants.Hardware.CmdLedAllOff);
            }
            
            CompleteStep();
        }

        private void SetGroupAlpha(CanvasGroup cg, float alpha)
        {
            if (cg)
            {
                cg.alpha = alpha;
                cg.gameObject.SetActive(alpha > 0f);
            }
        }

        protected override void OnDestroy()
        {
            _sequenceCts?.Cancel();
            _sequenceCts?.Dispose();
            _sequenceCts = null;

            base.OnDestroy();
        }
    }
}