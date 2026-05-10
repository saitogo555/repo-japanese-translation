using HarmonyLib;
using REPOJapaneseTranslation.Localization;

namespace REPOJapaneseTranslation.Patches;

/// <summary>
/// トラック画面のTaxmanラベルを日本語化するパッチ。
/// <para>
/// </summary>
[HarmonyPatch(typeof(TruckScreenText), nameof(TruckScreenText.UpdateTaxmanNickname))]
internal static class TruckScreenTextUpdateTaxmanNicknamePatch
{
    [HarmonyPrefix]
    private static void Prefix(ref string newName)
    {
        newName = TranslationManager.Translate(newName, logUntranslated: false);
    }
}

/// <summary>
/// トラック画面のテキスト("STARTING ENGINE" etc...)を日本語化するパッチ。
/// </summary>
[HarmonyPatch(typeof(TuckScreenLocked), nameof(TuckScreenLocked.LockChatToggle))]
internal static class TuckScreenLockedLockChatTogglePatch
{
    [HarmonyPrefix]
    private static void Prefix(ref string _lockedText)
    {
        _lockedText = TranslationManager.Translate(_lockedText, logUntranslated: false);
    }
}
