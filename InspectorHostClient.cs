using System;
using System.Reflection;
using UnityEngine;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// SceneEditor プラグインの InspectorHost へのリフレクションブリッジ。
    /// 登録すると、SceneEditor Inspector で対象オブジェクト選択時に内容描画が丸ごと委譲される。
    /// RegisterRows で登録すると、ホストが描く内容の末尾へ固有の行だけを足せる。
    /// SceneEditor が不在・旧バージョンの場合は isAvailable が false になり、
    /// 呼び出し側は登録しない (SceneEditor Inspector は従来描画のまま)
    /// </summary>
    public static class InspectorHostClient
    {
        private delegate object RegisterDelegate(
            string name,
            Func<GameObject, bool> canDraw,
            Action<GameObject, Rect> draw);

        private delegate object Register2Delegate(
            string name,
            Func<GameObject, bool> canDraw,
            Action<GameObject, Rect> draw,
            bool drawsHeader);

        private delegate object RegisterRowsDelegate(
            string name,
            Func<GameObject, bool> canDraw,
            Func<GameObject, Rect, float> drawRows);

        private static RegisterDelegate _register;
        private static RegisterRowsDelegate _registerRows;
        private static Register2Delegate _register2;
        private static Action<object> _unregister;
        private static Func<Rect> _getWindowRect;
        private static Func<bool> _isWindowVisible;
        private static Func<GameObject, Rect, float> _drawHeader;
        private static Func<GameObject, bool> _drawsModel;
        private static bool _initialized;

        public static bool isAvailable
        {
            get
            {
                Initialize();
                return _register != null;
            }
        }

        /// <summary>
        /// ホストのウィンドウ状態を取得できるか。
        /// 取得できない旧バージョンの SceneEditor では、委譲先はドロップダウンを
        /// 出さずに済む UI (前後送りボタン等) へ倒す
        /// </summary>
        public static bool isWindowStateAvailable
        {
            get
            {
                Initialize();
                return _getWindowRect != null;
            }
        }

        /// <summary>ホストのウィンドウのスクリーン矩形。取得できなければ zero</summary>
        public static Rect hostWindowRect
            => isWindowStateAvailable ? _getWindowRect() : new Rect();

        /// <summary>ホストのウィンドウが描画されているか。取得できなければ false</summary>
        public static bool isHostWindowVisible
            => isWindowStateAvailable && _isWindowVisible();

        /// <summary>
        /// ヘッダー行を委譲先が自前で描けるか。描けない旧バージョンの SceneEditor では
        /// ヘッダーはホストが委譲領域の外へ固定表示する (従来どおりの見た目)
        /// </summary>
        public static bool isHeaderDrawAvailable
        {
            get
            {
                Initialize();
                return _register2 != null && _drawHeader != null;
            }
        }

        /// <summary>
        /// ホストが描く内容の末尾へ固有の行だけを足せるか。
        /// 足せない旧バージョンの SceneEditor では、従来どおり Register で内容を丸ごと描く
        /// </summary>
        public static bool isRowsDrawAvailable
        {
            get
            {
                Initialize();
                return _registerRows != null;
            }
        }

        /// <summary>
        /// ホストが共通のモデル表示で描くかを問い合わせられるか。
        /// 問い合わせられない旧バージョンの SceneEditor では、委譲先が別の手掛かりで近似する
        /// </summary>
        public static bool isDrawsModelAvailable
        {
            get
            {
                Initialize();
                return _drawsModel != null;
            }
        }

        private static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            // ロード順によってはホストのアセンブリが未登場のことがあるため、
            // 型が見つかるまでは _initialized を立てずに再試行を続ける
            var type = DockingClient.FindHostType("InspectorHost");
            if (type == null)
            {
                return;
            }

            // ここから先はホストの型は見つかっている。シグネチャ不一致は
            // バージョン差による恒久的な問題なので、この場合のみ無効へ確定する
            _initialized = true;

            try
            {
                var register = type.GetMethod("Register", BindingFlags.Public | BindingFlags.Static);
                var unregister = type.GetMethod("Unregister", BindingFlags.Public | BindingFlags.Static);
                if (register == null || unregister == null)
                {
                    MTEUtils.LogWarning("InspectorHostClient: InspectorHost にシグネチャの一致するメソッドが見つかりませんでした");
                    return;
                }

                _register = (RegisterDelegate)Delegate.CreateDelegate(typeof(RegisterDelegate), register);
                _unregister = (Action<object>)Delegate.CreateDelegate(typeof(Action<object>), unregister);
            }
            catch (Exception e)
            {
                // ホスト側のバージョン差でシグネチャが合わない場合は登録を無効化する
                MTEUtils.LogWarning("InspectorHostClient: InspectorHost との接続に失敗しました: " + e.Message);
                _register = null;
                _unregister = null;
                return;
            }

            InitializeWindowState(type);
            InitializeHeaderDraw(type);
            InitializeRowsDraw(type);
            InitializeDrawsModel(type);
        }

        /// <summary>
        /// モデル表示の問い合わせも後から足した API なので、無くても既存の登録は成立させる
        /// </summary>
        private static void InitializeDrawsModel(Type type)
        {
            try
            {
                var drawsModel = type.GetMethod("DrawsModel", BindingFlags.Public | BindingFlags.Static);
                if (drawsModel == null)
                {
                    return;
                }

                _drawsModel = (Func<GameObject, bool>)Delegate.CreateDelegate(
                    typeof(Func<GameObject, bool>), drawsModel);
            }
            catch (Exception e)
            {
                MTEUtils.LogWarning("InspectorHostClient: モデル表示の問い合わせ API の解決に失敗しました: " + e.Message);
                _drawsModel = null;
            }
        }

        /// <summary>
        /// 行の委譲も後から足した API なので、無くても既存の登録は成立させる
        /// </summary>
        private static void InitializeRowsDraw(Type type)
        {
            try
            {
                var registerRows = type.GetMethod("RegisterRows", BindingFlags.Public | BindingFlags.Static);
                if (registerRows == null)
                {
                    return;
                }

                _registerRows = (RegisterRowsDelegate)Delegate.CreateDelegate(
                    typeof(RegisterRowsDelegate), registerRows);
            }
            catch (Exception e)
            {
                MTEUtils.LogWarning("InspectorHostClient: 行の委譲 API の解決に失敗しました: " + e.Message);
                _registerRows = null;
            }
        }

        /// <summary>
        /// ヘッダーの自前描画も後から足した API なので、無くても登録自体は成立させる。
        /// Register2 と DrawHeader は対で使うため、片方でも欠けたら両方無効にする
        /// </summary>
        private static void InitializeHeaderDraw(Type type)
        {
            try
            {
                var register2 = type.GetMethod("Register2", BindingFlags.Public | BindingFlags.Static);
                var drawHeader = type.GetMethod("DrawHeader", BindingFlags.Public | BindingFlags.Static);
                if (register2 == null || drawHeader == null)
                {
                    return;
                }

                _register2 = (Register2Delegate)Delegate.CreateDelegate(typeof(Register2Delegate), register2);
                _drawHeader = (Func<GameObject, Rect, float>)Delegate.CreateDelegate(
                    typeof(Func<GameObject, Rect, float>), drawHeader);
            }
            catch (Exception e)
            {
                MTEUtils.LogWarning("InspectorHostClient: ヘッダー描画 API の解決に失敗しました: " + e.Message);
                _register2 = null;
                _drawHeader = null;
            }
        }

        /// <summary>
        /// ウィンドウ状態は後から足した API なので、無くても登録自体は成立させる。
        /// 失敗が必須 API の無効化へ波及しないよう、解決も例外処理も分けている
        /// </summary>
        private static void InitializeWindowState(Type type)
        {
            try
            {
                var getWindowRect = type.GetMethod("GetWindowRect", BindingFlags.Public | BindingFlags.Static);
                var isWindowVisible = type.GetMethod("IsWindowVisible", BindingFlags.Public | BindingFlags.Static);
                if (getWindowRect == null || isWindowVisible == null)
                {
                    return;
                }

                _getWindowRect = (Func<Rect>)Delegate.CreateDelegate(typeof(Func<Rect>), getWindowRect);
                _isWindowVisible = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), isWindowVisible);
            }
            catch (Exception e)
            {
                MTEUtils.LogWarning("InspectorHostClient: ウィンドウ状態の取得に失敗しました: " + e.Message);
                _getWindowRect = null;
                _isWindowVisible = null;
            }
        }

        /// <summary>
        /// Inspector 描画をホストへ登録する。戻り値はハンドル (ホスト不在なら null)。
        /// drawsHeader に true を指定すると、ヘッダー行のぶんを引かない内容領域が渡され、
        /// 委譲先が自前のスクロールビューの先頭で <see cref="DrawHeader"/> を呼ぶ約束になる
        /// (ホストが対応していなければ false と同じ扱いになるので、
        /// <see cref="isHeaderDrawAvailable"/> で判定してから呼ぶこと)
        /// </summary>
        public static object Register(
            string name,
            Func<GameObject, bool> canDraw,
            Action<GameObject, Rect> draw,
            bool drawsHeader = false)
        {
            if (!isAvailable)
            {
                return null;
            }

            try
            {
                if (drawsHeader && isHeaderDrawAvailable)
                {
                    return _register2(name, canDraw, draw, true);
                }
                return _register(name, canDraw, draw);
            }
            catch (Exception e)
            {
                MTEUtils.LogWarning("InspectorHostClient: InspectorHost への登録に失敗しました: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// ホストが自前で描く内容 (現状は配置モデル・背景モデル) の末尾へ、固有の行だけを足す登録。
        /// drawRows は rect の左上から描き、使った高さ (末尾の余白を含まない) を返す。
        /// 戻り値はハンドル (非対応の旧ホスト・失敗時は null)。解除は Unregister を使う
        /// </summary>
        public static object RegisterRows(
            string name,
            Func<GameObject, bool> canDraw,
            Func<GameObject, Rect, float> drawRows)
        {
            if (!isAvailable || !isRowsDrawAvailable)
            {
                return null;
            }

            try
            {
                return _registerRows(name, canDraw, drawRows);
            }
            catch (Exception e)
            {
                MTEUtils.LogWarning("InspectorHostClient: InspectorHost への行の登録に失敗しました: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// ホストのヘッダー行 (ギズモ行 + アクティブ・名前・フォーカス行) を指定矩形へ描く。
        /// drawsHeader: true で登録した委譲先が、自前のスクロールビューの先頭で呼ぶ。
        /// 戻り値は描画に使った高さ (末尾の余白は含まない)。描けなければ 0
        /// </summary>
        public static float DrawHeader(GameObject go, Rect rect)
        {
            if (!isHeaderDrawAvailable)
            {
                return 0f;
            }

            return _drawHeader(go, rect);
        }

        /// <summary>
        /// go をホストが共通のモデル表示 (RegisterRows の行を足す先) で描くか。
        /// 問い合わせられなければ false (<see cref="isDrawsModelAvailable"/> で判定してから呼ぶこと)
        /// </summary>
        public static bool DrawsModel(GameObject go)
        {
            return isDrawsModelAvailable && _drawsModel(go);
        }

        public static void Unregister(object handle)
        {
            if (handle == null || !isAvailable)
            {
                return;
            }

            try
            {
                _unregister(handle);
            }
            catch (Exception e)
            {
                MTEUtils.LogWarning("InspectorHostClient: InspectorHost からの登録解除に失敗しました: " + e.Message);
            }
        }
    }
}
