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
    [SerializeField] private string videoFolderName = "Timelapse"; 
    [SerializeField] private string videoFileName = "Final_Timelapse.mp4";

    private void Start()
    {
        StartCoroutine(PlayVideoRoutine());
    }

    private IEnumerator PlayVideoRoutine()
    {
        // 1. 경로 설정
        string dataPath = Application.dataPath;
        DirectoryInfo parentDir = Directory.GetParent(dataPath);
        string rootPath = (parentDir != null) ? parentDir.FullName : dataPath;
        
        string folderPath = Path.Combine(rootPath, videoFolderName);
        string filePath = Path.Combine(folderPath, videoFileName);

        // 타임랩스 레코더의 상태를 보고 대기
        if (TimeLapseRecorder.Instance != null)
        {
            // (1) 프로세스 작업 중이라면 무한 대기 (타임아웃 없이 확실하게 기다림)
            while (TimeLapseRecorder.Instance.IsProcessing)
            {
                Debug.Log("[EndingManager] FFmpeg 변환 중... 잠시만 기다려주세요.");
                yield return new WaitForSeconds(0.5f);
            }

            // (2) 작업이 끝났는데 실패했다면? -> 재생 포기하고 에러 처리
            if (!TimeLapseRecorder.Instance.IsConversionSuccessful)
            {
                Debug.LogError($"[EndingManager] FFmpeg 변환 실패 (ExitCode: {TimeLapseRecorder.Instance.LastExitCode}). 영상을 재생할 수 없습니다.");
                yield break; 
            }
        }
        else
        {
            Debug.LogWarning("[EndingManager] TimeLapseRecorder 인스턴스를 찾을 수 없습니다. 파일 유무만 확인합니다.");
        }

        // 프로세스는 성공했다고 하는데, 파일 시스템 딜레이로 없을 수도 있으니 짧게 체크
        float waitTime = 0f;
        while (!File.Exists(filePath) && waitTime < 5.0f)
        {
            Debug.Log($"[EndingManager] 파일 시스템 동기화 대기 중...");
            yield return new WaitForSeconds(0.2f);
            waitTime += 0.2f;
        }

        if (File.Exists(filePath))
        {
            Debug.Log($"[EndingManager] 영상 로드 시작: {filePath}");

            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = "file://" + filePath; 
            videoPlayer.renderMode = VideoRenderMode.APIOnly; 
            videoPlayer.Prepare();

            while (!videoPlayer.isPrepared)
            {
                yield return null;
            }

            if (videoPlayer.texture != null)
            {
                videoDisplay.texture = videoPlayer.texture;
                videoPlayer.Play();
            }
        }
        else
        {
            Debug.LogError($"[EndingManager] 영상 파일을 찾을 수 없습니다: {filePath}");
        }
    }
}