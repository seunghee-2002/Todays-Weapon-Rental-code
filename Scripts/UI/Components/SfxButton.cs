using UnityEngine;
using UnityEngine.UI;

namespace TodaysWeaponRental
{
    /// <summary>
    /// Button에 붙여 클릭 시 SFX를 재생한다.
    /// 기존 onClick 리스너와 독립적으로 동작하므로 핸들러 코드 수정 없이 인스펙터에서만 부착하면 된다.
    /// 다수 View가 UnsubscribeEvents에서 onClick.RemoveAllListeners()를 호출하므로
    /// Awake가 아닌 OnEnable에서 매번 재등록해 두 번째 열림부터 소리가 사라지는 것을 막는다.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class SfxButton : MonoBehaviour
    {
        [SerializeField] private string clipName = "ButtonClick";

        private Button button;

        private void OnEnable()
        {
            if (button == null) button = GetComponent<Button>();
            button.onClick.RemoveListener(PlayClickSound); // 중복 등록 방지
            button.onClick.AddListener(PlayClickSound);
        }

        private void OnDisable()
        {
            if (button != null)
                button.onClick.RemoveListener(PlayClickSound);
        }

        private void PlayClickSound()
        {
            SoundManager.Instance?.PlaySFX(clipName);
        }
    }
}
