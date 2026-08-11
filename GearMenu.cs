using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

// Reference:
//   https://github.com/neguse11/cm3d2_plugins_okiba/blob/master/Lib/GearMenu.cs (WTFPL)
//   もとは CM3D2-01 さんの「CM3D2：SystemShortcutにボタン追加」
//   https://gist.github.com/CM3D2-01/adcf5072ff5ba812858a
//
// 本ファイルは上記をベースに以下を変更している:
//   - COM3D2.5 対応: SysShortcut.VisibleExplanation を呼ばず、リフレクションで
//     ラベル/スプライトを直接操作する（2.0/2.5 でメソッドシグネチャが異なるため）
//   - SysShortcut 未生成時のガード (IsReady)
//   - 非アクティブなシステムボタン（2.5 では Dic/Shop/GP003Help が環境次第で非表示）を
//     レイアウトから除外
//   - 他プラグインの PreOnReposition 呼び出しを try/catch で保護
//   - 本プラグインで未使用のオーバーロード（PluginBase 版 Add、文字列指定版
//     Contains/SetFrameColor/ResetFrameColor/SetText、keepExplanation 等）を削除
//   - Remove(GameObject) に null/未準備ガードと生成リソースの明示破棄を追加

// 本家はグローバルな GearMenu 名前空間を使うが、参照 DLL（COM3D2.ExternalPreset.Managed 等）が
// 同じ okiba 由来の GearMenu.Buttons を内包しており型が衝突する（CS0436）。
// そのため MTEUtils の名前空間配下に GearMenu クラスとして入れ子にしている
// （名前空間ではなくクラスにすることで using COM3D2.MotionTimelineEditor だけで参照できる）
namespace COM3D2.MotionTimelineEditor
{
    /// <summary>
    /// 歯車メニュー関連
    /// </summary>
    public static class GearMenu
    {
        /// <summary>
        /// 歯車メニューへのアイコン登録
        /// </summary>
        public static class Buttons
        {
            // 識別名の実体。同じ名前を保持すること。詳細は SetAndCallOnReposition を参照
            static string Name_ = "CM3D2.GearMenu.Buttons";

            // バージョン文字列の実体。改善、改造した場合は文字列の辞書順がより大きい値に更新すること
            // （レイアウトに非アクティブボタン除外・画面リサイズ追従を加えたため、
            //  本家/KYM の 0.0.2.0 から上げている。辞書順比較のため各桁は 1 桁を維持すること）
            static string Version_ = Name_ + " 0.0.2.2";

            /// <summary>
            /// 識別名
            /// </summary>
            public static string Name { get { return Name_; } }

            /// <summary>
            /// バージョン文字列
            /// </summary>
            public static string Version { get { return Version_; } }

            /// <summary>
            /// ギアメニューにボタンを追加できる状態か（SysShortcut 生成前は false）
            /// </summary>
            public static bool IsReady
            {
                get { return GameMain.Instance != null && GameMain.Instance.SysShortcut != null; }
            }

