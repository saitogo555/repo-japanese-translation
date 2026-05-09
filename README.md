# R.E.P.O. 日本語化Mod

[![Game Version](https://img.shields.io/badge/Game%20Version-v0.4.0-5c7cfa?style=for-the-badge)](#)
[![Thunderstore Version](https://img.shields.io/thunderstore/v/saitogo/REPOJapaneseTranslation?style=for-the-badge&logo=thunderstore&logoColor=white)](https://thunderstore.io/c/repo/p/saitogo/REPOJapaneseTranslation/)

R.E.P.O.のゲーム内テキストを日本語に翻訳するModです。

## 前提Mod

- [BepInExPack](https://thunderstore.io/c/repo/p/BepInEx/BepInExPack/) `5.4.2305+`
- [REPOLib](https://thunderstore.io/c/repo/p/Zehs/REPOLib/) `4.0.0`

## 機能

- ✅ UIテキストの日本語翻訳(メニュー、HUD、ショップなど)
- ✅ アイテム名・エネミー名・レベル名の日本語化
- ✅ アップグレード名の日本語化
- ✅ NotoSansJPフォントによる日本語表示

## インストール方法

### Thunderstore Mod Manager (推奨)

Thunderstore Mod Managerまたはr2modmanで `REPOJapaneseTranslation` と検索してインストールしてください。

前提Modも自動でインストールされます。

### 手動インストール

#### 前提Modを直接インストールしている場合 (Steam)

```
(ゲームルート)\BepInEx\plugins\REPOJapaneseTranslation\
├── REPOJapaneseTranslation.dll
└── fonts\
    └── NotoSansJP-Regular-subset.ttf
```

> **ゲームルートの例:**  
> `C:\Program Files (x86)\Steam\steamapps\common\REPO\`

#### Thunderstore Mod Managerを使用している場合

Thunderstore Mod Managerは通常のゲームフォルダとは別に、プロファイルごとに独立したBepInExフォルダが存在するので、そのフォルダに配置してください。

```
%APPDATA%\Thunderstore Mod Manager\DataFolder\REPO\profiles\<プロファイル名>\BepInEx\plugins\REPOJapaneseTranslation\
├── REPOJapaneseTranslation.dll
└── fonts\
    └── NotoSansJP-Regular-subset.ttf
```

> **パスの例:**  
> `C:\Users\<ユーザー名>\AppData\Roaming\Thunderstore Mod Manager\DataFolder\REPO\profiles\Default\BepInEx\plugins\REPOJapaneseTranslation\`  
> (`%APPDATA%` は `C:\Users\<ユーザー名>\AppData\Roaming` のショートカットです)

> **確認方法:**  
> Thunderstore Mod Managerの左サイドバーから**Settings → Locations → Profile folder**を開くと正確なパスを確認できます。

## 設定

`BepInEx\config\REPOJapaneseTranslation.cfg` で以下の設定が可能です:

| 設定キー | デフォルト | 説明 |
|----------|-----------|------|
| `EnableTranslation` | `true` | テキスト翻訳を有効にするか |
| `EnableJapaneseFont` | `true` | 日本語フォントフォールバックを有効にするか |
| `LogUntranslated` | `false` | 未翻訳テキストをログ出力するか(開発者向け) |

## 日本語フォントについて / About Japanese Font

このModは **NotoSansJP** フォントを同梱しており、`fonts\NotoSansJP-Regular-subset.ttf` として配置することでゲーム内の日本語グリフを正しく表示します。

フォントファイルが見つからない場合は日本語テキストの翻訳自体は機能しますが、日本語文字が正しく表示されない可能性があります。

## 翻訳の追加・修正

翻訳はDLLに埋め込まれています。未翻訳テキストを発見した場合は以下の方法でご協力ください：

1. 設定で `LogUntranslated = true` にしてゲームを起動し、BepInExのログで `[未翻訳]` タグの行を確認する
2. GitHubリポジトリの `src/REPOJapaneseTranslation/translations/ja.json` にプルリクエストを送る

## 開発者向けビルド

```bash
dotnet build src/REPOJapaneseTranslation/REPOJapaneseTranslation.csproj -c Release
# -> src/REPOJapaneseTranslation/bin/Release/netstandard2.1/REPOJapaneseTranslation.dll
```

ビルド成果物を配布する場合は、DLLに加えて `fonts/NotoSansJP-Regular-subset.ttf` も同梱してください。

## ライセンス / License

MIT License
