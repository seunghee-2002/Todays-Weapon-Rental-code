using UnityEngine;

namespace TodaysWeaponRental
{
    public class AspectRatioKeeper : MonoBehaviour
    {
        private void Start()
        {
            // 원하는 고정 비율
            float targetAspectRatio = 9f / 16f;

            // 현재 기기의 실제 화면 비율
            float currentAspectRatio = (float)Screen.width / Screen.height;

            // 현재 비율을 목표 비율로 나눔
            float scaleHeight = currentAspectRatio / targetAspectRatio;

            Camera camera = GetComponent<Camera>();

            if (scaleHeight < 1.0f)
            {
                // 실제 화면이 더 세로로 긴 경우 (위아래에 레터박스 추가)
                Rect rect = camera.rect;
                rect.width = 1.0f;
                rect.height = scaleHeight;
                rect.x = 0;
                rect.y = (1.0f - scaleHeight) / 2.0f;
                camera.rect = rect;

                CreateLetterboxClearCamera(camera);
            }
            else if (scaleHeight > 1.0f)
            {
                // 실제 화면이 더 가로로 넓은 경우 (양옆에 필러박스 추가)
                float scaleWidth = 1.0f / scaleHeight;
                Rect rect = camera.rect;
                rect.width = scaleWidth;
                rect.height = 1.0f;
                rect.x = (1.0f - scaleWidth) / 2.0f;
                rect.y = 0;
                camera.rect = rect;

                CreateLetterboxClearCamera(camera);
            }
        }

        // 메인 카메라 rect 바깥(레터박스/필러박스 영역)은 메인 카메라가 매 프레임 지우지 않아,
        // Overlay 캔버스가 그렸던 픽셀이 잔상으로 남는다(예: 로딩 화면 종료 후 "불러오는 중..." 텍스트).
        // 전체 화면을 매 프레임 검게 클리어하는 배경 카메라를 붙여 잔상을 제거한다.
        private void CreateLetterboxClearCamera(Camera mainCamera)
        {
            var go = new GameObject("LetterboxClearCamera");
            go.transform.SetParent(mainCamera.transform, false);

            var clearCam = go.AddComponent<Camera>();
            clearCam.rect = new Rect(0f, 0f, 1f, 1f);       // 전체 화면
            clearCam.depth = mainCamera.depth - 1f;         // 메인 카메라보다 먼저 렌더
            clearCam.clearFlags = CameraClearFlags.SolidColor;
            clearCam.backgroundColor = Color.black;
            clearCam.cullingMask = 0;                       // 오브젝트는 그리지 않고 클리어만 수행
        }
    }
}