using HarmonyLib;
using REPOJapaneseTranslation.Localization;

namespace REPOJapaneseTranslation.Patches;

/// <summary>
/// シーン読み込み後に日本語フォントフォールバックを適用するパッチ。
/// フォント初期化は Plugin.Awake() で行われます。
/// </summary>
[HarmonyPatch]
internal static class TMPFontPatch
{
    /// <summary>
    /// RunManager.Awake はゲームの各シーン開始時に呼ばれます。
    /// シーン切り替え後に全 TMP_Text へフォールバックフォントを再適用します。
    /// </summary>
    [HarmonyPatch(typeof(RunManager), nameof(RunManager.Awake))]
    [HarmonyPostfix]
    private static void ApplyFontAfterSceneLoad()
    {
        FontManager.ApplyFallbackToAllTextComponents();
    }
}
