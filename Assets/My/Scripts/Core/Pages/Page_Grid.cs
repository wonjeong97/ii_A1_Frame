using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core.Data;
using My.Scripts.Global;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;
using UnityEngine.SceneManagement;

namespace My.Scripts.Core.Pages
{
    /// <summary> 그리드 탐색 게임 페이지 컨트롤러 </summary>
    public class Page_Grid : GamePage<GridPageData>
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

        [Header("Popup References")]
        [SerializeField] private CanvasGroup popupCanvasGroup; // 리셋 팝업 그룹
        [SerializeField] private Text popupText; // 팝업 메시지 텍스트

        [Header("Popup Settings")]
        [SerializeField] private float warningDuration = 3f; // 1차 팝업 경고 시간
        [SerializeField] private float resetPopupDuration = 3f; // 2차 리셋 안내 시간

        [Header("Settings")] 
        [SerializeField] private List<Vector2Int> questionSpots; // 정답 좌표 리스트
        
        private readonly int gridSize = 10; // 그리드 크기 (10x10)
        private readonly float cellFadeDuration = 0.25f; // 셀 페이드 시간

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
        private Coroutine _textBlinkRoutine; // 텍스트 깜빡임 코루틴

        // --- 리셋 로직 변수 ---
        private string _msgWarning; // 경고 메시지
        private string _msgReset; // 리셋 메시지
        
        private float _inactivityThreshold = 20f; // 2차 리셋 시작 시간 (Settings)
        private float _countdownDuration = 10f;   // 리셋 카운트다운 (계산됨)
        
        private float _currentIdleTime = 0f; // 현재 대기 시간
        private const float BlinkThreshold = 10f; // 1차 경고(깜빡임) 시작 시간
        private bool _is1stWarningDone = false; // 1차 경고 완료 여부

        private bool _isResetSequenceActive = false; // 리셋 시퀀스 활성 여부
        private Coroutine _resetSequenceRoutine; // 리셋 시퀀스 코루틴
        private Coroutine _popupFadeRoutine; // 팝업 페이드 코루틴

        // 셀 페이드 정보 관리용 클래스
        private class CellFadeInfo { public int x, y; public float startVal, targetVal, timer; }
        private readonly List<CellFadeInfo> _activeFades = new List<CellFadeInfo>(); 

        /// <summary>  초기화: Settings에서 시간 설정을 로드. </summary>
        private void Start()
        {
            var settings = JsonLoader.Load<Settings>(GameConstants.Path.JsonSetting);
            if (settings != null)
            {
                _inactivityThreshold = settings.warningTime;
                
                float calculatedDuration = settings.resetTime - settings.warningTime - warningDuration;
                if (calculatedDuration > 0) _countdownDuration = calculatedDuration;
            }
        }

        /// <summary>  데이터 설정: 텍스트 및 메시지 데이터를 적용. </summary>
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

            if (!string.IsNullOrEmpty(data.warningMessage)) _msgWarning = data.warningMessage;
            if (!string.IsNullOrEmpty(data.resetMessage)) _msgReset = data.resetMessage;
        }

        /// <summary>  페이지 진입: 게임 상태 및 리소스 초기화. </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            _hasMoved = false;
            _isInputBlocked = false;
            
            ResetIdleState(true); // 대기 상태 즉시 초기화
            
            if (!InitializeGame()) return;
            
            // 중앙 위치에서 시작
            int startX = Mathf.Min(4, gridSize - 1);
            int startY = Mathf.Min(4, gridSize - 1);
            SetFocusToGrid(startX, startY, true);
        }

        /// <summary>  게임 초기화: 텍스처 생성, 변수 리셋, 정답 맵 설정. </summary>
        private bool InitializeGame()
        {
            if (!imageBlack || !imageFocus) return false;
            
            _blackRect = imageBlack.rectTransform;
            _cellWidth = _blackRect.rect.width / gridSize;
            _cellHeight = _blackRect.rect.height / gridSize;
            _foundSpots.Clear();
            _isStageCompleted = false;

            if (completionCanvasGroups != null) foreach (var cg in completionCanvasGroups) { if (cg) { cg.alpha = 0f; cg.gameObject.SetActive(true); } }
            if (textCanvasGroups != null) foreach (var cg in textCanvasGroups) if (cg) cg.alpha = 1f;

            // 정답 맵 구성
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

            // 텍스처 리소스 재생성
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

        /// <summary>  매 프레임 업데이트: 입력 감지 및 대기 시간 체크. </summary>
        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Input.GetKeyDown(KeyCode.Space) && !_isStageCompleted)
            {
                _isStageCompleted = true;
                StartCoroutine(ShowCompletionRoutine());
            }
