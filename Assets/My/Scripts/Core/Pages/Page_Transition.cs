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
        [SerializeField] private bool autoPass = true; // 자동 넘김 여부
        [SerializeField] private float autoPassDelay = 4.0f; // 자동 넘김 대기 시간
        
        [Tooltip("체크하면 종료 시 텍스트가 사라지지 않고 유지됩니다. (암전 전환 시 체크)")]
        [SerializeField] private bool keepContentOnFinish; // 종료 시 콘텐츠 유지 여부

        [Header("Common UI")] 
        [SerializeField] private Text descriptionText; // 설명 텍스트
        [SerializeField] private CanvasGroup contentGroup; // 콘텐츠 그룹

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

                // Space 키 입력 시 다음 단계로 진행 (수동 넘김)
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

        /// <summary>  연출 시퀀스 (등장 -> 대기 -> 퇴장) </summary>
        private IEnumerator SequenceRoutine()
        {
            // 1. 콘텐츠 등장
            yield return StartCoroutine(FadeGroup(contentGroup, 0f, 1f, 1f));
            if (namesGroup)
            {
                yield return StartCoroutine(FadeGroup(namesGroup, 0f, 1f, 1f));
            }
            
            // 2. 대기
            if (autoPass)
            {
                yield return CoroutineData.GetWaitForSeconds(autoPassDelay);
            }

            // 3. 종료 처리
            if (!_isCompleted && autoPass) 
            {
                // 유지 옵션이 꺼져있을 때만 페이드 아웃
                if (!keepContentOnFinish)
                {
                    if (descriptionText)
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
    }
}