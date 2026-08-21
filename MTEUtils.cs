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

        public static bool showMemoryUsage = false;

        public static void LogMemoryUsage(string tag)
        {
            if (showMemoryUsage && Time.frameCount % 60 == 0)
            {
                long totalMemory = GC.GetTotalMemory(false);
                Log("[{0}] Memory: {1:F2} MB", tag, totalMemory / 1024.0 / 1024.0);
            }
        }

        public static string CombinePaths(params string[] parts)
        {
            return parts.Aggregate(Path.Combine);
        }

        /// <summary>
        /// ゲームの SysDlg で通知ダイアログを出す。
        /// SysDlg は NGUI なのでプラグインの IMGUI ウィンドウより奥に描画される。
        /// IMGUI ウィンドウより手前に出したい場合は DialogPopupWindow.ShowDialog を使うこと
        /// </summary>
        public static void ShowDialog(string message)
        {
            GameMain.Instance.SysDlg.Show(
                message, SystemDialog.TYPE.OK, null, null);
        }

        /// <summary>
        /// ゲームの SysDlg で確認ダイアログを出す。
        /// IMGUI ウィンドウより手前に出したい場合は DialogPopupWindow.ShowConfirmDialog を使うこと
        /// </summary>
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

        /// <summary>
        /// スクリーン座標系のマウス位置の取得処理。
        /// Input.mousePosition が変換済み座標を返す環境（RT への描画等）では
        /// プラグイン側で生座標を返す関数に差し替える
        /// </summary>
        public static Func<Vector3> mousePositionGetter = () => Input.mousePosition;

        public static Vector3 mousePosition => mousePositionGetter();

        /// <summary>マウス位置を GUI 座標系 (左上原点) で返す</summary>
        public static Vector2 rawGuiPosition => new Vector2(mousePosition.x, Screen.height - mousePosition.y);

        /// <summary>
        /// 指定ウィンドウ以外の IMGUI ウィンドウがその座標を覆っているかの判定フック。
        /// 既定は常に false (トラッカーを持たない環境では従来どおり自窓だけで判定する)
        /// </summary>
        public static Func<int, Vector2, bool> isOverOtherWindowChecker = (windowId, guiPos) => false;

        /// <summary>
        /// マウスカーソルが GUI 座標系のウィンドウ矩形上にあるか（OnGUI 外からも呼べる）。
        /// 描画中の要素に対する判定は GUIView.IsMouseOverRect を使うこと
        /// </summary>
        public static bool IsMouseOverWindowRect(Rect windowRect)
        {
            var mousePosition = MTEUtils.mousePosition;
            var guiY = Screen.height - mousePosition.y;
            return mousePosition.x > windowRect.x &&
                mousePosition.x < windowRect.x + windowRect.width &&
                guiY > windowRect.y &&
                guiY < windowRect.y + windowRect.height;
        }

        public static void ResetInputOnScroll(Rect windowRect)
        {
            if (IsMouseOverWindowRect(windowRect) && Input.GetAxis("Mouse ScrollWheel") != 0f)
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

        /// <summary>
        /// メイドへのアイテム適用（AllProcProp）の完了を待ってからアクションを実行する
        /// </summary>
        public static void ExecuteAfterProcProp(Maid maid, Action action)
        {
            GameMain.Instance.StartCoroutine(ExecuteAfterProcPropInternal(maid, action));
        }

        private static IEnumerator ExecuteAfterProcPropInternal(Maid maid, Action action)
        {
            // セットアイテムの各部位への適用はAllProcProp内で行われるため、完了するまで待つ
            // maidのnullチェックは、待機中にメイドが破棄された場合に抜けるためのもの
            while (maid != null && maid.IsAllProcPropBusy)
            {
                yield return null;
            }
            action?.Invoke();
        }

        /// <summary>MenuDataBaseの構築完了を待つ上限時間（秒）</summary>
        private const float MenuDataBaseReadyTimeout = 60f;

        /// <summary>
        /// MenuDataBaseの非同期構築の完了を待ってからアクションを実行する
        /// </summary>
        public static void ExecuteAfterMenuDataBaseReady(Action action)
        {
            if (IsMenuDataBaseReady())
            {
                action?.Invoke();
                return;
            }

            GameMain.Instance.StartCoroutine(ExecuteAfterMenuDataBaseReadyInternal(action));
        }

        private static bool IsMenuDataBaseReady()
        {
            var menuDataBase = GameMain.Instance?.MenuDataBase;
            return menuDataBase != null && menuDataBase.JobFinished();
        }

        private static IEnumerator ExecuteAfterMenuDataBaseReadyInternal(Action action)
        {
            // 構築完了前はGetDataSize()が途中までの件数しか返さず、公式アイテムを取りこぼす
            LogDebug("MenuDataBaseの構築完了を待機します");

            var startTime = Time.realtimeSinceStartup;
            while (!IsMenuDataBaseReady())
            {
                // 待ち続けると呼び出し元のロード状態が戻らず、以後アイテム更新自体ができなくなるため、
                // 上限時間で打ち切って先へ進める
                if (Time.realtimeSinceStartup - startTime > MenuDataBaseReadyTimeout)
                {
                    LogWarning("MenuDataBaseの構築完了を待機できませんでした。アイテムの一部が表示されない可能性があります");
                    break;
                }
                yield return null;
            }

            action?.Invoke();
        }

        /// <summary>
        /// ファイルが存在するか判定する（MOD用ファイルシステムを優先し、無ければ本体側を確認）
        /// </summary>
        public static bool IsExistentFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            if (IsExistentFileInternal(fileName))
            {
                return true;
            }

            // 大文字小文字の違いで取りこぼさないように小文字でも確認する
            var lowerFileName = fileName.ToLower();
            return lowerFileName != fileName && IsExistentFileInternal(lowerFileName);
        }

        private static bool IsExistentFileInternal(string fileName)
        {
            if (GameUty.FileSystemMod != null && GameUty.FileSystemMod.IsExistentFile(fileName))
            {
                return true;
            }

            return GameUty.FileSystem != null && GameUty.FileSystem.IsExistentFile(fileName);
        }

        /// <summary>
        /// ボーン等の参照が揃っていて安全に触れる状態か。
        /// プロパティ適用中（IsAllProcPropBusy）でも true を返すため、
        /// 適用中の一時的な脱落で一覧が揺れると困る場面（GetReadyMaidList 等）ではこちらを使う
        /// </summary>
        public static bool IsBodyReady(this Maid maid)
        {
            return (maid != null && maid.body0 != null && maid.body0.m_Bones != null &&
                    maid.body0.trsEyeL != null && maid.body0.trsEyeR != null);
        }

        public static bool IsReady(this Maid maid)
        {
            return maid.IsBodyReady() && !maid.IsAllProcPropBusy;
        }

        public static List<Maid> GetReadyMaidList()
        {
            var result = new List<Maid>();
            var characterMgr = GameMain.Instance.CharacterMgr;

            int maidCount = characterMgr.GetMaidCount();
            for (int i = 0; i < maidCount; i++)
            {
                var maid = characterMgr.GetMaid(i);
                if (maid != null && maid.Visible && maid.IsBodyReady())
                {
                    result.Add(maid);
                }
            }

            int stockMaidCount = characterMgr.GetStockMaidCount();
            for (int j = 0; j < stockMaidCount; j++)
            {
                var maid = characterMgr.GetStockMaid(j);
                if (maid != null && maid.Visible && maid.IsBodyReady() && !result.Contains(maid))
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