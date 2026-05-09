using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using REPOJapaneseTranslation;

namespace REPOJapaneseTranslation.Localization;

/// <summary>
/// ローカルの TTF ファイルから TMP フォールバックフォントを生成するクラス。
/// FontEngine.LoadFontFace(Font, int) をパッチしてバイト列ロードにリダイレクトすることで
/// TMP_FontAsset.CreateFontAsset と動的グリフロードの両方を機能させます。
/// </summary>
internal static class FontManager
{
    private const string TtfFileName = "NotoSansJP-Regular-subset.ttf";
    private const string FontsFolderName = "fonts";

    // ダミー Font オブジェクト → TTF バイト列。
    private static readonly Dictionary<Font, byte[]> SystemFontMap = new();

    private static TMP_FontAsset? s_japaneseFontAsset;
    private static readonly HashSet<TMP_FontAsset> s_patchedFonts = new();
    private static bool s_initialized;
    private static bool s_fontEnginePatched;

    /// <param name="pluginLocation">プラグイン DLL のフルパス（TTF 探索の基準パスになります）</param>
    internal static void Initialize(string pluginLocation)
    {
        if (s_initialized) return;
        s_initialized = true;
        if (!Plugin.EnableJapaneseFont.Value) return;

        try
        {
            string pluginDir = Path.GetDirectoryName(pluginLocation) ?? string.Empty;
            string ttfPath = Path.Combine(pluginDir, FontsFolderName, TtfFileName);

            if (!File.Exists(ttfPath))
            {
                Plugin.Logger.LogWarning($"日本語フォントファイルが見つかりません: {ttfPath}");
                Plugin.Logger.LogWarning($"plugins/REPOJapaneseTranslation/{FontsFolderName}/{TtfFileName} を配置してください。");
                return;
            }

            byte[] fontBytes = File.ReadAllBytes(ttfPath);
            Plugin.Logger.LogInfo($"フォントファイルを読み込みました: {TtfFileName} ({fontBytes.Length:N0} bytes)");

            PatchFontEngine();

            s_japaneseFontAsset = CreateFontAssetFromBytes(fontBytes);
            if (s_japaneseFontAsset == null)
            {
                Plugin.Logger.LogWarning("日本語フォントの作成に失敗しました。日本語文字が正しく表示されない可能性があります。");
                return;
            }

            Plugin.Logger.LogInfo($"日本語フォントを作成しました: {s_japaneseFontAsset.name}");
            TryAddToTMPGlobalFallback(s_japaneseFontAsset);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"フォントの初期化中にエラーが発生しました: {ex.Message}");
        }
    }

    /// <summary>
    /// シーン内の全 TMP_Text コンポーネントのフォントに日本語フォントをフォールバックとして追加します。
    /// </summary>
    internal static void ApplyFallbackToAllTextComponents()
    {
        if (s_japaneseFontAsset == null) return;

        TMP_Text[] allTexts = UnityEngine.Object.FindObjectsOfType<TMP_Text>(includeInactive: true);
        int patched = 0;

        foreach (TMP_Text tmpText in allTexts)
        {
            if (tmpText.font == null) continue;
            if (s_patchedFonts.Contains(tmpText.font)) continue;

            if (!tmpText.font.fallbackFontAssetTable.Contains(s_japaneseFontAsset))
            {
                tmpText.font.fallbackFontAssetTable.Add(s_japaneseFontAsset);
                s_patchedFonts.Add(tmpText.font);
                patched++;
            }
        }

        if (patched > 0)
            Plugin.Logger.LogDebug($"{patched} 個のフォントアセットにフォールバックを適用しました。");
    }

    private static void PatchFontEngine()
    {
        if (s_fontEnginePatched) return;

        try
        {
            var harmony = new Harmony("REPOJapaneseTranslation.FontEngineHook");
            var target = typeof(FontEngine).GetMethod(
                "LoadFontFace",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Font), typeof(int) },
                null
            );
            var prefix = typeof(FontEngineLoadFontFacePatch)
                .GetMethod("Prefix",
                           BindingFlags.NonPublic | BindingFlags.Static);

            if (target != null && prefix != null)
            {
                harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                s_fontEnginePatched = true;
                Plugin.Logger.LogDebug("FontEngine.LoadFontFace(Font, int) をパッチしました。");
            }
            else
            {
                Plugin.Logger.LogWarning("FontEngine.LoadFontFace(Font, int) のメソッドが見つかりませんでした。");
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogDebug($"FontEngine パッチ適用をスキップしました: {ex.Message}");
        }
    }

    private static TMP_FontAsset? CreateFontAssetFromBytes(byte[] fontBytes)
    {
        // フォントデータの事前検証
        FontEngine.InitializeFontEngine();
        FontEngineError verifyErr = FontEngine.LoadFontFace(fontBytes);
        if (verifyErr != FontEngineError.Success)
        {
            Plugin.Logger.LogWarning($"FontEngine.LoadFontFace(byte[]) 失敗: {verifyErr}");
            return null;
        }

        // ダミーの Font オブジェクトを作成し、Harmony パッチがバイト列でロードできるよう登録
        Font dummyFont = new Font("NotoSansJP-subset");
        SystemFontMap[dummyFont] = fontBytes;

        // CreateFontAsset を呼ぶ — パッチにより LoadFontFace(Font, int) がバイト列ロードにリダイレクトされる
        TMP_FontAsset? fontAsset;
        try
        {
            fontAsset = TMP_FontAsset.CreateFontAsset(dummyFont);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogDebug($"TMP_FontAsset.CreateFontAsset 例外: {ex.Message}");
            fontAsset = null;
        }

        if (fontAsset == null)
        {
            CleanupFailedDummyFont(dummyFont);
            Plugin.Logger.LogWarning("TMP_FontAsset を作成できませんでした。");
            return null;
        }

        fontAsset.name = "JapaneseFallback_NotoSansJP";
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        return fontAsset;
    }

    private static void CleanupFailedDummyFont(Font dummyFont)
    {
        SystemFontMap.Remove(dummyFont);
        UnityEngine.Object.Destroy(dummyFont);
    }

    private static void TryAddToTMPGlobalFallback(TMP_FontAsset fontAsset)
    {
        try
        {
            if (TMP_Settings.instance == null)
            {
                Plugin.Logger.LogDebug("TMP_Settings のインスタンスがまだ生成されていません。");
                return;
            }

            FieldInfo? field = typeof(TMP_Settings).GetField(
                "m_FallbackFontAssets",
                BindingFlags.NonPublic | BindingFlags.Instance
            );

            if (field?.GetValue(TMP_Settings.instance) is List<TMP_FontAsset> fallbackList)
            {
                if (!fallbackList.Contains(fontAsset))
                {
                    fallbackList.Add(fontAsset);
                    Plugin.Logger.LogInfo("TMP グローバルフォールバックリストに日本語フォントを追加しました。");
                }
            }
            else
            {
                Plugin.Logger.LogDebug("TMP_Settings.m_FallbackFontAssets が見つかりませんでした。個別適用で対応します。");
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogDebug($"TMP グローバルフォールバック設定をスキップしました: {ex.Message}");
        }
    }

    /// <summary>
    /// FontEngine.LoadFontFace(Font, int) をフックし、登録済みのダミー Font を
    /// LoadFontFace(byte[], int) にリダイレクトします。
    /// </summary>
    private static class FontEngineLoadFontFacePatch
    {
        private static bool Prefix(Font font, int pointSize, ref FontEngineError __result)
        {
            if (font == null || !SystemFontMap.TryGetValue(font, out byte[]? fontBytes))
                return true; // 登録されていない Font → オリジナルに委譲

            __result = FontEngine.LoadFontFace(fontBytes, pointSize);
            return false; // オリジナルをスキップ
        }
    }
}
