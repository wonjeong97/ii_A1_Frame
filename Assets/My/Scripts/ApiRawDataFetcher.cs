using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class ApiRawDataFetcher : MonoBehaviour
{
    [Header("API Settings")]
    [SerializeField] private string apiUrl = "http://192.168.0.252:8500/api/getCurrentRoomUser.cfm?code=a1";

    private void Start()
    {
        // 게임 시작 시 자동으로 한 번 호출합니다.
        StartCoroutine(FetchRawDataRoutine());
    }

    [ContextMenu("Fetch API Now")]
    public void TestApi()
    {
        StartCoroutine(FetchRawDataRoutine());
    }

    private IEnumerator FetchRawDataRoutine()
    {
        Debug.Log($"[ApiFetcher] API 요청 시작: {apiUrl}");

        using (UnityWebRequest webRequest = UnityWebRequest.Get(apiUrl))
        {
            webRequest.timeout = 10; // 무한 대기 방지

            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.ConnectionError || 
                webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"[ApiFetcher] 통신 실패: {webRequest.error}");
            }
            else
            {
                // 파싱 없이 서버에서 내려온 문자열을 그대로 출력합니다.
                string rawResponse = webRequest.downloadHandler.text;
                Debug.Log($"[ApiFetcher] 통신 성공! 아래의 원본 데이터를 복사해서 알려주세요:\n\n{rawResponse}\n");
            }
        }
    }
}