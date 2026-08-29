// Scripts/Runtime/WeeklyQuestInstance.cs
using System;
using System.Linq;

namespace TodaysWeaponRental
{
    [Serializable]
    public class WeeklyQuestInstance
    {
        public WeeklyQuestData questData;
        public int[] currentProgress;
        public int startDay;
        public int deadlineDay;
        public QuestStatus status;

        public WeeklyQuestInstance(WeeklyQuestData data, int currentDay)
        {
            questData = data;
            currentProgress = new int[data.requirements.Count];
            startDay = currentDay;
            deadlineDay = currentDay + 6; // 7일 기준 (시작일 포함)
            status = QuestStatus.Active;
        }

        public bool IsComplete
        {
            get
            {
                for (int i = 0; i < questData.requirements.Count; i++)
                {
                    if (currentProgress[i] < questData.requirements[i].targetCount)
                        return false;
                }
                return true;
            }
        }

        public bool IsExpired(int currentDay) => currentDay > deadlineDay;
        
        public int RemainingDays(int currentDay) => Math.Max(0, deadlineDay - currentDay);
        
        public float GetOverallProgress()
        {
            if (questData.requirements.Count == 0) return 0f;
            
            float totalProgress = 0f;
            for (int i = 0; i < questData.requirements.Count; i++)
            {
                float reqProgress = (float)currentProgress[i] / questData.requirements[i].targetCount;
                totalProgress += UnityEngine.Mathf.Clamp01(reqProgress);
            }
            return totalProgress / questData.requirements.Count;
        }
    }
}