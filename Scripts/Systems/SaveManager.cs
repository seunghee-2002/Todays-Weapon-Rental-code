using System.IO;
using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>파일 로드 결과 상태. Corrupted는 본 파일/백업 모두 복구 실패를 의미한다.</summary>
    public enum LoadDataStatus
    {
        Success,
        Missing,
        RestoredFromBackup,
        Corrupted
    }

    public class SaveManager
    {
        // 게임 진행 데이터 (게임오버 시 삭제)
        private static string GameDataPath => Path.Combine(Application.persistentDataPath, "gamedata.json");

        // 유산 데이터 (항상 유지)
        private static string PlayerDataPath => Path.Combine(Application.persistentDataPath, "legacy.json");

        #region 게임 데이터 저장/로드

        /// <summary>
        /// 게임 진행 데이터 저장. 실패 시 false 반환 (호출부는 클라우드 업로드 등을 보류해야 한다)
        /// </summary>
        public static bool SaveGame(GameData data)
        {
            if (data == null)
            {
                Log.Error("SaveManager: Cannot save - GameData is null");
                return false;
            }

            string json = JsonUtility.ToJson(data, true);
            bool success = WriteAllTextAtomic(GameDataPath, json);

            if (success)
                Log.Info($"SaveManager: Game saved (Day {data.currentDay}, Gold {data.gold})");

            return success;
        }

        /// <summary>
        /// 게임 진행 데이터 로드.
        /// 본 파일 손상 시 .bak 백업으로 복구를 시도하고, 그마저 실패하면 null 반환.
        /// (손상 파일은 .corrupt-타임스탬프로 보존)
        /// </summary>
        public static GameData LoadGame()
        {
            if (!File.Exists(GameDataPath))
            {
                Log.Warn("SaveManager: No game save file found");
                return null;
            }

            if (TryLoadJson(GameDataPath, out GameData data))
            {
                Log.Info($"SaveManager: Game loaded (Day {data.currentDay}, Gold {data.gold})");
                return data;
            }

            BackupCorruptFile(GameDataPath);

            if (TryLoadJson(GameDataPath + ".bak", out GameData backup))
            {
                SaveGame(backup);
                Log.Warn($"SaveManager: gamedata restored from backup (Day {backup.currentDay})");
                return backup;
            }

            Log.Error("SaveManager: gamedata corrupted and no valid backup");
            return null;
        }

        /// <summary>
        /// 게임 진행 데이터 존재 여부
        /// </summary>
        public static bool HasGameData()
        {
            return File.Exists(GameDataPath);
        }

        /// <summary>
        /// 게임 진행 데이터 삭제 (백업/임시 파일 포함 - 게임오버한 회차가 백업으로 부활하지 않도록)
        /// </summary>
        public static void DeleteGameData()
        {
            DeleteFileSafe(GameDataPath);
            DeleteFileSafe(GameDataPath + ".bak");
            DeleteFileSafe(GameDataPath + ".tmp");
            Log.Info("SaveManager: Game data deleted");
        }

        #endregion

        #region 유산 데이터 저장/로드

        /// <summary>
        /// 유산 데이터 저장. 실패 시 false 반환
        /// </summary>
        public static bool SavePlayer(PlayerData data)
        {
            if (data == null)
            {
                Log.Error("SaveManager: Cannot save - PlayerData is null");
                return false;
            }

            string json = JsonUtility.ToJson(data, true);
            bool success = WriteAllTextAtomic(PlayerDataPath, json);

            if (success)
                Log.Info($"SaveManager: Legacy saved ({data.legacyPoints} points)");

            return success;
        }

        /// <summary>
        /// 유산 데이터 로드 (상태 포함).
        /// Corrupted면 data는 null이다 - 호출부(LegacyManager)는 빈 데이터로 조용히 확정하지 말고
        /// 클라우드 복구/사용자 안내를 거쳐야 한다
        /// </summary>
        public static (LoadDataStatus status, PlayerData data) LoadPlayerSafe()
        {
            if (!File.Exists(PlayerDataPath))
            {
                Log.Info("SaveManager: No legacy file found, creating new");
                return (LoadDataStatus.Missing, new PlayerData());
            }

            if (TryLoadJson(PlayerDataPath, out PlayerData data))
            {
                Log.Info($"SaveManager: Legacy loaded ({data.legacyPoints} points)");
                return (LoadDataStatus.Success, data);
            }

            BackupCorruptFile(PlayerDataPath);

            if (TryLoadJson(PlayerDataPath + ".bak", out PlayerData backup))
            {
                SavePlayer(backup);
                Log.Warn($"SaveManager: legacy restored from backup ({backup.legacyPoints} points)");
                return (LoadDataStatus.RestoredFromBackup, backup);
            }

            Log.Error("SaveManager: legacy corrupted and no valid backup");
            return (LoadDataStatus.Corrupted, null);
        }

        /// <summary>
        /// 유산 데이터 로드 (기존 호출부 호환용).
        /// 손상 여부까지 구분해야 하는 곳(LegacyManager)은 LoadPlayerSafe를 사용할 것
        /// </summary>
        public static PlayerData LoadPlayer()
        {
            var (_, data) = LoadPlayerSafe();
            return data ?? new PlayerData();
        }

        /// <summary>
        /// 유산 데이터 삭제 (완전 초기화용 - 백업/임시 파일 포함)
        /// </summary>
        public static void DeletePlayerData()
        {
            DeleteFileSafe(PlayerDataPath);
            DeleteFileSafe(PlayerDataPath + ".bak");
            DeleteFileSafe(PlayerDataPath + ".tmp");
            Log.Info("SaveManager: Legacy data deleted");
        }

        #endregion

        #region 완전 초기화

        /// <summary>
        /// 모든 데이터 삭제 (게임 데이터 + 유산 데이터)
        /// </summary>
        public static void DeleteAllData()
        {
            DeleteGameData();
            DeletePlayerData();
            Log.Info("SaveManager: All data deleted (complete reset)");
        }

        #endregion

        #region 내부 메서드

        /// <summary>
        /// 원자적 파일 쓰기: 임시 파일에 쓴 뒤 교체하고 직전본을 .bak으로 보존한다.
        /// 강제 종료/크래시가 겹쳐도 반쯤 쓰인 JSON이 본 파일을 덮지 않는다
        /// </summary>
        private static bool WriteAllTextAtomic(string path, string contents)
        {
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string tempPath = path + ".tmp";
                string backupPath = path + ".bak";

                File.WriteAllText(tempPath, contents);

                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(tempPath, path, backupPath);
                    }
                    catch (System.Exception)
                    {
                        // 일부 플랫폼에서 File.Replace가 실패할 수 있어 폴백:
                        // 원본을 .bak으로 먼저 보존한 뒤 삭제+이동
                        File.Copy(path, backupPath, true);
                        File.Delete(path);
                        File.Move(tempPath, path);
                    }
                }
                else
                {
                    File.Move(tempPath, path);
                }

                return true;
            }
            catch (System.Exception e)
            {
                Log.Error($"SaveManager: atomic write failed ({path}) - {e.Message}");
                return false;
            }
        }

        private static bool TryLoadJson<T>(string path, out T data) where T : class
        {
            data = null;
            try
            {
                if (!File.Exists(path)) return false;

                string json = File.ReadAllText(path);
                if (string.IsNullOrEmpty(json)) return false;

                data = JsonUtility.FromJson<T>(json);
                return data != null;
            }
            catch (System.Exception e)
            {
                Log.Error($"SaveManager: load failed ({path}) - {e.Message}");
                data = null;
                return false;
            }
        }

        /// <summary>손상 파일을 .corrupt-타임스탬프로 보존한다 (수동 복구/분석용)</summary>
        private static void BackupCorruptFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                string corruptPath = path + ".corrupt-" + System.DateTime.Now.ToString("yyyyMMddHHmmss");
                File.Copy(path, corruptPath, true);
                Log.Error($"SaveManager: corrupted file backed up - {corruptPath}");
            }
            catch (System.Exception e)
            {
                Log.Warn($"SaveManager: failed to backup corrupt file ({path}) - {e.Message}");
            }
        }

        private static void DeleteFileSafe(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (System.Exception e)
            {
                Log.Error($"SaveManager: Failed to delete file ({path}) - {e.Message}");
            }
        }

        #endregion
    }
}
