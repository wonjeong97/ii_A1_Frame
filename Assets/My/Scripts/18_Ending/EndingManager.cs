using System.IO;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using My.Scripts.Global;

public class EndingManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RawImage videoDisplay; 
    [SerializeField] private VideoPlayer videoPlayer; 

    [Header("Settings")]
    [SerializeField] private string videoFolderName = "Timelapse"; // 프로젝트 내 폴더명
    [SerializeField] private string videoFileName = "Final_Timelapse.mp4";

    private void Start()
    {
        StartCoroutine(PlayVideoRoutine());
    }

    private IEnumerator PlayVideoRoutine()
    {
        // 1. 경로 설정 (프로젝트 폴더/Timelapse)
        string dataPath = Application.dataPath;
        DirectoryInfo parentDir = Directory.GetParent(dataPath);
        string rootPath = (parentDir != null) ? parentDir.FullName : dataPath;
        
        string folderPath = Path.Combine(rootPath, videoFolderName);
        string filePath = Path.Combine(folderPath, videoFileName);

        // ------------------------------------------------------------------
        // [수정 1] 파일 생성 대기 (최대 30초로 연장)
        // ------------------------------------------------------------------
        float waitTime = 0f;
        float maxWaitTime = 30.0f; // 인코딩 시간을 고려하여 넉넉하게 설정

        while (!File.Exists(filePath) && waitTime < maxWaitTime)
        {
            Debug.Log($"[EndingManager] 영상 생성 대기 중... {waitTime:F1}s");
            yield return new WaitForSeconds(0.5f);
            waitTime += 0.5f;
        }

        if (!File.Exists(filePath))
        {
            Debug.LogError($"[EndingManager] 시간 초과! 영상을 찾을 수 없습니다: {filePath}");
            yield break;
        }

        // ------------------------------------------------------------------
        // [수정 2] 파일 쓰기 완료 대기 (파일 크기가 변하지 않을 때까지 대기)
        // ------------------------------------------------------------------
        FileInfo fileInfo = new FileInfo(filePath);
        long lastFileSize = -1;
        float stableTime = 0f;
        
        while (stableTime < 1.0f && waitTime < maxWaitTime) // 1초 동안 크기 변화가 없으면 완료로 간주
        {
            fileInfo.Refresh();
            long currentSize = fileInfo.Length;

            if (currentSize > 0 && currentSize == lastFileSize)
            {
                stableTime += 0.5f; // 크기가 같으면 안정화 시간 누적
            }
            else
            {
                stableTime = 0f; // 크기가 변했으면 다시 리셋
                lastFileSize = currentSize;
            }

            Debug.Log($"[EndingManager] 인코딩 진행 중... (크기: {currentSize} bytes)");
            yield return new WaitForSeconds(0.5f);
            waitTime += 0.5f;
        }

        // ------------------------------------------------------------------
        // [수정 3] 비디오 재생 및 NRE 방지
        // ------------------------------------------------------------------
        Debug.Log($"[EndingManager] 영상 로드 시작: {filePath}");

        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = "file://" + filePath; 
        videoPlayer.renderMode = VideoRenderMode.APIOnly; 
        videoPlayer.Prepare();

        // 준비 완료 대기
        while (!videoPlayer.isPrepared)
        {
            yield return null;
        }

        // [방어 코드] 텍스처가 정상적으로 생성되었는지 확인
        if (videoPlayer.texture != null)
        {
            videoDisplay.texture = videoPlayer.texture;
            videoPlayer.Play();
        }
        else
        {
            Debug.LogError("[EndingManager] 비디오 텍스처가 Null입니다. (코덱 문제 혹은 로딩 실패)");
        }
    }
    
    public void OnRestartBtnClick()
    {
        if(GameManager.Instance != null)
        {
            GameManager.Instance.ChangeScene(GameConstants.Scene.Title);
        }
    }
}