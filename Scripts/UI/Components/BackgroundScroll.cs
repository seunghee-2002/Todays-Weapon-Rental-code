using UnityEngine;
using TodaysWeaponRental;

public class BackgroundScroll : MonoBehaviour
{
    [SerializeField] private RectTransform cloudA;
    [SerializeField] private RectTransform cloudB;

    [SerializeField] private float speed = 20f;

    // 하루 영업시간(6시~21시) 총 게임 분. TimeManager.CurrentTime의 900과 동일 기준
    private const float DayTotalMinutes = 900f;
    // 날짜 전환(21시 -> 다음날 6시) 시 건너뛰는 밤 시간(게임 분)
    private const float NightMinutes = 540f;

    private float width;

    void Start()
    {
        width = cloudA.rect.width;

        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnTimeSkipStarted += OnTimeSkipStarted;
            TimeManager.Instance.OnDayChanged += OnDayChanged;
        }
    }

    void OnDestroy()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnTimeSkipStarted -= OnTimeSkipStarted;
            TimeManager.Instance.OnDayChanged -= OnDayChanged;
        }
    }

    void Update()
    {
        // TimeManager가 있으면(인게임) 시간 상태에 스크롤을 동기화. 없으면(메인메뉴 등) 기본 속도로 스크롤.
        TimeManager tm = TimeManager.Instance;
        if (tm != null && tm.IsTimePaused) return;      // 일시정지 시 구름 멈춤
        float scale = tm != null ? tm.CurrentTimeScale : 1f;

        MoveClouds(speed * scale * Time.deltaTime);     // 배속에 비례해 스크롤
    }

    // 아침 스킵/대화 시간 소모 등 AdvanceGameTime 경로의 스킵 (게임 분)
    private void OnTimeSkipStarted(float skippedMinutes)
    {
        JumpBySkippedTime(skippedMinutes);
    }

    // 날짜 전환(21시 -> 다음날 6시): 밤 시간만큼 스킵된 것으로 표현
    private void OnDayChanged(int day)
    {
        JumpBySkippedTime(NightMinutes);
    }

    // 스킵된 게임 분에 비례해 구름 위치를 즉시 이동 (일시정지 중에도 동작 - 튜토리얼 스킵 대응)
    private void JumpBySkippedTime(float skippedMinutes)
    {
        if (skippedMinutes <= 0f) return;

        MoveClouds(width * skippedMinutes / DayTotalMinutes);
    }

    private void MoveClouds(float move)
    {
        if (move <= 0f) return;

        cloudA.anchoredPosition += Vector2.right * move;
        cloudB.anchoredPosition += Vector2.right * move;

        // 스킵 점프는 한 번에 width 이상 이동할 수 있어 랩을 반복 적용
        int guard = 0;
        while ((cloudA.anchoredPosition.x >= width || cloudB.anchoredPosition.x >= width) && guard++ < 8)
        {
            if (cloudA.anchoredPosition.x >= width)
            {
                cloudA.anchoredPosition = new Vector2(
                    cloudB.anchoredPosition.x - width,
                    cloudA.anchoredPosition.y);
            }

            if (cloudB.anchoredPosition.x >= width)
            {
                cloudB.anchoredPosition = new Vector2(
                    cloudA.anchoredPosition.x - width,
                    cloudB.anchoredPosition.y);
            }
        }
    }
}