            /// <summary>
            /// 歯車メニューにボタンを追加
            /// </summary>
            /// <param name="name">ボタンオブジェクト名。null可</param>
            /// <param name="label">ツールチップテキスト。null可(ツールチップ非表示)。アイコンへのマウスオーバー時に表示される</param>
            /// <param name="pngData">アイコン画像。null可(システムアイコン使用)。32x32ピクセルのPNGファイル</param>
            /// <param name="action">コールバック。null可(コールバック削除)。アイコンクリック時に呼び出されるコールバック</param>
            /// <returns>生成されたボタンのGameObject。未生成状態や action が null の場合は null</returns>
            public static GameObject Add(string name, string label, byte[] pngData, Action<GameObject> action)
            {
                GameObject goButton = null;

                if (!IsReady)
                {
                    return goButton;
                }

                // 既に存在する場合は削除して続行
                if (Contains(name))
                {
                    Remove(name);
                }

                if (action == null)
                {
                    return goButton;
                }

                try
                {
                    // ギアメニューの子として、コンフィグパネル呼び出しボタンを複製
                    goButton = NGUITools.AddChild(Grid, UTY.GetChildObject(Grid, "Config", true));

                    // 名前を設定
                    if (name != null)
                    {
                        goButton.name = name;
                    }

                    // イベントハンドラ設定（同時に、元から持っていたハンドラは削除）
                    EventDelegate.Set(goButton.GetComponent<UIButton>().onClick, () => { action(goButton); });

                    // ポップアップテキストを追加
                    {
                        UIEventTrigger t = goButton.GetComponent<UIEventTrigger>();
                        EventDelegate.Add(t.onHoverOut, () => { VisibleExplanation(null, false); });
                        EventDelegate.Add(t.onDragStart, () => { VisibleExplanation(null, false); });
                        SetText(goButton, label);
                    }

                    // PNG イメージを設定
                    {
                        if (pngData == null)
                        {
                            pngData = DefaultIcon.Png;
                        }

                        // 本当はスプライトを削除したいが、削除するとパネルのα値とのTween同期が動作しない
                        // (動作させる方法が分からない) ので、スプライトを描画しないように設定する
                        UISprite us = goButton.GetComponent<UISprite>();
                        us.type = UIBasicSprite.Type.Filled;
                        us.fillAmount = 0.0f;

                        // テクスチャを生成
                        var tex = new Texture2D(1, 1);
                        tex.LoadImage(pngData);

                        // 新しくテクスチャスプライトを追加
                        UITexture ut = NGUITools.AddWidget<UITexture>(goButton);
                        ut.material = new Material(ut.shader);
                        ut.material.mainTexture = tex;
                        ut.MakePixelPerfect();
                    }

                    // グリッド内のボタンを再配置
                    Reposition();
                }
                catch
                {
                    // 既にオブジェクトを作っていた場合は削除
                    if (goButton != null)
                    {
                        NGUITools.Destroy(goButton);
                        goButton = null;
                    }
                    throw;
                }
                return goButton;
            }

            /// <summary>
            /// 歯車メニューからボタンを削除
            /// </summary>
            /// <param name="name">ボタン名。Add()に与えた名前</param>
            public static void Remove(string name)
            {
                Remove(Find(name));
            }

            /// <summary>
            /// 歯車メニューからボタンを削除
            /// </summary>
            /// <param name="go">ボタン。Add()の戻り値</param>
            public static void Remove(GameObject go)
            {
                if (go == null || !IsReady)
                {
                    return;
                }

                // Add で動的生成した Texture2D / Material は GameObject 破棄では解放されないため明示的に破棄する
                var uiTexture = go.GetComponentInChildren<UITexture>();
                if (uiTexture != null)
                {
                    var tex = uiTexture.mainTexture as Texture2D;
                    var mat = uiTexture.material;
                    uiTexture.mainTexture = null;
                    uiTexture.material = null;
                    if (tex != null)
                    {
                        UnityEngine.Object.Destroy(tex);
                    }
                    if (mat != null)
                    {
                        UnityEngine.Object.Destroy(mat);
                    }
                }

                // NGUITools.Destroy は実破棄まで最大 1 フレームかかるため、描画されないよう先に非アクティブ化する
                go.SetActive(false);
                NGUITools.Destroy(go);
                Reposition();
            }

            /// <summary>
            /// 歯車メニュー内のボタンの存在を確認
            /// </summary>
            /// <param name="name">ボタン名。Add()に与えた名前</param>
            public static bool Contains(string name)
            {
                return Find(name) != null;
            }

            /// <summary>
            /// ボタンに枠をつける
            /// </summary>
            /// <param name="go">ボタン。Add()の戻り値</param>
            /// <param name="color">枠の色</param>
            public static void SetFrameColor(GameObject go, Color color)
            {
                if (go == null)
                {
                    return;
                }
                var uiTexture = go.GetComponentInChildren<UITexture>();
                if (uiTexture == null)
                {
                    return;
                }
                var tex = uiTexture.mainTexture as Texture2D;
                if (tex == null)
                {
                    return;
                }
                for (int x = 1; x < tex.width - 1; x++)
                {
                    tex.SetPixel(x, 0, color);
                    tex.SetPixel(x, tex.height - 1, color);
                }
                for (int y = 1; y < tex.height - 1; y++)
                {
                    tex.SetPixel(0, y, color);
                    tex.SetPixel(tex.width - 1, y, color);
                }
                tex.Apply();
            }

            /// <summary>
            /// ボタンの枠を消す
            /// </summary>
            /// <param name="go">ボタンのGameObject。Add()の戻り値</param>
            public static void ResetFrameColor(GameObject go)
            {
                SetFrameColor(go, DefaultFrameColor);
            }

