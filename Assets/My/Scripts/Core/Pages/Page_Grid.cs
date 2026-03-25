using System;
using System.Collections;
using System.Collections.Generic;
using My.Scripts.Core.Data;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts.Core.Pages
{
    /// <summary> 
    /// 그리드 탐색 게임 페이지 컨트롤러.
    /// 플레이어가 다이얼을 조작해 보이지 않는 격자판을 이동하며 숨겨진 질문(정답) 칸을 찾아내는 미니게임을 제어합니다.
    /// </summary>
    public class Page_Grid : PopupGamePage<GridPageData>
    {
        [Header("UI References")] 
        [SerializeField] private Text textMain; // 데이터 주입용 명시적 텍스트
        [SerializeField] private CanvasGroup mainTextGroup; // 텍스트 묶음 동시 페이드아웃용 캔버스 그룹
        [SerializeField] private Text textSub; 
        [SerializeField] private Text[] questionTexts; 
        [SerializeField] private Text textCounting; // 문항 카운팅 텍스트 (1/15 등)

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
        [Tooltip("정답 칸에 있을 때 최소 투명도 (0: 완전 투명, 1: 까만 배경)")]
        [SerializeField, Range(0f, 1f)] private float breathAlphaMin = 0.1f;
        [Tooltip("정답 칸에 있을 때 최대 투명도 (0: 완전 투명, 1: 까만 배경)")]
        [SerializeField, Range(0f, 1f)] private float breathAlphaMax = 0.3f;
        [Tooltip("깜빡이는 속도 (높을수록 빠름)")]
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
        private Coroutine _failPopupRoutine;
        
        private Coroutine _timerRoutine;
        private const float GridTimeLimit = 15f;

        // --- 내부 로직 변수 ---
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
        private Coroutine _autoFadeRoutine;

        // --- 텍스트 및 경고 관련 ---
        private TextSetting _defaultTextSub; 
        private TextSetting _warningText; 
        private Coroutine _textFadeRoutine; 

        private Coroutine _textBlinkRoutine; 
        private const float BlinkThreshold = 10f; 
        private bool _is1stWarningDone = false; 

        private float _p1InputTimer = 0f; 
        private float _p2InputTimer = 0f;
        private Coroutine _simultaneousWarningRoutine;

        // --- 휠 입력 및 관성 보정 변수 ---
        private int _lastP1Key = -1;
        private int _lastP2Key = -1;
        
        private float _p1LastTime;
        private int _p1LastDir; 
        
        private float _p2LastTime;
        private int _p2LastDir; 

        private const float FastInputThreshold = 0.2f; 
        private const float BounceThreshold = 0.05f;    

        private int GetGridCenterX() => Mathf.Max(0, (gridSizeX - 1) / 2);
        private int GetGridCenterY() => Mathf.Max(0, (gridSizeY) / 2);

        private class CellFadeInfo
        {
            public int x, y;
            public float startVal, targetVal, timer;
        }

        private readonly List<CellFadeInfo> _activeFades = new List<CellFadeInfo>();

        /// <summary> JSON 데이터에서 텍스트와 정답 좌표를 가져와 초기화합니다. </summary>
        protected override void SetupData(GridPageData data)
        {
            if (data == null) return;

            // 캔버스 그룹 내부 자동 검색을 제거하고 명시적으로 할당된 textMain에만 데이터를 덮어씌웁니다.
            if (textMain) UIManager.Instance.SetText(textMain.gameObject, data.descriptionText1);
            if (textSub) UIManager.Instance.SetText(textSub.gameObject, data.descriptionText2);

            _defaultTextSub = data.descriptionText2;
            _warningText = data.descriptionText3;
            
            _failTextSetting = data.failText;
            if (textFail && _failTextSetting != null)
            {
                UIManager.Instance.SetText(textFail.gameObject, _failTextSetting);
            }

            if (data.questionSpots != null && data.questionSpots.Count > 0)
            {
                HashSet<Vector2Int> filtered = new HashSet<Vector2Int>();
                foreach (Vector2Int spot in data.questionSpots)
                {
                    if (spot.x >= 0 && spot.x < gridSizeX && spot.y >= 0 && spot.y < gridSizeY)
                    {
                        filtered.Add(spot);
                    }
                    questionSpots = new List<Vector2Int>(filtered);
                }
            }

            if (questionTexts != null)
            {
                for (int i = 0; i < questionTexts.Length; i++)
                {
                    if (!questionTexts[i]) continue;
                    if (data.questions != null && i < data.questions.Length)
                    {
                        UIManager.Instance.SetText(questionTexts[i].gameObject, data.questions[i]);
                        questionTexts[i].gameObject.SetActive(true);
                    }
                    else questionTexts[i].gameObject.SetActive(false);
                }
            }

            SetupPopupMessage(data.warningMessage, data.resetMessage);
        }

        /// <summary> 페이지 진입 시 그리드를 초기화하고 플레이어의 시작 위치를 배정합니다. </summary>
        public override void OnEnter()
        {
            base.OnEnter();

            // 문항 카운팅 텍스트 초기화 (튜토리얼은 0번으로 취급되어 빈칸 처리됨)
            if (textCounting)
            {
                int qNum = LevelManager.Instance ? LevelManager.Instance.CurrentQuestionNumber : 0;
                textCounting.text = qNum > 0 ? $"{qNum}/15" : "";
            }

            // 페이지 재진입 시 텍스트 상태 초기화 보장
            if (mainTextGroup) 
            {
                mainTextGroup.gameObject.SetActive(true);
                mainTextGroup.alpha = 1f;
            }
            if (textMain) 
            {
                textMain.gameObject.SetActive(true);
                Color c = textMain.color; c.a = 1f; textMain.color = c;
            }

            _hasMoved = false;
            _isInputBlocked = false;
            _isFailPopupActive = false;
            
            if (failPopupGroup)
            {
                failPopupGroup.alpha = 0f;
                failPopupGroup.gameObject.SetActive(false);
            }
            
            if (_autoFadeRoutine != null) 
            {
                StopCoroutine(_autoFadeRoutine);
            }
            _autoFadeRoutine = StartCoroutine(AutoFadeMainTextRoutine());
            
            if (_timerRoutine != null) StopCoroutine(_timerRoutine);
            _timerRoutine = StartCoroutine(TimerRoutine());

            _lastP1Key = -1;
            _lastP2Key = -1;
            _p1LastTime = 0f; _p1LastDir = 0;
            _p2LastTime = 0f; _p2LastDir = 0;

            _p1InputTimer = 0f;
            _p2InputTimer = 0f;
            if (_simultaneousWarningRoutine != null) { StopCoroutine(_simultaneousWarningRoutine); _simultaneousWarningRoutine = null; }

            ResetIdleState(true); 

            if (!InitializeGame()) return;

            int startX = GetGridCenterX();
            int startY = GetGridCenterY();

            if (_questionMap != null && _questionMap[startX, startY])
            {
                bool foundSafeSpot = false;
                
                Vector2Int[] offsets = new Vector2Int[] 
                {
                    new Vector2Int(0, 1), new Vector2Int(0, -1), 
                    new Vector2Int(1, 0), new Vector2Int(-1, 0),
                    new Vector2Int(1, 1), new Vector2Int(1, -1), 
                    new Vector2Int(-1, 1), new Vector2Int(-1, -1)
                };

                foreach (Vector2Int offset in offsets)
                {
                    int nx = startX + offset.x;
                    int ny = startY + offset.y;

                    if (nx >= 0 && nx < gridSizeX && ny >= 0 && ny < gridSizeY)
                    {
                        if (!_questionMap[nx, ny])
                        {
                            startX = nx;
                            startY = ny;
                            foundSafeSpot = true;
                            break;
                        }
                    }
                }

                if (!foundSafeSpot)
                {
                    Debug.LogWarning("[Page_Grid] 시작점 주변에 안전한 오답 칸이 없습니다.");
                }
            }

            SetFocusToGrid(startX, startY, true);
        }

        private bool InitializeGame()
        {
            if (!imageBlack) return false;
            if (gridSizeX <= 0 || gridSizeY <= 0) return false;
            
            _blackRect = imageBlack.rectTransform;
            _cellWidth = _blackRect.rect.width / gridSizeX;
            _cellHeight = _blackRect.rect.height / gridSizeY;
            
            _foundSpots.Clear();
            _isStageCompleted = false;

            if (completionCanvasGroups != null)
                foreach (CanvasGroup cg in completionCanvasGroups)
                    if (cg) { cg.alpha = 0f; cg.gameObject.SetActive(true); }

            if (textCanvasGroups != null)
                foreach (CanvasGroup cg in textCanvasGroups)
                    if (cg) cg.alpha = 1f;

            _questionMap = new bool[gridSizeX, gridSizeY];
            if (questionSpots != null)
            {
                foreach (Vector2Int s in questionSpots)
                    if (s.x >= 0 && s.x < gridSizeX && s.y >= 0 && s.y < gridSizeY)
                        _questionMap[s.x, s.y] = true;
                _totalQuestionCount = questionSpots.Count;
            }

            if (_totalQuestionCount == 0)
            {
                int centerX = GetGridCenterX();
                int centerY = GetGridCenterY();
                _questionMap[centerX, centerY] = true;
                _totalQuestionCount = 1;
            }

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

            _activeFades.Clear();
            return true;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space) && !_isStageCompleted)
            {
                _isStageCompleted = true;
                RevealAllQuestions();
                StartCoroutine(ShowCompletionRoutine());
            }

            if (_p1InputTimer > 0f) _p1InputTimer -= Time.deltaTime;
            if (_p2InputTimer > 0f) _p2InputTimer -= Time.deltaTime;

            bool hasValidInput = false;
            if (Input.touchCount > 0) hasValidInput = true;
            else
            {
                for (int i = 1; i <= 9; i++)
                {
                    if (Input.GetKey((KeyCode)((int)KeyCode.Alpha0 + i)) || 
                        Input.GetKey((KeyCode)((int)KeyCode.Keypad0 + i)))
                    {
                        hasValidInput = true;
                        break;
                    }
                }
                
                if (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Return) ||
                    Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow) ||
                    Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow))
                {
                    hasValidInput = true;
                }
            }

            if (hasValidInput)
            {
                ResetIdleState(false); 
                HandleMovement();
            }
            else
            {
                if (!_isInputBlocked && !_isStageCompleted && !isResetSequenceActive)
                {
                    currentIdleTime += Time.deltaTime;

                    if (currentIdleTime >= BlinkThreshold && currentIdleTime < inactivityThreshold)
                    {
                        if (!_is1stWarningDone && _textBlinkRoutine == null && _simultaneousWarningRoutine == null)
                        {
                            if (_warningText != null && textSub)
                                UIManager.Instance.SetText(textSub.gameObject, _warningText);

                            _textBlinkRoutine = StartCoroutine(BlinkRoutine());
                        }
                    }
                    else if (currentIdleTime >= inactivityThreshold)
                    {
                        StartResetSequence();
                    }
                }
            }

            UpdateCellFades();
        }

        protected override void ResetIdleState(bool immediate = false)
        {
            base.ResetIdleState(immediate);
            _is1stWarningDone = false;

            if (_textBlinkRoutine != null)
            {
                StopCoroutine(_textBlinkRoutine);
                _textBlinkRoutine = null;

                if (immediate && textSub) textSub.gameObject.SetActive(false);
                else if (textSub && _simultaneousWarningRoutine == null) textSub.gameObject.SetActive(true);
            }

            if (immediate && _simultaneousWarningRoutine != null)
            {
                StopCoroutine(_simultaneousWarningRoutine);
                _simultaneousWarningRoutine = null;
                if (textSub) textSub.gameObject.SetActive(false);
            }
        }

        protected override void StartResetSequence()
        {
            if (_textBlinkRoutine != null) { StopCoroutine(_textBlinkRoutine); _textBlinkRoutine = null; }
            if (_simultaneousWarningRoutine != null) { StopCoroutine(_simultaneousWarningRoutine); _simultaneousWarningRoutine = null; }

            if (textSub) textSub.gameObject.SetActive(false);

            base.StartResetSequence();
        }

        private IEnumerator BlinkRoutine()
        {
            if (textSub)
            {
                textSub.gameObject.SetActive(true);
                Color c = textSub.color;
                c.a = 0f;
                textSub.color = c;
            }

            for (int i = 0; i < 2; i++)
            {
                yield return StartCoroutine(FadeTo(textSub, 1f, 1f));
                yield return StartCoroutine(FadeTo(textSub, 0f, 1f));
            }
            if (textSub) textSub.gameObject.SetActive(false);

            _is1stWarningDone = true;
            _textBlinkRoutine = null;
        }

        private void HandleMovement()
        {
            if (_isInputBlocked || _isStageCompleted) return;

            int dx = 0, dy = 0;
            float now = Time.time;

            int p1KeyRaw = GetPressedKeyIndex(1, 4, _lastP1Key, _p1LastDir);
            int p2KeyRaw = GetPressedKeyIndex(5, 8, _lastP2Key, _p2LastDir);

            if (p1KeyRaw != -1 && p1KeyRaw == _lastP1Key) _p1InputTimer = 0f;
            if (p2KeyRaw != -1 && p2KeyRaw == _lastP2Key) _p2InputTimer = 0f;

            int p1Key = (p1KeyRaw != -1 && p1KeyRaw != _lastP1Key) ? p1KeyRaw : -1;
            int p2Key = (p2KeyRaw != -1 && p2KeyRaw != _lastP2Key) ? p2KeyRaw : -1;

            bool blockP1 = false;
            bool blockP2 = false;

            if (p1Key != -1 && p2Key != -1) 
            {
                blockP1 = true;
                blockP2 = true;
                TriggerSimultaneousWarning();
            }
            else if (p1Key != -1)
            {
                if (_p2InputTimer > 0f) 
                {
                    blockP1 = true;
                    TriggerSimultaneousWarning();
                }
                else 
                {
                    _p1InputTimer = 0.3f; 
                }
            }
            else if (p2Key != -1)
            {
                if (_p1InputTimer > 0f) 
                {
                    blockP2 = true;
                    TriggerSimultaneousWarning();
                }
                else 
                {
                    _p2InputTimer = 0.3f;
                }
            }

            if (blockP1) p1Key = -1;
            if (blockP2) p2Key = -1;

            if (p1Key != -1)
            {
                if (_lastP1Key != -1)
                {
                    int diff = (p1Key - _lastP1Key + 4) % 4;
                    int dir = 0; 

                    if (diff == 1) dir = 1;
                    else if (diff == 3) dir = -1;
                    else if (diff == 2)
                    {
                        if (now - _p1LastTime < FastInputThreshold && _p1LastDir != 0) 
                            dir = _p1LastDir;
                    }

                    if (dir != 0 && dir != _p1LastDir && _p1LastDir != 0 && (now - _p1LastTime < BounceThreshold))
                    {
                        dir = 0; 
                    }

                    if (dir != 0)
                    {
                        dy = (dir == 1) ? 1 : -1; 
                        _p1LastDir = dir;
                        _p1LastTime = now;
                    }
                }
                
                _lastP1Key = p1Key;
            }

            if (p2Key != -1)
            {
                if (_lastP2Key != -1)
                {
                    int currIdx = p2Key - 5;
                    int lastIdx = _lastP2Key - 5;
                    int diff = (currIdx - lastIdx + 4) % 4;
                    int dir = 0;

                    if (diff == 1) dir = 1;
                    else if (diff == 3) dir = -1;
                    else if (diff == 2)
                    {
                        if (now - _p2LastTime < FastInputThreshold && _p2LastDir != 0) 
                            dir = _p2LastDir;
                    }

                    if (dir != 0 && dir != _p2LastDir && _p2LastDir != 0 && (now - _p2LastTime < BounceThreshold))
                    {
                        dir = 0; 
                    }

                    if (dir != 0)
                    {
                        dx = (dir == 1) ? 1 : -1; 
                        _p2LastDir = dir;
                        _p2LastTime = now;
                    }
                }
                
                _lastP2Key = p2Key;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow)) dx = -1;
            if (Input.GetKeyDown(KeyCode.RightArrow)) dx = 1;
            if (Input.GetKeyDown(KeyCode.UpArrow)) dy = -1;
            if (Input.GetKeyDown(KeyCode.DownArrow)) dy = 1;

            if (dx != 0 || dy != 0)
            {
                if (!_hasMoved)
                {
                    _hasMoved = true;
                    
                    // 플레이어 개입 시 자동 페이드아웃 타이머 정지
                    if (_autoFadeRoutine != null)
                    {
                        StopCoroutine(_autoFadeRoutine);
                        _autoFadeRoutine = null;
                    }
                    
                    // 그룹이 등록되어 있다면 그룹 전체를 페이드아웃, 없다면 기존처럼 개별 텍스트(textMain) 페이드아웃 처리
                    if (mainTextGroup && mainTextGroup.gameObject.activeSelf)
                    {
                        StartCoroutine(FadeGroupTo(mainTextGroup, 0f, 0.3f, () => mainTextGroup.gameObject.SetActive(false)));
                    }
                    else if (textMain && textMain.gameObject.activeSelf)
                    {
                        StartCoroutine(FadeTo(textMain, 0f, 0.3f, () => textMain.gameObject.SetActive(false)));
                    }
                }

                if (textSub && textSub.gameObject.activeSelf)
                {
                    if (_textFadeRoutine == null && _simultaneousWarningRoutine == null && _textBlinkRoutine == null) 
                    {
                        _textFadeRoutine = StartCoroutine(FadeTo(textSub, 0f, 0.3f, () =>
                        {
                            textSub.gameObject.SetActive(false);
                            _textFadeRoutine = null;
                            if (_defaultTextSub != null) UIManager.Instance.SetText(textSub.gameObject, _defaultTextSub);
                        }));
                    }
                }

                int nextX = _currentGridX + dx, nextY = _currentGridY + dy;
                
                if (nextX >= 0 && nextX < gridSizeX && nextY >= 0 && nextY < gridSizeY) 
                    SetFocusToGrid(nextX, nextY);
            }
        }
        
        private IEnumerator AutoFadeMainTextRoutine()
        {
            yield return CoroutineData.GetWaitForSeconds(3.0f);

            // 대기 시간 동안 플레이어가 이동하여 텍스트가 지워졌다면 로직 무시
            if (_hasMoved) yield break;

            _hasMoved = true;

            if (mainTextGroup && mainTextGroup.gameObject.activeSelf)
            {
                StartCoroutine(FadeGroupTo(mainTextGroup, 0f, 0.3f, () => mainTextGroup.gameObject.SetActive(false)));
            }
            else if (textMain && textMain.gameObject.activeSelf)
            {
                StartCoroutine(FadeTo(textMain, 0f, 0.3f, () => textMain.gameObject.SetActive(false)));
            }
        }

        private void TriggerSimultaneousWarning()
        {
            if (_simultaneousWarningRoutine != null) return;

            if (_textBlinkRoutine != null) { StopCoroutine(_textBlinkRoutine); _textBlinkRoutine = null; }
            if (_textFadeRoutine != null) { StopCoroutine(_textFadeRoutine); _textFadeRoutine = null; }

            _simultaneousWarningRoutine = StartCoroutine(SimultaneousWarningRoutine());
        }

        private IEnumerator SimultaneousWarningRoutine()
        {
            if (textSub)
            {
                textSub.gameObject.SetActive(true);
                
                if (_defaultTextSub != null) UIManager.Instance.SetText(textSub.gameObject, _defaultTextSub);

                Color c = textSub.color;
                c.a = 0f;
                textSub.color = c;

                for (int i = 0; i < 2; i++)
                {
                    yield return StartCoroutine(FadeTo(textSub, 1f, 0.5f));
                    yield return StartCoroutine(FadeTo(textSub, 0f, 0.5f));
                }
                
                textSub.gameObject.SetActive(false);
            }
            _simultaneousWarningRoutine = null;
        }

        private int GetPressedKeyIndex(int start, int end, int lastKey, int lastDir)
        {
            List<int> pressedKeys = new List<int>();
            for (int i = start; i <= end; i++)
            {
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha0 + i)) || 
                    Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad0 + i)))
                {
                    pressedKeys.Add(i);
                }
            }

            if (pressedKeys.Count == 0) return -1;
            if (pressedKeys.Count == 1) return pressedKeys[0];

            if (lastKey != -1)
            {
                foreach (int k in pressedKeys)
                {
                    int currIdx = k - start;
                    int lastIdx = lastKey - start;
                    int diff = (currIdx - lastIdx + 4) % 4;
                    
                    int expectedDiff = (lastDir == -1) ? 3 : 1; 
                    if (diff == expectedDiff) return k;
                }

                foreach (int k in pressedKeys)
                {
                    int currIdx = k - start;
                    int lastIdx = lastKey - start;
                    int diff = (currIdx - lastIdx + 4) % 4;
                    
                    if (diff == 1 || diff == 3) return k;
                }
            }

            return pressedKeys[pressedKeys.Count - 1]; 
        }

        private IEnumerator FadeTo(Text target, float targetAlpha, float duration, Action onComplete = null)
        {
            if (!target) yield break;
            float startAlpha = target.color.a, timer = 0f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                Color c = target.color;
                c.a = Mathf.Lerp(startAlpha, targetAlpha, timer / duration);
                target.color = c;
                yield return null;
            }

            Color fc = target.color;
            fc.a = targetAlpha;
            target.color = fc;
            onComplete?.Invoke();
        }

        private IEnumerator FadeGroupTo(CanvasGroup target, float targetAlpha, float duration, Action onComplete = null)
        {
            if (!target) yield break;
            float startAlpha = target.alpha, timer = 0f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                target.alpha = Mathf.Lerp(startAlpha, targetAlpha, timer / duration);
                yield return null;
            }
            target.alpha = targetAlpha;
            onComplete?.Invoke();
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
                    SoundManager.Instance?.PlaySFX("카메라_2");
                    if (_foundSpots.Count >= _totalQuestionCount && !_isStageCompleted)
                    {
                        _isStageCompleted = true;
                        StartCoroutine(ShowCompletionRoutine());
                    }
                }
            }
        }

        private IEnumerator ShowCompletionRoutine()
        {   
            SoundManager.Instance?.PlaySFX("카메라_3");

            UpdateMaskPixelInstant(_currentGridX, _currentGridY, 1.0f, true);

            if (completionCanvasGroups != null)
            {
                float t = 0f;
                float duration = 1f;

                while (t < duration)
                {
                    t += Time.deltaTime;
                    float alpha = Mathf.Clamp01(t / duration);
                    
                    foreach (CanvasGroup cg in completionCanvasGroups)
                        if (cg) cg.alpha = alpha;
                    
                    yield return null;
                }

                foreach (CanvasGroup cg in completionCanvasGroups)
                    if (cg) cg.alpha = 1f;
            }

            yield return CoroutineData.GetWaitForSeconds(2.0f);
            
            float t2 = 0f, startA = imageGrid ? imageGrid.color.a : 1f;
            while (t2 < 0.5f)
            {
                t2 += Time.deltaTime;
                float p = t2 / 0.5f;
                if (imageGrid)
                {
                    Color c = imageGrid.color;
                    c.a = Mathf.Lerp(startA, 0f, p);
                    imageGrid.color = c;
                }

                if (textCanvasGroups != null)
                    foreach (CanvasGroup cg in textCanvasGroups)
                        if (cg) cg.alpha = Mathf.Lerp(1f, 0f, p);
                yield return null;
            }

            CompleteStep();
        }

        private void StartCellFade(int x, int y, float targetVal)
        {
            CellFadeInfo info = _activeFades.Find(f => f.x == x && f.y == y);
            if (info == null)
            {
                info = new CellFadeInfo
                    { x = x, y = y, timer = 0f, startVal = GetMaskPixelValue(x, y), targetVal = targetVal };
                _activeFades.Add(info);
            }
            else
            {
                info.startVal = GetMaskPixelValue(x, y);
                info.targetVal = targetVal;
                info.timer = 0f;
            }
        }

        private void UpdateCellFades()
        {
            bool needsApply = false;

            if (_activeFades.Count > 0)
            {
                for (int i = _activeFades.Count - 1; i >= 0; i--)
                {
                    CellFadeInfo f = _activeFades[i];
                    f.timer += Time.deltaTime;
                    float p = Mathf.Clamp01(f.timer / cellFadeDuration);
                    UpdateMaskPixelInstant(f.x, f.y, Mathf.Lerp(f.startVal, f.targetVal, p), false);
                    needsApply = true;

                    if (p >= 1.0f)
                    {
                        if (f.x == _currentGridX && f.y == _currentGridY) _isInputBlocked = false;
                        _activeFades.RemoveAt(i);
                    }
                }
            }

            if (!_isStageCompleted && _questionMap != null)
            {
                if (_questionMap[_currentGridX, _currentGridY])
                {
                    bool isFading = false;
                    for (int i = 0; i < _activeFades.Count; i++)
                    {
                        if (_activeFades[i].x == _currentGridX && _activeFades[i].y == _currentGridY)
                        {
                            isFading = true; break;
                        }
                    }

                    if (!isFading)
                    {
                        float pingPong = Mathf.PingPong(Time.time * breathSpeed, 1f);
                        float currentAlpha = Mathf.Lerp(breathAlphaMin, breathAlphaMax, pingPong);
                        
                        float breathMaskValue = 1.0f - currentAlpha;
                        
                        UpdateMaskPixelInstant(_currentGridX, _currentGridY, breathMaskValue, false);
                        needsApply = true;
                    }
                }
            }

            if (needsApply && _maskTexture) 
            {
                _maskTexture.Apply();
            }
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
            if (_autoFadeRoutine != null)
            {
                StopCoroutine(_autoFadeRoutine);
                _autoFadeRoutine = null;
            }
            
            if (_timerRoutine != null)
            {
                StopCoroutine(_timerRoutine);
                _timerRoutine = null;
            }
            
            if (_failPopupRoutine != null)
            {
                StopCoroutine(_failPopupRoutine);
                _failPopupRoutine = null;
            }
            
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
        
        private IEnumerator TimerRoutine()
        {
            if (textTimer) textTimer.text = "15";
            SetTimerColor(timerNormalColor);

            yield return CoroutineData.GetWaitForSeconds(0.5f);

            float currentTime = GridTimeLimit;
            int lastDisplayTime = 15;
            bool hasHinted = false;

            while (currentTime > 0)
            {
                if (_isStageCompleted) yield break;

                // 무응답 경고 및 시간 초과 실패 팝업 중에는 타이머 정지
                if (!isResetSequenceActive && !_isFailPopupActive)
                {
                    currentTime -= Time.deltaTime;
                    int displayTime = Mathf.CeilToInt(currentTime);

                    // 숫자가 바뀔 때마다 진입
                    if (displayTime != lastDisplayTime && displayTime > 0)
                    {
                        // 1. 매 초마다 사운드 재생
                        if (SoundManager.Instance) SoundManager.Instance.PlaySFX("공통_10_1초");
                        lastDisplayTime = displayTime;
                        
                        // 2. 10초가 남았을 때 한 번만 정답 칸 힌트 연출 시작
                        if (displayTime == 5 && !hasHinted)
                        {
                            hasHinted = true;
                            StartCoroutine(HintAnswerRoutine());
                        }
                    }

                    if (textTimer) textTimer.text = displayTime.ToString();

                    if (displayTime <= 5 && displayTime > 0) SetTimerColor(timerWarningColor);
                    else SetTimerColor(timerNormalColor);
                }
                yield return null;
            }

            if (textTimer) textTimer.text = "0";
            SetTimerColor(timerWarningColor);

            if (!_isStageCompleted && !isResetSequenceActive && !_isFailPopupActive)
            {
                _failPopupRoutine = StartCoroutine(ShowFailPopupRoutine());
            }
        }
        
        private IEnumerator ShowFailPopupRoutine()
        {
            _isFailPopupActive = true;
            _isInputBlocked = true; 
            _isStageCompleted = true; // 유저 개입이 더 이상 발생하지 않도록 차단

            // 1. 정답 칸의 마스크 값을 1.0(완전 투명/공개)으로 변경
            if (questionSpots != null)
            {
                foreach (var spot in questionSpots)
                {
                    if (spot.x >= 0 && spot.x < gridSizeX && spot.y >= 0 && spot.y < gridSizeY)
                    {
                        StartCellFade(spot.x, spot.y, 1.0f);
                    }
                }
            }

            // 셀이 부드럽게 밝아질 시간 0.1초 대기
            yield return CoroutineData.GetWaitForSeconds(0.1f);

            // 2. 실패 팝업 페이드 인
            if (failPopupGroup)
            {
                failPopupGroup.gameObject.SetActive(true);
                SoundManager.Instance.PlaySFX("공통_7");
                yield return StartCoroutine(FadeGroupTo(failPopupGroup, 1f, 0.3f));
            }

            // 3. 1초 대기 (유저가 실패 문구를 인지할 시간)
            yield return CoroutineData.GetWaitForSeconds(1.5f);

            // 화면을 전환하기 전에 팝업을 깔끔하게 페이드아웃
            if (failPopupGroup)
            {
                yield return StartCoroutine(FadeGroupTo(failPopupGroup, 0f, 0.3f));
                failPopupGroup.gameObject.SetActive(false);
            }

            _isFailPopupActive = false;
            
            // 4. 다음 페이지(Page_QnA)로 즉시 전환
            CompleteStep();
        }
        
        private IEnumerator HintAnswerRoutine()
        {
            if (questionSpots == null) yield break;

            // 이미 찾은 정답 칸은 힌트 연출에서 제외합니다.
            List<Vector2Int> unfoundSpots = new List<Vector2Int>();
            foreach (var spot in questionSpots)
            {
                if (!_foundSpots.Contains(spot)) unfoundSpots.Add(spot);
            }

            if (unfoundSpots.Count == 0) yield break;

            float t = 0f;
            float duration = 0.1f;

            // 1. 마스크 값 0.0(완전 까만색) -> 0.3(알파 0.7 느낌의 반투명)으로 페이드
            while (t < duration)
            {
                t += Time.deltaTime;
                float val = Mathf.Lerp(0.0f, 0.3f, t / duration);
                foreach (var spot in unfoundSpots)
                {
                    if (spot.x >= 0 && spot.x < gridSizeX && spot.y >= 0 && spot.y < gridSizeY)
                    {
                        UpdateMaskPixelInstant(spot.x, spot.y, val, false);
                    }
                }
                if (_maskTexture) _maskTexture.Apply();
                yield return null;
            }
            SoundManager.Instance?.PlaySFX("카메라_4");
            // 반투명 상태를 잠시 유지
            yield return CoroutineData.GetWaitForSeconds(0.2f);

            // 2. 마스크 값 0.3 -> 0.0(다시 까맣게 숨김)으로 복구
            t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float val = Mathf.Lerp(0.3f, 0.0f, t / duration);
                foreach (var spot in unfoundSpots)
                {
                    if (spot.x >= 0 && spot.x < gridSizeX && spot.y >= 0 && spot.y < gridSizeY)
                    {
                        UpdateMaskPixelInstant(spot.x, spot.y, val, false);
                    }
                }
                if (_maskTexture) _maskTexture.Apply();
                yield return null;
            }
            
            // 확실하게 0.0(완전 숨김)으로 고정
            foreach (var spot in unfoundSpots)
            {
                if (spot.x >= 0 && spot.x < gridSizeX && spot.y >= 0 && spot.y < gridSizeY)
                {
                    UpdateMaskPixelInstant(spot.x, spot.y, 0.0f, false);
                }
            }
            if (_maskTexture) _maskTexture.Apply();
        }

        // 이미지와 텍스트 색상을 동시에 바꿔주는 헬퍼 메서드
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
    }
}