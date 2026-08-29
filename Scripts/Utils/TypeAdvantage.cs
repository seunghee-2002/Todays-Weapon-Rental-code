using System;

namespace TodaysWeaponRental{
    public static class TypeAdvantage
    {
        // [WeaponType(8), ArmorType(4)]
        public static readonly float[,] weaponArmorBonus = new float[8, 4]
        {
            // Unarmored,  Light,   Heavy,  Magical
            {  0.30f, -0.15f, -0.45f,  0.60f }, // Sword
            { -0.45f, -0.30f,  0.60f,  0.45f }, // Axe
            { -0.30f,  0.60f, -0.30f,  0.15f }, // Bow
            { -0.15f,  0.45f, -0.15f,  0.30f }, // Crossbow
            {  0.45f,  0.45f, -0.45f, -0.45f }, // Staff
            {  0.60f,  0.30f, -0.30f, -0.45f }, // Tome
            {  0.45f, -0.30f,  0.30f, -0.15f }, // Dagger
            {  0.30f, -0.45f,  0.45f, -0.15f }, // Shuriken
        };

        // [WeaponType(8), AdventurerStat(4)] — STR/DEX/INT/LUK 순서
        public static readonly float[,] weaponStatMultipliers = new float[8, 4]
        {
            // STR,   DEX,   INT,   LUK
            { 1.2f,  0.5f,  0.2f,  0.2f }, // Sword
            { 1.5f,  0.2f,  0.0f,  0.3f }, // Axe
            { 0.2f,  1.2f,  0.4f,  0.4f }, // Bow
            { 0.9f,  0.2f,  0.9f,  0.2f }, // Crossbow
            { 0.0f,  0.5f,  1.2f,  0.4f }, // Staff
            { 0.0f,  0.0f,  1.5f,  0.5f }, // Tome
            { 0.2f,  0.6f,  0.0f,  1.3f }, // Dagger
            { 0.2f,  0.9f,  0.2f,  0.9f }, // Shuriken
        }; 

        // [WeaponType(8)] — 성공률 공개에 필요한 스탯 비트마스크. bit 0~3 = STR/DEX/INT/LUK
        // required는 전부 공개돼야 하고, anyOf는 0이 아니면 그 중 최소 1개가 공개돼야 한다.
        // 기준: weaponStatMultipliers의 최고 계수 스탯이 required, 계수 0.3 이상인 나머지가 anyOf.
        // 0.3은 무기 상세 패널의 화살표 1개 임계값과 같다 - 플레이어는 화살표로 조건을 읽을 수 있다.
        // Shuriken은 최고 계수가 DEX/LUK 동률이라 둘 다 required가 된다. Crossbow도 STR/INT 동률로 같다.
        private const int MaskSTR = 1 << 0;
        private const int MaskDEX = 1 << 1;
        private const int MaskINT = 1 << 2;
        private const int MaskLUK = 1 << 3;

        public static readonly int[] weaponRequiredStatMask = new int[8]
        {
            MaskSTR | MaskDEX, // Sword
            MaskSTR | MaskLUK, // Axe
            MaskDEX,           // Bow    - INT/LUK 계수 동률이라 나머지 하나는 anyOf
            MaskSTR | MaskINT, // Crossbow - STR/INT 계수 동률이라 둘 다 required
            MaskINT,           // Staff  - DEX/LUK 둘 다 화살표가 뜨므로 나머지 하나는 anyOf
            MaskINT | MaskLUK, // Tome
            MaskDEX | MaskLUK, // Dagger
            MaskDEX | MaskLUK, // Shuriken
        };

        public static readonly int[] weaponAnyOfStatMask = new int[8]
        {
            0,                 // Sword
            0,                 // Axe
            MaskINT | MaskLUK, // Bow
            0,                 // Crossbow
            MaskDEX | MaskLUK, // Staff
            0,                 // Tome
            0,                 // Dagger
            0,                 // Shuriken
        };

        /// <summary>공개된 스탯 마스크가 해당 무기의 성공률 공개 조건을 충족하는가.</summary>
        public static bool IsStatMaskSatisfied(WeaponType weaponType, int visibleMask)
        {
            int wi = (int)weaponType;
            int required = weaponRequiredStatMask[wi];
            int anyOf = weaponAnyOfStatMask[wi];

            if ((visibleMask & required) != required) return false;
            return anyOf == 0 || (visibleMask & anyOf) != 0;
        }

        public static float GetStatScore(int str, int dex, int intel, int luck, WeaponType weaponType)
        {
            int wi = (int)weaponType;
            return str   * weaponStatMultipliers[wi, 0]
                 + dex   * weaponStatMultipliers[wi, 1]
                 + intel * weaponStatMultipliers[wi, 2]
                 + luck  * weaponStatMultipliers[wi, 3];
        }
    } 
}