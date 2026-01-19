using System.Collections;
using My.Scripts.Global;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wonjeong.Data;
using Wonjeong.Reporter;
using Wonjeong.UI;
using Wonjeong.Utils;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [SerializeField] private Reporter reporter;

    private float _currentInactivityTimer;
    private bool _isTransitioning;
    private float _inactivityLimit = 60f;
    private float _fadeTime = 1.0f;

    // 플레이어 태그 정보 (0: 없음, 1: Player1, 2: Player2)
    public int firstTaggedPlayer = 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (reporter == null)
        {
            reporter = FindObjectOfType<Reporter>();
        }
    }

    private void Start()
    {
        Cursor.visible = false;
        LoadSettings();

        if (reporter != null && reporter.show)
        {
            reporter.show = false;
        }
    }

    private void LoadSettings()
    {
        Settings settings = JsonLoader.Load<Settings>(GameConstants.Path.JsonSetting); // 상수 사용
        if (settings != null)
        {
            _inactivityLimit = settings.inactivityTime;
            _fadeTime = settings.fadeTime;
        }
        else
        {
            // 로드 실패 시 기본값 설정 (안전장치)
            _inactivityLimit = 60f;
            _fadeTime = 1.0f;
        }
    }

    private void Update()
    {
        // D키: 리포터(로그) 제어
        if (Input.GetKeyDown(KeyCode.D) && reporter != null)
        {
            reporter.showGameManagerControl = !reporter.showGameManagerControl;
            if (reporter.show) reporter.show = false;
        }
        // M키: 마우스 커서 토글
        else if (Input.GetKeyDown(KeyCode.M))
        {
            Cursor.visible = !Cursor.visible;
        }

        if (_isTransitioning) return;

        HandleInactivity();
    }

    private void HandleInactivity()
    {
        // 현재 씬이 이미 Title이라면 비활성 타이머를 돌리지 않음
        if (SceneManager.GetActiveScene().name == GameConstants.Scene.Title)
        {
            _currentInactivityTimer = 0f;
            return;
        }

        // 입력 감지 시 타이머 초기화
        if (Input.anyKey || Input.touchCount > 0)
        {
            _currentInactivityTimer = 0f;
        }
        else
        {
            _currentInactivityTimer += Time.deltaTime;
            if (_currentInactivityTimer >= _inactivityLimit)
            {
                ReturnToTitle();
            }
        }
    }
    
    public void ChangeScene(string sceneName)
    {
        if (_isTransitioning) return;
            
        _isTransitioning = true;
        Debug.Log($"[GameManager] Scene Transition Requested: {sceneName}");
        StartCoroutine(ChangeSceneRoutine(sceneName));
    }

    // 실제 페이드 및 씬 로드를 담당하는 코루틴
    private IEnumerator ChangeSceneRoutine(string sceneName)
    {
        // 1. FadeManager 체크
        if (FadeManager.Instance == null)
        {
            Debug.LogWarning("[GameManager] FadeManager instance not found. Loading immediately.");
            SceneManager.LoadScene(sceneName);
            _isTransitioning = false;
            yield break;
        }

        // 2. 페이드 아웃
        bool fadeDone = false;
        FadeManager.Instance.FadeOut(_fadeTime, () => { fadeDone = true; });
        while (!fadeDone) yield return null;

        // 3. 비동기 씬 로드
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        // 씬 로딩 완료 대기
        while (asyncLoad != null && !asyncLoad.isDone) yield return null;

        Debug.Log($"[GameManager] {sceneName} Loaded.");

        // 4. 페이드 인
        FadeManager.Instance.FadeIn(_fadeTime);
        _isTransitioning = false;
    }

    public void ReturnToTitle()
    {
        if (_isTransitioning) return;
            
        Debug.Log("[GameManager] Inactivity Detected: Returning to Title...");

        // 상태 초기화
        firstTaggedPlayer = 0; 
        _currentInactivityTimer = 0f;

        // 공통 메서드 호출
        ChangeScene(GameConstants.Scene.Title);
    }

    private IEnumerator ReturnToTitleRoutine()
    {
        if (FadeManager.Instance == null)
        {
            Debug.LogError("[GameManager] FadeManager instance not found. Force loading Title.");
            SceneManager.LoadScene(GameConstants.Scene.Title);
            _isTransitioning = false;
            yield break;
        }

        // 1. 페이드 아웃 시작
        bool fadeDone = false;
        FadeManager.Instance.FadeOut(_fadeTime, () => { fadeDone = true; });

        // 페이드 아웃 완료 대기
        while (!fadeDone) yield return null;

        // 2. 게임 상태 초기화 (중요)
        firstTaggedPlayer = 0; // 태그 정보 리셋
        _currentInactivityTimer = 0f;

        // 3. 타이틀 씬 비동기 로드
        // GameConstants.Scene.Title 사용 ("Title")
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(GameConstants.Scene.Title);

        // 씬 로딩 완료 대기
        while (asyncLoad != null && !asyncLoad.isDone) yield return null;

        Debug.Log("[GameManager] Title Scene Loaded.");

        // 4. 페이드 인 및 상태 복구
        FadeManager.Instance.FadeIn(_fadeTime);
        _isTransitioning = false;
    }
}