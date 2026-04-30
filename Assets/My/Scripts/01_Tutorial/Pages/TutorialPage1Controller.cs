using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using My.Scripts.Core;
using My.Scripts.Core.Pages;
using My.Scripts.Global;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Wonjeong.Data;
using Wonjeong.UI;
using Wonjeong.Utils;
using My.Scripts._01_Tutorial;

namespace My.Scripts._01_Tutorial.Pages
{
    /// <summary>
    /// 튜토리얼 1페이지 텍스트 데이터 구조체.
    /// </summary>
    [Serializable]
    public class TutorialPage1Data
    {
        public TextSetting descriptionText;
        public string warningMessage;
        public string resetMessage;
    }

    /// <summary>
    /// 튜토리얼 진입 시 서버 폴링을 통해 실제 유저 상태를 동기화하는 컨트롤러.
    /// </summary>
    public class TutorialPage1Controller : PopupGamePage<TutorialPage1Data>
    {
        [Header("Page 1 UI")]
        [SerializeField] private Text descriptionText;
        [Header("API Manager")]
        [SerializeField] private APIManager apiManager;
        [Header("Polling Settings")]
        [SerializeField] private float pollInterval = 3.0f;

        private readonly float fadeTime = 1f;
        private Coroutine _pollCoroutine;
        private TutorialPage1Data _data;

        /// <summary>
        /// 초기화 단계에서 페이드인 연출을 위해 텍스트 투명도를 선제적으로 낮춤.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            if (!descriptionText)
            {
                Debug.LogWarning("descriptionText 누락됨.");
            }
            else
            {
                Color c = descriptionText.color;
                c.a = 0f;
                descriptionText.color = c;
            }

            if (!apiManager)
            {
                Debug.LogWarning("apiManager 누락됨.");
            }
        }

        /// <summary>
        /// 전달받은 데이터를 기반으로 UI 텍스트 컴포넌트의 값을 갱신함.
        /// </summary>
        /// <param name="data">적용할 텍스트 및 경고 메시지 데이터</param>
        protected override void SetupData(TutorialPage1Data data)
        {
            _data = data;
            if (descriptionText)
            {
                UIManager.Instance.SetText(descriptionText.gameObject, data.descriptionText);
            }
            SetupPopupMessage(data.warningMessage, data.resetMessage);
        }

        public override object ExtractCurrentData()
        {
            return new TutorialPage1Data
            {
                descriptionText = TutorialPageUtils.BuildTextSetting(descriptionText, _data?.descriptionText),
                warningMessage  = _data?.warningMessage ?? string.Empty,
                resetMessage    = _data?.resetMessage   ?? string.Empty,
            };
        }

        /// <summary>
        /// 페이지 활성화 시 무인 타이머를 초기화하고 네트워크 상태 폴링을 시작함.
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();
            ResetIdleState(true);

            if (descriptionText)
            {
                StartCoroutine(FadeInTextRoutine());
            }

