using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;

namespace COM3D2.MotionTimelineEditor
{
    public static class MTEUtils
    {
        private static string _pluginName = null;
        public static string PluginName
        {
            get
            {
                if (_pluginName == null)
                {
                    var name = typeof(MTEUtils).Assembly.GetName().Name;
                    _pluginName = name.Replace("COM3D2.", "").Replace(".Plugin", "");
                }
                return _pluginName;
            }
        }

        public static string ModDirPath
        {
            get => CombinePaths(UTY.gameProjectPath, "Mod");
        }

        public static CharacterMgr CharacterMgr => GameMain.Instance.CharacterMgr;

        private static string _presetDirPath = null;
        public static string PresetDirPath
        {
            get
            {
                if (_presetDirPath == null)
                {
                    _presetDirPath = CharacterMgr.PresetDirectory;
                }
                return _presetDirPath;
            }
        }

        [Conditional("DEBUG")]
        public static void LogDebug(string format, params object[] args)
        {
            string message = string.Format(format, args);
            if (Thread.CurrentThread.IsBackground)
            {
                MTEUtils.EnqueueAction(() =>
                {
                    UnityEngine.Debug.Log("[Debug] " + PluginName + ": " + message);
                });
                return;
            }
            UnityEngine.Debug.Log("[Debug] " + PluginName + ": " + message);
        }

        public static void Log(string format, params object[] args)
        {
            string message = string.Format(format, args);
            if (Thread.CurrentThread.IsBackground)
            {
                MTEUtils.EnqueueAction(() =>
                {
                    UnityEngine.Debug.Log(PluginName + ": " + message);
                });
                return;
            }
            UnityEngine.Debug.Log(PluginName + ": " + message);
        }

        public static void LogWarning(string format, params object[] args)
        {
            string message = string.Format(format, args);
            if (Thread.CurrentThread.IsBackground)
            {
                MTEUtils.EnqueueAction(() =>
                {
                    UnityEngine.Debug.LogWarning(PluginName + ": " + message);
                });
                return;
            }
            UnityEngine.Debug.LogWarning(PluginName + ": " + message);
        }
        
        public static void LogError(string format, params object[] args)
        {
            string message = string.Format(format, args);
            if (Thread.CurrentThread.IsBackground)
            {
                MTEUtils.EnqueueAction(() =>
                {
#if DEBUG
                    UnityEngine.Debug.LogError(PluginName + ": " + message + "\n" + Environment.StackTrace);
#else
                    UnityEngine.Debug.LogError(PluginName + ": " + message);
#endif
                });
                return;
            }
#if DEBUG
            UnityEngine.Debug.LogError(PluginName + ": " + message + "\n" + Environment.StackTrace);
#else
            UnityEngine.Debug.LogError(PluginName + ": " + message);
#endif
        }

        public static void AssertNull(bool condition, string message)
        {
            if (!condition)
            {
                StackFrame stackFrame = new StackFrame(1, true);
                string fileName = stackFrame.GetFileName();
                int fileLineNumber = stackFrame.GetFileLineNumber();
                string f_strMsg = fileName + "(" + fileLineNumber + ") \nNullPointerException：" + message;
                LogError(f_strMsg);
            }
        }

        public static void LogException(Exception e)
        {
            if (Thread.CurrentThread.IsBackground)
            {
                MTEUtils.EnqueueAction(() =>
                {
                    UnityEngine.Debug.LogException(e);
                });
                return;
            }
            UnityEngine.Debug.LogException(e);
        }

        public static string CombinePaths(params string[] parts)
        {
            return parts.Aggregate(Path.Combine);
        }

        public static void ShowDialog(string message)
        {
            GameMain.Instance.SysDlg.Show(
                message, SystemDialog.TYPE.OK, null, null);
        }

        public static void ShowConfirmDialog(
            string message,
            SystemDialog.OnClick onYes,
            SystemDialog.OnClick onNo = null)
        {
            GameMain.Instance.SysDlg.Show(
                message, SystemDialog.TYPE.YES_NO, onYes, onNo);
        }

        public static void UIHide()
        {
            var methodInfo = typeof(CameraMain).GetMethod("UIHide", BindingFlags.NonPublic | BindingFlags.Instance);
            methodInfo.Invoke(GameMain.Instance.MainCamera, null);
        }

        public static void UIResume()
        {
            var methodInfo = typeof(CameraMain).GetMethod("UIResume", BindingFlags.NonPublic | BindingFlags.Instance);
            methodInfo.Invoke(GameMain.Instance.MainCamera, null);
        }

        public static void OpenDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            path = Path.GetFullPath(path);

            MTEUtils.LogDebug("OpenDirectory: {0}", path);

