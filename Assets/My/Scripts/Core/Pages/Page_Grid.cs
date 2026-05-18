using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using My.Scripts.Core.Data;
using My.Scripts.Utils;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts.Core.Pages
{
    /// <summary> 
    /// 다이얼 입력을 받아 보이지 않는 격자판을 탐색하고 정답 위치를 찾아내는 미니게임을 제어함.
    /// UIManager.Instance 싱글톤 의존성을 완전히 도려내고 부모 인스턴스 주입 필드로 재매핑 완료되었습니다.
    /// </summary>
    public class Page_Grid : PopupGamePage<GridPageData>
    {
        [Header("UI References")] 
        [SerializeField] private Text textMain;
        [SerializeField] private CanvasGroup mainTextGroup;
        [SerializeField] private Text textSub; 
        [SerializeField] private Text[] questionTexts; 
        [SerializeField] private Text textCounting;

        [Header("Interaction")] 
        [SerializeField] private Image imageBlack; 
        [SerializeField] private Image imageGrid; 

        [Header("Completion & Groups")] 
        [SerializeField] private List<CanvasGroup> completionCanvasGroups; 
        [SerializeField] private List<CanvasGroup> textCanvasGroups; 
        
        [Header("Settings")]
        [SerializeField] private List<Vector2Int> questionSpots; 
        [SerializeField] private int gridSizeX = 6; 
        [SerializeField] private int gridSizeY = 5; 
        
        private readonly float cellFadeDuration = 0.05f; 

        [Header("Breathing Effect")]
        [SerializeField, Range(0f, 1f)] private float breathAlphaMin = 0.1f;
        [SerializeField, Range(0f, 1f)] private float breathAlphaMax = 0.3f;
        [SerializeField] private float breathSpeed = 2.0f;
        
        [Header("Timer UI")]
        [SerializeField] private Image imageTimer;
        [SerializeField] private Text textTimer;
        [SerializeField] private Color timerNormalColor = Color.white;
        [SerializeField] private Color timerWarningColor = Color.red;
        
        [Header("Fail Popup")]
        [SerializeField] private CanvasGroup failPopupGroup;
        [SerializeField] private Text textFail;
        
        private TextSetting _failTextSetting;
        private bool _isFailPopupActive;
        private const float GridTimeLimit = 15f;

        private RectTransform _blackRect; 
        private Texture2D _maskTexture; 
        private Material _eraserMaterial, _gridMaterial; 
        private readonly static int MaskTexID = Shader.PropertyToID("_MaskTex");

        private float _cellWidth, _cellHeight; 
        private int _currentGridX, _currentGridY; 
        private bool[,] _questionMap; 
        private bool _hasMoved; 
        private bool _isInputBlocked; 
        private bool _isStageCompleted; 
        private readonly HashSet<Vector2Int> _foundSpots = new HashSet<Vector2Int>(); 
        private int _totalQuestionCount; 

        private TextSetting _defaultTextSub; 
        private TextSetting _warningText; 
        private const float BlinkThreshold = 10f; 
        private bool _is1stWarningDone; 

        private PlayerWheelState _p1State;
        private PlayerWheelState _p2State;

        private const float FastInputThreshold = 0.2f; 
        private const float BounceThreshold = 0.05f;    
        
        private CancellationTokenSource _autoFadeCts;
        private CancellationTokenSource _timerCts;
        private CancellationTokenSource _failPopupCts;
        private CancellationTokenSource _textFadeCts;
        private CancellationTokenSource _textBlinkCts;
        private CancellationTokenSource _simultaneousWarningCts;
        private CancellationTokenSource _completionCts;
        private CancellationTokenSource _hintCts;

        private struct CellFadeState
        {
            public float startVal;
            public float targetVal;
            public float timer;
            public bool isActive;
        }
        private CellFadeState[,] _cellFadeStates;

        protected override void SetupData(GridPageData data)
        {
            if (data == null) return;

            ApplyTextSetting(textMain, data.descriptionText1, "descriptionText1");
            ApplyTextSetting(textSub, data.descriptionText2, "descriptionText2");
            ApplyTextSetting(textFail, data.failText, "failText");

            _defaultTextSub = data.descriptionText2;
            _warningText = data.descriptionText3;
            _failTextSetting = data.failText;

            FilterValidQuestionSpots(data);
            SetupQuestionTexts(data);
            SetupPopupMessage(data.warningMessage, data.resetMessage);
        }

        private void ApplyTextSetting(Text uiText, TextSetting setting, string fieldName)
        {
            if (setting != null)
            {
                if (uiText && _uiManager != null) _uiManager.SetText(uiText.gameObject, setting);
            }
            else
            {
                Debug.LogWarning($"{fieldName} 데이터 누락됨.");
            }
        }

        private void FilterValidQuestionSpots(GridPageData data)
        {
            if (data.questionSpots != null && data.questionSpots.Count > 0)
            {
                HashSet<Vector2Int> filtered = new HashSet<Vector2Int>();
                foreach (Vector2Int spot in data.questionSpots)
                {
                    if (spot.x >= 0 && spot.x < gridSizeX && spot.y >= 0 && spot.y < gridSizeY)
                    {
                        filtered.Add(spot);
                    }
                }
                questionSpots = new List<Vector2Int>(filtered);
            }
            else
            {
                Debug.LogWarning("questionSpots 배열이 비어있음.");
            }
        }

        private void SetupQuestionTexts(GridPageData data)
        {
            if (questionTexts == null) return;
            
            int questionDataCount = data.questions?.Length ?? 0;

            for (int i = 0; i < questionTexts.Length; i++)
            {
                Text txt = questionTexts[i];
                if (txt == null) continue; // 안전망 가드 클로즈

                bool hasData = i < questionDataCount && data.questions != null && data.questions[i] != null;
                
                txt.gameObject.SetActive(hasData);

                // 데이터가 있을 때만 안전하게 텍스트 주입 수행
                if (hasData && _uiManager != null)
                {
                    _uiManager.SetText(txt.gameObject, data.questions[i]);
                }
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();

            ResetPageUI();
            InitializeRoutines();

            if (!InitializeGame()) return;

            Vector2Int startPos = CalculateStartingPosition();
            SetFocusToGrid(startPos.x, startPos.y, true);
        }

        private void ResetPageUI()
        {
            if (textCounting)
            {
                int qNum = LevelManager.Instance ? LevelManager.Instance.CurrentQuestionNumber : 0;
                textCounting.text = qNum > 0 ? $"{qNum}/15" : string.Empty;
            }

            if (mainTextGroup) 
            {
                mainTextGroup.gameObject.SetActive(true);
                mainTextGroup.alpha = 1f;
            }
            
            if (textMain) 
            {
                textMain.gameObject.SetActive(true);
                Color c = textMain.color; 
                c.a = 1f; 
                textMain.color = c;
            }

            if (failPopupGroup)
            {
                failPopupGroup.alpha = 0f;
                failPopupGroup.gameObject.SetActive(false);
            }

            _hasMoved = false;
            _isInputBlocked = false;
            _isFailPopupActive = false;
            _isStageCompleted = false;
        }

        private void CancelAndDispose(ref CancellationTokenSource cts)
        {
            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
                cts = null;
            }
        }
        
        private void InitializeRoutines()
        {
            CancelAndDispose(ref _autoFadeCts);
            _autoFadeCts = new CancellationTokenSource();
            AutoFadeMainTextAsync(_autoFadeCts.Token).Forget();
            
            CancelAndDispose(ref _timerCts);
            _timerCts = new CancellationTokenSource();
            TimerAsync(_timerCts.Token).Forget();

            CancelAndDispose(ref _simultaneousWarningCts);
            CancelAndDispose(ref _completionCts);
            CancelAndDispose(ref _hintCts);

            _p1State = PlayerWheelState.Default;
            _p2State = PlayerWheelState.Default;

            ResetIdleState(true); 
        }

        private bool InitializeGame()
        {
            if (!imageBlack || gridSizeX <= 0 || gridSizeY <= 0) return false;
            
            _blackRect = imageBlack.rectTransform;
            _cellWidth = _blackRect.rect.width / gridSizeX;
            _cellHeight = _blackRect.rect.height / gridSizeY;
            
            _foundSpots.Clear();

            _cellFadeStates = new CellFadeState[gridSizeX, gridSizeY];

            ResetCanvasGroups(completionCanvasGroups, 0f, true);
            ResetCanvasGroups(textCanvasGroups, 1f, true);

            BuildQuestionMap();
            CreateGridMaterials();

            return true;
        }

        private void BuildQuestionMap()
        {
            _questionMap = new bool[gridSizeX, gridSizeY];
            _totalQuestionCount = 0;

            if (questionSpots != null)
            {
                foreach (Vector2Int s in questionSpots)
                {
                    if (s.x >= 0 && s.x < gridSizeX && s.y >= 0 && s.y < gridSizeY)
                    {
                        _questionMap[s.x, s.y] = true;
                    }
                }
                _totalQuestionCount = questionSpots.Count;
            }

            if (_totalQuestionCount == 0)
            {
                int centerX = Mathf.Max(0, (gridSizeX - 1) / 2);
                int centerY = Mathf.Max(0, gridSizeY / 2);
                _questionMap[centerX, centerY] = true;
                _totalQuestionCount = 1;
            }
        }

        private void CreateGridMaterials()
        {
            if (_maskTexture) Destroy(_maskTexture);
            if (_eraserMaterial) Destroy(_eraserMaterial);
            if (_gridMaterial) Destroy(_gridMaterial);

            _eraserMaterial = Instantiate(imageBlack.material);
            imageBlack.material = _eraserMaterial;
            
            _maskTexture = new Texture2D(gridSizeX, gridSizeY, TextureFormat.R8, false) { filterMode = FilterMode.Point };
            
            for (int y = 0; y < gridSizeY; y++)
            {
                for (int x = 0; x < gridSizeX; x++)
                {
                    _maskTexture.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
                }
            }
            _maskTexture.Apply();
            _eraserMaterial.SetTexture(MaskTexID, _maskTexture);

            if (imageGrid)
            {
                _gridMaterial = Instantiate(imageGrid.material);
                imageGrid.material = _gridMaterial;
                _gridMaterial.SetTexture(MaskTexID, _maskTexture);
            }
        }

        private Vector2Int CalculateStartingPosition()
        {
            int startX = Mathf.Max(0, (gridSizeX - 1) / 2);
            int startY = Mathf.Max(0, gridSizeY / 2);

            if (_questionMap == null || !_questionMap[startX, startY])
            {
                return new Vector2Int(startX, startY);
            }

            return FindAdjacentEmptySpot(startX, startY);
        }
        
        private Vector2Int FindAdjacentEmptySpot(int startX, int startY)
        {
            Vector2Int[] offsets = {
                new Vector2Int(0, 1), new Vector2Int(0, -1),
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(1, 1), new Vector2Int(1, -1),
                new Vector2Int(-1, 1), new Vector2Int(-1, -1)
            };

            foreach (Vector2Int offset in offsets)
            {
                int nx = startX + offset.x;
                int ny = startY + offset.y;

                if (IsPositionValidAndEmpty(nx, ny))
                {
                    return new Vector2Int(nx, ny);
                }
            }

            Debug.LogWarning("시작점 주변에 안전한 오답 칸이 없음.");
            return new Vector2Int(startX, startY);
        }
        
        private bool IsPositionValidAndEmpty(int x, int y)
        {
            if (x < 0 || x >= gridSizeX || y < 0 || y >= gridSizeY) return false;
            return !_questionMap[x, y];
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space) && !_isStageCompleted)
            {
                _isStageCompleted = true;
                RevealAllQuestions();
                CancelAndDispose(ref _completionCts);
                _completionCts = new CancellationTokenSource();
                ShowCompletionAsync(_completionCts.Token).Forget();
            }

            if (_p1State.inputTimer > 0f) _p1State.inputTimer -= Time.unscaledDeltaTime;
            if (_p2State.inputTimer > 0f) _p2State.inputTimer -= Time.unscaledDeltaTime;

            if (HasValidInput())
            {
                ResetIdleState(false); 
                HandleMovement();
            }
            else
            {
                HandleInactivity();
            }

            UpdateCellFades();
        }

        private bool HasValidInput()
        {
            if (Input.touchCount > 0) return true;

            for (int i = 1; i <= 9; i++)
            {
                if (Input.GetKey((KeyCode)((int)KeyCode.Alpha0 + i)) || 
                    Input.GetKey((KeyCode)((int)KeyCode.Keypad0 + i)))
                {
                    return true;
                }
            }
            
            if (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Return) ||
                Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow) ||
                Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow))
            {
                return true;
            }

            return false;
        }

        private void HandleInactivity()
        {
            if (_isInputBlocked || _isStageCompleted || isResetSequenceActive) return;

            currentIdleTime += Time.unscaledDeltaTime;

            if (currentIdleTime >= inactivityThreshold)
            {
                StartResetSequence();
                return;
            }

            if (currentIdleTime >= BlinkThreshold)
            {
                TryTriggerBlinkWarning();
            }
        }
        
        private void TryTriggerBlinkWarning()
        {
            if (_is1stWarningDone || _textBlinkCts != null || _simultaneousWarningCts != null) return;

            // [에러 해결 완료] 주입 인스턴스 참조 전환
            if (_warningText != null && textSub && _uiManager != null)
            {
                _uiManager.SetText(textSub.gameObject, _warningText);
            }
            
            _textBlinkCts = new CancellationTokenSource();
            BlinkAsync(_textBlinkCts.Token).Forget();
        }
        
        private async UniTaskVoid BlinkAsync(CancellationToken token)
        {
            try
            {
                if (textSub)
                {
                    textSub.gameObject.SetActive(true);
                    textSub.SetAlpha(0f);
                }

                for (int i = 0; i < 2; i++)
                {
                    await textSub.FadeAsync(0f, 1f, 1f, token);
                    await textSub.FadeAsync(1f, 0f, 1f, token);
                }
            }
            catch (OperationCanceledException)
            {
                // 취소되더라도 무조건 오브젝트 상태를 기저 상태로 정상 회수
            }
            finally
            {
                if (textSub) textSub.gameObject.SetActive(false);
                _is1stWarningDone = true;
                CancelAndDispose(ref _textBlinkCts);
            }
        }
        
        private void TriggerSimultaneousWarning()
        {
            if (_simultaneousWarningCts != null) return;

            CancelAndDispose(ref _textBlinkCts);
            CancelAndDispose(ref _textFadeCts);

            _simultaneousWarningCts = new CancellationTokenSource();
            SimultaneousWarningAsync(_simultaneousWarningCts.Token).Forget();
        }
        
        private async UniTaskVoid SimultaneousWarningAsync(CancellationToken token)
        {
            if (textSub)
            {
                textSub.gameObject.SetActive(true);
                // [에러 해결 완료] 주입 인스턴스 참조 전환
                if (_defaultTextSub != null && _uiManager != null) _uiManager.SetText(textSub.gameObject, _defaultTextSub);
                textSub.SetAlpha(0f);

                for (int i = 0; i < 2; i++)
                {
                    await textSub.FadeAsync(0f, 1f, 0.5f, token);
                    await textSub.FadeAsync(1f, 0f, 0.5f, token);
                }
                
                textSub.gameObject.SetActive(false);
            }
            CancelAndDispose(ref _simultaneousWarningCts);
        }

        protected override void ResetIdleState(bool immediate = false)
        {
            base.ResetIdleState(immediate);
            _is1stWarningDone = false;

            if (_textBlinkCts != null)
            {
                CancelAndDispose(ref _textBlinkCts);

                if (immediate && textSub) textSub.gameObject.SetActive(false);
                else if (textSub && _simultaneousWarningCts == null) textSub.gameObject.SetActive(true);
            }

            if (immediate && _simultaneousWarningCts != null)
            {
                CancelAndDispose(ref _simultaneousWarningCts);
                if (textSub) textSub.gameObject.SetActive(false);
            }
        }

        protected override void StartResetSequence()
        {
            CancelAndDispose(ref _textBlinkCts);
            CancelAndDispose(ref _simultaneousWarningCts);

            if (textSub) textSub.gameObject.SetActive(false);

            base.StartResetSequence();
        }

        private void HandleMovement()
        {
            if (_isInputBlocked || _isStageCompleted) return;

            int dx = 0;
            int dy = 0;

            ProcessHardwareDials(ref dx, ref dy);
            ProcessKeyboardArrows(ref dx, ref dy);

            if (dx != 0 || dy != 0)
            {
                ApplyGridMovement(dx, dy);
            }
        }
        
        private void ProcessHardwareDials(ref int dx, ref int dy)
        {
            int p1Key = GetValidPlayerKey(1, 4, ref _p1State);
            int p2Key = GetValidPlayerKey(5, 8, ref _p2State);

            ResolveSimultaneousInputs(ref p1Key, ref p2Key);

            if (p1Key != -1) dy = CalculateDirection(p1Key, ref _p1State, 0);
            if (p2Key != -1) dx = CalculateDirection(p2Key, ref _p2State, 5);
        }
        
        private int GetValidPlayerKey(int start, int end, ref PlayerWheelState state)
        {
            int keyRaw = WheelInputUtility.GetPressedKeyIndex(start, end);
            if (keyRaw != -1 && keyRaw == state.lastKey)
            {
                state.inputTimer = 0f;
            }
            return (keyRaw != -1 && keyRaw != state.lastKey) ? keyRaw : -1;
        }
        
        private void ProcessKeyboardArrows(ref int dx, ref int dy)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow)) dx = -1;
            if (Input.GetKeyDown(KeyCode.RightArrow)) dx = 1;
            if (Input.GetKeyDown(KeyCode.UpArrow)) dy = -1;
            if (Input.GetKeyDown(KeyCode.DownArrow)) dy = 1;
        }

        private void ResolveSimultaneousInputs(ref int p1Key, ref int p2Key)
        {
            bool isP1Active = (p1Key != -1);
            bool isP2Active = (p2Key != -1);

            if (isP1Active && isP2Active)
            {
                p1Key = -1;
                p2Key = -1;
                TriggerSimultaneousWarning();
                return;
            }

            if (isP1Active)
            {
                if (_p2State.inputTimer > 0f)
                {
                    p1Key = -1;
                    TriggerSimultaneousWarning();
                }
                else _p1State.inputTimer = 0.3f;
            }
            else if (isP2Active)
            {
                if (_p1State.inputTimer > 0f)
                {
                    p2Key = -1;
                    TriggerSimultaneousWarning();
                }
                else _p2State.inputTimer = 0.3f;
            }
        }

        private int CalculateDirection(int currentKey, ref PlayerWheelState state, int offset = 0)
        {
            int normalizedKey = currentKey - offset;
            return WheelInputUtility.ResolveDirection(normalizedKey, 4, ref state);
        }

        private void ApplyGridMovement(int dx, int dy)
        {
            HideInstructionTexts();

            int nextX = _currentGridX + dx;
            int nextY = _currentGridY + dy;
            
            if (nextX >= 0 && nextX < gridSizeX && nextY >= 0 && nextY < gridSizeY) 
            {
                SetFocusToGrid(nextX, nextY);
            }
        }
        
        private void HideInstructionTexts()
        {
            if (!_hasMoved)
            {
                _hasMoved = true;
                CancelAndDispose(ref _autoFadeCts);
                FadeOutMainInstructionAsync(this.GetCancellationTokenOnDestroy()).Forget();
            }

            FadeOutSubInstruction();
        }
        
        private async UniTaskVoid FadeOutMainInstructionAsync(CancellationToken token)
        {
            if (mainTextGroup && mainTextGroup.gameObject.activeSelf)
            {
                await mainTextGroup.FadeAsync(mainTextGroup.alpha, 0f, 0.3f, token);
                if (mainTextGroup) mainTextGroup.gameObject.SetActive(false);
            }
            else if (textMain && textMain.gameObject.activeSelf)
            {
                await textMain.FadeAsync(textMain.color.a, 0f, 0.3f, token);
                if (textMain) textMain.gameObject.SetActive(false);
            }
        }
        
        private void FadeOutSubInstruction()
        {
            if (!textSub || !textSub.gameObject.activeSelf) return;
            if (_textFadeCts != null || _simultaneousWarningCts != null || _textBlinkCts != null) return;

            _textFadeCts = new CancellationTokenSource();
            FadeOutSubInstructionAsync(_textFadeCts.Token).Forget();
        }

        private async UniTaskVoid FadeOutSubInstructionAsync(CancellationToken token)
        {
            await textSub.FadeAsync(textSub.color.a, 0f, 0.3f, token);
            
            if (textSub) textSub.gameObject.SetActive(false);
            CancelAndDispose(ref _textFadeCts);
            // [에러 해결 완료] 주입 인스턴스 참조 전환
            if (_defaultTextSub != null && textSub && _uiManager != null) _uiManager.SetText(textSub.gameObject, _defaultTextSub);
        }

        private async UniTaskVoid AutoFadeMainTextAsync(CancellationToken token)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(3.0), cancellationToken: token);

            if (_hasMoved) return;
            _hasMoved = true;

            FadeOutMainInstructionAsync(token).Forget();
        }

        private void SetFocusToGrid(int x, int y, bool isFirstInit = false)
        {
            if (!isFirstInit)
            {
                if (!_questionMap[_currentGridX, _currentGridY]) 
                    StartCellFade(_currentGridX, _currentGridY, 0.0f); 
                else 
                    UpdateMaskPixelInstant(_currentGridX, _currentGridY, 1.0f, false); 
                
                _isInputBlocked = true;
            }

            _currentGridX = x;
            _currentGridY = y;
            
            if (isFirstInit) UpdateMaskPixelInstant(x, y, 1.0f);
            else StartCellFade(x, y, 1.0f); 
            
            if (!isFirstInit) 
            {
                CheckQuestionFound(x, y);
            }
        }

        private void CheckQuestionFound(int x, int y)
        {
            if (_questionMap[x, y])
            {
                Vector2Int p = new Vector2Int(x, y);
                if (!_foundSpots.Contains(p))
                {
                    _foundSpots.Add(p);
                    if (_soundManager) _soundManager.PlaySFX("카메라_2");
                    if (_foundSpots.Count >= _totalQuestionCount && !_isStageCompleted)
                    {
                        _isStageCompleted = true;
                        CancelAndDispose(ref _completionCts);
                        _completionCts = new CancellationTokenSource();
                        ShowCompletionAsync(_completionCts.Token).Forget();
                    }
                }
            }
        }

        private async UniTaskVoid ShowCompletionAsync(CancellationToken token)
        {   
            if (_soundManager) _soundManager.PlaySFX("카메라_3");

            UpdateMaskPixelInstant(_currentGridX, _currentGridY, 1.0f, true);

            await FadeGroupsAsync(completionCanvasGroups, 0f, 1f, 1f, token);

            await UniTask.Delay(TimeSpan.FromSeconds(2.0), cancellationToken: token);
            
            await FadeOutGridAndTextsAsync(0.5f, token);

            CompleteStep();
        }

        private async UniTask FadeGroupsAsync(List<CanvasGroup> groups, float startAlpha, float endAlpha, float duration, CancellationToken token)
        {
            if (groups == null || groups.Count == 0) return;

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                SetCanvasGroupsAlpha(groups, Mathf.Lerp(startAlpha, endAlpha, t / duration));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            SetCanvasGroupsAlpha(groups, endAlpha);
        }
        
        private void SetCanvasGroupsAlpha(List<CanvasGroup> groups, float alpha)
        {
            if (groups == null) return;
            foreach (CanvasGroup cg in groups)
            {
                if (cg) cg.alpha = alpha;
            }
        }

        private async UniTask FadeOutGridAndTextsAsync(float duration, CancellationToken token)
        {
            float t = 0f;
            float startGridAlpha = imageGrid ? imageGrid.color.a : 1f;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float progress = t / duration;

                if (imageGrid)
                {
                    Color c = imageGrid.color;
                    c.a = Mathf.Lerp(startGridAlpha, 0f, progress);
                    imageGrid.color = c;
                }

                SetCanvasGroupsAlpha(textCanvasGroups, Mathf.Lerp(1f, 0f, progress));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        private void StartCellFade(int x, int y, float targetVal)
        {
            _cellFadeStates[x, y].startVal = GetMaskPixelValue(x, y);
            _cellFadeStates[x, y].targetVal = targetVal;
            _cellFadeStates[x, y].timer = 0f;
            _cellFadeStates[x, y].isActive = true;
        }

        private void UpdateCellFades()
        {
            bool needsApply = ProcessActiveFades();

            if (ApplyBreathingEffect())
            {
                needsApply = true;
            }
            
            if (needsApply && _maskTexture) 
            {
                _maskTexture.Apply();
            }
        }
        
        private bool ProcessActiveFades()
        {
            if (_cellFadeStates == null) return false;

            bool isUpdated = false;
    
            // 이중 루프는 오직 '순회' 역할만 전담하여 최상위 복잡도를 제거
            for (int x = 0; x < gridSizeX; x++)
            {
                for (int y = 0; y < gridSizeY; y++)
                {
                    // 단일 셀 업데이트 결과가 하나라도 true면 전체 마스크 Apply 트리거 활성화
                    if (UpdateSingleCellFade(x, y))
                    {
                        isUpdated = true;
                    }
                }
            }
            return isUpdated;
        }

        /// <summary>
        /// 단일 격자 셀의 페이드 상태를 갱신합니다. (추출된 고속 인터페이스)
        /// </summary>
        private bool UpdateSingleCellFade(int x, int y)
        {
            ref CellFadeState state = ref _cellFadeStates[x, y];
            if (!state.isActive) return false;

            state.timer += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(state.timer / cellFadeDuration);
    
            // 보간 연산 및 픽셀 마스크 업데이트
            float currentAlpha = Mathf.Lerp(state.startVal, state.targetVal, p);
            UpdateMaskPixelInstant(x, y, currentAlpha, false);

            // 해당 셀의 페이드가 도달 완료되었을 때의 상태 해제 처리
            if (p >= 1.0f)
            {
                state.isActive = false;
                if (x == _currentGridX && y == _currentGridY)
                {
                    _isInputBlocked = false;
                }
            }

            return true;
        }

        private bool ApplyBreathingEffect()
        {
            if (_isStageCompleted || _questionMap == null || !_questionMap[_currentGridX, _currentGridY]) return false;

            if (_cellFadeStates[_currentGridX, _currentGridY].isActive) return false;

            float pingPong = Mathf.PingPong(Time.unscaledTime * breathSpeed, 1f); 
            float currentAlpha = Mathf.Lerp(breathAlphaMin, breathAlphaMax, pingPong);
            float breathMaskValue = 1.0f - currentAlpha;
            
            UpdateMaskPixelInstant(_currentGridX, _currentGridY, breathMaskValue, false);
            return true;
        }

        private float GetMaskPixelValue(int x, int y) =>
            _maskTexture ? _maskTexture.GetPixel(x, (gridSizeY - 1) - y).r : 0f;

        private void UpdateMaskPixelInstant(int x, int y, float v, bool a = true)
        {
            if (_maskTexture)
            {
                _maskTexture.SetPixel(x, (gridSizeY - 1) - y, new Color(v, 0, 0, 0));
                if (a) _maskTexture.Apply();
            }
        }

        public override void OnExit()
        {   
            CancelAndDispose(ref _autoFadeCts);
            CancelAndDispose(ref _timerCts);
            CancelAndDispose(ref _failPopupCts);
            CancelAndDispose(ref _textFadeCts);
            CancelAndDispose(ref _textBlinkCts);
            CancelAndDispose(ref _simultaneousWarningCts);
            CancelAndDispose(ref _completionCts);
            CancelAndDispose(ref _hintCts);
            
            base.OnExit();
            CleanupResources();
        }

        protected override void OnDestroy()
        {
            CancelAndDispose(ref _autoFadeCts);
            CancelAndDispose(ref _timerCts);
            CancelAndDispose(ref _failPopupCts);
            CancelAndDispose(ref _textFadeCts);
            CancelAndDispose(ref _textBlinkCts);
            CancelAndDispose(ref _simultaneousWarningCts);
            CancelAndDispose(ref _completionCts);
            CancelAndDispose(ref _hintCts);

            base.OnDestroy();
            CleanupResources();
        }

        private void CleanupResources()
        {
            if (_maskTexture) Destroy(_maskTexture);
            if (_eraserMaterial) Destroy(_eraserMaterial);
            if (_gridMaterial) Destroy(_gridMaterial);
        }
        
        private async UniTaskVoid TimerAsync(CancellationToken token)
        {
            if (textTimer) textTimer.text = "15";
            SetTimerColor(timerNormalColor);

            await UniTask.Delay(TimeSpan.FromSeconds(0.5), cancellationToken: token);

            float currentTime = GridTimeLimit;
            int lastDisplayTime = 15;
            bool hasHinted = false;

            while (currentTime > 0)
            {
                if (_isStageCompleted) return;

                if (!isResetSequenceActive && !_isFailPopupActive)
                {
                    currentTime -= Time.unscaledDeltaTime;
                    int displayTime = Mathf.CeilToInt(currentTime);

                    ProcessTimerTick(displayTime, ref lastDisplayTime, ref hasHinted);
                }
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            if (textTimer) textTimer.text = "0";
            SetTimerColor(timerWarningColor);

            if (!_isStageCompleted && !isResetSequenceActive && !_isFailPopupActive)
            {
                CancelAndDispose(ref _failPopupCts);
                _failPopupCts = new CancellationTokenSource();
                ShowFailPopupAsync(_failPopupCts.Token).Forget();
            }
        }

        private void ProcessTimerTick(int displayTime, ref int lastDisplayTime, ref bool hasHinted)
        {
            if (displayTime != lastDisplayTime && displayTime > 0)
            {
                if (_soundManager) _soundManager.PlaySFX("공통_10_1초");
                lastDisplayTime = displayTime;
                
                if (displayTime == 5 && !hasHinted)
                {
                    hasHinted = true;
                    CancelAndDispose(ref _hintCts);
                    _hintCts = new CancellationTokenSource();
                    HintAnswerAsync(_hintCts.Token).Forget();
                }
            }

            if (textTimer) textTimer.text = displayTime.ToString();
            SetTimerColor(displayTime <= 5 && displayTime > 0 ? timerWarningColor : timerNormalColor);
        }
        
        private async UniTaskVoid ShowFailPopupAsync(CancellationToken token)
        {
            _isFailPopupActive = true;
            _isInputBlocked = true; 
            _isStageCompleted = true; 

            RevealAllQuestions();

            await UniTask.Delay(TimeSpan.FromSeconds(0.1), cancellationToken: token);

            if (failPopupGroup)
            {
                failPopupGroup.gameObject.SetActive(true);
                if (_soundManager) _soundManager.PlaySFX("공통_7");
                await failPopupGroup.FadeAsync(0f, 1f, 0.3f, token);
            }

            await UniTask.Delay(TimeSpan.FromSeconds(1.5), cancellationToken: token);

            if (failPopupGroup)
            {
                await failPopupGroup.FadeAsync(1f, 0f, 0.3f, token);
                failPopupGroup.gameObject.SetActive(false);
            }

            _isFailPopupActive = false;
            CompleteStep();
        }
        
        private async UniTaskVoid HintAnswerAsync(CancellationToken token)
        {
            if (AreAllQuestionsFound()) return;

            await FadeHintSpotsAsync(0.0f, 0.3f, 0.1f, token);
            
            if (_soundManager) _soundManager.PlaySFX("카메라_4");
            
            await UniTask.Delay(TimeSpan.FromSeconds(0.2), cancellationToken: token);

            await FadeHintSpotsAsync(0.3f, 0.0f, 0.1f, token);
        }

        private bool AreAllQuestionsFound()
        {
            if (questionSpots == null) return true;

            foreach (Vector2Int spot in questionSpots)
            {
                if (!_foundSpots.Contains(spot)) return false;
            }
            return true;
        }

        private async UniTask FadeHintSpotsAsync(float startVal, float endVal, float duration, CancellationToken token)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                ApplyHintMaskValue(Mathf.Lerp(startVal, endVal, t / duration));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            ApplyHintMaskValue(endVal);
        }
        
        private void ApplyHintMaskValue(float val)
        {
            if (questionSpots == null) return;

            foreach (Vector2Int spot in questionSpots)
            {
                if (!_foundSpots.Contains(spot) && spot.x >= 0 && spot.x < gridSizeX && spot.y >= 0 && gridSizeY > spot.y)
                {
                    UpdateMaskPixelInstant(spot.x, spot.y, val, false);
                }
            }
            if (_maskTexture) _maskTexture.Apply();
        }

        private void SetTimerColor(Color color)
        {
            if (imageTimer) imageTimer.color = color;
            if (textTimer) textTimer.color = color;
        }
        
        private void RevealAllQuestions()
        {
            if (questionSpots == null) return;

            foreach (Vector2Int spot in questionSpots)
            {
                if (spot.x >= 0 && spot.x < gridSizeX && spot.y >= 0 && spot.y < gridSizeY)
                {
                    StartCellFade(spot.x, spot.y, 1.0f);
                }
            }
            if (!_questionMap[_currentGridX, _currentGridY])
            {
                StartCellFade(_currentGridX, _currentGridY, 0.0f);
            }
        }

        private void ResetCanvasGroups(List<CanvasGroup> groups, float alpha, bool activate)
        {
            if (groups == null) return;
            foreach (CanvasGroup cg in groups)
            {
                if (cg)
                {
                    cg.alpha = alpha;
                    cg.gameObject.SetActive(activate);
                }
            }
        }
    }
}