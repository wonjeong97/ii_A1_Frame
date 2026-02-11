using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core.Data;
using My.Scripts.Global;
using My.Scripts.Timelapse;
using UnityEngine.SceneManagement;
using Wonjeong.UI;
using Wonjeong.Utils;

namespace My.Scripts.Core.Pages
{
    /// <summary> 전환 및 안내 텍스트 페이지 컨트롤러 </summary>
    public class Page_Transition : PopupGamePage<TransitionPageData>
    {
        [Header("Mode Settings")]
        [SerializeField] private bool useButtonAnim; // 버튼 연출 사용 여부 (Page 4)
        [SerializeField] private bool autoPass = true; // 자동 넘김 여부
        [SerializeField] private float autoPassDelay = 4.0f; // 자동 넘김 대기 시간
        
        [Tooltip("체크하면 종료 시 텍스트가 사라지지 않고 유지됩니다. (암전 전환 시 체크)")]
        [SerializeField] private bool keepContentOnFinish; // 종료 시 콘텐츠 유지 여부

        [Header("Common UI")] 
        [SerializeField] private Text descriptionText; // 설명 텍스트
        [SerializeField] private CanvasGroup contentGroup; // 콘텐츠 그룹

        [Header("Button Mode UI")] 
        [SerializeField] private RectTransform buttonRect; // 버튼 UI (Page 4)

        [Header("Intro Mode UI (Optional)")]
        [SerializeField] private Text playerAName; // 플레이어 A 이름
        [SerializeField] private Text playerBName; // 플레이어 B 이름
        [SerializeField] private CanvasGroup namesGroup; // 이름 그룹

        private bool _isCompleted; // 완료 여부
        private float _enterTime; // 진입 시간

        
        /// <summary> 데이터 설정: 텍스트 및 팝업 메시지 적용 </summary>
        protected override void SetupData(TransitionPageData data)
        {
            if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);

            // 플레이어 이름 데이터 적용 (옵션)
            if (playerAName) UIManager.Instance.SetText(playerAName.gameObject, data.playerAName);
            if (playerBName) UIManager.Instance.SetText(playerBName.gameObject, data.playerBName);

            // 팝업 메시지 설정 (부모 메서드)
            SetupPopupMessage(data.warningMessage, data.resetMessage);
        }

        /// <summary>  페이지 진입: 상태 초기화 및 연출 시작 </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;
            _enterTime = Time.time;

            // 팝업 타이머 초기화
            ResetIdleState(true);

            // UI 초기화
            if (contentGroup) contentGroup.alpha = 0f;
            if (namesGroup) namesGroup.alpha = 0f;
            if (buttonRect) buttonRect.localScale = Vector3.one;
            
