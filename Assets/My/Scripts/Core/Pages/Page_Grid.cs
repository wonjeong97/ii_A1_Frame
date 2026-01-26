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
    /// <summary> 그리드 탐색 게임 페이지 컨트롤러 </summary>
    public class Page_Grid : GamePage<GridPageData>
    {
        [Header("UI References")] 
        [SerializeField] private Text textMain; // 메인 설명 텍스트
        [SerializeField] private Text textSub; // 보조 설명 텍스트 (경고 등)
        [SerializeField] private Text[] questionTexts; // 질문 텍스트 배열

        [Header("Interaction")] 
        [SerializeField] private Image imageBlack; // 마스킹용 검은 배경
        [SerializeField] private Image imageGrid; // 그리드 라인 이미지
        [SerializeField] private Image imageFocus; // 현재 위치 포커스 이미지

        [Header("Completion & Groups")]
        [SerializeField] private List<CanvasGroup> completionCanvasGroups; // 완료 시 표시할 그룹 리스트
        [SerializeField] private List<CanvasGroup> textCanvasGroups; // 텍스트 그룹 리스트

        [Header("Settings")] 
        [SerializeField] private List<Vector2Int> questionSpots; // 정답 좌표 리스트
        
        private readonly int gridSize = 10; // 그리드 크기 (10x10)
        private readonly float cellFadeDuration = 0.25f; // 셀 페이드 시간

        // 내부 변수
        private RectTransform _blackRect; // 검은 배경 Rect
        private Texture2D _maskTexture; // 마스킹 텍스처
        private Material _eraserMaterial; // 지우개 효과 재질
        private Material _gridMaterial; // 그리드 효과 재질
        private static readonly int MaskTexID = Shader.PropertyToID("_MaskTex");

        private float _cellWidth, _cellHeight; // 셀 단위 크기
        private int _currentGridX, _currentGridY; // 현재 그리드 좌표
        private bool[,] _questionMap; // 정답 위치 맵
        private bool _hasMoved, _isInputBlocked, _isStageCompleted; // 상태 플래그
        private readonly HashSet<Vector2Int> _foundSpots = new HashSet<Vector2Int>(); // 발견한 정답 집합
        private int _totalQuestionCount; // 총 정답 개수

        private float _currentIdleTime; // 입력 대기 시간
        private const float IdleThreshold = 10f; // 대기 임계값 (초)
        private TextSetting _defaultTextSub, _warningText; // 텍스트 설정 데이터
        private Coroutine _textFadeRoutine, _textBlinkRoutine; // 코루틴 참조

        // 셀 페이드 정보 클래스
        private class CellFadeInfo
        {
            public int x, y;
            public float startVal, targetVal, timer;
        }

        private readonly List<CellFadeInfo> _activeFades = new List<CellFadeInfo>(); // 활성 페이드 목록

        /// <summary> 데이터 설정 (텍스트 및 정답 좌표 적용) </summary>
        protected override void SetupData(GridPageData data)
        {
            if (data == null) return;

            if (textMain) UIManager.Instance.SetText(textMain.gameObject, data.descriptionText1);
            if (textSub) UIManager.Instance.SetText(textSub.gameObject, data.descriptionText2);

            _defaultTextSub = data.descriptionText2;
            _warningText = data.descriptionText3;

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
        }

        /// <summary> 페이지 진입 (게임 초기화 및 시작 위치 설정) </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            _hasMoved = false;
            _isInputBlocked = false;
            _currentIdleTime = 0f;
            
            InitializeGame();
            
            // 시작 위치 설정 (중앙 부근)
            int startX = Mathf.Min(4, gridSize - 1);
            int startY = Mathf.Min(4, gridSize - 1);
            SetFocusToGrid(startX, startY, true);
        }

        /// <summary> 게임 리소스 및 상태 초기화 </summary>
        private void InitializeGame()
        {
            if (!imageBlack || !imageFocus) return;
            
            _blackRect = imageBlack.rectTransform;
            _cellWidth = _blackRect.rect.width / gridSize;
            _cellHeight = _blackRect.rect.height / gridSize;
            _foundSpots.Clear();
            _isStageCompleted = false;

            // 완료 그룹 숨기기
            if (completionCanvasGroups != null)
                foreach (var cg in completionCanvasGroups)
                {
                    if (cg)
                    {
                        cg.alpha = 0f;
                        cg.gameObject.SetActive(true);
                    }
                }

            // 텍스트 그룹 보이기
            if (textCanvasGroups != null)
                foreach (var cg in textCanvasGroups)
                    if (cg)
                        cg.alpha = 1f;

            // 정답 맵 생성
            _questionMap = new bool[gridSize, gridSize];
            if (questionSpots != null)
            {
                foreach (var s in questionSpots)
                    if (s.x >= 0 && s.x < gridSize && s.y >= 0 && s.y < gridSize)
                        _questionMap[s.x, s.y] = true;
                _totalQuestionCount = questionSpots.Count;
            }

            // 정답이 없으면 기본값 설정
            if (_totalQuestionCount == 0)
            {
                int defaultX = Mathf.Min(5, gridSize - 1);
                int defaultY = Mathf.Min(5, gridSize - 1);
                _questionMap[defaultX, defaultY] = true;
                _totalQuestionCount = 1;
            }

            // 마스킹 텍스처 및 재질 설정
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

        /// <summary> 프레임 업데이트 (입력 및 상태 처리) </summary>
        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 디버그용 강제 완료
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

        /// <summary> 입력 대기 체크 및 경고 처리 </summary>
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

        /// <summary> 방향키 입력 처리 </summary>
        private void HandleMovement()
        {
            if (!imageFocus || _isInputBlocked || _isStageCompleted) return;

            // 입력 상태 확인
            bool vHold = Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow);
            bool hHold = Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.LeftArrow);
            bool attemptMove = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) ||
                               Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.LeftArrow);

            // 움직임 시도 시 텍스트 복구
            if (attemptMove)
            {
                _currentIdleTime = 0f;
                if (_textFadeRoutine == null && _defaultTextSub != null && textSub != null)
                    UIManager.Instance.SetText(textSub.gameObject, _defaultTextSub);
            }

            // 대각선 이동 방지 (동시 입력 시 경고)
            if (attemptMove && vHold && hHold)
            {
                _currentIdleTime = 0f;
                if (_textBlinkRoutine == null && _textFadeRoutine == null) WarningBlinkTextSub();
                return;
            }

            // 이동 방향 계산
            int dx = 0, dy = 0;
            if (Input.GetKeyDown(KeyCode.UpArrow)) dy = -1;
            else if (Input.GetKeyDown(KeyCode.DownArrow)) dy = 1;
            else if (Input.GetKeyDown(KeyCode.RightArrow)) dx = 1;
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) dx = -1;

            if (dx != 0 || dy != 0)
            {
                // 첫 이동 시 메인 텍스트 숨김
                if (!_hasMoved)
                {
                    _hasMoved = true;
                    if (textMain != null)
                        StartCoroutine(FadeTo(textMain, 0f, 1.0f, () => textMain.gameObject.SetActive(false)));
                }

                // 보조 텍스트 숨김
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

                // 그리드 이동 적용
                int nextX = _currentGridX + dx, nextY = _currentGridY + dy;
                if (nextX >= 0 && nextX < gridSize && nextY >= 0 && nextY < gridSize) SetFocusToGrid(nextX, nextY);
            }
        }

        /// <summary> 보조 텍스트 깜빡임 경고 </summary>
        private void WarningBlinkTextSub()
        {
            if (!textSub || _textBlinkRoutine != null || _textFadeRoutine != null) return;
            _textBlinkRoutine = StartCoroutine(BlinkRoutine());
        }

        /// <summary> 깜빡임 코루틴 </summary>
        private IEnumerator BlinkRoutine()
        {
            textSub.gameObject.SetActive(true);
            yield return StartCoroutine(FadeTo(textSub, 0f, 1f));
            yield return StartCoroutine(FadeTo(textSub, 1f, 1f));
            yield return StartCoroutine(FadeTo(textSub, 0f, 1f, () => textSub.gameObject.SetActive(false)));
            _textBlinkRoutine = null;
        }

        /// <summary> 텍스트 알파값 페이드 코루틴 </summary>
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

        /// <summary> 포커스 이동 및 셀 마스킹 처리 </summary>
        private void SetFocusToGrid(int x, int y, bool isFirstInit = false)
        {
            if (!isFirstInit)
            {
                // 이전 위치 페이드 아웃 (정답이 아닐 경우)
                if (!_questionMap[_currentGridX, _currentGridY]) StartCellFade(_currentGridX, _currentGridY, 0.0f);
                _isInputBlocked = true; // 이동 중 입력 차단
            }

            _currentGridX = x;
            _currentGridY = y;
            
            // 포커스 이미지 위치 계산
            float startX = -(_blackRect.rect.width / 2f), startY = (_blackRect.rect.height / 2f);
            imageFocus.rectTransform.anchoredPosition = new Vector2(startX + (x * _cellWidth) + (_cellWidth / 2f),
                startY - (y * _cellHeight) - (_cellHeight / 2f));

            // 현재 위치 페이드 인 (마스킹 해제)
            if (isFirstInit) UpdateMaskPixelInstant(x, y, 1.0f);
            else StartCellFade(x, y, 1.0f);
            
            CheckQuestionFound(x, y);
        }

        /// <summary> 정답 발견 체크 </summary>
        private void CheckQuestionFound(int x, int y)
        {
            if (_questionMap[x, y])
            {
                Vector2Int currentPos = new Vector2Int(x, y);
                if (!_foundSpots.Contains(currentPos))
                {
                    _foundSpots.Add(currentPos);
                    // 모든 정답 발견 시 완료 처리
                    if (_foundSpots.Count >= _totalQuestionCount && !_isStageCompleted)
                    {
                        _isStageCompleted = true;
                        StartCoroutine(ShowCompletionRoutine());
                    }
                }
            }
        }

        /// <summary> 완료 연출 코루틴 </summary>
        private IEnumerator ShowCompletionRoutine()
        {
            // 완료 그룹 페이드 인
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

            // 그리드 및 텍스트 페이드 아웃
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

            CompleteStep(); // 단계 완료
        }

        /// <summary> 셀 페이드 시작 (값 변경 요청) </summary>
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

        /// <summary> 활성 셀 페이드 업데이트 </summary>
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
                    // 이동 완료 시 입력 차단 해제
                    if (fade.x == _currentGridX && fade.y == _currentGridY) _isInputBlocked = false;
                    _activeFades.RemoveAt(i);
                }
            }

            if (_maskTexture != null) _maskTexture.Apply();
        }

        /// <summary> 마스크 픽셀 값 조회 </summary>
        private float GetMaskPixelValue(int x, int y)
        {
            return _maskTexture != null ? _maskTexture.GetPixel(x, (gridSize - 1) - y).r : 0f;
        }

        /// <summary> 마스크 픽셀 값 즉시 설정 </summary>
        private void UpdateMaskPixelInstant(int x, int y, float rValue, bool apply = true)
        {
            if (_maskTexture != null)
            {
                _maskTexture.SetPixel(x, (gridSize - 1) - y, new Color(rValue, 0, 0, 0));
                if (apply) _maskTexture.Apply();
            }
        }

        /// <summary> 페이지 퇴장 (리소스 정리) </summary>
        public override void OnExit()
        {
            base.OnExit();
            CleanupResources();
        }

        private void OnDestroy()
        {
            CleanupResources();
        }

        /// <summary> 생성된 텍스처 및 재질 파괴 </summary>
        private void CleanupResources()
        {
            if (_maskTexture) Destroy(_maskTexture);
            if (_eraserMaterial) Destroy(_eraserMaterial);
            if (_gridMaterial) Destroy(_gridMaterial);
        }
    }
}