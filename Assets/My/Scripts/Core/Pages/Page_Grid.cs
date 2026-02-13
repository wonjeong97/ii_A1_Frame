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
        [SerializeField] private Text textMain; // 메인 설명 텍스트
        [SerializeField] private Text textSub; // 보조 설명 텍스트 (안내 및 경고)
        [SerializeField] private Text[] questionTexts; // 질문 리스트 텍스트

        [Header("Interaction")] 
        [SerializeField] private Image imageBlack; // 마스킹 배경
        [SerializeField] private Image imageGrid; // 그리드 라인
        [SerializeField] private Image imageFocus; // 현재 위치 포커스

        [Header("Completion & Groups")] 
        [SerializeField] private List<CanvasGroup> completionCanvasGroups; // 완료 시 표시할 그룹
        [SerializeField] private List<CanvasGroup> textCanvasGroups; // 텍스트 그룹
        
        [Header("Settings")]
        [SerializeField] private List<Vector2Int> questionSpots; // 정답 좌표 리스트
        [SerializeField] private int gridSize = 10; // 그리드 크기 (10x10)
        
        private readonly float cellFadeDuration = 0.5f; // 셀 페이드 시간

        // --- 내부 로직 변수 ---
        private RectTransform _blackRect; // 배경 Rect
        private Texture2D _maskTexture; // 마스킹 텍스처
        private Material _eraserMaterial, _gridMaterial; // 마스킹 재질
        private static readonly int MaskTexID = Shader.PropertyToID("_MaskTex");

        private float _cellWidth, _cellHeight; // 셀 단위 크기
        private int _currentGridX, _currentGridY; // 현재 좌표
        private bool[,] _questionMap; // 정답 위치 맵
        private bool _hasMoved; // 이동 여부 체크
        private bool _isInputBlocked; // 입력 차단 여부
        private bool _isStageCompleted; // 스테이지 완료 여부
        private readonly HashSet<Vector2Int> _foundSpots = new HashSet<Vector2Int>(); // 발견한 정답들
        private int _totalQuestionCount; // 총 정답 수

        // --- 텍스트 및 경고 관련 ---
        private TextSetting _defaultTextSub; // 기본 하단 텍스트 저장
        private TextSetting _warningText; // 경고용 텍스트 데이터
        private Coroutine _textFadeRoutine; // 텍스트 페이드 코루틴

        // Page_Grid만의 고유 기능인 '깜빡임' 관련 변수
        private Coroutine _textBlinkRoutine; // 텍스트 깜빡임 코루틴
        private const float BlinkThreshold = 10f; // 1차 경고 고정 시간
        private bool _is1stWarningDone = false; // 1차 경고 완료 여부

        // --- 휠 입력 및 관성 보정 변수 ---
        private int _lastP1Key = -1;
        private int _lastP2Key = -1;
        
        private float _p1LastTime;
        private int _p1LastDir; // 1: CW, -1: CCW
        
        private float _p2LastTime;
        private int _p2LastDir; // 1: CW, -1: CCW

        private const float FastInputThreshold = 0.2f; // 빠른 입력 판단 기준 시간 (초)

        // 셀 페이드 정보 관리
        private class CellFadeInfo
        {
            public int x, y;
            public float startVal, targetVal, timer;
        }

        private readonly List<CellFadeInfo> _activeFades = new List<CellFadeInfo>();

        /// <summary> 데이터 설정: 텍스트, 팝업 메시지 및 정답 좌표 적용 </summary>
        protected override void SetupData(GridPageData data)
        {
            if (data == null) return;

            if (textMain) UIManager.Instance.SetText(textMain.gameObject, data.descriptionText1);
            if (textSub) UIManager.Instance.SetText(textSub.gameObject, data.descriptionText2);

            _defaultTextSub = data.descriptionText2;
            _warningText = data.descriptionText3;

            // JSON에 좌표 데이터가 있다면 인스펙터 값을 덮어씌움
            if (data.questionSpots != null && data.questionSpots.Count > 0)
            {
                questionSpots = new List<Vector2Int>(data.questionSpots);
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

            // 팝업 메시지 설정 
            SetupPopupMessage(data.warningMessage, data.resetMessage);
        }

        /// <summary> 페이지 진입: 상태 초기화 </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            _hasMoved = false;
            _isInputBlocked = false;

            // 휠 상태 초기화
            _lastP1Key = -1;
            _lastP2Key = -1;
            _p1LastTime = 0f; _p1LastDir = 0;
            _p2LastTime = 0f; _p2LastDir = 0;

            ResetIdleState(true); // 즉시 초기화

            if (!InitializeGame()) return;

            // 시작 위치 설정
            int startX = (gridSize / 2) - 1;
            int startY = (gridSize / 2) - 1;
            SetFocusToGrid(startX, startY, true);
        }

        /// <summary> 게임 초기화: 텍스처 생성 및 정답 맵 설정 </summary>
        private bool InitializeGame()
        {
            if (!imageBlack || !imageFocus) return false;
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

            // 정답 맵 생성
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
                // 중앙 위치를 기본 정답으로 설정
                int center = (gridSize / 2) - 1;
                _questionMap[center, center] = true;
                _totalQuestionCount = 1;
            }

            // 리소스 재생성
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

        /// <summary> 매 프레임 업데이트: 입력 감지 및 비활성 체크 </summary>
        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Input.GetKeyDown(KeyCode.Space) && !_isStageCompleted)
            {
                _isStageCompleted = true;
                RevealAllQuestions();
                StartCoroutine(ShowCompletionRoutine());
            }
