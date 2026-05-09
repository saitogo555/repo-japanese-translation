using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using REPOJapaneseTranslation;

namespace REPOJapaneseTranslation.Localization;

/// <summary>
/// DLL に埋め込まれた翻訳辞書の読み込みと検索を管理します。
/// </summary>
internal static class TranslationManager
{
    // 正規化された英語キー → 日本語値
    private static readonly Dictionary<string, string> s_translations = new(StringComparer.Ordinal);
    // ゲームが .ToUpper() してからセットする場合のフォールバック（例: "< Go Back" → "< GO BACK"）
    private static readonly Dictionary<string, string> s_translationsUpper = new(StringComparer.Ordinal);
    private static bool s_initialized;

    internal static int TranslationCount => s_translations.Count;

    /// <summary>
    /// 埋め込みリソースから翻訳辞書を初期化します。
    /// </summary>
    internal static void Initialize()
    {
        if (s_initialized) return;
        s_initialized = true;

        Assembly assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "REPOJapaneseTranslation.translations.ja.json";

        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            Plugin.Logger.LogError($"埋め込みリソース '{resourceName}' が見つかりません。");
            return;
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        LoadJson(reader.ReadToEnd());

        Plugin.Logger.LogInfo($"翻訳辞書を読み込みました: {s_translations.Count} 件");
    }

    private static void LoadJson(string json)
    {
        try
        {
            var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            if (dict == null) return;

            foreach (var (key, value) in dict)
            {
                // _ または // で始まるキーはコメントとして無視
                if (string.IsNullOrEmpty(key) || key[0] == '_' || key.StartsWith("//", StringComparison.Ordinal))
                    continue;

                s_translations[key] = value;

                string upper = NormalizeKey(key).ToUpperInvariant();
                s_translationsUpper.TryAdd(upper, value);
            }
        }
        catch (JsonException ex)
        {
            Plugin.Logger.LogError($"翻訳JSONのパースに失敗しました: {ex.Message}");
        }
    }

    /// <summary>
    /// 英語テキストを日本語に変換します。翻訳がない場合は元のテキストを返します。
    /// </summary>
    internal static string Translate(string text)
    {
        if (!Plugin.EnableTranslation.Value || string.IsNullOrEmpty(text))
            return text;

        // 1. 完全一致
        if (s_translations.TryGetValue(text, out string? result))
            return result;

        // 2. 改行正規化して再検索
        string normalized = NormalizeKey(text);
        if (normalized != text && s_translations.TryGetValue(normalized, out result))
            return result;

        // 3. 大文字化フォールバック（ゲームが .ToUpper() してからセットするケース）
        string upperNormalized = normalized.ToUpperInvariant();
        if (s_translationsUpper.TryGetValue(upperNormalized, out result))
            return result;

        if (Plugin.LogUntranslated.Value)
            Plugin.Logger.LogDebug($"[未翻訳] \"{text.Replace("\n", "\\n")}\"");

        return text;
    }

    private static string NormalizeKey(string text)
    {
        return text.Replace("\r\n", "\n").Trim();
    }
}
