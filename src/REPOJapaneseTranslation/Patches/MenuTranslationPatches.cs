using HarmonyLib;
using REPOJapaneseTranslation.Localization;
using TMPro;

namespace REPOJapaneseTranslation.Patches;

/// <summary>
/// メニューページ生成後に、Prefab 初期値として既に入っている TMP テキストを翻訳します。
/// </summary>
internal static class MenuPageTranslator
{
    /// <summary>
    /// 指定されたメニューページ配下の TMP テキストとヘッダー名を翻訳します。
    /// </summary>
    internal static void TranslatePage(MenuPage? menuPage)
    {
        if (menuPage == null)
            return;

        // ページ Prefab のヘッダーは PageOpen の戻り時点ですでに設定済みで、
        // TMP_Text.text / SetText パッチを通らないため、現在値を直接翻訳します。
        menuPage.menuHeaderName = TranslationManager.Translate(menuPage.menuHeaderName, logUntranslated: false);

        TMP_Text[] texts = menuPage.GetComponentsInChildren<TMP_Text>(includeInactive: true);
        FontManager.ApplyFallbackToTextComponents(texts);

        foreach (TMP_Text text in texts)
            TranslateTextComponent(text);
    }

    private static void TranslateTextComponent(TMP_Text text)
    {
        if (text == null || string.IsNullOrEmpty(text.text))
            return;

        // ページ走査ではサーバー名など翻訳しないユーザー入力も見えるため、
        // 意図的に未翻訳ログへ出さないようにします。
        string translated = TranslationManager.Translate(text.text, logUntranslated: false);
        if (translated != text.text)
            text.text = translated;
    }
}

/// <summary>
/// MenuButton は buttonTextString を基準に毎フレーム表示文字列を戻す箇所があるため、
/// 表示用 TMP_Text だけでなく元文字列も先に翻訳しておきます。
/// </summary>
[HarmonyPatch(typeof(MenuButton), "Awake")]
internal static class MenuButtonTranslationPatch
{
    [HarmonyPrefix]
    private static void Prefix(MenuButton __instance)
    {
        if (__instance == null || string.IsNullOrEmpty(__instance.buttonTextString))
            return;

        __instance.buttonTextString = TranslationManager.Translate(__instance.buttonTextString);
    }
}

/// <summary>
/// メニューページのヘッダーやPrefab初期値は、プラグイン読み込み前に既に
/// TMP_Textへ入っているため、ページ生成後にページ配下をまとめて翻訳します。
/// </summary>
[HarmonyPatch(typeof(MenuManager), nameof(MenuManager.PageOpen), new[] { typeof(MenuPageIndex), typeof(bool) })]
internal static class MenuPageOpenTranslationPatch
{
    [HarmonyPostfix]
    private static void Postfix(MenuPage __result)
    {
        MenuPageTranslator.TranslatePage(__result);
    }
}

[HarmonyPatch(typeof(MenuPage), "Start")]
internal static class MenuPageStartTranslationPatch
{
    /// <summary>
    /// ページ生成後から Start までに初期化されたテキストを拾うため、Start 後にも翻訳します。
    /// </summary>
    [HarmonyPostfix]
    private static void Postfix(MenuPage __instance)
    {
        MenuPageTranslator.TranslatePage(__instance);
    }
}

/// <summary>
/// 1ボタンのポップアップに渡される文字列引数を翻訳します。
/// </summary>
[HarmonyPatch(typeof(MenuManager), nameof(MenuManager.PagePopUp))]
internal static class MenuPopUpTranslationPatch
{
    [HarmonyPrefix]
    private static void Prefix(
        ref string headerText,
        ref string bodyText,
        ref string buttonText)
    {
        headerText = TranslationManager.Translate(headerText);
        bodyText = TranslationManager.Translate(bodyText);
        buttonText = TranslationManager.Translate(buttonText);
    }
}

/// <summary>
/// 2択ポップアップに渡される見出し、本文、選択肢を翻訳します。
/// </summary>
[HarmonyPatch(typeof(MenuManager), nameof(MenuManager.PagePopUpTwoOptions))]
internal static class MenuTwoOptionPopUpTranslationPatch
{
    [HarmonyPrefix]
    private static void Prefix(
        ref string popUpHeader,
        ref string popUpText,
        ref string option1Text,
        ref string option2Text)
    {
        popUpHeader = TranslationManager.Translate(popUpHeader);
        popUpText = TranslationManager.Translate(popUpText);
        option1Text = TranslationManager.Translate(option1Text);
        option2Text = TranslationManager.Translate(option2Text);
    }
}