#endif

            // 1. 입력 감지
            if (Input.anyKey || Input.touchCount > 0)
            {
                ResetIdleState(false); // 부드럽게 초기화
                HandleMovement();
            }
            else
            {
                // 2. 비활성 시간 누적 (고유 로직 포함)
                if (!_isInputBlocked && !_isStageCompleted && !isResetSequenceActive)
                {
                    // 부모 변수 currentIdleTime 사용
                    currentIdleTime += Time.deltaTime;

                    // [Case 1] 1차 경고: 깜빡임 (10초 ~ 리셋시간)
                    if (currentIdleTime >= BlinkThreshold && currentIdleTime < inactivityThreshold)
                    {
                        if (!_is1stWarningDone && _textBlinkRoutine == null)
                        {
                            if (_warningText != null && textSub)
                                UIManager.Instance.SetText(textSub.gameObject, _warningText);

                            _textBlinkRoutine = StartCoroutine(BlinkRoutine());
                        }
                    }
                    // [Case 2] 2차 경고: 리셋 팝업 (부모 로직 호출)
                    else if (currentIdleTime >= inactivityThreshold)
                    {
                        StartResetSequence();
                    }
                }
            }

            UpdateCellFades();
        }

        /// <summary>  대기 상태 초기화 </summary>
        protected override void ResetIdleState(bool immediate = false)
        {
            // 1. 부모의 리셋 로직 실행 (팝업 끄기, 타이머 초기화 등)
            base.ResetIdleState(immediate);

            // 2. 자식(Page_Grid) 고유의 깜빡임 상태 초기화
            _is1stWarningDone = false;

            if (_textBlinkRoutine != null)
            {
                StopCoroutine(_textBlinkRoutine);
                _textBlinkRoutine = null;

                // 이동 중이라면 텍스트를 바로 끄지 않고 HandleMovement에서 처리
                if (immediate && textSub) textSub.gameObject.SetActive(false);
                else if (textSub) textSub.gameObject.SetActive(true);
            }
        }

        /// <summary> 리셋 시퀀스 시작 </summary>
        protected override void StartResetSequence()
        {
            // 깜빡임 코루틴 정리 후 팝업 띄우기
            if (_textBlinkRoutine != null)
            {
                StopCoroutine(_textBlinkRoutine);
                _textBlinkRoutine = null;
            }

            if (textSub) textSub.gameObject.SetActive(false);

            base.StartResetSequence();
        }

        /// <summary> 1차 경고: 텍스트 2회 깜빡임 후 소멸 </summary>
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
        /// <summary> 휠 입력 및 방향키 입력에 따른 이동 처리 (관성 보정 포함) </summary>
        private void HandleMovement()
        {
            if (!imageFocus || _isInputBlocked || _isStageCompleted) return;

            int dx = 0, dy = 0;
            float now = Time.time;

            // 1. Player 1 (Vertical: 1~4) -> 상하 (dy)
            int p1Key = GetPressedKeyIndex(1, 4);
            if (p1Key != -1)
            {
                if (_lastP1Key != -1)
                {
                    int diff = (p1Key - _lastP1Key + 4) % 4;
                    int dir = 0; // 1: CW(Down), -1: CCW(Up)

                    if (diff == 1) dir = 1;
                    else if (diff == 3) dir = -1;

                    // [관성 보정]
                    if (now - _p1LastTime < FastInputThreshold && _p1LastDir != 0)
                    {
                        if (diff == 2 || (dir != 0 && dir != _p1LastDir))
                        {
                            dir = _p1LastDir;
                        }
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
            int p2Key = GetPressedKeyIndex(5, 8);
            if (p2Key != -1)
            {
                if (_lastP2Key != -1)
                {
                    int currIdx = p2Key - 5;
                    int lastIdx = _lastP2Key - 5;
                    int diff = (currIdx - lastIdx + 4) % 4;
                    int dir = 0; // 1: CW(Right), -1: CCW(Left)

                    if (diff == 1) dir = 1;
                    else if (diff == 3) dir = -1;

                    // [관성 보정]
                    if (now - _p2LastTime < FastInputThreshold && _p2LastDir != 0)
                    {
                        if (diff == 2 || (dir != 0 && dir != _p2LastDir))
                        {
                            dir = _p2LastDir;
                        }
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

            // [추가] 3. 키보드 방향키 지원 (테스트/보조용)
            if (Input.GetKeyDown(KeyCode.LeftArrow)) dx = -1;
            if (Input.GetKeyDown(KeyCode.RightArrow)) dx = 1;
            if (Input.GetKeyDown(KeyCode.UpArrow)) dy = -1;
            if (Input.GetKeyDown(KeyCode.DownArrow)) dy = 1;

            if (dx != 0 || dy != 0)
            {
                if (!_hasMoved)
                {
                    _hasMoved = true;
                    if (textMain)
                        StartCoroutine(FadeTo(textMain, 0f, 1f, () => textMain.gameObject.SetActive(false)));
                }

                if (textSub && textSub.gameObject.activeSelf)
                {
                    if (_textFadeRoutine != null) StopCoroutine(_textFadeRoutine);

                    _textFadeRoutine = StartCoroutine(FadeTo(textSub, 0f, 1f, () =>
                    {
                        textSub.gameObject.SetActive(false);
                        _textFadeRoutine = null;
                        if (_defaultTextSub != null) UIManager.Instance.SetText(textSub.gameObject, _defaultTextSub);
                    }));
                }

                int nextX = _currentGridX + dx, nextY = _currentGridY + dy;
                if (nextX >= 0 && nextX < gridSize && nextY >= 0 && nextY < gridSize) SetFocusToGrid(nextX, nextY);
            }
        }

        /// <summary> 키 입력 헬퍼 (범위 내 눌린 키 인덱스 반환) </summary>
        private int GetPressedKeyIndex(int start, int end)
        {
            for (int i = start; i <= end; i++)
            {
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha0 + i))) return i;
            }
            return -1;
        }

        /// <summary> 텍스트 알파값 페이드 유틸리티 </summary>
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

        /// <summary> 포커스 이동 및 셀 마스킹 업데이트 </summary>
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

        /// <summary> 정답 위치 발견 체크 </summary>
        private void CheckQuestionFound(int x, int y)
        {
            if (_questionMap[x, y])
            {
                Vector2Int p = new Vector2Int(x, y);
                if (!_foundSpots.Contains(p))
                {
                    _foundSpots.Add(p);
                    if (_foundSpots.Count >= _totalQuestionCount && !_isStageCompleted)
                    {
                        _isStageCompleted = true;
                        StartCoroutine(ShowCompletionRoutine());
                    }
                }
            }
        }

        /// <summary> 완료 연출 시퀀스 </summary>
        private IEnumerator ShowCompletionRoutine()
        {
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
            
            // 아래 루프는 이미 p = t2 / 0.5f 로 정상 구현되어 있음
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

        /// <summary> 특정 셀의 페이드 효과 시작 </summary>
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

        /// <summary> 활성 셀 페이드 업데이트 및 텍스처 적용 </summary>
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

        /// <summary> 마스크 픽셀 값 조회 </summary>
        private float GetMaskPixelValue(int x, int y) =>
            _maskTexture != null ? _maskTexture.GetPixel(x, (gridSize - 1) - y).r : 0f;

        /// <summary> 마스크 픽셀 값 즉시 설정 </summary>
        private void UpdateMaskPixelInstant(int x, int y, float v, bool a = true)
        {
            if (_maskTexture != null)
            {
                _maskTexture.SetPixel(x, (gridSize - 1) - y, new Color(v, 0, 0, 0));
                if (a) _maskTexture.Apply();
            }
        }

        /// <summary> 페이지 퇴장 시 리소스 정리 및 시퀀스 중단 </summary>
        public override void OnExit()
        {
            // 부모의 종료 로직(리셋 시퀀스 중단 등)을 호출
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
        
        /// <summary> 모든 정답 위치의 마스크를 부드럽게 제거합니다. (스킵 연출용) </summary>
        private void RevealAllQuestions()
        {
            if (questionSpots == null) return;

            foreach (var spot in questionSpots)
            {
                // 범위 체크
                if (spot.x >= 0 && spot.x < gridSize && spot.y >= 0 && spot.y < gridSize)
                {
                    // StartCellFade를 호출하여 Update 루프에서 서서히 지워지도록(target=1.0f) 합니다.
                    StartCellFade(spot.x, spot.y, 1.0f);
                }
            }
            // 현재 플레이어 위치가 정답이 아니라면 다시 어둡게 복원 (Target 0.0f)
            if (!_questionMap[_currentGridX, _currentGridY])
            {
                StartCellFade(_currentGridX, _currentGridY, 0.0f);
            }
        }
    }
}