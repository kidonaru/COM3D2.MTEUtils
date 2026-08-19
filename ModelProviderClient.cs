using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// SceneEditor プラグインの ModelProviderHost へのリフレクションブリッジ。
    /// 登録すると、SceneEditor のボーン編集ウィンドウ等に自プラグイン管理のモデルが
    /// 対象として列挙されるようになる。
    /// SceneEditor が不在・旧バージョンの場合は isAvailable が false になり、
    /// 呼び出し側は登録しない (再試行は呼び出し側の登録ループの責務)
    ///
    /// 契約 (ホスト側 ModelProviderHost と同じ):
    /// - getModels は現在配置中のモデルのルート GameObject を毎回列挙して返すこと
    /// - getDisplayName は null 可。null / 空文字なら GameObject 名で表示される
    /// - Register の戻り値は解除用ハンドル。不要になったら必ず Unregister すること
    /// </summary>
    public static class ModelProviderClient
    {
        private delegate object RegisterDelegate(
            string pluginName,
            Func<List<GameObject>> getModels,
            Func<GameObject, string> getDisplayName);

        private static RegisterDelegate _register;
        private static Action<object> _unregister;
        private static bool _initialized;

        public static bool isAvailable
        {
            get
            {
                Initialize();
                return _register != null;
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
            var type = DockingClient.FindHostType("ModelProviderHost");
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
                    MTEUtils.LogWarning("ModelProviderClient: ModelProviderHost にシグネチャの一致するメソッドが見つかりませんでした");
                    return;
                }

                _register = (RegisterDelegate)Delegate.CreateDelegate(typeof(RegisterDelegate), register);
                _unregister = (Action<object>)Delegate.CreateDelegate(typeof(Action<object>), unregister);
            }
            catch (Exception e)
            {
                // ホスト側のバージョン差でシグネチャが合わない場合は登録を無効化する
                MTEUtils.LogWarning("ModelProviderClient: ModelProviderHost との接続に失敗しました: " + e.Message);
                _register = null;
                _unregister = null;
            }
        }

        /// <summary>モデル提供者をホストへ登録する。戻り値はハンドル (ホスト不在なら null)</summary>
        public static object Register(
            string pluginName,
            Func<List<GameObject>> getModels,
            Func<GameObject, string> getDisplayName)
        {
            if (!isAvailable)
            {
                return null;
            }

            try
            {
                return _register(pluginName, getModels, getDisplayName);
            }
            catch (Exception e)
            {
                MTEUtils.LogWarning("ModelProviderClient: ModelProviderHost への登録に失敗しました: " + e.Message);
                return null;
            }
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
                MTEUtils.LogWarning("ModelProviderClient: ModelProviderHost からの登録解除に失敗しました: " + e.Message);
            }
        }
    }
}
