// Assets/_Projects/Scripts/Editor/ScreenshotCaptureTool.cs
// 메뉴: Tools > Today's Weapon Rental > Screenshot Capture (단축키 F12)
//
// 플레이 모드에서 Game 뷰를 현재 렌더 해상도 그대로 PNG로 저장한다.
// 스토어 스크린샷용 - Game 뷰 해상도를 1080x1920(Fixed Resolution)으로
// 맞춘 뒤 캡처하면 플레이 스토어 규격(9:16, 1080px 이상)에 맞는다.
// 저장 위치: <프로젝트 루트>/StoreAssets/Screenshots/

#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TodaysWeaponRental.Editor
{
    public static class ScreenshotCaptureTool
    {
        [MenuItem("Tools/Today's Weapon Rental/Screenshot Capture _F12")]
        public static void Capture()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[ScreenshotCaptureTool] 플레이 모드에서만 캡처할 수 있습니다.");
                return;
            }

            string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "StoreAssets", "Screenshots");
            Directory.CreateDirectory(folder);

            string fileName = $"Screenshot_{Screen.width}x{Screen.height}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            string path = Path.Combine(folder, fileName);

            if (Screen.width != 1080 || Screen.height != 1920)
            {
                Debug.LogWarning($"[ScreenshotCaptureTool] 현재 Game 뷰 해상도가 {Screen.width}x{Screen.height}입니다. 스토어 규격은 1080x1920을 권장합니다.");
            }

            ScreenCapture.CaptureScreenshot(path);

            // 일시정지 중이면 프레임이 진행되지 않아 파일이 저장되지 않으므로 한 프레임 진행
            if (EditorApplication.isPaused)
            {
                EditorApplication.Step();
            }

            Debug.Log($"[ScreenshotCaptureTool] 저장: {path}");
        }
    }
}
#endif
