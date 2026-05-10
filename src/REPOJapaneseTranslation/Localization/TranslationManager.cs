using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using REPOJapaneseTranslation;

namespace REPOJapaneseTranslation.Localization;

/// <summary>
/// DLL に埋め込まれた翻訳辞書の読み込みと検索を管理します。
/// </summary>
internal static class TranslationManager
{
    private static readonly Regex s_placeholderTokenRegex = new(
        @"\{[^{}]+\}|\[[^\[\]]+\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

    private static readonly Regex s_actionSuffixRegex = new(
        @"^(?<body>.+?)(?<suffix>\s*(?:<[^>]+>)*\[[^\[\]]+\](?:</[^>]+>)*\s*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

    private static readonly Regex s_richTextSuffixRegex = new(
        @"^(?<body>.+?)(?<suffix>\s*(?:<[^>]+>)+\s*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

    private static readonly Regex s_labeledPrefixRegex = new(
        @"^(?<leadingTags>(?:<[^>]+>)*)(?<label>[^<>]+?)(?<separator>\s*>\s*)(?<trailingTags>(?:</[^>]+>)*)(?<body>.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

    // 正規化された英語キー → 日本語値
    private static readonly Dictionary<string, string> s_translations = new(StringComparer.Ordinal);
    // ゲームが .ToUpper() してからセットする場合のフォールバック（例: "< Go Back" → "< GO BACK"）
    private static readonly Dictionary<string, string> s_translationsUpper = new(StringComparer.Ordinal);
    private static readonly List<TranslationTemplate> s_templates = new();
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

                if (s_placeholderTokenRegex.IsMatch(key))
                    s_templates.Add(TranslationTemplate.Create(NormalizeKey(key), value));
            }

            s_templates.Sort((left, right) => right.SourceLength.CompareTo(left.SourceLength));
        }
        catch (JsonException ex)
        {
            Plugin.Logger.LogError($"翻訳JSONのパースに失敗しました: {ex.Message}");
        }
    }

    /// <summary>
    /// 英語テキストを日本語に変換します。翻訳がない場合は元のテキストを返します。
    /// </summary>
    internal static string Translate(string text, bool logUntranslated = true)
    {
        if (!Plugin.EnableTranslation.Value || string.IsNullOrEmpty(text))
            return text;

        SplitOuterWhitespace(text, out string leadingWhitespace, out string coreText, out string trailingWhitespace);
        if (coreText.Length == 0)
            return text;

        if (TryTranslateCore(coreText, out string result))
            return leadingWhitespace + result + trailingWhitespace;

        if (logUntranslated && Plugin.LogUntranslated.Value)
            Plugin.Logger.LogDebug($"[未翻訳] \"{text.Replace("\n", "\\n")}\"");

        return text;
    }

    private static bool TryTranslateCore(string text, out string result)
    {
        if (TryTranslateInlineText(text, out result))
            return true;

        if (TryTranslateMultilineBlock(text, out result))
            return true;

        result = text;
        return false;
    }

    private static bool TryTranslateInlineText(string text, out string result)
    {
        if (TryTranslateLookup(text, out result))
            return true;

        if (TryTranslateTemplate(text, out result))
            return true;

        if (TryTranslateLabeledPrefix(text, out result))
            return true;

        if (TryTranslateRichTextSuffix(text, out result))
            return true;

        if (TryTranslateActionSuffix(text, out result))
            return true;

        if (TryTranslatePhraseSequence(text, out result))
            return true;

        result = text;
        return false;
    }

    private static bool TryTranslateLookup(string text, out string result)
    {
        if (s_translations.TryGetValue(text, out result!))
            return true;

        string normalized = NormalizeKey(text);
        if (normalized != text && s_translations.TryGetValue(normalized, out result!))
            return true;

        string upperNormalized = normalized.ToUpperInvariant();
        if (s_translationsUpper.TryGetValue(upperNormalized, out result!))
            return true;

        result = text;
        return false;
    }

    private static bool TryTranslateTemplate(string text, out string result)
    {
        string normalized = NormalizeKey(text);

        foreach (TranslationTemplate template in s_templates)
        {
            if (template.TryTranslate(normalized, out result))
                return true;
        }

        result = text;
        return false;
    }

    private static bool TryTranslateLabeledPrefix(string text, out string result)
    {
        Match match = s_labeledPrefixRegex.Match(text);
        if (!match.Success)
        {
            result = text;
            return false;
        }

        string leadingTags = match.Groups["leadingTags"].Value;
        string label = match.Groups["label"].Value;
        string separator = match.Groups["separator"].Value;
        string trailingTags = match.Groups["trailingTags"].Value;
        string body = match.Groups["body"].Value;

        if (!TryTranslateCore(body, out string translatedBody))
        {
            result = text;
            return false;
        }

        string translatedLabel = TryTranslateLookup(label, out string lookedUpLabel) ? lookedUpLabel : label;
        result = leadingTags + translatedLabel + separator + trailingTags + translatedBody;
        return true;
    }

    private static bool TryTranslateActionSuffix(string text, out string result)
    {
        Match match = s_actionSuffixRegex.Match(text);
        if (!match.Success)
        {
            result = text;
            return false;
        }

        string body = match.Groups["body"].Value;
        string suffix = match.Groups["suffix"].Value;

        if (!TryTranslateLookup(body, out string translatedBody)
            && !TryTranslateTemplate(body, out translatedBody)
            && !TryTranslatePhraseSequence(body, out translatedBody))
        {
            result = text;
            return false;
        }

        result = translatedBody + suffix;
        return true;
    }

    private static bool TryTranslateRichTextSuffix(string text, out string result)
    {
        Match match = s_richTextSuffixRegex.Match(text);
        if (!match.Success)
        {
            result = text;
            return false;
        }

        string body = match.Groups["body"].Value;
        string suffix = match.Groups["suffix"].Value;

        if (!TryTranslateLookup(body, out string translatedBody)
            && !TryTranslateTemplate(body, out translatedBody)
            && !TryTranslatePhraseSequence(body, out translatedBody))
        {
            result = text;
            return false;
        }

        result = translatedBody + suffix;
        return true;
    }

    private static bool TryTranslateMultilineBlock(string text, out string result)
    {
        if (!text.Contains('\n'))
        {
            result = text;
            return false;
        }

        string[] lines = text.Split('\n');
        bool changed = false;

        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            SplitOuterWhitespace(line, out string leadingWhitespace, out string coreText, out string trailingWhitespace);
            if (coreText.Length == 0)
                continue;

            if (!TryTranslateInlineText(coreText, out string translatedLine))
                continue;

            string updatedLine = leadingWhitespace + translatedLine + trailingWhitespace;
            if (updatedLine == line)
                continue;

            lines[index] = updatedLine;
            changed = true;
        }

        result = changed ? string.Join("\n", lines) : text;
        return changed;
    }

