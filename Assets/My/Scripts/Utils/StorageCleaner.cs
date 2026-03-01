using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace My.Scripts.Utils
{
    /// <summary>
    /// 앱 실행 시 백그라운드에서 동작하여 오래된 사진 및 영상 폴더를 정리하는 유틸리티 클래스
    /// </summary>
    public static class StorageCleaner
    {
        private const int MaxKeepDays = 7; // 보관할 최대 기간 (일)

        // 프로그램이 실행되고 첫 씬이 로드된 직후 1회 실행.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RunCleanupOnStartup()
        {
            try
            {
                UniTask.RunOnThreadPool(() =>
                {
                    Debug.Log($"[StorageCleaner] 백그라운드 자동 정리 시작 (보관 기준: {MaxKeepDays}일)");

                    // 루트 경로 가져오기
                    string dataPath = Application.dataPath;
                    DirectoryInfo parentDir = Directory.GetParent(dataPath);
                    string rootPath = parentDir != null ? parentDir.FullName : dataPath;

                    // 검사할 타겟 최상위 폴더 경로들
                    string[] targetFolders = {
                        Path.Combine(rootPath, "Pictures"),
                        Path.Combine(rootPath, "Timelapse", "Realtime_Video"),
                        Path.Combine(rootPath, "Timelapse", "Timelapse_Video"),
                        Path.Combine(rootPath, "Timelapse", "Realtime_Source"),
                        Path.Combine(rootPath, "Timelapse", "Timelapse_Source")
                    };

                    // 기준 날짜 계산
                    DateTime thresholdDate = DateTime.Now.Date.AddDays(-MaxKeepDays);

                    foreach (string folderPath in targetFolders)
                    {
                        CleanOldFolders(folderPath, thresholdDate);
                    }

                    Debug.Log("[StorageCleaner] 백그라운드 자동 정리 완료");
                
                }).Forget(); // .Forget()을 붙여 비동기 작업이 백그라운드에서 안전하게 독립적으로 실행되도록 명시
            }
            catch (Exception e)
            {
                Debug.LogError($"[StorageCleaner] 정리 작업 중 예외 발생: {e.Message}");
            }
        }

        private static void CleanOldFolders(string targetPath, DateTime thresholdDate)
        {
            if (!Directory.Exists(targetPath)) return;

            DirectoryInfo dirInfo = new DirectoryInfo(targetPath);
            DirectoryInfo[] subDirs = dirInfo.GetDirectories(); // 하위 폴더들 (예: "2026-03-02")

            foreach (DirectoryInfo subDir in subDirs)
            {
                // 폴더 이름이 "yyyy-MM-dd" 형식의 날짜인지 파싱 시도
                if (DateTime.TryParseExact(subDir.Name, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime folderDate))
                {
                    // 폴더의 날짜가 기준 날짜보다 이전(과거)이라면 삭제
                    if (folderDate.Date < thresholdDate)
                    {
                        try
                        {
                            subDir.Delete(true); // true: 내부 파일까지 모두 강제 삭제
                            Debug.Log($"[StorageCleaner] 오래된 폴더 삭제 완료: {subDir.FullName}");
                        }
                        catch (Exception e)
                        {
                            // 파일이 열려있거나 권한이 없는 등 예외 발생 시 에러 로그만 남김 (게임 멈춤 방지)
                            Debug.LogWarning($"[StorageCleaner] 폴더 삭제 실패 ({subDir.FullName}): {e.Message}");
                        }
                    }
                }
            }
        }
    }
}