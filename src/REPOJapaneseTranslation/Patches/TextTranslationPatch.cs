using HarmonyLib;
using REPOJapaneseTranslation.Localization;
using TMPro;

namespace REPOJapaneseTranslation.Patches;

/// <summary>
/// TMP_Text の text プロパティセッターにパッチを当て、
/// 英語テキストを日本語に翻訳します。
/// </summary>
[HarmonyPatch(typeof(TMP_Text))]
internal static class TextTranslationPatch
{
    /// <summary>
    /// text プロパティが設定される直前に翻訳を適用します。
    /// </summary>
    [HarmonyPatch("text", MethodType.Setter)]
    [HarmonyPrefix]
    private static void TranslateText(ref string value)
    {
        if (value == null) return;
        value = TranslationManager.Translate(value);
    }
}
