#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TodaysWeaponRental.Editor
{
    public static class AndroidApkBuild
    {
        private const string OutputArg = "-buildOutput";
        private const string ApkPathArg = "-apkPath";
        private const string DevelopmentBuildArg = "-developmentBuild";
        private const string CleanBuildArg = "-cleanBuild";
        private const string BundleVersionArg = "-bundleVersion";
        private const string VersionCodeArg = "-versionCode";
        private const string KeystoreNameArg = "-keystoreName";
        private const string KeystorePassArg = "-keystorePass";
        private const string KeyaliasNameArg = "-keyaliasName";
        private const string KeyaliasPassArg = "-keyaliasPass";

        [MenuItem("Tools/Today's Weapon Rental/Build/Android APK")]
        public static void BuildApkFromMenu()
        {
            BuildApk(new BuildConfig
            {
                OutputPath = CreateDefaultOutputPath(),
                DevelopmentBuild = false,
                CleanBuild = false
            }, exitEditor: false);
        }

        [MenuItem("Tools/Today's Weapon Rental/Build/Android Development APK")]
        public static void BuildDevelopmentApkFromMenu()
        {
            BuildApk(new BuildConfig
            {
                OutputPath = CreateDefaultOutputPath("dev"),
                DevelopmentBuild = true,
                CleanBuild = false
            }, exitEditor: false);
        }

        public static void BuildFromCommandLine()
        {
            BuildApk(BuildConfig.FromCommandLine(), exitEditor: true);
        }

        private static void BuildApk(BuildConfig config, bool exitEditor)
        {
            try
            {
                string[] scenes = EditorBuildSettings.scenes
                    .Where(scene => scene.enabled)
                    .Select(scene => scene.path)
                    .ToArray();

                if (scenes.Length == 0)
                    throw new InvalidOperationException("No enabled scenes found in EditorBuildSettings.");

                string outputPath = Path.GetFullPath(config.OutputPath);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ProjectRoot);

                ApplyPlayerSettings(config);

                EditorUserBuildSettings.buildAppBundle = false;
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                    throw new InvalidOperationException("Failed to switch active build target to Android.");

                BuildOptions options = BuildOptions.None;
                if (config.DevelopmentBuild)
                    options |= BuildOptions.Development;
                if (config.CleanBuild)
                    options |= BuildOptions.CleanBuildCache;

                Debug.Log($"[AndroidApkBuild] Building APK: {outputPath}");
                Debug.Log($"[AndroidApkBuild] Scenes: {string.Join(", ", scenes)}");

                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = options
                });

                BuildSummary summary = report.summary;
                if (summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException($"Android APK build failed: {summary.result}");

                Debug.Log($"[AndroidApkBuild] Build succeeded: {outputPath}");
                Debug.Log($"[AndroidApkBuild] Size: {summary.totalSize} bytes, time: {summary.totalTime}");

                if (exitEditor)
                    EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AndroidApkBuild] {ex}");
                if (exitEditor)
                    EditorApplication.Exit(1);
                else
                    throw;
            }
        }

        private static void ApplyPlayerSettings(BuildConfig config)
        {
            if (!string.IsNullOrWhiteSpace(config.BundleVersion))
                PlayerSettings.bundleVersion = config.BundleVersion;

            if (config.VersionCode > 0)
                PlayerSettings.Android.bundleVersionCode = config.VersionCode;

            if (string.IsNullOrWhiteSpace(config.KeystoreName))
                return;

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = Path.GetFullPath(config.KeystoreName);

            if (!string.IsNullOrEmpty(config.KeystorePass))
                PlayerSettings.Android.keystorePass = config.KeystorePass;
            if (!string.IsNullOrEmpty(config.KeyaliasName))
                PlayerSettings.Android.keyaliasName = config.KeyaliasName;
            if (!string.IsNullOrEmpty(config.KeyaliasPass))
                PlayerSettings.Android.keyaliasPass = config.KeyaliasPass;
        }

        private static string CreateDefaultOutputPath(string suffix = null)
        {
            string productName = SanitizeFileName(PlayerSettings.productName);
            string version = SanitizeFileName(PlayerSettings.bundleVersion);
            int versionCode = PlayerSettings.Android.bundleVersionCode;
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string suffixText = string.IsNullOrWhiteSpace(suffix) ? string.Empty : $"_{suffix}";
            string fileName = $"{productName}_{version}_{versionCode}_{timestamp}{suffixText}.apk";
            return Path.Combine(ProjectRoot, "Builds", "Android", fileName);
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Build";

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
                value = value.Replace(invalidChar, '_');

            return value.Replace(' ', '-');
        }

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private sealed class BuildConfig
        {
            public string OutputPath;
            public bool DevelopmentBuild;
            public bool CleanBuild;
            public string BundleVersion;
            public int VersionCode;
            public string KeystoreName;
            public string KeystorePass;
            public string KeyaliasName;
            public string KeyaliasPass;

            public static BuildConfig FromCommandLine()
            {
                Dictionary<string, string> args = ParseCommandLineArgs(Environment.GetCommandLineArgs());

                string outputPath = FirstNonEmpty(
                    GetArg(args, ApkPathArg),
                    GetArg(args, OutputArg),
                    Environment.GetEnvironmentVariable("TWR_APK_PATH"),
                    Environment.GetEnvironmentVariable("TWR_BUILD_OUTPUT"),
                    CreateDefaultOutputPath());

                return new BuildConfig
                {
                    OutputPath = outputPath,
                    DevelopmentBuild = GetBool(args, DevelopmentBuildArg, "TWR_DEVELOPMENT_BUILD", false),
                    CleanBuild = GetBool(args, CleanBuildArg, "TWR_CLEAN_BUILD", true),
                    BundleVersion = FirstNonEmpty(GetArg(args, BundleVersionArg), Environment.GetEnvironmentVariable("TWR_BUNDLE_VERSION")),
                    VersionCode = GetInt(args, VersionCodeArg, "TWR_VERSION_CODE", 0),
                    KeystoreName = FirstNonEmpty(GetArg(args, KeystoreNameArg), Environment.GetEnvironmentVariable("TWR_KEYSTORE_NAME")),
                    KeystorePass = FirstNonEmpty(GetArg(args, KeystorePassArg), Environment.GetEnvironmentVariable("TWR_KEYSTORE_PASS")),
                    KeyaliasName = FirstNonEmpty(GetArg(args, KeyaliasNameArg), Environment.GetEnvironmentVariable("TWR_KEYALIAS_NAME")),
                    KeyaliasPass = FirstNonEmpty(GetArg(args, KeyaliasPassArg), Environment.GetEnvironmentVariable("TWR_KEYALIAS_PASS"))
                };
            }

            private static Dictionary<string, string> ParseCommandLineArgs(string[] commandLineArgs)
            {
                var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < commandLineArgs.Length; i++)
                {
                    string current = commandLineArgs[i];
                    if (!current.StartsWith("-", StringComparison.Ordinal))
                        continue;

                    string next = i + 1 < commandLineArgs.Length ? commandLineArgs[i + 1] : string.Empty;
                    if (string.IsNullOrEmpty(next) || next.StartsWith("-", StringComparison.Ordinal))
                    {
                        result[current] = "true";
                        continue;
                    }

                    result[current] = next;
                    i++;
                }

                return result;
            }

            private static string GetArg(Dictionary<string, string> args, string name)
            {
                return args.TryGetValue(name, out string value) ? value : null;
            }

            private static bool GetBool(Dictionary<string, string> args, string argName, string envName, bool fallback)
            {
                string raw = FirstNonEmpty(GetArg(args, argName), Environment.GetEnvironmentVariable(envName));
                if (string.IsNullOrWhiteSpace(raw))
                    return fallback;

                return raw.Equals("1", StringComparison.OrdinalIgnoreCase)
                    || raw.Equals("true", StringComparison.OrdinalIgnoreCase)
                    || raw.Equals("yes", StringComparison.OrdinalIgnoreCase);
            }

            private static int GetInt(Dictionary<string, string> args, string argName, string envName, int fallback)
            {
                string raw = FirstNonEmpty(GetArg(args, argName), Environment.GetEnvironmentVariable(envName));
                return int.TryParse(raw, out int value) ? value : fallback;
            }

            private static string FirstNonEmpty(params string[] values)
            {
                foreach (string value in values)
                {
                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }

                return null;
            }
        }
    }
}
#endif