            /// <summary>
            /// マウスオーバー時のテキスト指定
            /// </summary>
            /// <param name="go">ボタンのGameObject。Add()の戻り値</param>
            /// <param name="label">マウスオーバー時のテキスト。null可</param>
            public static void SetText(GameObject go, string label)
            {
                var t = go.GetComponent<UIEventTrigger>();
                t.onHoverOver.Clear();
                EventDelegate.Add(t.onHoverOver, () => { VisibleExplanation(label, label != null); });
                var b = go.GetComponent<UIButton>();

                // 既にホバー中なら説明を変更する
                if (b.state == UIButtonColor.State.Hover)
                {
                    VisibleExplanation(label, label != null);
                }
            }

            static readonly FieldInfo spriteExplanationInfo = typeof(SystemShortcut).GetField("m_spriteExplanation", BindingFlags.Instance | BindingFlags.NonPublic);
            static readonly FieldInfo labelExplanationInfo = typeof(SystemShortcut).GetField("m_labelExplanation", BindingFlags.Instance | BindingFlags.NonPublic);

            /// <summary>
            /// ポップアップのラベル表示。SystemShortcut.VisibleExplanation は
            /// 2.0 (string, bool) と 2.5 (int, bool) でシグネチャが異なるため、
            /// リフレクションでラベル/スプライトを直接操作して両対応する
            /// </summary>
            /// <param name="text">ラベル文字列</param>
            /// <param name="visible">ラベルの表示状態</param>
            public static void VisibleExplanation(string text, bool visible)
            {
                var m_spriteExplanation = spriteExplanationInfo.GetValue(SysShortcut) as UISprite;
                var m_labelExplanation = labelExplanationInfo.GetValue(SysShortcut) as UILabel;
                if (m_labelExplanation != null)
                {
                    m_labelExplanation.text = visible ? text : null;
                    m_labelExplanation.width = 0;
                    m_labelExplanation.MakePixelPerfect();
                }
                if (m_spriteExplanation != null)
                {
                    if (visible && m_labelExplanation != null)
                    {
                        m_spriteExplanation.width = m_labelExplanation.width + 15;
                    }
                    m_spriteExplanation.gameObject.SetActive(visible);
                }
            }

            // システムショートカット内のGameObjectを見つける
            static GameObject Find(string name)
            {
                Transform t = GridUI.GetChildList().FirstOrDefault(c => c.gameObject.name == name);
                return t == null ? null : t.gameObject;
            }

            /// <summary>
            /// 画面サイズ変更後にギアメニューを右上へ再配置する。
            /// バニラはリサイズ時に Base を再配置しないため、プラグイン側から呼び出す。
            /// onReposition の所有権がより新しいバージョンの他プラグインにある場合は
            /// その実装に委譲される (既存のバージョン調停規約に従う)
            /// </summary>
            public static void OnScreenSizeChanged()
            {
                if (IsReady)
                {
                    Reposition();
                }
            }

            // グリッド内のボタンを再配置
            static void Reposition()
            {
                // 必要なら UIGrid.onRepositionを設定、呼び出しを行う
                SetAndCallOnReposition(GridUI);

                // 次回の UIGrid.Update 処理時にグリッド内のボタン再配置が行われるようリクエスト
                GridUI.repositionNow = true;
            }

            // 必要に応じて UIGrid.onReposition を登録、呼び出す
            static void SetAndCallOnReposition(UIGrid uiGrid)
            {
                string targetVersion = GetOnRepositionVersion(uiGrid);

                // バージョン文字列が null の場合、知らないクラスが登録済みなのであきらめる
                if (targetVersion == null)
                {
                    return;
                }

                // 何も登録されていないか、自分より古いバージョンだったら新しい onReposition を登録する
                if (targetVersion == string.Empty || string.Compare(targetVersion, Version, false) < 0)
                {
                    uiGrid.onReposition = (new OnRepositionHandler(Version)).OnReposition;
                }

                // PreOnReposition を持つ場合はそれを呼び出す
                if (uiGrid.onReposition != null)
                {
                    object target = uiGrid.onReposition.Target;
                    if (target != null)
                    {
                        Type type = target.GetType();
                        MethodInfo mi = type.GetMethod("PreOnReposition");
                        if (mi != null)
                        {
                            try
                            {
                                mi.Invoke(target, new object[] { });
                            }
                            catch (Exception e)
                            {
                                // 他プラグイン側の例外で呼び出し元を巻き込まないよう、自前レイアウトにフォールバックする。
                                // 失敗するハンドラを呼び続けないよう、所有権も自分に差し替える
                                Debug.LogWarning("GearMenu: PreOnReposition の呼び出しに失敗しました: " + e);
                                uiGrid.onReposition = (new OnRepositionHandler(Version)).OnReposition;
                                new OnRepositionHandler(Version).PreOnReposition();
                            }
                        }
                    }
                }
            }

