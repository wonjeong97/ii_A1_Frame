using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class ApiTestRunner : MonoBehaviour
{
    [Header("API Settings")]
    [SerializeField] private string apiUrl = "http://192.168.0.252:8500/api/checkRoomState.cfm?code=a1";

    private void Start()
    {
        // 게임 시작 시 자동으로 한 번 호출합니다.
        StartCoroutine(CheckRoomStateRoutine());
    }

    // 인스펙터 창에서 컴포넌트 우클릭 -> "Test API Now"를 누르면 게임 플레이 중이 아니어도 테스트 가능합니다.
    [ContextMenu("Test API Now")]
    public void TestApi()
    {
        StartCoroutine(CheckRoomStateRoutine());
    }

    private IEnumerator CheckRoomStateRoutine()
    {
        Debug.Log($"[ApiTestRunner] API 요청 시작: {apiUrl}");

        using (UnityWebRequest webRequest = UnityWebRequest.Get(apiUrl))
        {
            // 무한 대기 방지를 위한 타임아웃 10초 설정
            webRequest.timeout = 10; 

            // 서버에 요청 보내고 대기
            yield return webRequest.SendWebRequest();

            // 네트워크 에러 또는 HTTP 에러 확인
            if (webRequest.result == UnityWebRequest.Result.ConnectionError || 
                webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"[ApiTestRunner] 통신 실패: {webRequest.error}");
            }
            else
            {
                // 성공적으로 받아온 데이터를 문자열로 추출
                string responseText = webRequest.downloadHandler.text;
                Debug.Log($"[ApiTestRunner] 통신 성공! 수신된 응답 데이터:\n{responseText}");
            }
        }
    }
}