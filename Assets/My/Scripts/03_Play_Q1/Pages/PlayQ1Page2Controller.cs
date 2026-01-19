using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts._03_Play_Q1.Pages
{
    // ---------------------------------------------------------
    // 데이터 클래스
    // ---------------------------------------------------------
    [Serializable]
    public class PlayQ1Page2Data
    {
        public TextSetting descriptionText1;
        public TextSetting descriptionText2;
        public TextSetting descriptionText3;
        public TextSetting[] questions; // JSON의 질문 텍스트 배열
    }

    // ---------------------------------------------------------
    // 컨트롤러 클래스
    // ---------------------------------------------------------
    public class PlayQ1Page2Controller : PlayQ1PageBase
    {
        [Header("Page UI")]
        [SerializeField] private Text text1; 
        [SerializeField] private Text text2; 

        [Header("Question UI")] 
        [SerializeField] private Text[] questions; // 인스펙터에서 할당

        [Header("Interaction Objects")]
        [SerializeField] private Image imageBlack; 
        [SerializeField] private Image imageGrid;  
        [SerializeField] private Image imageFocus; 

        [Header("Completion UI")]
        [SerializeField] private List<CanvasGroup> completionCanvasGroups;
        [SerializeField] private List<CanvasGroup> textCanvasGroups;       

        [Header("Settings")]
        [SerializeField] private int gridSize = 10; 
        [SerializeField] private List<Vector2Int> questionSpots; 
        [SerializeField] private float cellFadeDuration = 0.5f;  

        // 내부 변수
        private RectTransform _blackRect;
        private Texture2D _maskTexture;
        private Material _eraserMaterial;
        private Material _gridMaterial;
        private static readonly int MaskTexID = Shader.PropertyToID("_MaskTex");

        private float _cellWidth;
        private float _cellHeight;
        private int _currentGridX; 
        private int _currentGridY; 
        
        private bool[,] _questionMap;
        private bool _hasMoved;         
        private bool _isInputBlocked;   

        private readonly HashSet<Vector2Int> _foundSpots = new HashSet<Vector2Int>();
        private int _totalQuestionCount;
        private bool _isStageCompleted; 

        private float _currentIdleTime; 
        private const float IdleThreshold = 10f; 
        
        private TextSetting _defaultText2Data; 
        private TextSetting _warningTextData;  

        private Coroutine _text2FadeRoutine;
        private Coroutine _text2BlinkRoutine;

        private class CellFadeInfo
        {
            public int x;
            public int y;
            public float startVal;
            public float targetVal; 
            public float timer;
        }
        private readonly List<CellFadeInfo> _activeFades = new List<CellFadeInfo>();

        // ---------------------------------------------------------
        // 초기화 및 데이터 설정
        // ---------------------------------------------------------
        public override void SetupData(object data)
        {
            var pageData = data as PlayQ1Page2Data;
            if (pageData == null) return;
            
            // 기존 텍스트 세팅
            if (text1) UIManager.Instance.SetText(text1.gameObject, pageData.descriptionText1);
            if (text2) UIManager.Instance.SetText(text2.gameObject, pageData.descriptionText2);

            _defaultText2Data = pageData.descriptionText2;
            _warningTextData = pageData.descriptionText3;

            // 질문 텍스트 세팅
            if (questions != null)
            {
                for (int i = 0; i < questions.Length; i++)
                {
                    if (questions[i] == null) continue;

                    if (pageData.questions != null && i < pageData.questions.Length)
                    {
                        UIManager.Instance.SetText(questions[i].gameObject, pageData.questions[i]);
                        questions[i].gameObject.SetActive(true);
                    }
                    else
                    {
                        questions[i].gameObject.SetActive(false);
                    }
                }
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            
            _hasMoved = false;
            _isInputBlocked = false;
            _currentIdleTime = 0f;

            InitializeGame();
            SetFocusToGrid(4, 4, true); 
        }

        private void InitializeGame()
        {
            if (imageBlack == null || imageFocus == null) return;

            _blackRect = imageBlack.rectTransform;
            float totalWidth = _blackRect.rect.width;
            float totalHeight = _blackRect.rect.height;

            _cellWidth = totalWidth / gridSize;
            _cellHeight = totalHeight / gridSize;

            _foundSpots.Clear();
            _totalQuestionCount = 0;
            _isStageCompleted = false;

            if (completionCanvasGroups != null)
            {
                foreach (var cg in completionCanvasGroups)
                {
                    if (cg != null) { cg.alpha = 0f; cg.gameObject.SetActive(true); }
                }
            }
            
            // [복구] 텍스트 그룹 초기화: 다시 처음부터 보이게 설정 (Alpha 1)
            if (textCanvasGroups != null)
            {
                foreach (var cg in textCanvasGroups) if (cg != null) cg.alpha = 1f;
            }

            _questionMap = new bool[gridSize, gridSize];
            if (questionSpots != null)
            {
                foreach (var spot in questionSpots)
                {
                    if (spot.x >= 0 && spot.x < gridSize && spot.y >= 0 && spot.y < gridSize)
                        _questionMap[spot.x, spot.y] = true;
                }
                _totalQuestionCount = questionSpots.Count;
            }
            if (questionSpots != null && questionSpots.Count == 0) 
            {
                _questionMap[5, 5] = true;
                _totalQuestionCount = 1;
            }

            // 1. Black 이미지용 머티리얼 인스턴스 생성
            _eraserMaterial = Instantiate(imageBlack.material);
            imageBlack.material = _eraserMaterial;

            // 2. 마스크 텍스처 생성
            _maskTexture = new Texture2D(gridSize, gridSize, TextureFormat.R8, false) 
            { 
                filterMode = FilterMode.Point 
            };
            Color32[] colors = new Color32[gridSize * gridSize];
            _maskTexture.SetPixels32(colors);
            _maskTexture.Apply();

            // 3. Black 이미지에 마스크 전달
            _eraserMaterial.SetTexture(MaskTexID, _maskTexture);

            // 4. Grid 이미지에도 동일한 마스크 전달
            if (imageGrid != null)
            {
                _gridMaterial = Instantiate(imageGrid.material);
                imageGrid.material = _gridMaterial;
                _gridMaterial.SetTexture(MaskTexID, _maskTexture);
            }

            _activeFades.Clear();
        }

        // ---------------------------------------------------------
        // 메인 루프
        // ---------------------------------------------------------
        private void Update()
        {   
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Input.GetKeyDown(KeyCode.Space) && !_isStageCompleted)
            {
                _isStageCompleted = true; 
                StartCoroutine(ShowCompletionRoutine()); 
            }
#endif
            HandleMovement();
            UpdateCellFades(); 
            HandleIdleCheck(); 
        }

        private void HandleIdleCheck()
        {
            if (_isInputBlocked || _isStageCompleted) return; 

            _currentIdleTime += Time.deltaTime;

            if (_currentIdleTime >= IdleThreshold)
            {
                if (_text2BlinkRoutine != null || _text2FadeRoutine != null) return;

                _currentIdleTime = 0f;
                if (_warningTextData != null && text2 != null)
                    UIManager.Instance.SetText(text2.gameObject, _warningTextData);

                WarningBlinkText2();
            }
        }

        private void HandleMovement()
        {
            if (imageFocus == null || _isInputBlocked || _isStageCompleted) return;

            bool vHold = Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow);
            bool hHold = Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.LeftArrow);
            bool attemptMove = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) || 
                               Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.LeftArrow);

            if (attemptMove)
            {
                _currentIdleTime = 0f;
                if (_text2FadeRoutine == null && _defaultText2Data != null && text2 != null)
                    UIManager.Instance.SetText(text2.gameObject, _defaultText2Data);
            }

            if (attemptMove && vHold && hHold)
            {
                _currentIdleTime = 0f;
                if (_text2BlinkRoutine == null && _text2FadeRoutine == null) WarningBlinkText2();
                return;
            }

            int dx = 0, dy = 0;
            if (Input.GetKeyDown(KeyCode.UpArrow)) dy = -1;
            else if (Input.GetKeyDown(KeyCode.DownArrow)) dy = 1;
            else if (Input.GetKeyDown(KeyCode.RightArrow)) dx = 1;
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) dx = -1;

            if (dx != 0 || dy != 0)
            {
                if (!_hasMoved)
                {
                    _hasMoved = true;
                    if (text1 != null && text1.gameObject.activeInHierarchy) 
                        StartCoroutine(FadeTo(text1, 0f, 1.0f, () => text1.gameObject.SetActive(false)));
                }

                if (text2 != null && text2.gameObject.activeSelf)
                {
                    if (_text2FadeRoutine == null)
                    {
                        if (_text2BlinkRoutine != null) { StopCoroutine(_text2BlinkRoutine); _text2BlinkRoutine = null; }
                        _text2FadeRoutine = StartCoroutine(FadeTo(text2, 0f, 1.0f, () => 
                        {
                            text2.gameObject.SetActive(false);
                            _text2FadeRoutine = null;
                        }));
                    }
                }

                int nextX = _currentGridX + dx;
                int nextY = _currentGridY + dy;

                if (nextX >= 0 && nextX < gridSize && nextY >= 0 && nextY < gridSize)
                    SetFocusToGrid(nextX, nextY);
            }
        }

        private void WarningBlinkText2()
        {
            if (text2 == null || _text2BlinkRoutine != null || _text2FadeRoutine != null) return;
            _text2BlinkRoutine = StartCoroutine(BlinkRoutine());
        }

        private IEnumerator BlinkRoutine()
        {
            text2.gameObject.SetActive(true);
            Color c = text2.color; c.a = 1f; text2.color = c;
            float fadeTime = 1.0f; 

            for (int i = 0; i < 1; i++)
            {
                yield return StartCoroutine(FadeTo(text2, 0f, fadeTime));
                yield return StartCoroutine(FadeTo(text2, 1f, fadeTime));
            }
            yield return StartCoroutine(FadeTo(text2, 0f, fadeTime, () => text2.gameObject.SetActive(false)));
            _text2BlinkRoutine = null;
        }

        private IEnumerator FadeTo(Text target, float targetAlpha, float duration, Action onComplete = null)
        {
            float startAlpha = target.color.a;
            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float alpha = Mathf.Lerp(startAlpha, targetAlpha, timer / duration);
                Color color = target.color; color.a = alpha; target.color = color;
                yield return null;
            }
            Color finalColor = target.color; finalColor.a = targetAlpha; target.color = finalColor;
            onComplete?.Invoke();
        }

        private void SetFocusToGrid(int x, int y, bool isFirstInit = false)
        {
            if (!isFirstInit)
            {
                if (!_questionMap[_currentGridX, _currentGridY]) 
                    StartCellFade(_currentGridX, _currentGridY, 0.0f);
                _isInputBlocked = true;
            }

            _currentGridX = x;
            _currentGridY = y;

            float startX = -(_blackRect.rect.width / 2f);
            float posX = startX + (x * _cellWidth) + (_cellWidth / 2f);
            float startY = (_blackRect.rect.height / 2f);
            float posY = startY - (y * _cellHeight) - (_cellHeight / 2f);

            imageFocus.rectTransform.anchoredPosition = new Vector2(posX, posY);

            if (isFirstInit) UpdateMaskPixelInstant(x, y, 1.0f);
            else StartCellFade(x, y, 1.0f);

            CheckQuestionFound(x, y);
        }

        private void CheckQuestionFound(int x, int y)
        {
            if (_questionMap[x, y])
            {
                Vector2Int currentPos = new Vector2Int(x, y);
                if (!_foundSpots.Contains(currentPos))
                {
                    _foundSpots.Add(currentPos);
                    
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
            // 1. 완료 UI 페이드 인
            if (completionCanvasGroups != null)
            {
                float timer = 0f, duration = 1.0f;
                while (timer < duration)
                {
                    timer += Time.deltaTime;
                    float alpha = Mathf.Clamp01(timer / duration);
                    foreach (var cg in completionCanvasGroups) if (cg != null) cg.alpha = alpha;
                    yield return null;
                }
                foreach (var cg in completionCanvasGroups) if (cg != null) cg.alpha = 1f;
            }

            // 2. 2초 대기
            yield return new WaitForSeconds(2.0f);

            // 3. 페이드 아웃 (그리드, 텍스트)
            float fadeOutTimer = 0f, fadeOutDuration = 0.5f;
            float gridStartAlpha = (imageGrid != null) ? imageGrid.color.a : 1f;

            while (fadeOutTimer < fadeOutDuration)
            {
                fadeOutTimer += Time.deltaTime;
                float progress = fadeOutTimer / fadeOutDuration;
                
                if (imageGrid != null)
                {
                    Color c = imageGrid.color; c.a = Mathf.Lerp(gridStartAlpha, 0f, progress); imageGrid.color = c;
                }
                if (textCanvasGroups != null)
                {
                    foreach (var cg in textCanvasGroups) if (cg != null) cg.alpha = Mathf.Lerp(1f, 0f, progress);
                }
                yield return null;
            }

            if (imageGrid != null) { Color c = imageGrid.color; c.a = 0f; imageGrid.color = c; }
            if (textCanvasGroups != null) foreach (var cg in textCanvasGroups) if (cg != null) cg.alpha = 0f;

            CompleteStep();
        }

        private void StartCellFade(int x, int y, float targetVal)
        {
            CellFadeInfo info = _activeFades.Find(f => f.x == x && f.y == y);
            if (info == null)
            {
                info = new CellFadeInfo { x = x, y = y, timer = 0f, startVal = GetMaskPixelValue(x, y), targetVal = targetVal };
                _activeFades.Add(info);
            }
            else
            {
                info.startVal = GetMaskPixelValue(x, y); info.targetVal = targetVal; info.timer = 0f;
            }
        }

        private void UpdateCellFades()
        {
            if (_activeFades.Count == 0) return;
            for (int i = _activeFades.Count - 1; i >= 0; i--)
            {
                var fade = _activeFades[i];
                fade.timer += Time.deltaTime;
                float progress = Mathf.Clamp01(fade.timer / cellFadeDuration);
                float currentVal = Mathf.Lerp(fade.startVal, fade.targetVal, progress);

                UpdateMaskPixelInstant(fade.x, fade.y, currentVal, false);

                if (progress >= 1.0f)
                {
                    if (fade.x == _currentGridX && fade.y == _currentGridY) _isInputBlocked = false;
                    _activeFades.RemoveAt(i);
                }
            }
            if (_maskTexture != null) _maskTexture.Apply();
        }

        private float GetMaskPixelValue(int x, int y)
        {
            if (_maskTexture == null) return 0f;
            int texY = (gridSize - 1) - y;
            return _maskTexture.GetPixel(x, texY).r;
        }

        private void UpdateMaskPixelInstant(int x, int y, float rValue, bool apply = true)
        {
            if (_maskTexture == null) return;
            int texY = (gridSize - 1) - y;
            _maskTexture.SetPixel(x, texY, new Color(rValue, 0, 0, 0));
            if (apply) _maskTexture.Apply();
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
            if (_maskTexture != null) { Destroy(_maskTexture); _maskTexture = null; }
            if (_eraserMaterial != null) { Destroy(_eraserMaterial); _eraserMaterial = null; }
            if (_gridMaterial != null) { Destroy(_gridMaterial); _gridMaterial = null; }
        }
    }
}