            // UIGrid.onReposition を保持するオブジェクトのバージョン文字列を得る
            //  null            知らないクラスもしくはバージョン文字列だった
            //  string.Empty    UIGrid.onRepositionが未登録だった
            //  その他          取得したバージョン文字列
            static string GetOnRepositionVersion(UIGrid uiGrid)
            {
                if (uiGrid.onReposition == null)
                {
                    // 未登録だった
                    return string.Empty;
                }

                object target = uiGrid.onReposition.Target;
                if (target == null)
                {
                    // Delegate.Target が null ということは、
                    // UIGrid.onReposition は static なメソッドなので、たぶん知らないクラス
                    return null;
                }

                Type type = target.GetType();
                if (type == null)
                {
                    // 型情報が取れないので、あきらめる
                    return null;
                }

                FieldInfo fi = type.GetField("Version", BindingFlags.Instance | BindingFlags.Public);
                if (fi == null)
                {
                    // public な Version メンバーを持っていないので、たぶん知らないクラス
                    return null;
                }

                string targetVersion = fi.GetValue(target) as string;
                if (targetVersion == null || !targetVersion.StartsWith(Name))
                {
                    // 知らないバージョン文字列だった
                    return null;
                }

                return targetVersion;
            }

            public static SystemShortcut SysShortcut { get { return GameMain.Instance.SysShortcut; } }
            public static UISprite SysShortcutExplanation
            {
                get { return spriteExplanationInfo.GetValue(SysShortcut) as UISprite; }
            }
            public static GameObject Base { get { return SysShortcut.gameObject.transform.Find("Base").gameObject; } }
            public static UISprite BaseSprite { get { return Base.GetComponent<UISprite>(); } }
            public static GameObject Grid { get { return Base.gameObject.transform.Find("Grid").gameObject; } }
            public static UIGrid GridUI { get { return Grid.GetComponent<UIGrid>(); } }
            public static readonly Color DefaultFrameColor = new Color(1f, 1f, 1f, 0f);

            // UIGrid.onReposition処理用のクラス
            // Delegate.Targetの値を生かすために、static ではなくインスタンスとして生成
            class OnRepositionHandler
            {
                public string Version;

                public OnRepositionHandler(string version)
                {
                    this.Version = version;
                }

                // 実配置は PreOnReposition 側で行うため、NGUI からのコールバックは何もしない
                // （Version を持つ Delegate.Target を提供するためのダミー実装）
                public void OnReposition()
                {
                }

