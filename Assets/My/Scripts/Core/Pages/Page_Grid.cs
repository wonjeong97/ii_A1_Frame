using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core.Data;
using Wonjeong.Data;
using Wonjeong.UI;

namespace My.Scripts.Core.Pages
{
    public class Page_Grid : GamePage
    {
        [Header("UI References")] [SerializeField]
        private Text textMain;

        [SerializeField] private Text textSub;
        [SerializeField] private Text[] questionTexts;

        [Header("Interaction")] [SerializeField]
        private Image imageBlack;

        [SerializeField] private Image imageGrid;
        [SerializeField] private Image imageFocus;

        [Header("Completion & Groups")] [SerializeField]
        private List<CanvasGroup> completionCanvasGroups;

        [SerializeField] private List<CanvasGroup> textCanvasGroups;

        [Header("Settings")] [SerializeField] private int gridSize = 10;
        [SerializeField] private List<Vector2Int> questionSpots;
        [SerializeField] private float cellFadeDuration = 0.5f;

        // 내부 변수
        private RectTransform _blackRect;
        private Texture2D _maskTexture;
        private Material _eraserMaterial;
        private Material _gridMaterial;
        private static readonly int MaskTexID = Shader.PropertyToID("_MaskTex");

        private float _cellWidth, _cellHeight;
        private int _currentGridX, _currentGridY;
        private bool[,] _questionMap;
        private bool _hasMoved, _isInputBlocked, _isStageCompleted;
        private readonly HashSet<Vector2Int> _foundSpots = new HashSet<Vector2Int>();
        private int _totalQuestionCount;

        private float _currentIdleTime;
        private const float IdleThreshold = 10f;
        private TextSetting _defaultTextSub, _warningText;
        private Coroutine _textFadeRoutine, _textBlinkRoutine;

        private class CellFadeInfo
        {
            public int x, y;
            public float startVal, targetVal, timer;
        }

        private readonly List<CellFadeInfo> _activeFades = new List<CellFadeInfo>();

        public override void SetupData(object data)
        {
            var pageData = data as GridPageData;
            if (pageData == null) return;

            if (textMain) UIManager.Instance.SetText(textMain.gameObject, pageData.descriptionText1);
            if (textSub) UIManager.Instance.SetText(textSub.gameObject, pageData.descriptionText2);

            _defaultTextSub = pageData.descriptionText2;
            _warningText = pageData.descriptionText3;

            if (questionTexts != null)
            {
                for (int i = 0; i < questionTexts.Length; i++)
                {
                    if (!questionTexts[i]) continue;
                    if (pageData.questions != null && i < pageData.questions.Length)
                    {
                        UIManager.Instance.SetText(questionTexts[i].gameObject, pageData.questions[i]);
                        questionTexts[i].gameObject.SetActive(true);
                    }
                    else questionTexts[i].gameObject.SetActive(false);
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
            int startX = Mathf.Min(4, gridSize - 1);
            int startY = Mathf.Min(4, gridSize - 1);
            SetFocusToGrid(startX, startY, true);
        }

        private void InitializeGame()
        {
            if (!imageBlack || !imageFocus) return;
            _blackRect = imageBlack.rectTransform;
            _cellWidth = _blackRect.rect.width / gridSize;
            _cellHeight = _blackRect.rect.height / gridSize;
            _foundSpots.Clear();
            _isStageCompleted = false;

            if (completionCanvasGroups != null)
                foreach (var cg in completionCanvasGroups)
                {
                    if (cg)
                    {
                        cg.alpha = 0f;
                        cg.gameObject.SetActive(true);
                    }
                }

            if (textCanvasGroups != null)
                foreach (var cg in textCanvasGroups)
                    if (cg)
                        cg.alpha = 1f;

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
                int defaultX = Mathf.Min(5, gridSize - 1);
                int defaultY = Mathf.Min(5, gridSize - 1);
                _questionMap[defaultX, defaultY] = true;
                _totalQuestionCount = 1;
            }

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
        }

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
                if (_textBlinkRoutine != null || _textFadeRoutine != null) return;
                _currentIdleTime = 0f;
                if (_warningText != null && textSub != null)
                    UIManager.Instance.SetText(textSub.gameObject, _warningText);
                WarningBlinkTextSub();
            }
        }

