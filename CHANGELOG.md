# Changelog

## v1.0.0

### Added

- R.E.P.O. の UI テキストを日本語化する BepInEx プラグインを実装
- DLL に埋め込まれた翻訳辞書 `ja.json` を読み込む翻訳基盤を追加
- `TMP_Text.text` セッターへの Harmony パッチで、主要な TextMeshPro テキスト更新を横断的に翻訳する仕組みを追加
- 一般 UI、HUD、ショップ、チュートリアル、確認ダイアログ、コスメティクス画面などの翻訳を追加
- アイテム名、敵名、レベル名、アップグレード名、Photon リージョン名の翻訳を追加
- 設定項目 `EnableTranslation`、`EnableJapaneseFont`、`LogUntranslated` を追加
- 同梱の `NotoSansJP-Regular-subset.ttf` を使った日本語フォントフォールバックを追加