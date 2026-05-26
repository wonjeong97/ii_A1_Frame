using System;
using System.Globalization;
using System.IO;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging; 
using ZLogger; 
using VContainer; 

namespace My.Scripts.Utils
{
    /// <summary>
    /// 앱 실행 시 백그라운드에서 오래된 데이터(사진, 영상)를 자동 삭제하여 디스크 용량을 관리하는 유틸리티입니다.
    /// </summary>
    public class StorageCleaner : MonoBehaviour
    {
        private const int MaxKeepDays = 3; 
        
        private readonly static string[] RelativeTargetFolders = 
        {
            "Pictures",
            Path.Combine("Timelapse", "Realtime_Video"),
            Path.Combine("Timelapse", "Timelapse_Video"),
            Path.Combine("Timelapse", "Realtime_Source"),
            Path.Combine("Timelapse", "Timelapse_Source")
        };

        // --- 의존성 주입 (DI) 변수 ---
        private ILogger<StorageCleaner> _logger;

        [Inject]
        public void Construct(ILogger<StorageCleaner> logger)
        {
            _logger = logger;
        }

        private void Start()
        {
            string dataPath = Application.dataPath;
            
            RunCleanupAsync(dataPath, this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary>
        /// 유니티 메인 스레드 프리징을 막기 위해 스레드 풀에서 1회 디스크 정리를 수행합니다.
        /// </summary>
        private async UniTaskVoid RunCleanupAsync(string dataPath, CancellationToken ct)
        {
            try
            {
                // 완전히 격리된 백그라운드 스레드 풀 진입 (메인 스레드 점유율 0%)
                await UniTask.SwitchToThreadPool();
                ct.ThrowIfCancellationRequested();

                _logger.ZLogInformation($"[StorageCleaner] 앱 시작 시 백그라운드 자동 정리 시작 (보관 기준: {MaxKeepDays}일)");

                DirectoryInfo parentDir = Directory.GetParent(dataPath);
                string rootPath = parentDir != null ? parentDir.FullName : dataPath;

                DateTime thresholdDate = DateTime.Now.AddDays(-MaxKeepDays);

                foreach (string relativeFolder in RelativeTargetFolders)
                {
                    ct.ThrowIfCancellationRequested();
                    
                    string targetPath = Path.Combine(rootPath, relativeFolder);
                    CleanOldFolders(targetPath, thresholdDate, ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                _logger?.ZLogError(e, $"[StorageCleaner] 자동 정리 중 예외 발생: {e.Message}");
            }
            finally
            {
                // 백그라운드 작업 완료 후 안전하게 메인 스레드로 복귀
                await UniTask.SwitchToMainThread();
            }
        }

        private void CleanOldFolders(string targetPath, DateTime thresholdDate, CancellationToken ct)
        {
            if (!Directory.Exists(targetPath)) return;

            DirectoryInfo dirInfo = new DirectoryInfo(targetPath);
            
            // GetDirectories 배열 일괄 할당(GC) 대신 EnumerateDirectories 지연 평가 순회 사용  
            foreach (DirectoryInfo subDir in dirInfo.EnumerateDirectories())
            {
                // 디스크 스캔 도중 앱이 종료될 경우 즉시 중단하여 잔여 OS 파일 락(File Lock) 해제
                if (ct.IsCancellationRequested) return;
                
                TryDeleteIfOld(subDir, thresholdDate);
            }
        }
        
        private void TryDeleteIfOld(DirectoryInfo subDir, DateTime thresholdDate)
        {
            if (!DateTime.TryParseExact(subDir.Name, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime folderDate))
            {
                return;
            }

            // 명시적으로 Date 필드를 비교하여 시간/분 단위의 미세 오차 무시
            if (folderDate.Date >= thresholdDate.Date)
            {
                return;
            }

            DeleteDirectorySafe(subDir);
        }

        private void DeleteDirectorySafe(DirectoryInfo subDir)
        {
            try
            {
                subDir.Delete(true); 
                _logger.ZLogInformation($"[StorageCleaner] 오래된 폴더 삭제 완료: {subDir.FullName}");
            }
            catch (IOException)
            {
                _logger.ZLogWarning($"[StorageCleaner] 폴더가 사용 중임 (삭제 보류): {subDir.Name}");
            }
            catch (Exception e)
            {
                _logger.ZLogWarning($"[StorageCleaner] 폴더 삭제 실패 ({subDir.Name}): {e.Message}");
            }
        }
    }
}