                public void PreOnReposition()
                {
                    var g = GridUI;
                    var b = BaseSprite;

                    // ratio : 画面横幅に対するボタン全体の横幅の比率。0.5 なら画面半分
                    float ratio = 3.0f / 4.0f;
                    float pixelSizeAdjustment = UIRoot.GetPixelSizeAdjustment(Base);

                    g.cellHeight = g.cellWidth;
                    g.arrangement = UIGrid.Arrangement.CellSnap;
                    g.sorting = UIGrid.Sorting.None;
                    g.pivot = UIWidget.Pivot.TopRight;
                    g.maxPerLine = (int)(Screen.width / (g.cellWidth / pixelSizeAdjustment) * ratio);

                    // 非アクティブなボタン（2.5 では Dic/Shop/GP003Help が環境次第で非表示）が
                    // 枠数と Base 幅の計算に混ざらないよう除外する
                    var children = new List<Transform>();
                    foreach (Transform child in g.GetChildList())
                    {
                        if (child.gameObject.activeSelf)
                        {
                            children.Add(child);
                        }
                    }

                    int itemCount = children.Count;
                    int spriteItemX = Math.Min(g.maxPerLine, itemCount);
                    int spriteItemY = Math.Max(1, (itemCount - 1) / g.maxPerLine + 1);
                    int spriteWidthMargin = (int)(g.cellWidth * 3 / 2 + 8);
                    int spriteHeightMargin = (int)(g.cellHeight / 2);
                    float pivotOffsetY = spriteHeightMargin * 1.5f + 1f;

                    b.pivot = UIWidget.Pivot.TopRight;
                    b.width = (int)(spriteWidthMargin + g.cellWidth * spriteItemX);
                    b.height = (int)(spriteHeightMargin + g.cellHeight * spriteItemY + 2f);

                    // もとの Base の localPosition (946,502) は 1920x1080 の画面右上からの
                    // マージン (14,38) を意味する。UIRoot は Constrained (fitWidth/fitHeight) のため
                    // 縦長ウィンドウでは上端のローカル Y が 540 を超える。固定値ではなく
                    // 現在の画面実寸から右上位置を計算して追従させる
                    float halfW = Screen.width * pixelSizeAdjustment * 0.5f;
                    float halfH = Screen.height * pixelSizeAdjustment * 0.5f;
                    Base.transform.localPosition = new Vector3(halfW - 14.0f, halfH - 38.0f + pivotOffsetY, 0.0f);

                    // ギアボタン本体は Base の子ではなく SysShortcut 直下の別オブジェクト
                    // (元位置 (912,502) = 右上からのマージン (48,38)) なので、同様に追従させる
                    var gear = SysShortcut.transform.Find("Gear");
                    if (gear != null)
                    {
                        gear.localPosition = new Vector3(halfW - 48.0f, halfH - 38.0f, 0.0f);
                    }

                    Grid.transform.localPosition = new Vector3(
                        -2.0f + (-spriteItemX - 1 + spriteItemY - 1) * g.cellWidth,
                        -1.0f - pivotOffsetY,
                        0f);

                    {
                        int a = 0;
                        string[] specialNames = GameMain.Instance.CMSystem.NetUse ? OnlineButtonNames : OfflineButtonNames;
                        foreach (Transform child in children)
                        {
                            int i = a++;

                            // システムが持っているオブジェクトの場合は特別に順番をつける
                            int si = Array.IndexOf(specialNames, child.gameObject.name);
                            if (si >= 0)
                            {
                                i = si;
                            }

                            float x = (-i % g.maxPerLine + spriteItemX - 1) * g.cellWidth;
                            float y = (i / g.maxPerLine) * g.cellHeight;
                            child.localPosition = new Vector3(x, -y, 0f);
                        }
                    }

                    // マウスオーバー時のテキストの位置を指定
                    {
                        UISprite sse = SysShortcutExplanation;
                        Vector3 v = sse.gameObject.transform.localPosition;
                        v.y = Base.transform.localPosition.y - b.height - sse.height;
                        sse.gameObject.transform.localPosition = v;
                    }
                }

                // オンライン時のボタンの並び順。インデクスの若い側が右になる
                static string[] OnlineButtonNames = new string[] {
                    "Config", "Ss", "SsUi", "Shop", "ToTitle", "Info", "Exit"
                };

                // オフライン時のボタンの並び順。インデクスの若い側が右になる
                static string[] OfflineButtonNames = new string[] {
                    "Config", "Ss", "SsUi", "ToTitle", "Info", "Exit"
                };
            }
        }

        // デフォルトアイコン (32x32 ピクセルの PNG イメージ)
        internal static class DefaultIcon
        {
            public static byte[] Png = Convert.FromBase64String(
                    "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAIAAAD8GO2jAAAAA3NCSVQICAjb4U/g" +
                    "AAAACXBIWXMAABYlAAAWJQFJUiTwAAAA/0lEQVRIie2WPYqFMBRGb35QiARM4QZS" +
                    "uAX3X7sDkWwgRYSQgJLEKfLGh6+bZywG/JrbnZPLJfChfd/hzuBb6QBA89i2zTln" +
                    "jFmWZV1XAPjrZgghAKjrum1bIUTTNFVVvQXOOaXUNE0xxhDC9++llBDS972U8iTQ" +
                    "Ws/zPAyDlPJreo5SahxHzrkQAo4baK0B4Dr9gGTgW4Ax5pxfp+dwzjH+JefhvaeU" +
                    "lhJQSr33J0GMsRT9A3j7P3gEj+ARPIJHUFBACCnLPYAvAWPsSpn4SAiBMXYSpJSs" +
                    "taUE1tqU0knQdR0AKKWu0zMkAwEA5QZnjClevHIvegnuq47o37frH81sg91rI7H3" +
                    "AAAAAElFTkSuQmCC");
        }
    }
}
