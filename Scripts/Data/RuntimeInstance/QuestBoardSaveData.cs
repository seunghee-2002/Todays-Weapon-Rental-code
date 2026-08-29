// Scripts/Data/SaveData/QuestBoardSaveData.cs
using System;
using System.Collections.Generic;

namespace TodaysWeaponRental
{
    [Serializable]
    public class QuestBoardSaveData
    {
        public int generatedDay;
        public List<string> poolDungeonIDs = new List<string>();      // 플레이어에게 노출되는 전체 후보 풀
        public List<string> selectedDungeonIDs = new List<string>();  // 플레이어가 확정한 던전
        public List<string> highlightedDungeonIDs = new List<string>();
        public int refreshCount;
        public bool isConfirmed;
        // 수색 단계에서 사용자가 닫기 버튼을 직접 눌렀는지 — 당일 재오픈 방지 (자동/수동 모두)
        public bool scoutPhaseClosedByUser;
        // key: dungeonStaticID / value: (int)ArmorType — 당일 롤링된 확정 방어구 타입
        public SerializableDictionary<string, int> dailyDungeonArmorTypes = new SerializableDictionary<string, int>();
        // key: dungeonStaticID / value: 확정된 수색 비용 — 의뢰판 확정 시 결정
        public SerializableDictionary<string, int> scoutCosts = new();
        // key: dungeonStaticID / value: 확정된 수색 소요 시간(분) — 의뢰판 확정 시 결정
        public SerializableDictionary<string, int> scoutDurations = new();
    }
}