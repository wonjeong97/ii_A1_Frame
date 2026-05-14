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
            DirectoryInfo[] subDirs = dirInfo.GetDirectories(); 

            foreach (DirectoryInfo subDir in subDirs)
            {
                TryDeleteIfOld(subDir, thresholdDate);
            }
        }
        
        /// <summary>
        /// 폴더명을 날짜로 파싱하여 보관 기한이 지난 폴더인지 식별함.
        /// </summary>
        private static void TryDeleteIfOld(DirectoryInfo subDir, DateTime thresholdDate)
        {
            if (!DateTime.TryParseExact(subDir.Name, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime folderDate))
            {
                return;
            }

            if (folderDate.Date >= thresholdDate)
            {
                return;
            }

            DeleteDirectorySafe(subDir);
        }

        /// <summary>
        /// 파일 잠금(Lock) 등 런타임 환경에서 발생할 수 있는 삭제 실패 예외를 로깅 및 캡슐화함.
        /// </summary>
        private static void DeleteDirectorySafe(DirectoryInfo subDir)
        {
            try
            {
                subDir.Delete(true); 
                Debug.Log($"[StorageCleaner] 오래된 폴더 삭제 완료: {subDir.FullName}");
            }
            catch (IOException)
            {
                Debug.LogWarning($"[StorageCleaner] 폴더가 사용 중임: {subDir.Name}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[StorageCleaner] 폴더 삭제 실패 ({subDir.Name}): {e.Message}");
            }
        }
    }
}