        private void HandleMovement()
        {
            if (!imageFocus || _isInputBlocked || _isStageCompleted) return;

            bool vHold = Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow);
            bool hHold = Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.LeftArrow);
            bool attemptMove = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) ||
                               Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.LeftArrow);

            if (attemptMove)
            {
                _currentIdleTime = 0f;
                if (_textFadeRoutine == null && _defaultTextSub != null && textSub != null)
                    UIManager.Instance.SetText(textSub.gameObject, _defaultTextSub);
            }

            if (attemptMove && vHold && hHold)
            {
                _currentIdleTime = 0f;
                if (_textBlinkRoutine == null && _textFadeRoutine == null) WarningBlinkTextSub();
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
                    if (textMain != null)
                        StartCoroutine(FadeTo(textMain, 0f, 1.0f, () => textMain.gameObject.SetActive(false)));
                }

                if (textSub != null && textSub.gameObject.activeSelf && _textFadeRoutine == null)
                {
                    if (_textBlinkRoutine != null)
                    {
                        StopCoroutine(_textBlinkRoutine);
                        _textBlinkRoutine = null;
                    }

                    _textFadeRoutine = StartCoroutine(FadeTo(textSub, 0f, 1.0f, () =>
                    {
                        textSub.gameObject.SetActive(false);
                        _textFadeRoutine = null;
                    }));
                }

                int nextX = _currentGridX + dx, nextY = _currentGridY + dy;
                if (nextX >= 0 && nextX < gridSize && nextY >= 0 && nextY < gridSize) SetFocusToGrid(nextX, nextY);
            }
        }

        private void WarningBlinkTextSub()
        {
            if (!textSub || _textBlinkRoutine != null || _textFadeRoutine != null) return;
            _textBlinkRoutine = StartCoroutine(BlinkRoutine());
        }

        private IEnumerator BlinkRoutine()
        {
            textSub.gameObject.SetActive(true);
            yield return StartCoroutine(FadeTo(textSub, 0f, 1f));
            yield return StartCoroutine(FadeTo(textSub, 1f, 1f));
            yield return StartCoroutine(FadeTo(textSub, 0f, 1f, () => textSub.gameObject.SetActive(false)));
            _textBlinkRoutine = null;
        }

        private IEnumerator FadeTo(Text target, float targetAlpha, float duration, Action onComplete = null)
        {
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
            if (completionCanvasGroups != null)
            {
                float t = 0f;
                while (t < 1.0f)
                {
                    t += Time.deltaTime;
                    foreach (var cg in completionCanvasGroups)
                        if (cg)
                            cg.alpha = Mathf.Clamp01(t);
                    yield return null;
                }

                foreach (var cg in completionCanvasGroups)
                    if (cg)
                        cg.alpha = 1f;
            }

            yield return new WaitForSeconds(2.0f);

            float t2 = 0f;
            float startA = imageGrid ? imageGrid.color.a : 1f;
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
                        if (cg)
                            cg.alpha = Mathf.Lerp(1f, 0f, p);
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
                var fade = _activeFades[i];
                fade.timer += Time.deltaTime;
                float progress = Mathf.Clamp01(fade.timer / cellFadeDuration);
                UpdateMaskPixelInstant(fade.x, fade.y, Mathf.Lerp(fade.startVal, fade.targetVal, progress), false);
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
            return _maskTexture != null ? _maskTexture.GetPixel(x, (gridSize - 1) - y).r : 0f;
        }

        private void UpdateMaskPixelInstant(int x, int y, float rValue, bool apply = true)
        {
            if (_maskTexture != null)
            {
                _maskTexture.SetPixel(x, (gridSize - 1) - y, new Color(rValue, 0, 0, 0));
                if (apply) _maskTexture.Apply();
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
    }
}