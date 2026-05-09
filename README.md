# R.E.P.O. 日本語化Mod

[![Game Version](https://img.shields.io/badge/Game%20Version-v0.4.1-5c7cfa?style=for-the-badge)](#)
[![Thunderstore Version](https://img.shields.io/thunderstore/v/saitogo/REPOJapaneseTranslation?style=for-the-badge&logo=thunderstore&logoColor=white)](https://thunderstore.io/c/repo/p/saitogo/REPOJapaneseTranslation/)

R.E.P.O.のゲーム内テキストを日本語に翻訳するModです。

## 前提Mod

- [BepInExPack](https://thunderstore.io/c/repo/p/BepInEx/BepInExPack/) `5.4.2305+`
- [REPOLib](https://thunderstore.io/c/repo/p/Zehs/REPOLib/) `4.0.0+`

## 機能

- ✅ テキストの日本語翻訳(メニュー、HUD、ショップなど)
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

## 不具合報告

不具合の報告は、[GitHub Issues](https://github.com/saitogo555/repo-japanese-translation/issues)からお願いします。可能であれば、以下の情報をあわせて記載してください。

- ゲームバージョン
- Modバージョン
- 導入方法 (Thunderstore Mod Manager / r2modman / 手動)
- 前提Modのバージョン (BepInExPack, REPOLib)
- 発生した症状
- 再現手順
- スクリーンショットや動画

不具合報告は歓迎します。
報告前に、同様の内容が既存のIssuesにないか確認していただけると助かります。

## 開発者向けビルド

```bash
bash build.sh
# -> src/REPOJapaneseTranslation/bin/Release/netstandard2.1/REPOJapaneseTranslation.dll
```

または

```bash
dotnet clean src/REPOJapaneseTranslation/REPOJapaneseTranslation.csproj -c Release
dotnet build src/REPOJapaneseTranslation/REPOJapaneseTranslation.csproj -c Release
# -> src/REPOJapaneseTranslation/bin/Release/netstandard2.1/REPOJapaneseTranslation.dll
```

## ライセンス

MIT License
