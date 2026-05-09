# REPO Japanese Language Mod — 開発手順ドキュメント

このドキュメントは、R.E.P.O. 向け日本語化Mod「REPO Japanese Language」を開発した際の
調査手順・技術的判断・ファイル構成をまとめたものです。

## 目次

1. [プロジェクト概要](#1-プロジェクト概要)
2. [解析したファイル](#2-解析したファイル)
3. [ゲームのテキスト表示構造の調査](#3-ゲームのテキスト表示構造の調査)
4. [翻訳システムの設計](#4-翻訳システムの設計)
5. [フォントシステムの調査と修正](#5-フォントシステムの調査と修正)
6. [翻訳エントリの収集方法](#6-翻訳エントリの収集方法)
7. [詰まったポイントと解決策](#7-詰まったポイントと解決策)
8. [ファイル構成](#8-ファイル構成)
9. [ビルドとデプロイ手順](#9-ビルドとデプロイ手順)

## 1. プロジェクト概要

| 項目 | 内容 |
|||
| ゲーム | R.E.P.O. (Steam App ID: 3241660) |
| Unity バージョン | 2022.3.67f2 |
| BepInEx | 5.4.23.5 |
| REPOLib | 4.0.0 |
| TextMeshPro | 3.0.7 |
| 翻訳エントリ数 | 648+ |

## 2. 解析したファイル

### ゲームインストールフォルダ構造

```
G:\Steam\steamapps\common\REPO\
├── REPO.exe
├── REPO_Data\
│   ├── Managed\
│   │   ├── Assembly-CSharp.dll          ← ゲーム本体のC#コード
│   │   ├── Unity.TextMeshPro.dll        ← TMPro テキストレンダリング
│   │   └── UnityEngine.*.dll
│   └── StreamingAssets\
│       └── aa\
│           └── StandaloneWindows64\
│               ├── localization-string-tables-english(unitedstates)(en-us)_assets_all.bundle
│               │                          ← ★ ゲームの全UIテキストが入ったバンドル
│               └── *.bundle
└── BepInEx\
    └── plugins\
        └── REPOJapaneseTranslation\
            ├── REPOJapaneseTranslation.dll
            └── fonts\
                └── NotoSansJP-Regular-subset.ttf
```

### 解析した主要ファイル

#### `Assembly-CSharp.dll`
- ゲーム本体のC#コードが入ったDLL
- `strings` コマンドで文字列を抽出し、UIに使われているクラス名・メソッド名を特定
- 重要クラスの発見:
  - `LocalizationManager` — ゲーム自身のローカライゼーション管理クラス（**クラス名衝突に注意**）
  - `TMP_Text` — TextMeshProのテキストコンポーネント
  - セーブ/ロード関係の文字列

```bash
# 解析コマンド例
strings REPO_Data/Managed/Assembly-CSharp.dll | grep -i "locali\|translate\|toupper"
```

#### `localization-string-tables-english(unitedstates)(en-us)_assets_all.bundle`
- R.E.P.O. がゲーム内UIテキストを Unity Localization パッケージで管理していることを発見
- **UnityFS形式**、LZ4HC圧縮
- 400+ の英語UIテキストが収録されている
- Pythonスクリプトで手動デコードして全テキストを抽出 → `ja.json` の翻訳元にした

```
バンドル構造:
magic(8B) + format_ver(4B) + unity_ver(可変) + unity_rev(可変)
+ bundle_size(8B) + ci_size(4B) + ui_size(4B) + flags(4B)
→ 16バイト境界にアラインされたオフセット = 64
→ CIブロック: hash(16B) + block_count(4B) + [uc_size+c_size+flags] × N + ノード情報
→ データブロック: LZ4HCで圧縮 (flags & 0x3f == 3)
```

## 3. ゲームのテキスト表示構造の調査

### TextMeshPro (TMP) の使用確認

R.E.P.O. は全UIテキストに **TextMeshPro (TMP)** を使用している。  
TMP テキストコンポーネントの `text` プロパティ セッターが全テキスト変更の窓口になっている。

```csharp
// TMP_Text.text setter が呼ばれるたびにテキストが更新される
someLabel.text = "Settings";
```

### Unity Localization パッケージの確認

ゲームは **Unity の Localization パッケージ** (`com.unity.localization`) を使用して、
文字列テーブル (`StringTable`) をAddressable Assetsとして配布している。

- バンドルに `Game_en-US`、`HUD_en-US`、`Menu_en-US` の3テーブルが存在
- ランタイムに `LocalizeStringEvent` コンポーネントがTMPコンポーネントを更新する

### ToUpper() 問題

一部のUIテキスト（`< GO BACK`、`(CLICK TO RENAME)` 等）はゲーム内で
`.ToUpper()` してからTMPに設定されていることを確認。

```bash
strings Assembly-CSharp.dll | grep -i "toupper"
# → "ToUpper" が存在することを確認
```

ロケールバンドルでは `"< Go Back"` として保存されているが、
実際にTMPに設定されるのは `"< GO BACK"` という大文字形式。

## 4. 翻訳システムの設計

### アーキテクチャ

```
[ゲームコード]
    ↓ someLabel.text = "Settings"
[Harmony Prefix Patch on TMP_Text.text setter]
    ↓ TranslationManager.Translate("Settings")
    ↓ → "設定"
[TMP_Text にセット]
    ↓ 日本語で表示
```

### TranslationManager の仕組み

`src/REPOJapaneseTranslation/Localization/TranslationManager.cs`

1. **起動時**: 組み込みリソース `translations/ja.json` を読み込む
2. **登録時**: 原文キーに加えて、正規化後に `.ToUpperInvariant()` したキーも保持する
3. **翻訳時**: 3段階のフォールバック検索
   - ① 完全一致 (Ordinal)
   - ② 改行正規化後に完全一致
   - ③ 大文字化キーで一致（ToUpper問題への対処）

```csharp
string normalized = text.Replace("\r\n", "\n").Trim();
string upperNormalized = normalized.ToUpperInvariant();

if (_translations.TryGetValue(text, out translated)) return translated;
if (normalized != text && _translations.TryGetValue(normalized, out translated)) return translated;
if (_translationsUpper.TryGetValue(upperNormalized, out translated)) return translated;
```

### Harmony パッチ

`src/REPOJapaneseTranslation/Patches/TextTranslationPatch.cs`

```csharp
[HarmonyPatch(typeof(TMP_Text))]
[HarmonyPatch("text", MethodType.Setter)]
[HarmonyPrefix]
private static void TranslateText(ref string value)
{
    value = TranslationManager.Translate(value);
}
```

**なぜ `text` セッターをパッチするか**:  
Unity Localization の `LocalizeStringEvent` も最終的に `TMP_Text.text = ...` を呼ぶため、
ここを一箇所パッチするだけで全テキスト変更をインターセプトできる。

## 5. フォントシステムの調査と修正

### 問題: 同梱 TTF から TMP_FontAsset を安定して生成したい

`TMP_FontAsset.CreateFontAsset(Font)` は内部で `FontEngine.LoadFontFace(Font, int)` を呼ぶため、
単純にダミーの `Font` を渡すだけではフォントバイト列に辿れず失敗することがある。

**原因の特定**:
- 日本語フォントは `fonts/NotoSansJP-Regular-subset.ttf` としてプラグインに同梱している
- `TMP_FontAsset.CreateFontAsset(Font)` は `Font` インスタンスからフォントデータを直接引けないと失敗する
- 動的グリフ追加時も同じ `LoadFontFace(Font, int)` 経路が再利用される

### 解決策: ダミー Font を TTF バイト列にリダイレクト

TTF をバイト列で読み込み、ダミー `Font` と対応付けてから
`FontEngine.LoadFontFace(Font, int)` を `LoadFontFace(byte[], int)` にリダイレクトする。

```csharp
private static bool Prefix(Font font, int pointSize, ref FontEngineError __result)
{
    if (font == null || !SystemFontMap.TryGetValue(font, out byte[] fontBytes))
        return true;

    __result = FontEngine.LoadFontFace(fontBytes, pointSize);
    return false;
}
```

**なぜこの方法が有効か**:  
初回の `TMP_FontAsset.CreateFontAsset()` だけでなく、TMPが新しい日本語グリフを動的に追加する際も同じパスを通るため、
漢字・ひらがな・カタカナの動的レンダリングが機能する。

## 6. 翻訳エントリの収集方法

### Step 1: ゲームのロケールバンドルを抽出

```python
# UnityFS バンドルのLZ4HC圧縮を手動デコードするPythonスクリプト
# bundle header → CI block → data block の順で解析

def lz4_decompress_block(src, expected_size):
    # 純粋Pythonで実装したLZ4ブロックデコーダー
    ...

bundle = open('localization-string-tables-english(unitedstates)(en-us)_assets_all.bundle', 'rb').read()
# CIブロック: offset=64, size=65B → 展開後91B
# データブロック: offset=129, c_size=12352B → uc_size=28936B
decompressed = lz4_decompress_block(bundle[129:129+12352], 28936)
```

### Step 2: 文字列の抽出

展開したバイナリから正規表現で可読文字列を抽出:

```python
import re
for m in re.finditer(rb'[ -~]{3,}', decompressed):
    s = m.group().decode('latin1').strip()
    # → "Settings", "Load Save", "(Click to Rename)" など400+エントリ
```

### Step 3: 既存ja.jsonと比較して差分を特定

```python
existing = set(json.load(open('ja.json')).keys())
extracted = { ... }  # バンドルから抽出したセット
missing = extracted - existing
# → 335件の未翻訳エントリを特定
```

### Step 4: 翻訳・ja.jsonに追加

抽出した英語テキストを日本語訳して `ja.json` に追加。  
翻訳のカテゴリ:
- メニュー・ロビー・マルチプレイヤー
- セーブ/ロードシステム
- グラフィックス・オーディオ・コントロール設定
- ゲームプレイ設定（カメラ、感度等）
- HUD（月フェーズ、撤収ポイント、アップグレード名）
- アイテム名・敵名・レベル名
- コスメティクス（ボディパーツ全種）
- チュートリアル・確認ダイアログの長文
- 地域名（Photon サーバー選択）

## 7. 詰まったポイントと解決策

### ① クラス名衝突

**問題**: ゲーム本体に `LocalizationManager` クラスが存在するため、
Modの同名クラスがコンパイルエラーになる。

**解決**: Modのクラスを `TranslationManager` にリネーム。

### ② NuGet パッケージバージョン衝突

**問題**: `Newtonsoft.Json` を最新版で追加するとREPOLibと衝突してクラッシュ。

**解決**: REPOLib v4.0.0 が要求するバージョン `13.0.4` を厳密に使用する。

```xml
<PackageReference Include="Newtonsoft.Json" Version="13.0.4" />
```

### ③ TMP が null を返す（フォント問題）

**問題**: `TMP_FontAsset.CreateFontAsset(Font)` が `null` を返す。

**原因**: ダミー `Font` 単体では、同梱した TTF バイト列を `FontEngine` に渡せない。

**解決**: Harmony で `FontEngine.LoadFontFace(Font, int)` をパッチして
`SystemFontMap` に登録したバイト列へリダイレクトする。

### ④ 大文字テキストが翻訳されない

**問題**: `"< GO BACK"`, `"(CLICK TO RENAME)"`, `"TOTAL HAUL:"` が翻訳されない。

**原因**: ゲームが `.ToUpper()` した後でTMPに文字列をセットするため、
バンドルの `"< Go Back"` とは大文字小文字が一致しない。

**解決**: 登録時に正規化済みキーの `.ToUpperInvariant()` を
`_translationsUpper` に保持し、`Translate()` 側でも正規化後に大文字化して検索する。

```csharp
_translationsUpper[NormalizeKey(kv.Key).ToUpperInvariant()] = kv.Value;
// ...
if (_translationsUpper.TryGetValue(normalized.ToUpperInvariant(), out translated)) return translated;
```

## 8. ファイル構成

```
src/
└── REPOJapaneseTranslation/
    ├── REPOJapaneseTranslation.csproj
    ├── Plugin.cs
    ├── config/
    │   └── REPOJapaneseTranslation.cfg
    ├── fonts/
    │   └── NotoSansJP-Regular-subset.ttf
    ├── Localization/
    │   ├── TranslationManager.cs
    │   └── FontManager.cs
    ├── Patches/
    │   ├── TextTranslationPatch.cs
    │   └── TMPFontPatch.cs
    └── translations/
        └── ja.json
```

### 各ファイルの役割

#### `Plugin.cs`
- BepInEx `BaseUnityPlugin` を継承するエントリポイント
- BepInEx Config で以下の設定値を公開:
  - `EnableTranslation` (bool, default: true)
  - `EnableJapaneseFont` (bool, default: true)
  - `LogUntranslated` (bool, default: false) — 未翻訳テキストをログ出力

#### `TranslationManager.cs`
- `ja.json` から翻訳辞書を構築
- 組み込みリソースを読み込み、完全一致・正規化・大文字化の3経路で検索する
- 3段階フォールバック検索（完全一致 → 正規化 → 大文字化）

#### `FontManager.cs`
- 同梱 TTF から TMP FontAsset を動的生成
- Harmony パッチで `FontEngine.LoadFontFace(Font, int)` を `LoadFontFace(byte[], int)` にリダイレクト
- 全TMPテキストコンポーネントのフォントへ日本語フォールバックを追加する
- ダミー `Font` と TTF バイト列を内部マップで紐付ける

#### `TextTranslationPatch.cs`
- `TMP_Text.text` セッターへのHarmonyプレフィックスパッチ
- 全テキスト変更をインターセプトして `TranslationManager.Translate()` を呼ぶ

#### `TMPFontPatch.cs`
- シーン読み込み後に `FontManager.ApplyFallbackToAllTextComponents()` を呼ぶ
- シーン切り替え後の TMP フォントにも日本語フォールバックを再適用する

#### `ja.json`
- キー: 英語テキスト（完全一致）
- 値: 日本語訳
- `_` または `//` で始まるキーはコメントとして無視される
- 組み込みリソースとしてDLLに埋め込まれる翻訳ソース



## 9. ビルドとデプロイ手順

### 必要な環境

- .NET SDK 6.0以上
- WSL (Windows Subsystem for Linux) または Windows コマンドプロンプト

### ビルド

```bash
cd /path/to/repo-jp-lang
dotnet build src/REPOJapaneseTranslation/REPOJapaneseTranslation.csproj -c Release
# → src/REPOJapaneseTranslation/bin/Release/netstandard2.1/REPOJapaneseTranslation.dll
```

### デプロイ

生成されたDLLとフォントファイルを以下の2箇所にコピー:

```bash
# ゲームルート（直接起動時）
cp src/REPOJapaneseTranslation/bin/Release/netstandard2.1/REPOJapaneseTranslation.dll \
    "G:/Steam/steamapps/common/REPO/BepInEx/plugins/REPOJapaneseTranslation/"
cp src/REPOJapaneseTranslation/fonts/NotoSansJP-Regular-subset.ttf \
    "G:/Steam/steamapps/common/REPO/BepInEx/plugins/REPOJapaneseTranslation/fonts/"

# Thunderstore Mod Manager プロファイル
cp src/REPOJapaneseTranslation/bin/Release/netstandard2.1/REPOJapaneseTranslation.dll \
    "C:/Users/{ユーザー}/AppData/Roaming/Thunderstore Mod Manager/DataFolder/REPO/profiles/Default/BepInEx/plugins/REPOJapaneseTranslation/"
cp src/REPOJapaneseTranslation/fonts/NotoSansJP-Regular-subset.ttf \
    "C:/Users/{ユーザー}/AppData/Roaming/Thunderstore Mod Manager/DataFolder/REPO/profiles/Default/BepInEx/plugins/REPOJapaneseTranslation/fonts/"
```

### 翻訳の更新

翻訳辞書はDLLに埋め込まれているため、
`src/REPOJapaneseTranslation/translations/ja.json` を編集した後に再ビルドして反映する。

### 未翻訳テキストの確認

`BepInEx/config/REPOJapaneseTranslation.cfg` で
`LogUntranslated = true` に設定すると、翻訳されなかったテキストが
`BepInEx/LogOutput.log` に `[未翻訳]` タグ付きで出力される。

## 参考リンク

- [REPOLib GitHub](https://github.com/ZehsTeam/REPOLib) — BepInEx helper library for R.E.P.O.
- [BepInEx 5 ドキュメント](https://docs.bepinex.dev/articles/user_guide/installation/index.html)
- [HarmonyX ドキュメント](https://harmony.pardeike.net/articles/intro.html)
- [TextMeshPro ドキュメント](https://docs.unity3d.com/Packages/com.unity.textmeshpro@3.0/manual/index.html)
- [Unity Localization パッケージ](https://docs.unity3d.com/Packages/com.unity.localization@1.5/manual/index.html)
