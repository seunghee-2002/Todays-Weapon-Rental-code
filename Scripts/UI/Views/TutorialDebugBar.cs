#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TodaysWeaponRental
{
    /// <summary>
    /// [에디터 전용] 튜토리얼 시작 시 TutorialManager가 띄우는 디버그 바.
    /// 단계 점프 버튼 + 닫기(X). 닫으면 이 플레이 세션 동안 다시 뜨지 않는다.
    /// TutorialDebugBar.Show()로 코드 생성되며 프리팹/인스펙터 작업이 없다(Canvas 미사용, IMGUI로 직접 그림).
    /// </summary>
    public class TutorialDebugBar : MonoBehaviour
    {
        private static TutorialDebugBar instance;
        private static bool dismissed;   // 닫기(X) 시 true. 플레이 세션 동안 유지, 재시작 시 리셋.

        // Enter Play Mode(도메인 리로드 off) 대비 정적 상태 초기화. 오브젝트는 생성하지 않는다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            dismissed = false;
        }

        /// <summary>튜토리얼 시작 시 호출. 이미 떠 있거나 한 번 닫힌 뒤면 무시한다.</summary>
        public static void Show()
        {
            if (dismissed || instance != null) return;

            var go = new GameObject("[TutorialDebugBar]");
            instance = go.AddComponent<TutorialDebugBar>();
            Log.Info("[TutorialDebugBar] 생성됨 (튜토리얼 시작)");
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private GUIStyle buttonStyle;

        /// <summary>표시할 버튼 목록. 단계가 늘면 여기에 한 줄 추가하면 된다(3개마다 자동 줄바꿈).</summary>
        private List<(string label, Action action)> BuildEntries() => new List<(string, Action)>
        {
            ("1단계 무기상점",   () => TutorialManager.Instance?.DebugStartFromStep(TutorialStep.WeaponShop)),
            ("2단계 대장장이",   () => TutorialManager.Instance?.DebugStartFromStep(TutorialStep.Blacksmith)),
            ("3단계 아침이벤트", () => TutorialManager.Instance?.DebugStartFromStep(TutorialStep.MorningEvent)),
            ("4단계 의뢰판",     () => TutorialManager.Instance?.DebugStartFromStep(TutorialStep.QuestBoard)),
            ("5단계 모험준비",   () => TutorialManager.Instance?.DebugStartFromStep(TutorialStep.PrepEnter)),
            ("7단계 가방",       () => TutorialManager.Instance?.DebugStartFromStep(TutorialStep.Inventory)),
            ("8단계 손님2",      () => TutorialManager.Instance?.DebugStartFromStep(TutorialStep.SecondGuest)),
            ("9단계 모험결과",   () => TutorialManager.Instance?.DebugStartFromStep(TutorialStep.AdventureResult)),
            ("10단계 퀘스트",    () => TutorialManager.Instance?.DebugStartFromStep(TutorialStep.Quest)),
            ("11단계 나머지UI",  () => TutorialManager.Instance?.DebugStartFromStep(TutorialStep.RemainingUI)),
            ("12단계 마무리",    () => TutorialManager.Instance?.DebugStartFromStep(TutorialStep.Finish)),
            ("X 닫기",           () => { dismissed = true; Destroy(gameObject); }),
        };

        private void OnGUI()
        {
            const int fontSize = 24;
            const int perRow = 3;
            const float buttonHeight = 52f;

            buttonStyle ??= new GUIStyle(GUI.skin.button) { fontSize = fontSize };

            var entries = BuildEntries();
            int rows = Mathf.CeilToInt(entries.Count / (float)perRow);
            float areaHeight = rows * (buttonHeight + 4f) + 8f;

            // 클릭 액션은 GUI 레이아웃을 모두 끝낸 뒤 실행한다(중간에 오브젝트가 파괴돼도 안전).
            Action clicked = null;

            GUILayout.BeginArea(new Rect(0f, 0f, Screen.width, areaHeight), GUI.skin.box);

            for (int i = 0; i < entries.Count; i += perRow)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < Mathf.Min(i + perRow, entries.Count); j++)
                {
                    if (GUILayout.Button(entries[j].label, buttonStyle, GUILayout.Height(buttonHeight)))
                        clicked = entries[j].action;
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.EndArea();

            clicked?.Invoke();
        }
    }
}
#endif
