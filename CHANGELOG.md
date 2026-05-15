# Changelog

## v1.0.6

### Added

- 新しい翻訳を追加

## v1.0.5

### Added

- 新しい翻訳を追加

### Changed

- 「グラバー」を「掴み手」に表現を変更
- 「OK」を「わかった」に表現を変更

## v1.0.4

### Added

- サーバー一覧画面など、メニューPrefabの初期テキストとして保持される見出しを翻訳するパッチを追加
- サーバー参加確認ポップアップ用の翻訳を追加
- `YEP!`、`NOPE!`、`Join Server` の翻訳を追加
- トラック画面のテキスト（`STARTING ENGINE`、`HITTING THE ROAD`、`DESTROYING SLACKERS`）の翻訳を追加
- `TruckScreenPatches.cs` を追加: `TruckScreenText.UpdateTaxmanNickname` と `TuckScreenLocked.LockChatToggle` へのHarmonyパッチ

### Changed

- TMP汎用パッチとメニュー専用パッチをファイル分割して整理
- TMP汎用パッチのファイル名を `TMPTextTranslationPatch.cs` に変更
- メニュー生成直後のページ配下TMPにも日本語フォントフォールバックを適用するように変更

### Fixed

- `PUBLIC GAME` と `SERVER LIST` のオレンジ色見出しが翻訳されない問題を修正
- `TMP_Text.OnEnable` を対象にした不正なHarmonyパッチで起動時エラーが出る問題を修正
- トラック画面の `TAXMAN:` ラベルが翻訳されない問題を修正
- トラック画面の `STARTING ENGINE...` 等がリッチテキストとアニメーション付きドットが混在するため翻訳されない問題を修正
- セーブデータ選択画面の "TOTAL HAUL:" が翻訳されない問題を修正

## v1.0.3

### Fixed

- 未使用の依存関係を削除
- 日本語訳の微調整

## v1.0.2

### Changed

- Thunderstoreパッケージでファイルのコピー先を変更
- バージョン定数を自動生成ではなくソースファイルで管理するように変更

### Fixed

- 日本語訳を微調整
- Releaseビルドで古いプラグインバージョンが残ることがある問題を修正
- `LogUntranslated = true` 時にデバッグモード有効ログが出るように修正

## v1.0.1

### Updated

- Thunderstoreパッケージのファイルパスを修正
- バージョン番号を1.0.1に更新

## v1.0.0

### Added

- R.E.P.O.のUIテキストを日本語化する BepInEx プラグインを実装
- DLLに埋め込まれた翻訳辞書 `ja.json` を読み込む翻訳基盤を追加
- `TMP_Text.text` セッターへのHarmonyパッチで、主要なTextMeshProテキスト更新を横断的に翻訳する仕組みを追加
- 一般UI、HUD、ショップ、チュートリアル、確認ダイアログ、コスメティクス画面などの翻訳を追加
- アイテム名、敵名、レベル名、アップグレード名、Photonリージョン名の翻訳を追加
- 設定項目 `EnableTranslation`、`EnableJapaneseFont`、`LogUntranslated` を追加
- 同梱の `NotoSansJP-Regular-subset.ttf` を使った日本語フォントフォールバックを追加
