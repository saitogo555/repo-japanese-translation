# Changelog

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
