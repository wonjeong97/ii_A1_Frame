using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core.Data;
using Wonjeong.UI;

namespace My.Scripts.Core.Pages
{
    /// <summary> 전환 및 안내 텍스트 페이지 컨트롤러 </summary>
    public class Page_Transition : GamePage<TransitionPageData>
    {
        [Header("Mode Settings")]
        [SerializeField] private bool useButtonAnim; // 버튼 연출 사용 여부
        [SerializeField] private bool autoPass = true; // 자동 넘김 여부
        [SerializeField] private float autoPassDelay = 4.0f; // 자동 넘김 대기 시간
        
        [Tooltip("체크하면 종료 시 텍스트가 사라지지 않고 유지됩니다. (암전 전환 시 체크)")]
        [SerializeField] private bool keepContentOnFinish; // 종료 시 콘텐츠 유지 여부

        [Header("Common UI")] 
        [SerializeField] private Text descriptionText; // 설명 텍스트
        [SerializeField] private CanvasGroup contentGroup; // 콘텐츠 그룹

        [Header("Button Mode UI")] 
        [SerializeField] private RectTransform buttonRect; // 버튼 UI

        [Header("Intro Mode UI (Optional)")]
        [SerializeField] private Text playerAName; // 플레이어 A 이름
        [SerializeField] private Text playerBName; // 플레이어 B 이름
        [SerializeField] private CanvasGroup namesGroup; // 이름 그룹

        private bool _isCompleted; // 완료 여부
        private float _enterTime; // 진입 시간

        /// <summary> 데이터 설정 (텍스트 적용) </summary>
        protected override void SetupData(TransitionPageData data)
        {
            if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);

            // 플레이어 이름 데이터 적용 (옵션)
            if (playerAName) UIManager.Instance.SetText(playerAName.gameObject, data.playerAName);
            if (playerBName) UIManager.Instance.SetText(playerBName.gameObject, data.playerBName);
        }

        /// <summary> 페이지 진입 (초기화 및 시퀀스 시작) </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;
            _enterTime = Time.time;

            // UI 초기화
            if (contentGroup) contentGroup.alpha = 0f;
            if (namesGroup) namesGroup.alpha = 0f;
            if (buttonRect) buttonRect.localScale = Vector3.one;

            StartCoroutine(SequenceRoutine());
        }

        /// <summary> 입력 감지 (강제 스킵) </summary>
        private void Update()
        {
            if (_isCompleted) return;
            if (Time.time - _enterTime < 1f) return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 스페이스바 입력 시 강제 완료
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _isCompleted = true;
                CompleteStep();
            }
#endif
        }

        /// <summary> 연출 시퀀스 (등장 -> 대기/애니메이션 -> 퇴장) </summary>
        private IEnumerator SequenceRoutine()
        {
            // 1. 콘텐츠 등장
            yield return StartCoroutine(FadeGroup(contentGroup, 0f, 1f, 1.0f));
            if (namesGroup) yield return StartCoroutine(FadeGroup(namesGroup, 0f, 1f, 0.5f));

            // 2. 모드별 동작
            if (useButtonAnim && buttonRect)
            {
                // 버튼 애니메이션 재생
                yield return StartCoroutine(ButtonAnim());
            }
            else if (autoPass)
            {
                // 자동 넘김 대기
                yield return new WaitForSeconds(autoPassDelay);
            }

            // 3. 종료 처리
            if (!_isCompleted && autoPass) 
            {
                // 유지 옵션이 꺼져있을 때만 페이드 아웃
                if (!keepContentOnFinish)
                {
                    if (!useButtonAnim && descriptionText)
                    {
                        yield return StartCoroutine(FadeGroup(contentGroup, 1f, 0f, 0.5f));
                        if (namesGroup) yield return StartCoroutine(FadeGroup(namesGroup, 1f, 0f, 0.5f));
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

        /// <summary> 버튼 스케일 애니메이션 </summary>
        private IEnumerator ButtonAnim()
        {
            for (int i = 0; i < 2; i++)
            {
                if (_isCompleted) yield break;
                
                // 줄어듬
                float t = 0;
                while (t < 0.5f)
                {
                    t += Time.deltaTime;
                    buttonRect.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.9f, Mathf.SmoothStep(0, 1, t / 0.5f));
                    yield return null;
                }

                // 커짐
                t = 0;
                while (t < 0.5f)
                {
                    t += Time.deltaTime;
                    buttonRect.localScale = Vector3.Lerp(Vector3.one * 0.9f, Vector3.one, Mathf.SmoothStep(0, 1, t / 0.5f));
                    yield return null;
                }
            }
        }
    }
}