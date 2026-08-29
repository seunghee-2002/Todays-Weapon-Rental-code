using System.Collections.Generic;
using UnityEngine;

namespace TodaysWeaponRental
{
    [CreateAssetMenu(fileName = "NicknameConfig", menuName = "TodaysWeaponRental/Config/NicknameConfig")]
    public class NicknameConfig : ScriptableObject
    {
        [Header("길이 제한")]
        public int minLength = 2;
        public int maxLength = 12;

        [Header("허용 패턴 (정규식)")]
        // 한글 / 영문 / 숫자 / _ 만 허용한다. 가나·한자를 한때 허용했다가 되돌렸다 -
        // ja/zh 폴백 폰트를 릴리즈 전 Static으로 재베이크하면(번역 텍스트에 실제로 쓰인
        // 글리프만 굽는다) 아틀라스에 없는 문자는 두부가 되는데, 닉네임은 임의 입력이라
        // 미리 구워둘 수가 없다. Dynamic 유지로 피할 수는 있지만 소스 TTF 약 19MB를
        // 빌드에 계속 실어야 한다. 자세한 배경은 Documents 다국어_도입전략.md 10절.
        //
        // **서버와 반드시 같아야 한다** - Tools/CloudCode/changeNickname.js의 ALLOWED_PATTERN.
        // 어긋나면 클라가 통과시킨 닉네임을 서버가 INVALID로 거부한다(그 반대도 마찬가지).
        // 값을 바꿨으면 Tools/CloudCode/sync_profanity.ps1 을 돌려 서버 쪽을 재생성할 것.
        [Tooltip("한글/영문/숫자/_. 변경 시 sync_profanity.ps1로 changeNickname.js 동기화 필수")]
        public string allowedPattern =
            "^[가-힣a-zA-Z0-9_]+$";

        [Header("변경 비용")]
        public int nicknameChangeCost = 50;
        [Tooltip("무료 변경 가능 횟수. 이 값 미만 동안은 무료, 이후엔 nicknameChangeCost 차감")]
        public int freeChangeCount = 1;

        [Header("욕설 단어 (클라 1차 필터)")]
        [Tooltip("이 목록에 포함된 단어가 닉네임에 들어 있으면 거부. 대소문자 무시.")]
        public List<string> profanityWords = new List<string>();
    }
}