            // 중복 실행 방지를 위해 기존 코루틴 정리
            if (_pollCoroutine != null)
            {
                StopCoroutine(_pollCoroutine);
            }
            _pollCoroutine = StartCoroutine(PollRoomStateRoutine());
        }

        /// <summary>
        /// 페이지 비활성화 시 불필요한 서버 자원 낭비를 막기 위해 폴링을 중단함.
        /// </summary>
        public override void OnExit()
        {
            if (_pollCoroutine != null)
            {
                StopCoroutine(_pollCoroutine);
                _pollCoroutine = null;
            }
            base.OnExit();
        }

        /// <summary>
        /// 입력 장치 이벤트를 감지하여 대기 상태 해제 및 다음 스텝 진입을 제어함.
        /// </summary>
        private void Update()
        {
            if (Input.anyKey || Input.touchCount > 0)
            {
                ResetIdleState(false);
                if (Input.GetKeyDown(KeyCode.Return))
                {
                    CompleteStep();
                }
            }
            else
            {
                UpdateInactivity();
            }
        }

        /// <summary>
        /// 서버 상태를 주기적으로 확인하여 체험 중인 유저의 세션 정보를 파싱함.
        /// </summary>
        private IEnumerator PollRoomStateRoutine()
        {
#if UNITY_EDITOR
            yield break;
#endif

            float emptyUserStartTime = -1f;
            
            while (true)
            {
                if (!GameManager.Instance || GameManager.Instance.ApiConfig == null)
                {
                    yield return CoroutineData.GetWaitForSeconds(pollInterval);
                    continue;
                }

                string checkUrl = $"{GameManager.Instance.ApiConfig.CheckRoomStateUrl}?code={GameConstants.Module.Code.ToLower()}";
                string userUrl = $"{GameManager.Instance.ApiConfig.GetCurrentRoomUserUrl}?code={GameConstants.Module.Code.ToLower()}";

                bool isRoomEmpty = false;
                bool isNetworkError = false;

                using (UnityWebRequest stateReq = UnityWebRequest.Get(checkUrl))
                {
                    stateReq.timeout = 10;
                    yield return stateReq.SendWebRequest();

                    if (stateReq.result == UnityWebRequest.Result.Success)
                    {
                        if (stateReq.downloadHandler.text.IndexOf(GameConstants.Api.StatusEmpty, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            isRoomEmpty = true;
                        }
                    }
                    else
                    {
                        isNetworkError = true;
                    }
                }

                if (isRoomEmpty)
                {
                    yield return CoroutineData.GetWaitForSeconds(1.0f);
                    if (GameManager.Instance)
                    {
                        GameManager.Instance.ReturnToTitle();
                    }
                    yield break;
                }

                if (!isNetworkError)
                {
                    bool isUserEmpty = false;

                    using (UnityWebRequest userReq = UnityWebRequest.Get(userUrl))
                    {
                        userReq.timeout = 10;
                        yield return userReq.SendWebRequest();

                        if (userReq.result == UnityWebRequest.Result.Success)
                        {
                            string rawText = userReq.downloadHandler.text;
                            if (rawText.IndexOf(GameConstants.Api.StatusEmpty, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                isUserEmpty = true;
                            }
                            else if (rawText.Contains(","))
                            {
                                emptyUserStartTime = -1f;

                                string[] parts = rawText.Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);
                                if (parts.Length >= 1)
                                {
                                    string uidLeft = parts[0].Trim();
                                    if (parts.Length >= 2 && SessionManager.Instance)
                                    {
                                        SessionManager.Instance.PlayerAUid = uidLeft;
                                        SessionManager.Instance.PlayerBUid = parts[1].Trim();
                                    }

                                    if (apiManager)
                                    {
                                        bool fetchSuccess = false;
                                        bool fetchFaulted = false;

                                        // 비동기 통신 타임아웃 처리를 통해 무한 대기 현상 방지
                                        yield return apiManager.FetchDataAsync(uidLeft)
                                                               .Timeout(TimeSpan.FromSeconds(25))
                                                               .ToCoroutine(
                                                                    r => fetchSuccess = r,
                                                                    ex => { fetchFaulted = true; }
                                                                );

                                        if (fetchFaulted || !fetchSuccess || !SessionManager.Instance || SessionManager.Instance.CurrentUserId == 0)
                                        {
                                            yield return CoroutineData.GetWaitForSeconds(pollInterval);
                                            continue;
                                        }
                                    }
                                    CompleteStep();
                                    yield break;
                                }
                            }
                        }
                        else
                        {
                            isNetworkError = true;
                        }
                    }

                    if (isUserEmpty)
                    {
                        if (emptyUserStartTime < 0f)
                        {
                            emptyUserStartTime = Time.time;
                        }

                        // ex) 현재시간(20f) - 빈유저시작시간(5f) >= 15f -> true (일시적 통신 지연이 아닌 실제 이탈로 간주)
                        if (Time.time - emptyUserStartTime >= 15f)
                        {
                            if (GameManager.Instance)
                            {
                                GameManager.Instance.ReturnToTitle();
                            }
                            yield break;
                        }
                    }
                }

                yield return CoroutineData.GetWaitForSeconds(pollInterval);
            }
        }

        /// <summary>
        /// 튜토리얼 진입 시각적 단서를 주기 위해 알파값을 서서히 올림.
        /// </summary>
        private IEnumerator FadeInTextRoutine()
        {
            float timer = 0f;
            while (timer < fadeTime)
            {
                timer += Time.deltaTime;
                if (descriptionText)
                {
                    Color c = descriptionText.color;
                    c.a = Mathf.Clamp01(timer / fadeTime);
                    descriptionText.color = c;
                }
                yield return null;
            }
        }
    }
}