#endif
            
            // 1. 입력 감지
            if (Input.anyKey || Input.touchCount > 0)
            {
                // 입력 시 부드럽게 대기 상태 초기화 (팝업 페이드 아웃 등)
                ResetIdleState(false); 
                HandleMovement();
            }
            else
            {
                // 2. 비활성 시간 누적
                if (!_isInputBlocked && !_isStageCompleted && !_isResetSequenceActive)
                {
                    _currentIdleTime += Time.deltaTime;
                    
                    // [Case 1] 1차 경고: 10초 경과 시 텍스트 깜빡임 (2회)
                    if (_currentIdleTime >= BlinkThreshold && _currentIdleTime < _inactivityThreshold)
                    {
                        if (!_is1stWarningDone && _textBlinkRoutine == null)
                        {
                            if (_warningText != null && textSub != null)
                                UIManager.Instance.SetText(textSub.gameObject, _warningText);
                            
                            _textBlinkRoutine = StartCoroutine(BlinkRoutine());
                        }
                    }
                    // [Case 2] 2차 경고: 설정된 시간(20초) 경과 시 리셋 팝업 시퀀스
                    else if (_currentIdleTime >= _inactivityThreshold)
                    {
                        StartResetSequence();
                    }
                }
            }

            UpdateCellFades();
        }

        /// <summary>  대기 타이머 및 경고 상태를 초기화 </summary>
        /// <param name="immediate">true면 즉시 종료, false면 페이드 아웃 처리</param>
        private void ResetIdleState(bool immediate)
        {
            _currentIdleTime = 0f;
            _is1stWarningDone = false;

            // 깜빡임 코루틴 중단
            if (_textBlinkRoutine != null)
            {
                StopCoroutine(_textBlinkRoutine);
                _textBlinkRoutine = null;
                
                // 이동 중이라면 텍스트를 바로 끄지 않고 HandleMovement에서 처리
                if (immediate && textSub) textSub.gameObject.SetActive(false);
                else if (textSub) textSub.gameObject.SetActive(true);
            }

            // 리셋 시퀀스 중단
            if (_isResetSequenceActive)
            {
                StopResetSequence(immediate);
                if (!immediate) Debug.Log("[Page_Grid] 입력 감지: 리셋 취소");
            }
            // 시퀀스는 끝났지만 팝업이 떠 있는 경우 (페이드 아웃 중 등)
            else if (popupCanvasGroup && popupCanvasGroup.gameObject.activeSelf)
            {
                if (immediate) StopResetSequence(true);
            }
        }

        // --- 1차 경고 로직 ---

        /// <summary>  1차 경고: 텍스트를 2회 깜빡이고 사라지게 한다. </summary>
        private IEnumerator BlinkRoutine()
        {
            if (textSub)
            {
                textSub.gameObject.SetActive(true);
                Color c = textSub.color; c.a = 1f; textSub.color = c;
            }

            // 2회 깜빡임
            for (int i = 0; i < 2; i++)
            {
                yield return StartCoroutine(FadeTo(textSub, 0f, 0.5f)); 
                yield return StartCoroutine(FadeTo(textSub, 1f, 0.5f));
                
                yield return CoroutineData.GetWaitForSeconds(0.2f);
            }

            // 완료 후 페이드 아웃
            yield return StartCoroutine(FadeTo(textSub, 0f, 1.0f));
            if (textSub) textSub.gameObject.SetActive(false);

            _is1stWarningDone = true; 
            _textBlinkRoutine = null;
        }

        // --- 2차 리셋 로직 ---

        /// <summary>  리셋 시퀀스(팝업 표시)를 시작. </summary>
        private void StartResetSequence()
        {
            if (_isResetSequenceActive) return;
            _isResetSequenceActive = true;
            
            // 기존 깜빡임 로직 정리
            if (_textBlinkRoutine != null)
            {
                StopCoroutine(_textBlinkRoutine);
                _textBlinkRoutine = null;
            }
            if (textSub) textSub.gameObject.SetActive(false);

            _resetSequenceRoutine = StartCoroutine(ResetProcessRoutine());
        }

        /// <summary> 리셋 시퀀스를 중단하고 팝업을 닫음. </summary>
        /// <param name="immediate">true면 즉시 닫음, false면 1초 페이드 아웃</param>
        private void StopResetSequence(bool immediate = true)
        {
            _isResetSequenceActive = false;
            _currentIdleTime = 0f;
            
            if (_resetSequenceRoutine != null) StopCoroutine(_resetSequenceRoutine);
            
            if (popupCanvasGroup)
            {
                if (immediate)
                {
                    popupCanvasGroup.alpha = 0f;
                    popupCanvasGroup.gameObject.SetActive(false);
                }
                else
                {
                    // 활성화된 팝업은 페이드 아웃
                    if (popupCanvasGroup.gameObject.activeSelf)
                    {
                        if (_popupFadeRoutine != null) StopCoroutine(_popupFadeRoutine);
                        _popupFadeRoutine = StartCoroutine(FadePopupOut());
                    }
                }
            }
        }

        /// <summary>  팝업 페이드 아웃 코루틴 </summary>
        private IEnumerator FadePopupOut()
        {
            yield return StartCoroutine(FadeGroup(popupCanvasGroup, popupCanvasGroup.alpha, 0f, 1.0f));
            popupCanvasGroup.gameObject.SetActive(false);
        }

        /// <summary>  리셋 프로세스: 경고 팝업 -> 카운트 -> 초기화 안내 -> 타이틀 이동 </summary>
        private IEnumerator ResetProcessRoutine()
        {
            Debug.Log("[Page_Grid] 리셋 시퀀스 시작");

            // [1단계] 경고 팝업
            ShowPopup(_msgWarning);
            yield return CoroutineData.GetWaitForSeconds(warningDuration); 

            // [2단계] 카운트다운
            float timer = _countdownDuration;
            while (timer > 0f)
            {
                timer -= 1.0f;
                yield return CoroutineData.GetWaitForSeconds(1.0f);
            }

            // [3단계] 초기화 안내 (텍스트 변경, 창 유지)
            ShowPopup(_msgReset);
            yield return CoroutineData.GetWaitForSeconds(resetPopupDuration);

            // [4단계] 타이틀로 이동
            if (GameManager.Instance != null) GameManager.Instance.ReturnToTitle();
            else SceneManager.LoadScene(GameConstants.Scene.Title);
        }

        /// <summary>  팝업 표시: 이미 켜져있으면 텍스트만 교체, 꺼져있으면 1초 페이드 인. </summary>
        private void ShowPopup(string message)
        {
            if (!popupCanvasGroup) return;
            if (popupText) popupText.text = message;

            if (!popupCanvasGroup.gameObject.activeSelf)
            {
                popupCanvasGroup.alpha = 0f;
                popupCanvasGroup.gameObject.SetActive(true);
            }

            if (_popupFadeRoutine != null) StopCoroutine(_popupFadeRoutine);
            _popupFadeRoutine = StartCoroutine(FadeGroup(popupCanvasGroup, popupCanvasGroup.alpha, 1f, 1.0f));
        }

        /// <summary>  캔버스 그룹 페이드 유틸리티 </summary>
        private IEnumerator FadeGroup(CanvasGroup cg, float start, float end, float duration)
        {
            float t = 0f;
            cg.alpha = start;
            while (t < duration)
            {
                t += Time.deltaTime;
                cg.alpha = Mathf.Lerp(start, end, t / duration);
                yield return null;
            }
            cg.alpha = end;
        }

        // --- 게임 로직 (이동) ---

        /// <summary>  방향키 입력에 따른 그리드 이동 처리 </summary>
        private void HandleMovement()
        {
            if (!imageFocus || _isInputBlocked || _isStageCompleted) return;

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

                // 이동 시 켜져 있는 보조 텍스트 부드럽게 끄기
                if (textSub != null && textSub.gameObject.activeSelf)
                {
                    if (_textFadeRoutine != null) StopCoroutine(_textFadeRoutine);
                    
                    _textFadeRoutine = StartCoroutine(FadeTo(textSub, 0f, 1.0f, () =>
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

        /// <summary>  텍스트 알파값 페이드 유틸리티 </summary>
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

        /// <summary> 그리드 포커스 이동 및 마스킹 갱신 </summary>
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

        /// <summary>  정답 위치 발견 체크 </summary>
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

        /// <summary> 스테이지 완료 연출 </summary>
        private IEnumerator ShowCompletionRoutine()
        {
            if (completionCanvasGroups != null)
            {
                float t = 0f;
                while (t < 1.0f)
                {
                    t += Time.deltaTime;
                    foreach (var cg in completionCanvasGroups)
                        if (cg) cg.alpha = Mathf.Clamp01(t);
                    yield return null;
                }
                foreach (var cg in completionCanvasGroups) if (cg) cg.alpha = 1f;
            }

            yield return CoroutineData.GetWaitForSeconds(2.0f);

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
                        if (cg) cg.alpha = Mathf.Lerp(1f, 0f, p);
                yield return null;
            }

            CompleteStep(); 
        }

        /// <summary>  특정 셀의 페이드 효과 시작 </summary>
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

        /// <summary>  활성 셀 페이드 업데이트 및 텍스처 적용 </summary>
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

        /// <summary>  페이지 퇴장 시 리소스 정리 및 시퀀스 중단 </summary>
        public override void OnExit()
        {
            StopResetSequence(true);
            base.OnExit();
            CleanupResources();
        }

        private void OnDestroy()
        {
            CleanupResources();
        }

        /// <summary>  생성된 텍스처 및 재질 메모리 해제 </summary>
        private void CleanupResources()
        {
            if (_maskTexture) Destroy(_maskTexture);
            if (_eraserMaterial) Destroy(_eraserMaterial);
            if (_gridMaterial) Destroy(_gridMaterial);
        }
    }
}