    private static bool TryTranslatePhraseSequence(string text, out string result)
    {
        result = text;

        if (!CanUsePhraseSegmentation(text))
            return false;

        SplitNumericPrefix(text, out string prefix, out string body);
        if (body.Length == 0)
            return false;

        string[] tokens = body.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2)
            return false;

        List<string> translatedFragments = new();
        int tokenIndex = 0;

        while (tokenIndex < tokens.Length)
        {
            if (!TryMatchLongestPhrase(tokens, tokenIndex, out int matchedTokenCount, out string translatedFragment))
                return false;

            translatedFragments.Add(translatedFragment);
            tokenIndex += matchedTokenCount;
        }

        if (translatedFragments.Count < 2)
            return false;

        result = prefix + string.Join("\n", translatedFragments);
        return true;
    }

    private static bool TryMatchLongestPhrase(
        IReadOnlyList<string> tokens,
        int startIndex,
        out int matchedTokenCount,
        out string translatedFragment)
    {
        int remainingTokenCount = tokens.Count - startIndex;
        int maxTokenCount = Math.Min(5, remainingTokenCount);

        for (int tokenCount = maxTokenCount; tokenCount >= 1; tokenCount--)
        {
            string candidate = string.Join(" ", tokens, startIndex, tokenCount);
            if (!TryTranslateLookup(candidate, out translatedFragment))
                continue;

            matchedTokenCount = tokenCount;
            return true;
        }

        matchedTokenCount = 0;
        translatedFragment = string.Empty;
        return false;
    }

    private static bool CanUsePhraseSegmentation(string text)
    {
        bool hasLetter = false;

        foreach (char character in text)
        {
            if (char.IsLetter(character))
            {
                hasLetter = true;
                continue;
            }

            if (char.IsDigit(character)
                || char.IsWhiteSpace(character)
                || character == '\''
                || character == '-'
                || character == '('
                || character == ')'
                || character == '&')
            {
                continue;
            }

            return false;
        }

        return hasLetter;
    }

    private static void SplitNumericPrefix(string text, out string prefix, out string body)
    {
        int index = 0;
        while (index < text.Length && char.IsDigit(text[index]))
            index++;

        if (index == 0 || index >= text.Length || !char.IsWhiteSpace(text[index]))
        {
            prefix = string.Empty;
            body = text;
            return;
        }

        while (index < text.Length && char.IsWhiteSpace(text[index]))
            index++;

        prefix = text[..index];
        body = text[index..];
    }

    private static void SplitOuterWhitespace(string text, out string leadingWhitespace, out string coreText, out string trailingWhitespace)
    {
        int start = 0;
        while (start < text.Length && char.IsWhiteSpace(text[start]))
            start++;

        int end = text.Length - 1;
        while (end >= start && char.IsWhiteSpace(text[end]))
            end--;

        leadingWhitespace = text[..start];
        coreText = start <= end ? text[start..(end + 1)] : string.Empty;
        trailingWhitespace = end + 1 < text.Length ? text[(end + 1)..] : string.Empty;
    }

    private static string NormalizeKey(string text)
    {
        return text.Replace("\r\n", "\n").Trim();
    }

    private sealed class TranslationTemplate
    {
        private readonly Regex _pattern;
        private readonly string _translatedTemplate;
        private readonly string[] _placeholderTokens;

        private TranslationTemplate(Regex pattern, string translatedTemplate, string[] placeholderTokens, int sourceLength)
        {
            _pattern = pattern;
            _translatedTemplate = translatedTemplate;
            _placeholderTokens = placeholderTokens;
            SourceLength = sourceLength;
        }

        internal int SourceLength { get; }

        internal static TranslationTemplate Create(string sourceTemplate, string translatedTemplate)
        {
            MatchCollection matches = s_placeholderTokenRegex.Matches(sourceTemplate);
            var patternBuilder = new StringBuilder();
            var placeholderTokens = new string[matches.Count];

            patternBuilder.Append('^');

            int previousIndex = 0;
            for (int index = 0; index < matches.Count; index++)
            {
                Match match = matches[index];
                patternBuilder.Append(Regex.Escape(sourceTemplate[previousIndex..match.Index]));
                patternBuilder.Append($"(?<p{index}>.+?)");

                placeholderTokens[index] = match.Value;
                previousIndex = match.Index + match.Length;
            }

            patternBuilder.Append(Regex.Escape(sourceTemplate[previousIndex..]));
            patternBuilder.Append('$');

            var pattern = new Regex(
                patternBuilder.ToString(),
                RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
            );

            return new TranslationTemplate(pattern, translatedTemplate, placeholderTokens, sourceTemplate.Length);
        }

        internal bool TryTranslate(string text, out string result)
        {
            Match match = _pattern.Match(text);
            if (!match.Success)
            {
                result = text;
                return false;
            }

            var placeholderValues = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < _placeholderTokens.Length; index++)
            {
                string token = _placeholderTokens[index];
                if (placeholderValues.ContainsKey(token))
                    continue;

                placeholderValues[token] = match.Groups[$"p{index}"].Value;
            }

            result = _translatedTemplate;
            foreach ((string token, string value) in placeholderValues)
                result = result.Replace(token, value, StringComparison.Ordinal);

            return true;
        }
    }
}
