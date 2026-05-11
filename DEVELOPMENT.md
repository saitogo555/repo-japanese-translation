# REPO Japanese Translation Mod — 開発メモ

このドキュメントは、R.E.P.O. 向け日本語化Mod「REPO Japanese Translation」を開発した際の
調査内容、実装方針、ファイル構成、ビルド手順を整理したものです。

本Modでは、ゲーム内UIテキストの日本語化と、日本語表示に必要なフォント対応を行っています。

## 目次

1. [プロジェクト概要](#1-プロジェクト概要)
2. [調査対象ファイル](#2-調査対象ファイル)
3. [テキスト表示の調査](#3-テキスト表示の調査)
4. [翻訳システムの設計](#4-翻訳システムの設計)
5. [フォント処理の調査と対応](#5-フォント処理の調査と対応)
6. [翻訳エントリの収集方法](#6-翻訳エントリの収集方法)
7. [詰まった点と対応内容](#7-詰まった点と対応内容)
8. [ファイル構成](#8-ファイル構成)
9. [ビルドとデプロイ](#9-ビルドとデプロイ)

## 1. プロジェクト概要

| 項目 | 内容 |
|---|---|
| ゲーム | R.E.P.O. (Steam App ID: 3241660) |
| Unity バージョン | 2022.3.67f2 |
| BepInEx | 5.4.23.5 |
| TextMeshPro | 3.0.7 |
| 翻訳エントリ数 | 660件 |

## 2. 調査対象ファイル

### ゲームインストールフォルダ構造

以下はゲームフォルダを `<GAME_DIR>` とした相対的な構成例です。Steam のインストール先や Mod Manager のプロファイル場所は環境ごとに異なるため、実際の保存先に読み替えてください。

```text
<GAME_DIR>\
├── REPO.exe
├── REPO_Data\
│   ├── Managed\
│   │   ├── Assembly-CSharp.dll
│   │   ├── Unity.TextMeshPro.dll
│   │   └── UnityEngine.*.dll
│   └── StreamingAssets\
│       └── aa\
│           └── StandaloneWindows64\
│               ├── localization-string-tables-english(unitedstates)(en-us)_assets_all.bundle
│               └── *.bundle
```

### 主要ファイル

#### `Assembly-CSharp.dll`
- ゲーム本体の C# コードが含まれる DLL
- `strings` コマンドで文字列を抽出し、UI 関連のクラス名やメソッド名を調査
- 調査中に確認できた主なクラス・要素:
  - `LocalizationManager` — ゲーム側のローカライズ管理クラス
  - `TMP_Text` — TextMeshPro のテキストコンポーネント
  - セーブ / ロード関連の文字列群

```bash
strings <GAME_DIR>/REPO_Data/Managed/Assembly-CSharp.dll | grep -i "locali\|translate\|toupper"
```

#### `localization-string-tables-english(unitedstates)(en-us)_assets_all.bundle`
- UI テキストが Unity Localization ベースで管理されていることを確認
- 形式は UnityFS、圧縮は LZ4HC
- 英語 UI テキストを多数収録
- Python スクリプトで手動展開し、翻訳元データを抽出して `ja.json` 作成に利用

#### `BepInEx/LogOutput.log`
- プラグイン起動ログと `LogUntranslated` の出力先
- 現在の翻訳エントリ数や、実プレイ時の未翻訳文字列の確認に使用

#### `BepInEx/config/ja/Text/TranslationJP_Upgrade.txt`
- 旧 xUnity AutoTranslator ベースの翻訳ダンプ
- マップのアップグレード一覧が逐次追記された複数行文字列として再描画されている痕跡を確認する補助資料
- 例: `体力 STAMINA=体力\nスタミナ`

```text
バンドル構造:
magic(8B) + format_ver(4B) + unity_ver(可変) + unity_rev(可変)
+ bundle_size(8B) + ci_size(4B) + ui_size(4B) + flags(4B)
→ 16バイト境界にアラインされたオフセット = 64
→ CIブロック: hash(16B) + block_count(4B) + [uc_size+c_size+flags] × N + ノード情報
→ データブロック: LZ4HCで圧縮 (flags & 0x3f == 3)
```

## 3. テキスト表示の調査

### TextMeshPro の使用

調査した範囲では、R.E.P.O. の UI テキスト表示には TextMeshPro 系コンポーネントが使われていました。

また、主要なテキスト更新は `TMP_Text.text` セッターに加えて `TMP_Text.SetText(...)` 系メソッド経由でも反映されており、
両方を翻訳処理の差し込みポイントとして扱う必要があることを確認しました。

```csharp
someLabel.text = "Settings";
someLabel.SetText("Settings");
```

### Unity Localization の利用

ゲームでは Unity Localization パッケージ由来と見られる文字列テーブルが使われており、
`Game_en-US`、`HUD_en-US`、`Menu_en-US` などのテーブルを確認しました。

調査した挙動では、ローカライズ結果は最終的に TextMeshPro 側のテキスト更新へ反映されていました。

### 大文字化による不一致

一部の UI テキストは、ゲーム側で `.ToUpper()` を適用した後に表示されていました。

そのため、ロケールデータ内の原文が `"< Go Back"` であっても、
実際の表示時には `"< GO BACK"` となり、単純な完全一致では翻訳に失敗するケースがありました。

```bash
strings Assembly-CSharp.dll | grep -i "toupper"
```

### Prefab 初期値として入っているメニュー見出し

`PUBLIC GAME` や `SERVER LIST` のようなオレンジ色のメニュー見出しは、
`TMP_Text.text` セッターや `TMP_Text.SetText(...)` で実行時に代入される文字列ではありませんでした。

`Assembly-CSharp.dll` を確認すると、メニューページは `MenuManager.PageOpen(...)` で Prefab から生成され、
その時点で `MenuPage.menuHeader` の `TMP_Text.text` には Prefab 初期値が既に入っています。

そのため、この種類の文字列は「代入の瞬間を Harmony で捕捉する」方法だけでは翻訳できません。
ページ生成後に `MenuPage` 配下の `TMP_Text` を走査し、現在値を翻訳し直す必要があります。

## 4. 翻訳システムの設計

### 基本構成

```text
[ゲームコード]
    ↓ someLabel.text = "Settings"
    ↓ または someLabel.SetText("Settings")
[Harmony Prefix Patch on TMP_Text.text setter / SetText]
    ↓ TranslationManager.Translate("Settings")
    ↓ → "設定"
[TMP_Text にセット]
    ↓ 日本語で表示
```

### `TranslationManager` の役割

`src/Localization/TranslationManager.cs`

起動時に組み込みリソース `translations/ja.json` を読み込み、
原文キーと正規化済みキーを辞書として保持します。現在は通常の辞書検索に加えて、
実行時に組み立てられる文字列にも対応するための補助ロジックを持っています。

翻訳時は、次の順でフォールバック検索を行います。

1. 完全一致
2. 改行正規化後の一致
3. 大文字化したキーでの一致
4. プレースホルダ付きテンプレートの一致
5. `FOCUS > ...` のような接頭辞付き表示の本体翻訳
6. 末尾の操作ヒント付き表示の本体翻訳
7. リッチテキスト接尾辞付き表示の本体翻訳
8. 複数行ブロックの行単位翻訳
9. 全大文字の複合語列を既知語へ分解して翻訳

```csharp
if (TryTranslateLookup(text, out result)) return result;
if (TryTranslateTemplate(text, out result)) return result;
if (TryTranslateLabeledPrefix(text, out result)) return result;
if (TryTranslateRichTextSuffix(text, out result)) return result;
if (TryTranslateActionSuffix(text, out result)) return result;
if (TryTranslatePhraseSequence(text, out result)) return result;
```

プレースホルダ付きテンプレートは、`Hold to Reroll ({cost})` のような辞書キーから
正規表現テンプレートを作り、実表示の `HOLD TO REROLL (-$5K)` のような値入り文字列にも一致させます。

また、`Medium Health Pack (50) [E]` のようなアイテム名と操作ヒントの結合文字列、
`1 CROUCH REST HEALTH` のようなマップ上のアップグレード一覧も翻訳対象に含めています。

```csharp
private static readonly Regex s_placeholderTokenRegex = new(@"\{[^{}]+\}|\[[^\[\]]+\]");
private static readonly Regex s_actionSuffixRegex = new(
    @"^(?<body>.+?)(?<suffix>\s*(?:<[^>]+>)*\[[^\[\]]+\](?:</[^>]+>)*\s*)$"
);
```

### Harmony パッチ

`src/Patches/TMPTextTranslationPatch.cs`

```csharp
[HarmonyTargetMethods]
[HarmonyPrefix]
private static void TranslateText(ref string __0)
{
    __0 = TranslationManager.Translate(__0);
}
```

`TMP_Text.text` セッターだけでなく、`TMP_Text.SetText(...)` 系メソッドも対象にすることで、
調査範囲で確認できた主要な UI テキスト更新をまとめて捕捉できました。

```csharp
[HarmonyTargetMethods]
private static IEnumerable<MethodBase> TargetMethods()
{
    ...
}

[HarmonyPrefix]
private static void TranslateText(ref string __0)
{
    __0 = TranslationManager.Translate(__0);
}
```

`src/Patches/MenuTranslationPatches.cs`

メニュー系 UI には、TMP 更新経路とは別に、ゲーム独自の文字列保持フィールドがあります。
そのため、次の専用パッチを分離して管理しています。

- `MenuPageOpenTranslationPatch`
  - `MenuManager.PageOpen(MenuPageIndex, bool)` の戻り値である `MenuPage` を受け取り、ページ配下の既存 `TMP_Text` を翻訳
- `MenuPageStartTranslationPatch`
  - `MenuPage.Start` 後にも同じ走査を実行し、ページ生成後に初期化される値を拾う
- `MenuButtonTranslationPatch`
  - `MenuButton.buttonTextString` を翻訳し、毎フレーム表示文字列を戻す処理に対応
- `MenuPopUpTranslationPatch`
  - `MenuManager.PagePopUp(...)` の引数を翻訳
- `MenuTwoOptionPopUpTranslationPatch`
  - `MenuManager.PagePopUpTwoOptions(...)` の見出し、本文、選択肢を翻訳

ページ配下の走査ではサーバー名などのユーザー入力文字列も見えるため、
`TranslationManager.Translate(..., logUntranslated: false)` を使って未翻訳ログを抑制しています。

## 5. フォント処理の調査と対応

### 問題

同梱した TTF ファイルから、TextMeshPro 用の `TMP_FontAsset` を安定して生成する必要がありました。

しかし、`TMP_FontAsset.CreateFontAsset(Font)` は内部で `FontEngine.LoadFontFace(Font, int)` を利用するため、
ダミーの `Font` を渡すだけではフォント実体に到達できず、生成に失敗するケースがありました。

### 原因

- 日本語フォントは `fonts/NotoSansJP-Regular-subset.ttf` として同梱
- `CreateFontAsset(Font)` は `Font` インスタンスからフォントデータを解決できないと失敗する
- 動的グリフ追加時も同じ経路が再利用される

### 対応方法

TTF をバイト列として読み込み、ダミー `Font` と関連付けたうえで、
`FontEngine.LoadFontFace(Font, int)` を `LoadFontFace(byte[], int)` にリダイレクトしました。

```csharp
private static bool Prefix(Font font, int pointSize, ref FontEngineError __result)
{
    if (font == null || !SystemFontMap.TryGetValue(font, out byte[] fontBytes))
        return true;

    __result = FontEngine.LoadFontFace(fontBytes, pointSize);
    return false;
}
```

### この方式を採用した理由

この方法であれば、初回の `TMP_FontAsset.CreateFontAsset()` だけでなく、
TextMeshPro による動的グリフ追加時にも同じフォントデータを使えます。

その結果、ひらがな・カタカナ・漢字を含む日本語表示を安定して扱えるようになりました。

### TTF ファイルと翻訳処理の関係

翻訳処理自体は文字列置換なので、`NotoSansJP-Regular-subset.ttf` が無くても動作します。
ただし、日本語が表示できるかどうかは、対象の TMP フォントアセットまたは TMP フォールバックに
日本語グリフが存在するかに依存します。

TTF 無しでも日本語が表示される場合は、次のいずれかが起きています。

- ゲーム側の対象フォントアセットが既に日本語グリフを持っている
- TMP のグローバルフォールバックに日本語表示可能なフォントがある
- 別Modや実行環境側がフォントフォールバックを追加している

同梱 TTF は、環境差や画面ごとのフォント差で豆腐文字や欠落が出ることを避けるための保険です。
確認済みの画面で不要に見えても、配布物から削除するのは非推奨です。

## 6. 翻訳エントリの収集方法

### Step 1: ロケールバンドルの展開

```python
def lz4_decompress_block(src, expected_size):
    ...

bundle = open('localization-string-tables-english(unitedstates)(en-us)_assets_all.bundle', 'rb').read()
decompressed = lz4_decompress_block(bundle[129:129+12352], 28936)
```

UnityFS バンドルを手動で解析し、LZ4HC 圧縮されたデータブロックを展開しました。

### Step 2: 可読文字列の抽出

```python
import re

for m in re.finditer(rb'[ -~]{3,}', decompressed):
    s = m.group().decode('latin1').strip()
```

展開後のバイナリから可読文字列を抽出し、
設定画面・ロビー・セーブ画面などで使われる UI 文言を収集しました。

### Step 3: 既存翻訳との差分確認

```python
existing = set(json.load(open('ja.json')).keys())
extracted = { ... }
missing = extracted - existing
```

既存の `ja.json` と比較し、未翻訳の文字列を洗い出しました。

### Step 4: 翻訳の追加

抽出した英語テキストを日本語化し、`ja.json` に反映しました。

主な対象カテゴリは次のとおりです。

- メニュー、ロビー、マルチプレイヤー
- セーブ / ロード関連
- グラフィックス、オーディオ、コントロール設定
- カメラ、感度などのゲームプレイ設定
- HUD 表示
- アイテム名、敵名、レベル名
- コスメティクス名称
- チュートリアル、確認ダイアログ
- Photon サーバー選択時の地域名

### Step 5: 実行時に組み立てられる文字列の調査

静的な string table の差分比較だけでは、実プレイ中に組み立てられる表示文字列を取りこぼしました。

そのため、追加調査では次の資料も併用しました。

- スクリーンショットによる再現確認
- `LogUntranslated = true` にしたときの `BepInEx/LogOutput.log`
- Thunderstore profile に残っていた旧翻訳ダンプ

この調査で、次のような runtime-composed text を確認しました。

- コスト値が埋め込まれた reroll 表示
- アイテム名やアップグレード名に `[E]` が付与された interact 表示
- マップ画面のアップグレード一覧のように、既存行へ新しい英語行が追記される複数行ブロック

特にマップ画面の一覧は、旧翻訳ダンプに `体力 STAMINA=体力\nスタミナ` のような痕跡があり、
行単位ではなく累積文字列として再評価されていることが分かりました。

## 7. 詰まった点と対応内容

### 1. クラス名の衝突

**問題**  
ゲーム本体に `LocalizationManager` クラスが存在し、Mod 側で同名クラスを定義すると衝突しました。

**対応**  
Mod 側のクラス名を `TranslationManager` に変更しました。

### 2. NuGet パッケージのバージョン衝突

**問題**  
`Newtonsoft.Json` のバージョンを不用意に上げると、実行環境側の依存関係と整合しない構成になり、クラッシュする可能性があります。

**対応**  
動作確認済みの `13.0.4` を使用しました。

```xml
<PackageReference Include="Newtonsoft.Json" Version="13.0.4" />
```

### 3. フォントアセット生成失敗

**問題**  
`TMP_FontAsset.CreateFontAsset(Font)` が `null` を返しました。

**原因**  
ダミー `Font` だけでは、同梱した TTF バイト列を `FontEngine` に渡せませんでした。

**対応**  
`FontEngine.LoadFontFace(Font, int)` を Harmony で補足し、
`SystemFontMap` に登録した TTF バイト列へリダイレクトしました。

### 4. 大文字テキストが翻訳されない

**問題**  
`"< GO BACK"`、`"(CLICK TO RENAME)"`、`"TOTAL HAUL:"` などが翻訳されませんでした。

**原因**  
ゲーム側で `.ToUpper()` を適用した後の文字列が、そのまま TMP に渡されていたためです。

**対応**  
正規化済みキーの `.ToUpperInvariant()` を別辞書に保持し、
翻訳時にも大文字化して照合するようにしました。

```csharp
_translationsUpper[NormalizeKey(kv.Key).ToUpperInvariant()] = kv.Value;

if (_translationsUpper.TryGetValue(normalized.ToUpperInvariant(), out translated))
    return translated;
```

### 5. 実行時に組み立てられる文字列が翻訳されない

**問題**  
サービスステーション外の `Medium Health Pack (50) [E]` や `SPRINT SPEED UPGRADE [E]`、
`HOLD TO REROLL (-$5K)`、マップ画面の `1 CROUCH REST HEALTH` のような表示が英語のまま、
または日本語と英語が混在した状態で残りました。

**原因**  
これらは辞書キーそのものではなく、実行時に次のような加工を受けた文字列だったためです。

- プレースホルダの値埋め込み
- アイテム名やアップグレード名の末尾への `[E]` 付与
- 大文字化された既知語の連結
- 複数行ブロックへの逐次追記

**対応**  
`TranslationManager` に runtime-composed text 向けの経路を追加しました。

- `TranslationTemplate` によるプレースホルダ付きテンプレート一致
- `[E]` などの接尾辞を保持した本体翻訳
- 複数行ブロックの行単位翻訳
- 大文字複合語列の最長一致による分解翻訳

```csharp
if (TryTranslateLookup(text, out result))
    return result;
if (TryTranslateTemplate(text, out result))
    return result;
if (TryTranslateActionSuffix(text, out result))
    return result;
if (TryTranslatePhraseSequence(text, out result))
    return result;
```

### 6. `TMP_Text.SetText(...)` 経由の表示が翻訳されない

**問題**  
`RANDOM MATCHMAKING`、`FOCUS > ...`、一部のチュートリアル文言のように、
辞書にキーがあっても英語のまま残るケースがありました。

**原因**  
一部の UI は `TMP_Text.text` セッターではなく、`TMP_Text.SetText(...)` 系メソッドで更新されていました。
当初のパッチはセッターしか捕捉していなかったため、この経路を通る文字列は翻訳処理に入りませんでした。

**対応**  
Harmony パッチの対象を `TMP_Text.text` セッターに加えて、
第1引数が `string` の `TMP_Text.SetText(...)` オーバーロード全体へ拡張しました。
あわせて、`FOCUS > ...` のような接頭辞付き表示や、sprite を伴う表示は本体側を分解して翻訳する経路を追加しました。

### 7. メニューPrefab初期値の見出しが翻訳されない

**問題**  
`PUBLIC GAME` と `SERVER LIST` のオレンジ色見出しが、辞書に対応キーがあっても英語のまま残りました。

**原因**  
これらはページPrefabの `MenuPage.menuHeader` に初期値として入っており、
`MenuManager.PageOpen(...)` で生成された時点ですでに `TMP_Text.text` に存在していました。
そのため、`TMP_Text.text` セッターや `SetText(...)` のパッチでは捕捉できませんでした。

一度 `TMP_Text.OnEnable` を対象にする案も試しましたが、R.E.P.O. 同梱の TextMeshPro では
Harmony が対象メソッドを見つけられず、起動時に `Undefined target method` 例外が発生しました。

**対応**  
`TMP_Text.OnEnable` パッチは採用せず、`MenuManager.PageOpen(...)` 後と `MenuPage.Start` 後に
`MenuPage` 配下の `TMP_Text` を `GetComponentsInChildren<TMP_Text>(includeInactive: true)` で走査し、
既に入っている現在値を翻訳する方式に変更しました。

この方式により、`Public Game` / `Server List` の大文字フォールバックが適用され、
実表示の `PUBLIC GAME` / `SERVER LIST` も翻訳されます。

### 8. サーバー参加確認ポップアップが翻訳されない

**問題**  
サーバー一覧から部屋を選択したときの `JOIN SERVER`、`Are you sure you want to join ...`、
`YEP!`、`NOPE!` が英語のまま残りました。

**原因**  
サーバー参加確認文は `MenuPageServerList.CreateServerElement(...)` 内で、
部屋名を含む動的文字列として `MenuButtonPopUp.bodyText` に設定されていました。

```text
Are you sure you want to join
''{server name}''
```

また、選択肢テキストは `MenuManager.PagePopUpTwoOptions(...)` の引数や
`MenuButton.buttonTextString` を経由します。

**対応**  
`MenuManager.PagePopUpTwoOptions(...)` の引数を翻訳し、辞書には
`Are you sure you want to join\n''{server}''` 形式のテンプレートを追加しました。
あわせて `Join Server`、`YEP!`、`NOPE!` を追加しました。

### 9. 未翻訳ログがサーバー名で埋まる

**問題**  
ページ配下の `TMP_Text` をまとめて走査すると、サーバー名やユーザー入力文字列も翻訳候補になります。
`LogUntranslated = true` の状態では、意図的に翻訳しない文字列までログに大量出力される可能性があります。

**対応**  
`TranslationManager.Translate(string text, bool logUntranslated = true)` に
`logUntranslated` 引数を追加しました。ページ走査時は `false` を渡し、
通常の TMP 更新パッチでは従来どおり未翻訳ログを出すようにしています。

### 10. トラック画面の "TAXMAN:" ラベルと "STARTING ENGINE..." が翻訳されない

**問題**  
トラック画面（サービスステーション兼チャット端末）の Taxman 名ラベルと、ゲーム開始時のロック状態テキストが英語のまま残りました。

**原因（Taxman ラベル）**  
`TruckScreenText.UpdateTaxmanNickname(string newName)` が Unity Localization から "Taxman" を受け取り、
以下のようにリッチテキストタグで包んで `nicknameTaxman` フィールドに格納します。

```csharp
nicknameTaxman = "\n\n<color=#4d0000ff><b>" + newName + ":</b></color>\n";
```

その後 `textMesh.text += currentNickname` でダイアログ全体テキストに逐次結合されるため、
TMP setter パッチは複合テキスト全体を受け取ってしまいキー照合できません。

**原因（STARTING ENGINE 等）**  
`TuckScreenLocked.LockChatToggle()` が `lockedText` フィールドに格納後、
`Update()` で毎フレーム `lockedText + <color=...>.</color>...` というアニメーション付き文字列を TMP にセットします。
ドットがカラータグの間に挟まるためリッチテキストサフィックス正規表現が分割できず、翻訳不可でした。

**対応**  
`TruckScreenPatches.cs` を新規追加し、以下 2 つの Prefix パッチを追加しました。

- `TruckScreenText.UpdateTaxmanNickname` Prefix: リッチテキスト組み立て前に `newName` を翻訳
- `TuckScreenLocked.LockChatToggle` Prefix: フィールド格納前に `_lockedText` を翻訳

あわせて `ja.json` に `"STARTING ENGINE"`、`"HITTING THE ROAD"`、`"DESTROYING SLACKERS"` を追加しました。
`"Taxman"` は既存エントリ `"Taxman": "税金マン"` が使われます。

### 11. セーブデータ選択画面の "TOTAL HAUL:" が翻訳されない

**問題**  
セーブデータを選択したときの詳細パネルに表示される "TOTAL HAUL:" ラベルが英語のまま残りました。

**原因**  
`MenuPageSaves.SaveFileSelected` が Unity Localization から "Total Haul:" を取得し、
前後にリッチテキストタグと数値を結合して `saveFileInfoRow2.text` に一括セットします。

```csharp
string text6 = localizedTotalHaul?.GetLocalizedString() ?? "Total Haul:";
saveFileInfoRow2.text = "<color=#336680><sprite name=$$$> " + text6 + "      $ <color=white><b>" + text5 + "</b></color>k</color>";
```

TMP setter パッチはこの複合文字列全体を受け取り、辞書の `"Total Haul:"` とは一致しません。
`ja.json` には `"Total Haul:": "総回収額:"` が既に登録されていましたが、照合に届いていませんでした。

**対応**  
`MenuTranslationPatches.cs` に `MenuPageSaves.SaveFileSelected` の Postfix パッチを追加し、
メソッド実行後に `saveFileInfoRow2.text` 内の `"Total Haul:"` を `TranslationManager.Translate` の結果で部分置換しています。

## 8. ファイル構成

```text
.
├── build.sh
└── src/
    ├── PluginInfo.cs
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
    │   ├── MenuTranslationPatches.cs
    │   ├── TMPTextTranslationPatch.cs
    │   ├── TruckScreenPatches.cs
    │   └── TMPFontPatch.cs
    └── translations/
        └── ja.json
```

### 各ファイルの役割

#### `Plugin.cs`
- BepInEx `BaseUnityPlugin` を継承するエントリポイント
- 設定項目:
  - `EnableTranslation` (bool, default: true)
  - `EnableJapaneseFont` (bool, default: true)
  - `LogUntranslated` (bool, default: false)
- `LogUntranslated = true` のときにデバッグモード有効ログを出力

#### `PluginInfo.cs`
- プラグイン GUID、表示名、バージョンをソースコード上で定義
- Release / Debug の生成キャッシュに依存せず、同じ版数を確実に埋め込む

#### `TranslationManager.cs`
- `ja.json` から翻訳辞書を構築
- 組み込みリソースを読み込んで翻訳データを初期化
- 完全一致、正規化、大文字化の基本検索を行う
- `{cost}` や `[inventory1]` などを含むテンプレートを実表示に一致させる
- `[E]` 付き interact 表示や複数行ブロック、複合アップグレード名を扱う

#### `FontManager.cs`
- 同梱 TTF から TMP FontAsset を動的生成
- `FontEngine.LoadFontFace(Font, int)` をバイト列経由へリダイレクト
- 全 TMP テキストコンポーネントへ日本語フォールバックを追加
- メニュー生成直後のページ配下 TMP へも日本語フォールバックを追加
- パッチの二重適用を避けつつ、失敗時はダミー `Font` をクリーンアップ

#### `TMPTextTranslationPatch.cs`
- `TMP_Text.text` セッターと `TMP_Text.SetText(...)` への Harmony パッチ
- テキスト更新時に `TranslationManager.Translate()` を呼ぶ

#### `MenuTranslationPatches.cs`
- メニュー専用の Harmony パッチを集約
- `MenuManager.PageOpen(...)` / `MenuPage.Start` 後にページ配下の初期TMPテキストを翻訳
- `MenuButton.buttonTextString` を翻訳
- `MenuManager.PagePopUp(...)` / `PagePopUpTwoOptions(...)` の文字列引数を翻訳
- `MenuPageSaves.SaveFileSelected` Postfix でセーブ画面の "Total Haul:" 部分置換を実施

#### `TruckScreenPatches.cs`
- トラック画面（`TruckScreenText`、`TuckScreenLocked`）専用のHarmonyパッチ
- `TruckScreenText.UpdateTaxmanNickname` の `newName` をリッチテキスト組み立て前に翻訳する Prefix パッチ
- `TuckScreenLocked.LockChatToggle` の `_lockedText` をフィールド格納前に翻訳する Prefix パッチ

#### `TMPFontPatch.cs`
- シーン読み込み後にフォントフォールバックを再適用
- シーン切り替え後の UI にも日本語フォントを反映

#### `ja.json`
- キー: 英語原文
- 値: 日本語訳
- `_` または `//` で始まるキーはコメントとして無視
- DLL に組み込みリソースとして埋め込み

#### `build.sh`
- リポジトリルートから Release ビルドを実行する補助スクリプト
- `dotnet clean` の後に `dotnet build src/REPOJapaneseTranslation.csproj -c Release` を実行

## 9. ビルドとデプロイ

### 必要環境

- .NET SDK 6.0 以上
- WSL または Windows コマンドプロンプト

### ビルド

```bash
cd <REPO_DIR>
dotnet build src/REPOJapaneseTranslation.csproj -c Release
# または
bash build.sh
```

PATH に `dotnet` が通っていない環境では、次のように `DOTNET_ROOT` を通して実行できます。

```bash
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"
"$DOTNET_ROOT/dotnet" build src/REPOJapaneseTranslation.csproj -c Release
```

生成物:

```text
src/bin/Release/netstandard2.1/REPOJapaneseTranslation.dll
```

### デプロイ

生成した DLL とフォントファイルを、導入先の BepInEx plugins フォルダへコピーします。

ここでは次のプレースホルダを使います。

- `<REPO_DIR>`: このリポジトリのルート
- `<GAME_DIR>`: R.E.P.O. のゲームインストールフォルダ
- `<PROFILE_BEPINEX_DIR>`: Thunderstore Mod Manager / r2modman などの対象プロファイル内にある `BepInEx` フォルダ

```bash
# ゲームフォルダへ直接導入する場合
mkdir -p "<GAME_DIR>/BepInEx/plugins/REPOJapaneseTranslation"
cp "<REPO_DIR>/src/bin/Release/netstandard2.1/REPOJapaneseTranslation.dll" \
    "<GAME_DIR>/BepInEx/plugins/REPOJapaneseTranslation/"
cp "<REPO_DIR>/src/fonts/NotoSansJP-Regular-subset.ttf" \
    "<GAME_DIR>/BepInEx/plugins/REPOJapaneseTranslation/"

# Mod Manager のプロファイルへ導入する場合
mkdir -p "<PROFILE_BEPINEX_DIR>/plugins/REPOJapaneseTranslation"
cp "<REPO_DIR>/src/bin/Release/netstandard2.1/REPOJapaneseTranslation.dll" \
    "<PROFILE_BEPINEX_DIR>/plugins/REPOJapaneseTranslation/"
cp "<REPO_DIR>/src/fonts/NotoSansJP-Regular-subset.ttf" \
    "<PROFILE_BEPINEX_DIR>/plugins/REPOJapaneseTranslation/"
```

### 翻訳更新

翻訳辞書は DLL に埋め込まれているため、
`src/translations/ja.json` を編集した後は再ビルドが必要です。

### 未翻訳テキストの確認

`BepInEx/config/REPOJapaneseTranslation.cfg` で `LogUntranslated = true` にすると、
未翻訳テキストを `BepInEx/LogOutput.log` に `[未翻訳]` タグ付きで出力できます。

## 参考リンク

- [BepInEx 5 ドキュメント](https://docs.bepinex.dev/articles/user_guide/installation/index.html)
- [HarmonyX ドキュメント](https://harmony.pardeike.net/articles/intro.html)
- [TextMeshPro ドキュメント](https://docs.unity3d.com/Packages/com.unity.textmeshpro@3.0/manual/index.html)
- [Unity Localization パッケージ](https://docs.unity3d.com/Packages/com.unity.localization@1.5/manual/index.html)
