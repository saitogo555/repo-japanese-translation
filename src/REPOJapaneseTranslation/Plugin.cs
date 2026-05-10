using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using REPOJapaneseTranslation.Localization;
using REPOJapaneseTranslation.Patches;

namespace REPOJapaneseTranslation;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    private readonly Harmony _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);

    public static Plugin Instance { get; private set; } = null!;
    public static new ManualLogSource Logger { get; private set; } = null!;

    internal static ConfigEntry<bool> EnableTranslation { get; private set; } = null!;
    internal static ConfigEntry<bool> EnableJapaneseFont { get; private set; } = null!;
    internal static ConfigEntry<bool> LogUntranslated { get; private set; } = null!;

#pragma warning disable IDE0051
    private void Awake()
#pragma warning restore IDE0051
    {
        Instance = this;
        Logger = base.Logger;

        EnableTranslation = Config.Bind(
            "General",
            "EnableTranslation",
            true,
            "テキストを日本語に翻訳するかどうか / Whether to translate text to Japanese");

        EnableJapaneseFont = Config.Bind(
            "General",
            "EnableJapaneseFont",
            true,
            "日本語フォントをフォールバックとして追加するかどうか / Whether to add a Japanese font as TMP fallback");

        LogUntranslated = Config.Bind(
            "Debug",
            "LogUntranslated",
            false,
            "未翻訳のテキストをログに出力するかどうか (開発者向け) / Log untranslated strings (for developers)");

        if (LogUntranslated.Value)
            Logger.LogInfo("デバッグモード有効: 未翻訳テキストのログ出力を有効化しました。");

        TranslationManager.Initialize();
        FontManager.Initialize(Info.Location);

        // このアセンブリ内の HarmonyPatch 属性付きクラスをまとめて登録します。
        // パッチ追加時に Plugin.cs 側の登録漏れを起こしにくくするためです。
        _harmony.PatchAll(typeof(TMPTextTranslationPatch).Assembly);

        Logger.LogInfo($"{MyPluginInfo.PLUGIN_NAME} v{MyPluginInfo.PLUGIN_VERSION} が読み込まれました！");
        Logger.LogInfo($"翻訳エントリ数: {TranslationManager.TranslationCount}");
    }
}
