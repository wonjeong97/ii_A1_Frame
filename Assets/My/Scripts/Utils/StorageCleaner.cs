using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace My.Scripts.Utils
{
    /// <summary>
    /// 앱 실행 시 백그라운드에서 오래된 데이터(사진, 영상)를 자동 삭제하여 디스크 용량을 관리하는 유틸리티입니다.
    /// </summary>
    public static class StorageCleaner
    {
        private const int MaxKeepDays = 3; 

        /// <summary> 
        /// 첫 씬 로드 직후 자동으로 실행됩니다. 
        /// 메인 스레드 프리징을 방지하기 위해 UniTask.RunOnThreadPool을 사용하여 백그라운드에서 작업을 수행합니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RunCleanupOnStartup()
        {
            try
            {
                UniTask.RunOnThreadPool(() =>
                {
                    Debug.Log($"[StorageCleaner] 백그라운드 자동 정리 시작 (보관 기준: {MaxKeepDays}일)");

                    string dataPath = Application.dataPath;
                    DirectoryInfo parentDir = Directory.GetParent(dataPath);
                    string rootPath = parentDir != null ? parentDir.FullName : dataPath;

                    // 스캔 대상: 합성 사진, 리얼타임 비디오, 타임랩스 비디오 폴더
                    string[] targetFolders = {
                        Path.Combine(rootPath, "Pictures"),
                        Path.Combine(rootPath, "Timelapse", "Realtime_Video"),
                        Path.Combine(rootPath, "Timelapse", "Timelapse_Video"),
                        Path.Combine(rootPath, "Timelapse", "Realtime_Source"),
                        Path.Combine(rootPath, "Timelapse", "Timelapse_Source")
                    };

                    DateTime thresholdDate = DateTime.Now.AddDays(-MaxKeepDays);

                    foreach (string folder in targetFolders)
                    {
                        CleanOldFolders(folder, thresholdDate);
                    }
                }).Forget();
            }
            catch (Exception e)
            {
                Debug.LogError($"[StorageCleaner] 정리 작업 중 예외 발생: {e.Message}");
            }
        }

        /// <summary> 
        /// 대상 폴더 내 하위 폴더들을 전수 조사하여 날짜 기준에 미달하는 데이터를 삭제합니다. 
        /// </summary>
        private static void CleanOldFolders(string targetPath, DateTime thresholdDate)
        {
            if (!Directory.Exists(targetPath)) return;

            DirectoryInfo dirInfo = new DirectoryInfo(targetPath);
            // "yyyy-MM-dd" 형식으로 생성된 하위 날짜 폴더들을 가져옵니다.
            DirectoryInfo[] subDirs = dirInfo.GetDirectories(); 

            foreach (DirectoryInfo subDir in subDirs)
            {
                // 폴더 명칭이 날짜 형식이 아닌 경우(예: 임시 폴더) 무시하여 데이터 손실 방지
                if (DateTime.TryParseExact(subDir.Name, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime folderDate))
                {
                    // 기준 날짜보다 이전(과거)인 폴더만 선별 삭제
                    if (folderDate.Date < thresholdDate)
                    {
                        try
                        {
                            // 내부 파일 및 하위 디렉토리를 포함하여 강제 삭제
                            subDir.Delete(true); 
                            Debug.Log($"[StorageCleaner] 오래된 폴더 삭제 완료: {subDir.FullName}");
                        }
                        catch (IOException)
                        {
                            // 파일이 다른 프로세스(FFmpeg 등)에 의해 사용 중일 때 발생하는 충돌 무시
                            Debug.LogWarning($"[StorageCleaner] 폴더가 사용 중임: {subDir.Name}");
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[StorageCleaner] 폴더 삭제 실패 ({subDir.Name}): {e.Message}");
                        }
                    }
                }
            }
        }
    }
}