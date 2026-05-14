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

        private class CellFadeInfo
        {
            public int x, y;
            public float startVal, targetVal, timer;
        }

        private readonly List<CellFadeInfo> _activeFades = new List<CellFadeInfo>();

        /// <summary>
        /// 외부 JSON 데이터의 텍스트와 정답 좌표를 UI 컴포넌트에 바인딩함.
        /// </summary>
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

        /// <summary>
        /// 텍스트 설정을 UI 컴포넌트에 적용하고 누락 시 경고를 출력함.
        /// </summary>
        private void ApplyTextSetting(Text uiText, TextSetting setting, string fieldName)
        {
            if (setting != null)
            {
                if (uiText) UIManager.Instance.SetText(uiText.gameObject, setting);
            }
            else
            {
                Debug.LogWarning($"{fieldName} 데이터 누락됨.");
            }
        }

        /// <summary>
        /// 유효한 그리드 범위 내의 정답 좌표만 필터링하여 저장함.
        /// </summary>
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

        /// <summary>
        /// 배열 형태의 문항 텍스트 데이터를 UI에 순차적으로 적용함.
        /// </summary>
        private void SetupQuestionTexts(GridPageData data)
        {
            if (questionTexts == null) return;

            for (int i = 0; i < questionTexts.Length; i++)
            {
                if (!questionTexts[i]) continue;
                
                if (data.questions != null && i < data.questions.Length && data.questions[i] != null)
                {
                    UIManager.Instance.SetText(questionTexts[i].gameObject, data.questions[i]);
                    questionTexts[i].gameObject.SetActive(true);
                }
                else
                {
                    questionTexts[i].gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 페이지 진입 시 그리드 상태를 초기화하고 플레이어 시작 지점을 계산하여 배치함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();

            ResetPageUI();
            InitializeRoutines();

            if (!InitializeGame()) return;

            Vector2Int startPos = CalculateStartingPosition();
            SetFocusToGrid(startPos.x, startPos.y, true);
        }

        /// <summary>
        /// UI 컴포넌트들의 초기 활성화 상태 및 투명도를 설정함.
        /// </summary>
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

        /// <summary>
        /// 취소 토큰 소스를 안전하게 해제하고 null로 초기화합니다.
        /// </summary>
        private void CancelAndDispose(ref CancellationTokenSource cts)
        {
            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
                cts = null;
            }
        }
        
        /// <summary>
        /// 상태 변수 및 동작 중인 비동기 작업을 초기화함.
        /// </summary>
        private void InitializeRoutines()
        {
            CancelAndDispose(ref _autoFadeCts);
            _autoFadeCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            AutoFadeMainTextAsync(_autoFadeCts.Token).Forget();
            
            CancelAndDispose(ref _timerCts);
            _timerCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            TimerAsync(_timerCts.Token).Forget();

            CancelAndDispose(ref _simultaneousWarningCts);
            CancelAndDispose(ref _completionCts);
            CancelAndDispose(ref _hintCts);

            _p1State = PlayerWheelState.Default;
            _p2State = PlayerWheelState.Default;

            ResetIdleState(true); 
        }

        /// <summary>
        /// 그리드 게임에 필요한 매터리얼과 맵 데이터를 생성함.
        /// </summary>
        /// <returns>초기화 성공 여부</returns>
        private bool InitializeGame()
        {
            if (!imageBlack || gridSizeX <= 0 || gridSizeY <= 0) return false;
            
            _blackRect = imageBlack.rectTransform;
            _cellWidth = _blackRect.rect.width / gridSizeX;
            _cellHeight = _blackRect.rect.height / gridSizeY;
            
            _foundSpots.Clear();
            _activeFades.Clear();

            ResetCanvasGroups(completionCanvasGroups, 0f, true);
            ResetCanvasGroups(textCanvasGroups, 1f, true);

            BuildQuestionMap();
            CreateGridMaterials();

            return true;
        }

        /// <summary>
        /// 정답 좌표를 기반으로 2D 부울 맵을 구성함.
        /// </summary>
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

        /// <summary>
        /// 마스크 처리용 텍스처와 매터리얼 인스턴스를 생성함.
        /// </summary>
        private void CreateGridMaterials()
        {
            if (_maskTexture) Destroy(_maskTexture);
            if (_eraserMaterial) Destroy(_eraserMaterial);
            if (_gridMaterial) Destroy(_gridMaterial);

            _eraserMaterial = Instantiate(imageBlack.material);
            imageBlack.material = _eraserMaterial;
            
            _maskTexture = new Texture2D(gridSizeX, gridSizeY, TextureFormat.R8, false) { filterMode = FilterMode.Point };
            _maskTexture.SetPixels32(new Color32[gridSizeX * gridSizeY]);
            _maskTexture.Apply();
            _eraserMaterial.SetTexture(MaskTexID, _maskTexture);

            if (imageGrid)
            {
                _gridMaterial = Instantiate(imageGrid.material);
                imageGrid.material = _gridMaterial;
                _gridMaterial.SetTexture(MaskTexID, _maskTexture);
            }
        }

        /// <summary>
        /// 플레이어의 시작 좌표를 중앙으로 설정하되, 정답과 겹치면 오작동을 피하기 위해 인접한 빈 공간을 탐색함.
        /// </summary>
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
        
        /// <summary>
        /// 시작 지점이 막혀있을 경우 8방향을 순회하며 가장 가까운 안전 지대를 반환함.
        /// </summary>
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

        /// <summary>
        /// 매 프레임 입력 및 무응답 상태를 확인하여 로직을 제어함.
        /// </summary>
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space) && !_isStageCompleted)
            {
                _isStageCompleted = true;
                RevealAllQuestions();
                CancelAndDispose(ref _completionCts);
                _completionCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
                ShowCompletionAsync(_completionCts.Token).Forget();
            }

            if (_p1State.inputTimer > 0f) _p1State.inputTimer -= Time.deltaTime;
            if (_p2State.inputTimer > 0f) _p2State.inputTimer -= Time.deltaTime;

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

        /// <summary>
        /// 화면 터치, 키패드 또는 화살표 키와 같은 유효 입력이 발생했는지 검사함.
        /// </summary>
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

        /// <summary>
        /// 무응답 시간을 누적하고 임계치에 따라 경고 및 실패 팝업을 트리거함.
        /// </summary>
        private void HandleInactivity()
        {
            if (_isInputBlocked || _isStageCompleted || isResetSequenceActive) return;

            currentIdleTime += Time.deltaTime;

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

            if (_warningText != null && textSub)
            {
                UIManager.Instance.SetText(textSub.gameObject, _warningText);
            }
            
            _textBlinkCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            BlinkAsync(_textBlinkCts.Token).Forget();
        }
        
        private async UniTaskVoid BlinkAsync(CancellationToken token)
        {
            if (textSub)
            {
                textSub.gameObject.SetActive(true);
                UIFadeUtility.SetAlpha(textSub, 0f);
            }

            for (int i = 0; i < 2; i++)
            {
                await UIFadeUtility.FadeGraphicAsync(textSub, 0f, 1f, 1f, token);
                await UIFadeUtility.FadeGraphicAsync(textSub, 1f, 0f, 1f, token);
            }
            
            if (textSub) textSub.gameObject.SetActive(false);

            _is1stWarningDone = true;
            CancelAndDispose(ref _textBlinkCts);
        }
        
        private void TriggerSimultaneousWarning()
        {
            if (_simultaneousWarningCts != null) return;

            CancelAndDispose(ref _textBlinkCts);
            CancelAndDispose(ref _textFadeCts);

            _simultaneousWarningCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            SimultaneousWarningAsync(_simultaneousWarningCts.Token).Forget();
        }
        
        private async UniTaskVoid SimultaneousWarningAsync(CancellationToken token)
        {
            if (textSub)
            {
                textSub.gameObject.SetActive(true);
                if (_defaultTextSub != null) UIManager.Instance.SetText(textSub.gameObject, _defaultTextSub);
                UIFadeUtility.SetAlpha(textSub, 0f);

                for (int i = 0; i < 2; i++)
                {
                    await UIFadeUtility.FadeGraphicAsync(textSub, 0f, 1f, 0.5f, token);
                    await UIFadeUtility.FadeGraphicAsync(textSub, 1f, 0f, 0.5f, token);
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

        /// <summary>
        /// 플레이어 입력 방향을 해석하여 그리드 이동을 처리하고 동시 입력을 제어함.
        /// </summary>
        private void HandleMovement()
        {
            if (_isInputBlocked || _isStageCompleted) return;

            float now = Time.time;
            int dx = 0;
            int dy = 0;

            ProcessHardwareDials(now, ref dx, ref dy);
            ProcessKeyboardArrows(ref dx, ref dy);

            if (dx != 0 || dy != 0)
            {
                ApplyGridMovement(dx, dy);
            }
        }
        
        /// <summary>
        /// 하드웨어 다이얼 입력을 분석하여 이동할 벡터를 추출함.
        /// </summary>
        private void ProcessHardwareDials(float now, ref int dx, ref int dy)
        {
            int p1Key = GetValidPlayerKey(1, 4, ref _p1State);
            int p2Key = GetValidPlayerKey(5, 8, ref _p2State);

            ResolveSimultaneousInputs(ref p1Key, ref p2Key);

            if (p1Key != -1) dy = CalculateDirection(p1Key, ref _p1State, now, 0);
            if (p2Key != -1) dx = CalculateDirection(p2Key, ref _p2State, now, 5);
        }
        
        /// <summary>
        /// 입력 상태 구조체를 바탕으로 유효한 키 입력 여부를 판별함.
        /// </summary>
        private int GetValidPlayerKey(int start, int end, ref PlayerWheelState state)
        {
            int keyRaw = WheelInputUtility.GetPressedKeyIndex(start, end);
            if (keyRaw != -1 && keyRaw == state.lastKey)
            {
                state.inputTimer = 0f;
            }
            return (keyRaw != -1 && keyRaw != state.lastKey) ? keyRaw : -1;
        }
        
        /// <summary>
        /// 디버깅 및 PC 환경을 위한 방향키 입력을 감지함.
        /// </summary>
        private void ProcessKeyboardArrows(ref int dx, ref int dy)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow)) dx = -1;
            if (Input.GetKeyDown(KeyCode.RightArrow)) dx = 1;
            if (Input.GetKeyDown(KeyCode.UpArrow)) dy = -1;
            if (Input.GetKeyDown(KeyCode.DownArrow)) dy = 1;
        }

        /// <summary>
        /// 양측 플레이어의 동시 조작을 감지하고 무효화하여 충돌을 방지함.
        /// </summary>
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

        /// <summary> 이전 입력과의 차이를 분석하여 유효한 이동 방향(-1, 1)을 산출함. </summary>
        private int CalculateDirection(int currentKey, ref PlayerWheelState state, float now, int offset = 0)
        {
            if (state.lastKey == -1)
            {
                state.lastKey = currentKey;
                return 0;
            }

            int diff = GetModularDifference(currentKey, state.lastKey, offset);
            int dir = WheelInputUtility.ResolveDirection(diff, now, ref state);

            if (dir != 0)
            {
                state.lastDir = dir;
                state.lastTime = now;
            }
            
            state.lastKey = currentKey;
            return (dir == 1) ? 1 : (dir == -1) ? -1 : 0;
        }
        
        private int GetModularDifference(int currentKey, int lastKey, int offset)
        {
            int currIdx = currentKey - offset;
            int lastIdx = lastKey - offset;
            return (currIdx - lastIdx + 4) % 4;
        }

        /// <summary> 산출된 방향(dx, dy)을 바탕으로 그리드 좌표를 갱신하고 연출을 트리거함. </summary>
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
        
        /// <summary> 유저의 첫 이동이 감지되면 화면 상의 가이드 텍스트들을 일괄 페이드아웃함. </summary>
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
                await UIFadeUtility.FadeCanvasGroupAsync(mainTextGroup, mainTextGroup.alpha, 0f, 0.3f, token);
                if (mainTextGroup) mainTextGroup.gameObject.SetActive(false);
            }
            else if (textMain && textMain.gameObject.activeSelf)
            {
                await UIFadeUtility.FadeGraphicAsync(textMain, textMain.color.a, 0f, 0.3f, token);
                if (textMain) textMain.gameObject.SetActive(false);
            }
        }
        
        private void FadeOutSubInstruction()
        {
            if (!textSub || !textSub.gameObject.activeSelf) return;
            if (_textFadeCts != null || _simultaneousWarningCts != null || _textBlinkCts != null) return;

            _textFadeCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            FadeOutSubInstructionAsync(_textFadeCts.Token).Forget();
        }

        private async UniTaskVoid FadeOutSubInstructionAsync(CancellationToken token)
        {
            await UIFadeUtility.FadeGraphicAsync(textSub, textSub.color.a, 0f, 0.3f, token);
            
            if (textSub) textSub.gameObject.SetActive(false);
            CancelAndDispose(ref _textFadeCts);
            if (_defaultTextSub != null && textSub) UIManager.Instance.SetText(textSub.gameObject, _defaultTextSub);
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
                    if (SoundManager.Instance) SoundManager.Instance.PlaySFX("카메라_2");
                    if (_foundSpots.Count >= _totalQuestionCount && !_isStageCompleted)
                    {
                        _isStageCompleted = true;
                        CancelAndDispose(ref _completionCts);
                        _completionCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
                        ShowCompletionAsync(_completionCts.Token).Forget();
                    }
                }
            }
        }

        /// <summary>
        /// 모든 정답을 찾았을 때의 시각적 전환 연출을 단계별로 수행함.
        /// </summary>
        private async UniTaskVoid ShowCompletionAsync(CancellationToken token)
        {   
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("카메라_3");

            UpdateMaskPixelInstant(_currentGridX, _currentGridY, 1.0f, true);

            await FadeGroupsAsync(completionCanvasGroups, 0f, 1f, 1f, token);

            await UniTask.Delay(TimeSpan.FromSeconds(2.0), cancellationToken: token);
            
            await FadeOutGridAndTextsAsync(0.5f, token);

            CompleteStep();
        }

        /// <summary>
        /// 캔버스 그룹 리스트의 투명도를 일괄적으로 조절함.
        /// </summary>
        private async UniTask FadeGroupsAsync(List<CanvasGroup> groups, float startAlpha, float endAlpha, float duration, CancellationToken token)
        {
            if (groups == null || groups.Count == 0) return;

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                SetCanvasGroupsAlpha(groups, Mathf.Lerp(startAlpha, endAlpha, t / duration));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            SetCanvasGroupsAlpha(groups, endAlpha);
        }
        
        /// <summary>
        /// 리스트 내부의 유효한 캔버스 그룹에 투명도를 일괄 적용함.
        /// </summary>
        private void SetCanvasGroupsAlpha(List<CanvasGroup> groups, float alpha)
        {
            if (groups == null) return;
            foreach (CanvasGroup cg in groups)
            {
                if (cg) cg.alpha = alpha;
            }
        }

        /// <summary>
        /// 게임 종료 전 그리드 이미지와 텍스트를 부드럽게 숨김.
        /// </summary>
        private async UniTask FadeOutGridAndTextsAsync(float duration, CancellationToken token)
        {
            float t = 0f;
            float startGridAlpha = imageGrid ? imageGrid.color.a : 1f;

            while (t < duration)
            {
                t += Time.deltaTime;
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
            CellFadeInfo info = _activeFades.Find(f => f.x == x && f.y == y);
            if (info == null)
            {
                info = new CellFadeInfo
                { 
                    x = x, 
                    y = y, 
                    timer = 0f, 
                    startVal = GetMaskPixelValue(x, y), 
                    targetVal = targetVal 
                };
                _activeFades.Add(info);
            }
            else
            {
                info.startVal = GetMaskPixelValue(x, y);
                info.targetVal = targetVal;
                info.timer = 0f;
            }
        }

        /// <summary>
        /// 페이딩 애니메이션과 숨쉬기(Breathing) 효과를 병합하여 마스크를 갱신함.
        /// </summary>
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
        
        /// <summary>
        /// 진행 중인 셀 페이딩 연산을 처리하고 완료된 건은 목록에서 제거함.
        /// </summary>
        private bool ProcessActiveFades()
        {
            if (_activeFades.Count == 0) return false;

            bool isUpdated = false;
            for (int i = _activeFades.Count - 1; i >= 0; i--)
            {
                CellFadeInfo f = _activeFades[i];
                f.timer += Time.deltaTime;
                float p = Mathf.Clamp01(f.timer / cellFadeDuration);
                
                UpdateMaskPixelInstant(f.x, f.y, Mathf.Lerp(f.startVal, f.targetVal, p), false);
                isUpdated = true;

                if (p >= 1.0f)
                {
                    if (f.x == _currentGridX && f.y == _currentGridY) _isInputBlocked = false;
                    _activeFades.RemoveAt(i);
                }
            }
            return isUpdated;
        }

        /// <summary>
        /// 현재 초점이 맞춰진 정답 칸에 숨쉬기 투명도 효과를 연산하여 적용함.
        /// </summary>
        private bool ApplyBreathingEffect()
        {
            if (_isStageCompleted || _questionMap == null || !_questionMap[_currentGridX, _currentGridY]) return false;

            for (int i = 0; i < _activeFades.Count; i++)
            {
                if (_activeFades[i].x == _currentGridX && _activeFades[i].y == _currentGridY) return false;
            }

            float pingPong = Mathf.PingPong(Time.time * breathSpeed, 1f);
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
            base.OnDestroy();
            CleanupResources();
        }

        private void CleanupResources()
        {
            if (_maskTexture) Destroy(_maskTexture);
            if (_eraserMaterial) Destroy(_eraserMaterial);
            if (_gridMaterial) Destroy(_gridMaterial);
        }
        
        /// <summary>
        /// 시간 흐름에 따른 타이머 텍스트 갱신 및 경고음 재생을 제어함.
        /// </summary>
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
                    currentTime -= Time.deltaTime;
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
                _failPopupCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                ShowFailPopupAsync(_failPopupCts.Token).Forget();
            }
        }

        /// <summary>
        /// 매 타이머 틱(초)마다 필요한 UI 갱신과 효과음을 트리거함.
        /// </summary>
        private void ProcessTimerTick(int displayTime, ref int lastDisplayTime, ref bool hasHinted)
        {
            if (displayTime != lastDisplayTime && displayTime > 0)
            {
                if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_10_1초");
                lastDisplayTime = displayTime;
                
                if (displayTime == 5 && !hasHinted)
                {
                    hasHinted = true;
                    CancelAndDispose(ref _hintCts);
                    _hintCts = CancellationTokenSource.CreateLinkedTokenSource(_timerCts.Token);
                    HintAnswerAsync(_hintCts.Token).Forget();
                }
            }

            if (textTimer) textTimer.text = displayTime.ToString();
            SetTimerColor(displayTime <= 5 && displayTime > 0 ? timerWarningColor : timerNormalColor);
        }
        
        /// <summary>
        /// 타임아웃 발생 시 모든 칸을 강제로 노출하고 실패 팝업을 표시함.
        /// </summary>
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
                if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_7");
                await UIFadeUtility.FadeCanvasGroupAsync(failPopupGroup, 0f, 1f, 0.3f, token);
            }

            await UniTask.Delay(TimeSpan.FromSeconds(1.5), cancellationToken: token);

            if (failPopupGroup)
            {
                await UIFadeUtility.FadeCanvasGroupAsync(failPopupGroup, 1f, 0f, 0.3f, token);
                failPopupGroup.gameObject.SetActive(false);
            }

            _isFailPopupActive = false;
            CompleteStep();
        }
        
        /// <summary>
        /// 찾지 못한 정답 칸들을 일시적으로 반투명하게 빛내어 힌트를 제공함.
        /// </summary>
        private async UniTaskVoid HintAnswerAsync(CancellationToken token)
        {
            if (AreAllQuestionsFound()) return;

            await FadeHintSpotsAsync(0.0f, 0.3f, 0.1f, token);
            
            if (SoundManager.Instance) SoundManager.Instance.PlaySFX("카메라_4");
            
            await UniTask.Delay(TimeSpan.FromSeconds(0.2), cancellationToken: token);

            await FadeHintSpotsAsync(0.3f, 0.0f, 0.1f, token);
        }

        /// <summary>
        /// 모든 정답이 발견되었는지 확인함.
        /// </summary>
        private bool AreAllQuestionsFound()
        {
            if (questionSpots == null) return true;

            foreach (Vector2Int spot in questionSpots)
            {
                if (!_foundSpots.Contains(spot)) return false;
            }
            return true;
        }

        /// <summary>
        /// 미발견 정답 칸들의 투명도를 일괄 보간함.
        /// </summary>
        private async UniTask FadeHintSpotsAsync(float startVal, float endVal, float duration, CancellationToken token)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                ApplyHintMaskValue(Mathf.Lerp(startVal, endVal, t / duration));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            ApplyHintMaskValue(endVal);
        }
        
        /// <summary>
        /// 아직 찾지 못한 정답 칸에 동일한 마스크(알파)값을 일괄 적용함.
        /// </summary>
        private void ApplyHintMaskValue(float val)
        {
            if (questionSpots == null) return;

            foreach (Vector2Int spot in questionSpots)
            {
                if (!_foundSpots.Contains(spot) && spot.x >= 0 && spot.x < gridSizeX && spot.y >= 0 && spot.y < gridSizeY)
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

        /// <summary>
        /// 캔버스 그룹 목록의 상태를 초기화함.
        /// </summary>
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