using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using REPOJapaneseTranslation.Localization;
using TMPro;

namespace REPOJapaneseTranslation.Patches;

/// <summary>
/// TMP_Text の主要な文字列更新経路にパッチを当て、
/// 英語テキストを日本語に翻訳します。
/// </summary>
[HarmonyPatch]
internal static class TextTranslationPatch
{
    /// <summary>
    /// text プロパティセッターと SetText 系メソッドを翻訳対象に含めます。
    /// </summary>
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? textSetter = AccessTools.PropertySetter(typeof(TMP_Text), nameof(TMP_Text.text));
        if (textSetter != null)
            yield return textSetter;

        foreach (MethodInfo method in AccessTools.GetDeclaredMethods(typeof(TMP_Text)))
        {
            if (method.Name != nameof(TMP_Text.SetText))
                continue;

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length == 0 || parameters[0].ParameterType != typeof(string))
                continue;

            yield return method;
        }
    }

    [HarmonyPrefix]
    private static void TranslateText(ref string __0)
    {
        __0 = TranslationManager.Translate(__0);
    }
}
