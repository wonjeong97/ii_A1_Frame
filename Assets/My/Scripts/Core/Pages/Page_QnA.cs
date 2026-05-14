using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using My.Scripts.Core.Data;
using My.Scripts.Global;
using My.Scripts.Hardware;
using My.Scripts.Utils;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts.Core.Pages
{
    /// <summary>
    /// 양측 플레이어의 하드웨어 입력을 받아 질문에 대한 답변을 처리하고 API를 동기화하는 페이지.
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

        /// <summary>
        /// 외부 JSON 데이터의 텍스트를 UI에 바인딩하며, 누락 시 경고 로그를 출력함.
        /// </summary>
        /// <param name="data">초기화할 QnA 데이터 객체</param>
        protected override void SetupData(QnAPageData data)
        {
            if (data == null) return;

            ApplyTextSetting(descriptionText, data.descriptionText, "descriptionText");
            ApplyTextSetting(questionText, data.questionText, "questionText");
            ApplyTextSetting(nicknameA, data.nicknamePlayerA, "nicknamePlayerA");
            ApplyTextSetting(nicknameB, data.nicknamePlayerB, "nicknamePlayerB");

            SetupAnswerTexts(data.answerTexts);
        }
        
        /// <summary>
        /// 단일 텍스트 컴포넌트에 데이터를 적용하고, 유효하지 않을 경우 경고 로그를 출력함.
        /// </summary>
        private void ApplyTextSetting(Text uiText, TextSetting setting, string fieldName)
        {
            if (setting != null && !string.IsNullOrEmpty(setting.text))
            {
                if (uiText) UIManager.Instance.SetText(uiText.gameObject, setting);
            }
            else
            {
                Debug.LogWarning($"{fieldName} 누락됨.");
            }
        }
        
        /// <summary>
        /// 다중 선택지 텍스트 배열을 순회하며 UI에 적용하고 활성 상태를 제어함.
        /// </summary>
        private void SetupAnswerTexts(TextSetting[] providedAnswers)
        {
            if (answerTexts == null) return;

            if (providedAnswers == null)
            {
                Debug.LogWarning("answerTexts 배열 데이터 누락됨.");
                return;
            }

            for (int i = 0; i < answerTexts.Length; i++)
            {
                if (!answerTexts[i]) continue;
                
                if (i < providedAnswers.Length && providedAnswers[i] != null)
                {
                    UIManager.Instance.SetText(answerTexts[i].gameObject, providedAnswers[i]);
                    answerTexts[i].gameObject.SetActive(true);
                }
                else
                {
                    answerTexts[i].gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 페이지 활성화 시 내부 상태를 초기화하고 연출 시퀀스를 시작함.
        /// </summary>
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

            if (SessionManager.Instance && GameManager.Instance)
            {
                Sprite spriteA = GameManager.Instance.GetColorSprite(SessionManager.Instance.PlayerAColor);
                if (imgLightA) imgLightA.sprite = spriteA;

                Sprite spriteB = GameManager.Instance.GetColorSprite(SessionManager.Instance.PlayerBColor);
                if (imgLightB) imgLightB.sprite = spriteB;
            }

            _sequenceCts?.Cancel();
            _sequenceCts?.Dispose();
            _sequenceCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            
            ShowSequenceAsync(_sequenceCts.Token).Forget();
        }
        
        public override void OnExit()
        {
            _sequenceCts?.Cancel();
            _sequenceCts?.Dispose();
            _sequenceCts = null;
            base.OnExit();
        }

        /// <summary>
        /// 매 프레임 키보드 또는 터치 입력을 감지하여 무응답 타이머를 갱신함.
        /// </summary>
        private void Update()
        {
            if (_completionStarted) return;

            bool inputDetected = false;
            if (_isInputEnabled) inputDetected = ProcessCommonKeyboardInput();

            if (inputDetected || Input.anyKey || Input.touchCount > 0) ResetIdleState(false);
            else UpdateInactivity(!_isInputEnabled);
        }

        /// <summary>
        /// 아두이노 하드웨어의 버튼 이벤트를 수신하여 로직으로 전달함.
        /// </summary>
        /// <param name="input">버튼 신호 문자열</param>
        /// <param name="isLeft">좌측(Player A) 여부</param>
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

        /// <summary>
        /// 유효한 입력에 대해 LED 상태를 끄고 API 서버에 선택값을 전송함.
        /// </summary>
        private void ProcessInput(int selectedValue, string side)
        {
            bool isPlayerA = side.Equals("left");

            // 이미 답변을 완료한 상태라면 하위 로직을 무시함 (가드 클로즈)
            if (HasAlreadyAnswered(isPlayerA)) return;

            ResetIdleState(false);
            MarkAsAnswered(isPlayerA);
            
            TurnOffHardwareLed(isPlayerA);
            SendApiUpdate(side, selectedValue);
            ShowLightUI(isPlayerA);
            
            CheckCompletion();
        }
        
        /// <summary> 해당 플레이어가 이미 답변을 제출했는지 확인함. </summary>
        private bool HasAlreadyAnswered(bool isPlayerA)
        {
            return isPlayerA ? _isAnsweredA : _isAnsweredB;
        }

        /// <summary> 플레이어의 답변 완료 상태를 갱신함. </summary>
        private void MarkAsAnswered(bool isPlayerA)
        {
            if (isPlayerA) _isAnsweredA = true;
            else _isAnsweredB = true;
        }

        /// <summary> 선택 완료 피드백으로 해당 플레이어 측 아두이노 LED를 소등함. </summary>
        private void TurnOffHardwareLed(bool isPlayerA)
        {
            if (!ArduinoManager.Instance) return;
            
            if (isPlayerA) ArduinoManager.Instance.SendCommandToLeft(GameConstants.Hardware.CmdLedAllOff);
            else ArduinoManager.Instance.SendCommandToRight(GameConstants.Hardware.CmdLedAllOff);
        }

        /// <summary> 현재 문항 번호와 선택한 값을 API 서버로 전송함. </summary>
        private void SendApiUpdate(string side, int selectedValue)
        {
            if (!GameManager.Instance || !LevelManager.Instance) return;

            int qNo = LevelManager.Instance.CurrentQuestionNumber;
            if (qNo > 0)
            {
                GameManager.Instance.SendValueUpdateAPI(qNo, side, selectedValue);
            }
        }

        /// <summary> 화면 상의 선택 완료 라이트 점등 연출을 가동함. </summary>
        private void ShowLightUI(bool isPlayerA)
        {
            CanvasGroup targetCg = isPlayerA ? cgLightA : cgLightB;
            if (targetCg)
            {
                targetCg.gameObject.SetActive(true);
                UIFadeUtility.FadeCanvasGroupAsync(targetCg, 0f, 1f, 1.0f, this.GetCancellationTokenOnDestroy()).Forget();
            }
        }

        /// <summary>
        /// 양측 플레이어가 모두 답변을 완료했는지 확인하고 완료 절차를 트리거함.
        /// </summary>
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
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_22");
            await UniTask.Delay(TimeSpan.FromSeconds(1.0), cancellationToken: token);
            CompleteStep();
        }
        
        private async UniTaskVoid ShowSequenceAsync(CancellationToken token)
        {
            if (canvasGroup) await UniTask.WaitUntil(canvasGroup, cg => cg.alpha >= 0.9f, PlayerLoopTiming.Update, token);
            
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_8");
            
            if (contentsGroup)
            {
                contentsGroup.gameObject.SetActive(true);
                await UIFadeUtility.FadeCanvasGroupAsync(contentsGroup, 0f, 1f, 0.5f, token);
            }
            
            if (ArduinoManager.Instance)
            {
                ArduinoManager.Instance.SendCommandToBoth(GameConstants.Hardware.CmdSoundOn);
                await UniTask.Delay(TimeSpan.FromSeconds(0.1), cancellationToken: token);
                ArduinoManager.Instance.SendCommandToBoth(GameConstants.Hardware.CmdLedAllOn);
            }
            
            bool isTutorial = LevelManager.Instance && LevelManager.Instance.CurrentQuestionNumber == 0;
            
            if (isTutorial)
            {
                _isInputEnabled = false; 
                await UniTask.Delay(TimeSpan.FromSeconds(3.5), cancellationToken: token);
                
                if (ArduinoManager.Instance)
                {
                    ArduinoManager.Instance.SendCommandToBoth(GameConstants.Hardware.CmdLedAllOff);
                }
                
                CompleteStep();
            }
            else
            {
                _isInputEnabled = true;
            }
        }

        /// <summary>
        /// 캔버스 그룹의 투명도를 즉시 설정하고, 0일 경우 비활성화하여 오버드로우를 줄임.
        /// </summary>
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