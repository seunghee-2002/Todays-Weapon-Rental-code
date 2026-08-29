using System;
using System.Collections.Generic;
using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// UpgradeKey→int 딕셔너리 전용 직렬화 클래스.
    /// JsonUtility의 제네릭 클래스 enum List 역직렬화 버그를 피하기 위해
    /// keys를 List&lt;int&gt;로 저장한다.
    /// </summary>
    [Serializable]
    public class UpgradeDictionary : ISerializationCallbackReceiver
    {
        [SerializeField] private List<int> keys = new List<int>();
        [SerializeField] private List<int> values = new List<int>();

        private Dictionary<UpgradeKey, int> dict = new Dictionary<UpgradeKey, int>();

        public void OnBeforeSerialize()
        {
            keys.Clear();
            values.Clear();
            foreach (var kvp in dict)
            {
                keys.Add((int)kvp.Key);
                values.Add(kvp.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            dict.Clear();
            int count = Mathf.Min(keys.Count, values.Count);
            for (int i = 0; i < count; i++)
            {
                var key = (UpgradeKey)keys[i];
                if (!dict.ContainsKey(key))
                    dict[key] = values[i];
            }
        }

        public bool ContainsKey(UpgradeKey key) => dict.ContainsKey(key);

        public int this[UpgradeKey key]
        {
            get => dict.TryGetValue(key, out int v) ? v : 0;
            set => dict[key] = value;
        }

        public Dictionary<UpgradeKey, int>.Enumerator GetEnumerator() => dict.GetEnumerator();
    }

    /// <summary>
    /// 유산 데이터 (게임오버 후에도 유지되는 데이터)
    /// legacy.json에 단독 저장. gameData와 동기화하지 않음.
    /// </summary>
    [Serializable]
    public class PlayerData
    {
        // legacy 독립 저장 시각 (Unix 초). GameData.lastSavedAt과 별도로 legacy만의 최신본 비교에 사용
        public long legacyLastSavedAt = 0;

        public int legacyPoints = 0;
        public UpgradeDictionary permanentUpgrades = new UpgradeDictionary();
        public int legacyUpgradePurchaseCount = 0; // 유산 업그레이드 누적 구매 횟수 (Analytics G20 purchase_order용)
        public bool hasEverGameOver = false; // 한 번이라도 게임오버를 했는지 (초회차 판정)
        public bool hasShownLegacyUnlockPopup = false; // 강화 해금 안내 팝업을 이미 표시했는지 (1회성)
        public bool hasCompletedTutorial = false; // 전체 튜토리얼(2~12단계)을 한 번이라도 완주했는지 (완주 후 회차는 1단계만 진행)

        // 사운드 설정 (회차와 무관한 전역 설정 — 게임오버에도 유지)
        public float bgmVolume = 1f;
        public float sfxVolume = 1f;
        public bool isBGMMuted = false;
        public bool isSFXMuted = false;

        // BGM 변형 선택 (baseKey -> "RANDOM" 또는 정확한 변형 키). 항목이 없으면 그룹 기본값을 따른다.
        public SerializableDictionary<string, string> bgmVariantSelections = new SerializableDictionary<string, string>();

        // UI/편의 설정 (영구 — 회차와 무관하게 유지)
        public bool isNPCDialogueEnabled = true;
        public bool isAdventureSkipEnabled = false;
        public float replaySpeed = 1f;

        // 닉네임 로컬 미러 (주 저장소는 Cloud Save, 오프라인 폴백용 로컬 보관)
        public string playerNickname = "";

        // 도감 (영구 누적 — 게임오버/새 회차와 무관하게 유지)
        public SerializableDictionary<string, DungeonStatData> dungeonStats = new SerializableDictionary<string, DungeonStatData>();
        public SerializableDictionary<string, int> weaponDiscoveryProgress = new SerializableDictionary<string, int>();

        public PlayerData()
        {
            InitializeDefaultUpgrades();
        }

        /// <summary>
        /// 존재하지 않는 키만 0으로 초기화 (기존 값 보존)
        /// </summary>
        private void InitializeDefaultUpgrades()
        {
            // A. 시작 조건
            if (!permanentUpgrades.ContainsKey(UpgradeKey.StartingGold))          permanentUpgrades[UpgradeKey.StartingGold] = 0;
            if (!permanentUpgrades.ContainsKey(UpgradeKey.WeaponRare))            permanentUpgrades[UpgradeKey.WeaponRare] = 0;
            if (!permanentUpgrades.ContainsKey(UpgradeKey.WeaponEpic))            permanentUpgrades[UpgradeKey.WeaponEpic] = 0;
            if (!permanentUpgrades.ContainsKey(UpgradeKey.StartingInsight))       permanentUpgrades[UpgradeKey.StartingInsight] = 0;
            if (!permanentUpgrades.ContainsKey(UpgradeKey.InventorySlots))        permanentUpgrades[UpgradeKey.InventorySlots] = 0;

            // B. 무기 & 대장간
            if (!permanentUpgrades.ContainsKey(UpgradeKey.EnforceRate))           permanentUpgrades[UpgradeKey.EnforceRate] = 0;
            if (!permanentUpgrades.ContainsKey(UpgradeKey.EvolveRate))            permanentUpgrades[UpgradeKey.EvolveRate] = 0;
            if (!permanentUpgrades.ContainsKey(UpgradeKey.EnforceCost))           permanentUpgrades[UpgradeKey.EnforceCost] = 0;
            if (!permanentUpgrades.ContainsKey(UpgradeKey.EvolveCost))            permanentUpgrades[UpgradeKey.EvolveCost] = 0;
            if (!permanentUpgrades.ContainsKey(UpgradeKey.MaterialReduction))     permanentUpgrades[UpgradeKey.MaterialReduction] = 0;
            if (!permanentUpgrades.ContainsKey(UpgradeKey.RerollCount))           permanentUpgrades[UpgradeKey.RerollCount] = 0;
            if (!permanentUpgrades.ContainsKey(UpgradeKey.RerollCost))            permanentUpgrades[UpgradeKey.RerollCost] = 0;
            if (!permanentUpgrades.ContainsKey(UpgradeKey.DisassembleBonus))      permanentUpgrades[UpgradeKey.DisassembleBonus] = 0;

            // C. 수입 & 모험
            if (!permanentUpgrades.ContainsKey(UpgradeKey.CommissionRate))        permanentUpgrades[UpgradeKey.CommissionRate] = 0;
            if (!permanentUpgrades.ContainsKey(UpgradeKey.TipRate))               permanentUpgrades[UpgradeKey.TipRate] = 0;
            if (!permanentUpgrades.ContainsKey(UpgradeKey.GreatSuccessRate))      permanentUpgrades[UpgradeKey.GreatSuccessRate] = 0;
            if (!permanentUpgrades.ContainsKey(UpgradeKey.AdventureSpeed))        permanentUpgrades[UpgradeKey.AdventureSpeed] = 0;

            // D. 방문자 & 상점
            if (!permanentUpgrades.ContainsKey(UpgradeKey.RegularAdventurer))     permanentUpgrades[UpgradeKey.RegularAdventurer] = 0;
            if (!permanentUpgrades.ContainsKey(UpgradeKey.MorningEventGuarantee)) permanentUpgrades[UpgradeKey.MorningEventGuarantee] = 0;
            if (!permanentUpgrades.ContainsKey(UpgradeKey.NamedSpawnWeight))      permanentUpgrades[UpgradeKey.NamedSpawnWeight] = 0;
            if (!permanentUpgrades.ContainsKey(UpgradeKey.ShopRefresh))           permanentUpgrades[UpgradeKey.ShopRefresh] = 0;
        }

        public int GetUpgradeLevel(UpgradeKey key)
        {
            return permanentUpgrades[key];
        }

        public void SetUpgradeLevel(UpgradeKey key, int level)
        {
            permanentUpgrades[key] = level;
        }
    }
}
