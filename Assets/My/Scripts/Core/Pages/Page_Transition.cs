using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core.Data;
using Wonjeong.UI;

namespace My.Scripts.Core.Pages
{
    public class Page_Transition : GamePage
    {
        [Header("Mode Settings")]
        [SerializeField] private bool useButtonAnim; // 버튼 연출 사용 여부 (Page 4)
        [SerializeField] private bool autoPass = true; // 시간 지나면 자동 넘김
        [SerializeField] private float autoPassDelay = 4.0f; // 대기 시간
        
        [Tooltip("체크하면 종료 시 텍스트가 사라지지 않고 유지됩니다. (암전 전환 시 체크)")]
        [SerializeField] private bool keepContentOnFinish = false;

        [Header("Common UI")] 
        [SerializeField] private Text descriptionText;
        [SerializeField] private CanvasGroup contentGroup;

        [Header("Button Mode UI")] 
        [SerializeField] private RectTransform buttonRect;

        [Header("Intro Mode UI (Optional)")]
        [SerializeField] private Text playerAName;
        [SerializeField] private Text playerBName;
        [SerializeField] private CanvasGroup namesGroup;

        private bool _isCompleted;
        private float _enterTime;

        public override void SetupData(object data)
        {
            var pageData = data as TransitionPageData;
            if (pageData == null) return;

            if (descriptionText) UIManager.Instance.SetText(descriptionText.gameObject, pageData.descriptionText);

            // 옵션: 플레이어 이름 데이터가 있다면 설정
            if (playerAName) UIManager.Instance.SetText(playerAName.gameObject, pageData.playerAName);
            if (playerBName) UIManager.Instance.SetText(playerBName.gameObject, pageData.playerBName);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            _isCompleted = false;
            _enterTime = Time.time;

            if (contentGroup) contentGroup.alpha = 0f;
            if (namesGroup) namesGroup.alpha = 0f;
            if (buttonRect) buttonRect.localScale = Vector3.one;

            StartCoroutine(SequenceRoutine());
        }

        private void Update()
        {
            if (_isCompleted) return;
            if (Time.time - _enterTime < 1f) return;
//#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 스페이스바 입력 시 강제 완료
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _isCompleted = true;
                CompleteStep();
            }
//#endif
        }

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
                // 자동 넘김 모드 대기
                yield return new WaitForSeconds(autoPassDelay);
            }

            // 3. 종료 처리
            if (!_isCompleted && autoPass) 
            {
                // [수정됨] keepContentOnFinish가 꺼져있을 때만 페이드아웃 실행
                // 켜져있으면(True) 페이드아웃 없이 그대로 유지한 채 완료 신호를 보냄
                if (!keepContentOnFinish)
                {
                    if (!useButtonAnim && descriptionText)
                    {
                        yield return StartCoroutine(FadeGroup(contentGroup, 1f, 0f, 0.5f));
                        // namesGroup도 같이 끄고 싶다면 추가 가능
                        if (namesGroup) yield return StartCoroutine(FadeGroup(namesGroup, 1f, 0f, 0.5f));
                    }
                }
                
                CompleteStep();
            }
        }

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

        private IEnumerator ButtonAnim()
        {
            for (int i = 0; i < 2; i++)
            {
                if (_isCompleted) yield break;
                float t = 0;
                while (t < 0.5f)
                {
                    t += Time.deltaTime;
                    buttonRect.localScale =
                        Vector3.Lerp(Vector3.one, Vector3.one * 0.9f, Mathf.SmoothStep(0, 1, t / 0.5f));
                    yield return null;
                }

                t = 0;
                while (t < 0.5f)
                {
                    t += Time.deltaTime;
                    buttonRect.localScale =
                        Vector3.Lerp(Vector3.one * 0.9f, Vector3.one, Mathf.SmoothStep(0, 1, t / 0.5f));
                    yield return null;
                }
            }
        }
    }
}