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
    /// <summary>  그리드 탐색 게임 페이지 컨트롤러 </summary>
    public class Page_Grid : PopupGamePage<GridPageData>
    {
        [Header("UI References")] 
        [SerializeField] private Text textMain; 
        [SerializeField] private Text textSub; 
        [SerializeField] private Text[] questionTexts; 

        [Header("Interaction")] 
        [SerializeField] private Image imageBlack; 
        [SerializeField] private Image imageGrid; 
        [SerializeField] private Image imageFocus; 

        [Header("Completion & Groups")] 
        [SerializeField] private List<CanvasGroup> completionCanvasGroups; 
        [SerializeField] private List<CanvasGroup> textCanvasGroups; 
        
        [Header("Settings")]
        [SerializeField] private List<Vector2Int> questionSpots; 
        [SerializeField] private int gridSize = 10; 
        
        private readonly float cellFadeDuration = 0.5f; 

        // --- 내부 로직 변수 ---
        private RectTransform _blackRect; 
        private Texture2D _maskTexture; 
        private Material _eraserMaterial, _gridMaterial; 
        private static readonly int MaskTexID = Shader.PropertyToID("_MaskTex");

        private float _cellWidth, _cellHeight; 
        private int _currentGridX, _currentGridY; 
        private bool[,] _questionMap; 
        private bool _hasMoved; 
        private bool _isInputBlocked; 
        private bool _isStageCompleted; 
        private readonly HashSet<Vector2Int> _foundSpots = new HashSet<Vector2Int>(); 
        private int _totalQuestionCount; 

        // --- 텍스트 및 경고 관련 ---
        private TextSetting _defaultTextSub; 
        private TextSetting _warningText; 
        private Coroutine _textFadeRoutine; 

        private Coroutine _textBlinkRoutine; 
        private const float BlinkThreshold = 10f; 
        private bool _is1stWarningDone = false; 

        // [추가] 동시 입력 방지 및 경고 연출용 변수
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
        
        private int GetGridCenterIndex() => Mathf.Max(0, (gridSize - 1) / 2);

        private class CellFadeInfo
        {
            public int x, y;
            public float startVal, targetVal, timer;
        }

        private readonly List<CellFadeInfo> _activeFades = new List<CellFadeInfo>();

        protected override void SetupData(GridPageData data)
        {
            if (data == null) return;

            if (textMain) UIManager.Instance.SetText(textMain.gameObject, data.descriptionText1);
            if (textSub) UIManager.Instance.SetText(textSub.gameObject, data.descriptionText2);

            _defaultTextSub = data.descriptionText2;
            _warningText = data.descriptionText3;

            if (data.questionSpots != null && data.questionSpots.Count > 0)
            {
                var filtered = new HashSet<Vector2Int>();
                foreach (var spot in data.questionSpots)
                {
                    if (spot.x >= 0 && spot.x < gridSize && spot.y >= 0 && spot.y < gridSize)
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

        public override void OnEnter()
        {
            base.OnEnter();
            _hasMoved = false;
            _isInputBlocked = false;

            _lastP1Key = -1;
            _lastP2Key = -1;
            _p1LastTime = 0f; _p1LastDir = 0;
            _p2LastTime = 0f; _p2LastDir = 0;

            _p1InputTimer = 0f;
            _p2InputTimer = 0f;
            if (_simultaneousWarningRoutine != null) { StopCoroutine(_simultaneousWarningRoutine); _simultaneousWarningRoutine = null; }

            ResetIdleState(true); 

            if (!InitializeGame()) return;

            int center = GetGridCenterIndex();
            SetFocusToGrid(center, center, true);
        }

        private bool InitializeGame()
        {
            if (!imageBlack || !imageFocus) return false;

            if (gridSize <= 0) return false;
            
            _blackRect = imageBlack.rectTransform;
            _cellWidth = _blackRect.rect.width / gridSize;
            _cellHeight = _blackRect.rect.height / gridSize;
            _foundSpots.Clear();
            _isStageCompleted = false;

            if (completionCanvasGroups != null)
                foreach (var cg in completionCanvasGroups)
                    if (cg) { cg.alpha = 0f; cg.gameObject.SetActive(true); }

            if (textCanvasGroups != null)
                foreach (var cg in textCanvasGroups)
                    if (cg) cg.alpha = 1f;

            _questionMap = new bool[gridSize, gridSize];
            if (questionSpots != null)
            {
                foreach (var s in questionSpots)
                    if (s.x >= 0 && s.x < gridSize && s.y >= 0 && s.y < gridSize)
                        _questionMap[s.x, s.y] = true;
                _totalQuestionCount = questionSpots.Count;
            }

            if (_totalQuestionCount == 0)
            {
                int center = GetGridCenterIndex();
                _questionMap[center, center] = true;
                _totalQuestionCount = 1;
            }

            if (_maskTexture) Destroy(_maskTexture);
            if (_eraserMaterial) Destroy(_eraserMaterial);
            if (_gridMaterial) Destroy(_gridMaterial);

            _eraserMaterial = Instantiate(imageBlack.material);
            imageBlack.material = _eraserMaterial;
            _maskTexture = new Texture2D(gridSize, gridSize, TextureFormat.R8, false) { filterMode = FilterMode.Point };
            _maskTexture.SetPixels32(new Color32[gridSize * gridSize]);
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

            // [추가] 입력 독점 타이머 감소
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

        // --- 게임 로직 (이동) ---
        private void HandleMovement()
        {
            if (!imageFocus || _isInputBlocked || _isStageCompleted) return;

            int dx = 0, dy = 0;
            float now = Time.time;

            int p1Key = GetPressedKeyIndex(1, 4);
            int p2Key = GetPressedKeyIndex(5, 8);

            bool blockP1 = false;
            bool blockP2 = false;

            // 동시 입력 방지 및 독점 타이머 적용
            if (p1Key != -1 && p2Key != -1) // 완벽히 동시에 들어왔을 경우
            {
                blockP1 = true;
                blockP2 = true;
                TriggerSimultaneousWarning();
            }
            else if (p1Key != -1)
            {
                if (_p2InputTimer > 0f) // P2가 선점 중
                {
                    blockP1 = true;
                    TriggerSimultaneousWarning();
                }
                else // P1 입력 허용 및 0.5초 타이머 세팅
                {
                    _p1InputTimer = 0.5f;
                }
            }
            else if (p2Key != -1)
            {
                if (_p1InputTimer > 0f) // P1이 선점 중
                {
                    blockP2 = true;
                    TriggerSimultaneousWarning();
                }
                else // P2 입력 허용 및 0.5초 타이머 세팅
                {
                    _p2InputTimer = 0.5f;
                }
            }

            // 차단된 플레이어의 키 입력 무효화
            if (blockP1) p1Key = -1;
            if (blockP2) p2Key = -1;

            // 1. Player 1 (Vertical: 1~4) -> 상하 (dy)
            if (p1Key != -1)
            {
                if (_lastP1Key != -1)
                {
                    int diff = (p1Key - _lastP1Key + 4) % 4;
                    int dir = 0; 

                    if (diff == 1) dir = 1;
                    else if (diff == 3) dir = -1;

                    if (now - _p1LastTime < FastInputThreshold && _p1LastDir != 0)
                    {
                        if (diff == 2 || (dir != 0 && dir != _p1LastDir)) dir = _p1LastDir;
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

            // 2. Player 2 (Horizontal: 5~8) -> 좌우 (dx)
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

                    if (now - _p2LastTime < FastInputThreshold && _p2LastDir != 0)
                    {
                        if (diff == 2 || (dir != 0 && dir != _p2LastDir)) dir = _p2LastDir;
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
                    if (textMain && textMain.gameObject.activeSelf)
                        StartCoroutine(FadeTo(textMain, 0f, 1f, () => textMain.gameObject.SetActive(false)));
                }

                if (textSub && textSub.gameObject.activeSelf)
                {
                    // 경고 깜빡임이나 기존 페이드가 작동 중이지 않을 때만 정상적으로 사라지게 함 (Stuttering 방지)
                    if (_textFadeRoutine == null && _simultaneousWarningRoutine == null && _textBlinkRoutine == null) 
                    {
                        _textFadeRoutine = StartCoroutine(FadeTo(textSub, 0f, 1f, () =>
                        {
                            textSub.gameObject.SetActive(false);
                            _textFadeRoutine = null;
                            if (_defaultTextSub != null) UIManager.Instance.SetText(textSub.gameObject, _defaultTextSub);
                        }));
                    }
                }

                int nextX = _currentGridX + dx, nextY = _currentGridY + dy;
                if (nextX >= 0 && nextX < gridSize && nextY >= 0 && nextY < gridSize) SetFocusToGrid(nextX, nextY);
            }
        }

        /// <summary> 동시 입력 시 경고 텍스트 연출 트리거 </summary>
        private void TriggerSimultaneousWarning()
        {
            // 이미 경고가 깜빡이고 있다면 무시 (중복 실행 방지)
            if (_simultaneousWarningRoutine != null) return;

            // 실행 중이던 다른 텍스트 코루틴들 강제 종료
            if (_textBlinkRoutine != null) { StopCoroutine(_textBlinkRoutine); _textBlinkRoutine = null; }
            if (_textFadeRoutine != null) { StopCoroutine(_textFadeRoutine); _textFadeRoutine = null; }

            _simultaneousWarningRoutine = StartCoroutine(SimultaneousWarningRoutine());
        }

        /// DescriptionText2(기본 서브 텍스트)를 띄우고 빠른 속도로 2회 깜빡임 </summary>
        private IEnumerator SimultaneousWarningRoutine()
        {
            if (textSub)
            {
                textSub.gameObject.SetActive(true);
                
                // 설정해둔 DescriptionText2 로 텍스트 내용 교체
                if (_defaultTextSub != null) UIManager.Instance.SetText(textSub.gameObject, _defaultTextSub);

                Color c = textSub.color;
                c.a = 0f;
                textSub.color = c;

                // 2회 빠른 깜빡임
                for (int i = 0; i < 2; i++)
                {
                    yield return StartCoroutine(FadeTo(textSub, 1f, 0.5f));
                    yield return StartCoroutine(FadeTo(textSub, 0f, 0.5f));
                }
                
                textSub.gameObject.SetActive(false);
            }
            _simultaneousWarningRoutine = null;
        }

        private int GetPressedKeyIndex(int start, int end)
        {
            for (int i = start; i <= end; i++)
            {
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha0 + i))) return i;
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad0 + i))) return i;
            }
            return -1;
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

        private void SetFocusToGrid(int x, int y, bool isFirstInit = false)
        {
            if (!isFirstInit)
            {
                if (!_questionMap[_currentGridX, _currentGridY]) StartCellFade(_currentGridX, _currentGridY, 0.0f);
                _isInputBlocked = true;
            }

            _currentGridX = x;
            _currentGridY = y;
            float startX = -(_blackRect.rect.width / 2f), startY = (_blackRect.rect.height / 2f);
            imageFocus.rectTransform.anchoredPosition = new Vector2(startX + (x * _cellWidth) + (_cellWidth / 2f),
                startY - (y * _cellHeight) - (_cellHeight / 2f));
            if (isFirstInit) UpdateMaskPixelInstant(x, y, 1.0f);
            else StartCellFade(x, y, 1.0f);
            CheckQuestionFound(x, y);
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
            if (completionCanvasGroups != null)
            {
                float t = 0f;
                float duration = 1f;

                while (t < duration)
                {
                    t += Time.deltaTime;
                    float alpha = Mathf.Clamp01(t / duration);
                    
                    foreach (var cg in completionCanvasGroups)
                        if (cg) cg.alpha = alpha;
                    
                    yield return null;
                }

                foreach (var cg in completionCanvasGroups)
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
                    foreach (var cg in textCanvasGroups)
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
            if (_activeFades.Count == 0) return;
            for (int i = _activeFades.Count - 1; i >= 0; i--)
            {
                var f = _activeFades[i];
                f.timer += Time.deltaTime;
                float p = Mathf.Clamp01(f.timer / cellFadeDuration);
                UpdateMaskPixelInstant(f.x, f.y, Mathf.Lerp(f.startVal, f.targetVal, p), false);
                if (p >= 1.0f)
                {
                    if (f.x == _currentGridX && f.y == _currentGridY) _isInputBlocked = false;
                    _activeFades.RemoveAt(i);
                }
            }

            if (_maskTexture != null) _maskTexture.Apply();
        }

        private float GetMaskPixelValue(int x, int y) =>
            _maskTexture != null ? _maskTexture.GetPixel(x, (gridSize - 1) - y).r : 0f;

        private void UpdateMaskPixelInstant(int x, int y, float v, bool a = true)
        {
            if (_maskTexture != null)
            {
                _maskTexture.SetPixel(x, (gridSize - 1) - y, new Color(v, 0, 0, 0));
                if (a) _maskTexture.Apply();
            }
        }

        public override void OnExit()
        {
            base.OnExit();
            CleanupResources();
        }

        private void OnDestroy()
        {
            CleanupResources();
        }

        private void CleanupResources()
        {
            if (_maskTexture) Destroy(_maskTexture);
            if (_eraserMaterial) Destroy(_eraserMaterial);
            if (_gridMaterial) Destroy(_gridMaterial);
        }
        
        private void RevealAllQuestions()
        {
            if (questionSpots == null) return;

            foreach (var spot in questionSpots)
            {
                if (spot.x >= 0 && spot.x < gridSize && spot.y >= 0 && spot.y < gridSize)
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