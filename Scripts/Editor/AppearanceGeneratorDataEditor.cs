#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TodaysWeaponRental
{
    [CustomEditor(typeof(AppearanceGeneratorData))]
    public class AppearanceGeneratorDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("=== 풀 테스트 ===", EditorStyles.boldLabel);

            var data = (AppearanceGeneratorData)target;

            if (GUILayout.Button("모든 직업군 풀 출력 (Console)"))
            {
                PrintAllPools(data);
            }
        }

        private void PrintAllPools(AppearanceGeneratorData data)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[AppearanceGeneratorData] 풀 테스트 결과\n");

            foreach (Gender gender in System.Enum.GetValues(typeof(Gender)))
            {
                if (gender == Gender.None) continue;

                foreach (AdventurerJobType job in System.Enum.GetValues(typeof(AdventurerJobType)))
                {
                    string tag = $"{gender} {job}";

                    sb.AppendLine($"--- {tag} ---");
                    sb.AppendLine($"  Helmet : [{string.Join(", ", data.GetHelmetPool(gender, job))}]");
                    sb.AppendLine($"  Back   : [{string.Join(", ", data.GetBackPool(gender, job))}]");
                    sb.AppendLine($"  Top    : [{string.Join(", ", data.GetTopPool(gender, job))}]");
                    sb.AppendLine($"  Bottom : [{string.Join(", ", data.GetBottomPool(gender, job))}]");
                    sb.AppendLine($"  Boots  : [{string.Join(", ", data.GetBootsPool(gender, job))}]");
                    sb.AppendLine($"  Gloves : [{string.Join(", ", data.GetGlovesPool(gender, job))}]");
                    sb.AppendLine();
                }

                sb.AppendLine($"--- {gender} 공용 신체 ---");
                sb.AppendLine($"  Brow : [{string.Join(", ", data.GetBrowPool(gender))}]");
                sb.AppendLine($"  Hair : [{string.Join(", ", data.GetHairPool(gender))}]");
                sb.AppendLine();
            }

            sb.AppendLine("--- 성별 무관 신체 ---");
            sb.AppendLine($"  Skin    : [{string.Join(", ", data.GetSkinPool())}]");
            sb.AppendLine($"  Eyes    : [{string.Join(", ", data.GetEyesPool())}]");
            sb.AppendLine($"  Mouth   : [{string.Join(", ", data.GetMouthPool())}]");
            sb.AppendLine($"  Beard   : [{string.Join(", ", data.GetBeardPool())}]");
            sb.AppendLine($"  Eyewear : [{string.Join(", ", data.GetEyewearPool())}]");

            Debug.Log(sb.ToString());
        }
    }
}
#endif