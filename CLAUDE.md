# PULSE 3D — Claude 向け作業指示

## Git ワークフロー

- 作業ブランチは `unity`
- 1タスク完了するたびに `unity` ブランチへ commit & push する

## タスク管理

- タスクは GitHub Issues で管理する（リポジトリ: `showyou/pulse_3d`）
- 新しいタスクが発生したら Issue を作成する
- 作業完了時は該当 Issue をクローズする
- API には環境変数 `$GITHUB_MY_TOKEN` を使用する