            if (!string.IsNullOrEmpty(path))
            {
                if (File.Exists(path))
                {
                    Process.Start("explorer.exe", "/select," + path);
                }
                else if (Directory.Exists(path))
                {
                    Process.Start("explorer.exe", path);
                }
                else
                {
                    MTEUtils.LogWarning("指定されたディレクトリが存在しません: {0}", path);
                }
            }
        }

        public static void AdjustWindowPosition(ref Rect rect)
        {
            if (rect.x < 0) rect.x = 0;
            if (rect.y < 0) rect.y = 0;

            if (rect.x + rect.width > Screen.width)
            {
                rect.x = Screen.width - rect.width;
            }
            if (rect.y + rect.height > Screen.height)
            {
                rect.y = Screen.height - rect.height;
            }
        }

        public static void ResetInputOnScroll(Rect windowRect)
        {
            var mousePosition = Input.mousePosition;
            if (mousePosition.x > windowRect.x &&
                mousePosition.x < windowRect.x + windowRect.width &&
                Screen.height - mousePosition.y > windowRect.y &&
                Screen.height - mousePosition.y < windowRect.y + windowRect.height &&
                Input.GetAxis("Mouse ScrollWheel") != 0f)
            {
                Input.ResetInputAxes();
            }
        }

        public static void ExecuteNextFrame(Action action)
        {
            GameMain.Instance.StartCoroutine(ExecuteNextFrameInternal(action));
        }

        private static IEnumerator ExecuteNextFrameInternal(Action action)
        {
            yield return null;
            action?.Invoke();
        }

        public static bool IsReady(this Maid maid)
        {
            return (maid != null && maid.body0 != null && maid.body0.m_Bones != null &&
                    maid.body0.trsEyeL != null && maid.body0.trsEyeR != null &&
                    !maid.IsAllProcPropBusy);
        }

        public static List<Maid> GetReadyMaidList()
        {
            var result = new List<Maid>();
            var characterMgr = GameMain.Instance.CharacterMgr;

            int maidCount = characterMgr.GetMaidCount();
            for (int i = 0; i < maidCount; i++)
            {
                var maid = characterMgr.GetMaid(i);
                if (maid != null && maid.Visible && maid.IsReady())
                {
                    result.Add(maid);
                }
            }

            int stockMaidCount = characterMgr.GetStockMaidCount();
            for (int j = 0; j < stockMaidCount; j++)
            {
                var maid = characterMgr.GetStockMaid(j);
                if (maid != null && maid.Visible && maid.IsReady() && !result.Contains(maid))
                {
                    result.Add(maid);
                }
            }
            return result;
        }

        public static List<T> GetEnumValues<T>() where T : Enum
        {
            return Enum.GetValues(typeof(T)).Cast<T>().ToList();
        }

        public static void EnqueueAction(Action action)
        {
            MainThreadDispatcher.Enqueue(action);
        }

        private static readonly Dictionary<CharacterMgr.PresetType, string> _presetTypeName = new Dictionary<CharacterMgr.PresetType, string>
        {
            { CharacterMgr.PresetType.Wear, "服" },
            { CharacterMgr.PresetType.Body, "体" },
            { CharacterMgr.PresetType.All, "服/体" },
        };

        public static string GetPresetTypeName(CharacterMgr.PresetType presetType)
        {
            return _presetTypeName.GetOrDefault(presetType, "");
        }

        public static Vector3 GetNormalizedEulerAngles(Vector3 angles)
        {
            for (int i = 0; i < 3; i++)
            {
                int value = (int) angles[i];
                if (value > 180)
                {
                    angles[i] -= (value + 180) / 360 * 360;
                }
                else if (value < -180)
                {
                    angles[i] -= (value - 180) / 360 * 360;
                }
            }

            return angles;
        }

        public static string FormatWithNamedParameters(
            string format,
            IDictionary<string, object> parameters)
        {
            foreach (var kvp in parameters)
            {
                string placeholder = "{" + kvp.Key + "}";
                string formattedValue = kvp.Value.ToString();
                
                // 書式指定子がある場合（例：{frame:D6}）
                string formatSpecifierPattern = "{" + kvp.Key + ":([^}]+)}";
                var matches = Regex.Matches(format, formatSpecifierPattern);
                
                foreach (Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        string formatSpecifier = match.Groups[1].Value;
                        string fullPlaceholder = "{" + kvp.Key + ":" + formatSpecifier + "}";
                        
                        // 数値の場合は書式指定子を適用
                        if (kvp.Value is IFormattable formattable)
                        {
                            formattedValue = formattable.ToString(formatSpecifier, CultureInfo.InvariantCulture);
                        }
                        
                        format = format.Replace(fullPlaceholder, formattedValue);
                    }
                }
                
                // 書式指定子のない単純な置換
                format = format.Replace(placeholder, formattedValue);
            }
            
            return format;
        }
    }
}