            if (gameObject.name == "Page6" && SceneManager.GetActiveScene().name == GameConstants.Scene.PlayQ15)
            {
                if (TimeLapseRecorder.Instance && !TimeLapseRecorder.Instance.IsProcessing)
                {
                    Debug.Log($"[{gameObject.name}] OnEnter: 리얼타임 영상 변환 시작");
                    TimeLapseRecorder.Instance.ConvertToRealtimeVideo();
                }
            }
            StartCoroutine(SequenceRoutine());
        }

        /// <summary>  매 프레임 업데이트: 입력 감지 및 비활성 체크 </summary>
        private void Update()
        {
            if (_isCompleted) return;
            if (Time.time - _enterTime < 1.5f) return; // 진입 직후 오입력 방지

            // 1. 입력 감지 (Space 키 또는 기타 입력)
            if (Input.anyKey || Input.touchCount > 0)
            {
                // 입력 시 리셋 타이머 초기화 (부드럽게)
                ResetIdleState(false);

                // Space 키 입력 시 다음 단계로 진행 (Page 4 버튼 기능)
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    _isCompleted = true;
                    CompleteStep();
                }
            }
            else
            {
                // 2. 비활성 시간 누적 (부모 메서드)
                UpdateInactivity();
            }
        }

        /// <summary>  연출 시퀀스 (등장 -> 대기/애니메이션 -> 퇴장) </summary>
        private IEnumerator SequenceRoutine()
        {
            // 1. 콘텐츠 등장
            yield return StartCoroutine(FadeGroup(contentGroup, 0f, 1f, 1f));
            if (namesGroup)
            {
                yield return StartCoroutine(FadeGroup(namesGroup, 0f, 1f, 1f));
            }
            
            // 2. 모드별 동작
            if (useButtonAnim && buttonRect)
            {
                // 버튼 애니메이션 재생 (입력 대기 상태)
                yield return StartCoroutine(ButtonAnim());
            }
            else if (autoPass)
            {
                // 자동 넘김 대기
                yield return CoroutineData.GetWaitForSeconds(autoPassDelay);
            }

            // 3. 종료 처리 (자동 넘김인 경우)
            if (!_isCompleted && autoPass) 
            {
                // 유지 옵션이 꺼져있을 때만 페이드 아웃
                if (!keepContentOnFinish)
                {
                    if (!useButtonAnim && descriptionText)
                    {
                        yield return StartCoroutine(FadeGroup(contentGroup, 1f, 0f, 0.5f));
                        if (namesGroup)
                        {
                            yield return StartCoroutine(FadeGroup(namesGroup, 1f, 0f, 0.5f));
                        }
                    }
                }
                
                CompleteStep();
            }
        }

        /// <summary> 캔버스 그룹 페이드 코루틴 </summary>
        private IEnumerator FadeGroup(CanvasGroup cg, float start, float end, float duration)
        {
            if (!cg) yield break;
            float t = 0;
            cg.alpha = start;
            while (t < duration)
            {
                t += Time.deltaTime;
                cg.alpha = Mathf.Lerp(start, end, t / duration);
                yield return null;
            }

            cg.alpha = end;
        }
        
       /// <summary> 버튼 연출 애니메이션 </summary>
        private IEnumerator ButtonAnim()
        {
            // 알파값 조절을 위해 Image 컴포넌트 가져오기
            Image btnImage = buttonRect  ? buttonRect.GetComponent<Image>() : null;

            // _isCompleted가 될 때까지 전체 시퀀스 무한 반복
            while (!_isCompleted)
            {
                // 깜빡임 애니메이션 (투명도 조절)
                // 2회 반복 재생
                for (int i = 0; i < 2; i++)
                {
                    if (_isCompleted) yield break;

                    // Fade Out (1 -> 0.1)
                    float t = 0;
                    while (t < 0.5f)
                    {
                        if (_isCompleted) yield break;
                        t += Time.deltaTime;
                        
                        if (btnImage)
                        {
                            Color c = btnImage.color;
                            c.a = Mathf.Lerp(1f, 0.1f, t / 0.5f);
                            btnImage.color = c;
                        }
                        yield return null;
                    }

                    // Fade In (0.3 -> 1)
                    t = 0;
                    while (t < 0.5f)
                    {
                        if (_isCompleted) yield break;
                        t += Time.deltaTime;
                        
                        if (btnImage)
                        {
                            Color c = btnImage.color;
                            c.a = Mathf.Lerp(0.1f, 1f, t / 0.5f);
                            btnImage.color = c;
                        }
                        yield return null;
                    }
                }

                // [기존] 스케일 애니메이션 (주석 처리됨)
                /*
                for (int i = 0; i < 2; i++)
                {
                    if (_isCompleted) yield break;
                    
                    float t = 0;
                    while (t < 0.5f)
                    {
                        if (_isCompleted) yield break;
                        t += Time.deltaTime;
                        buttonRect.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.9f, Mathf.SmoothStep(0, 1, t / 0.5f));
                        yield return null;
                    }
                    
                    t = 0;
                    while (t < 0.5f)
                    {
                        if (_isCompleted) yield break;
                        t += Time.deltaTime;
                        buttonRect.localScale = Vector3.Lerp(Vector3.one * 0.9f, Vector3.one, Mathf.SmoothStep(0, 1, t / 0.5f));
                        yield return null;
                    }
                }
                */

                // 2회 재생 후 1초 대기
                if (!_isCompleted)
                {
                    yield return CoroutineData.GetWaitForSeconds(1.0f);
                }
            }
        }
    }
}