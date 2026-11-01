# WillPingApp の構成図

現在のC#実装（`WillPingApp/*.cs`）を表すMermaidファイルです。旧BAT・PowerShell版と、将来の設計案は対象外です。Mermaid対応のエディターやビューアーで各 `.mmd` ファイルを開いて表示してください。

| ファイル | 内容 |
| --- | --- |
| [class-diagram.mmd](class-diagram.mmd) | UMLクラス図。フォーム、疎通確認、ログ保存の責務と依存関係 |
| [monitor-sequence.mmd](monitor-sequence.mmd) | UMLシーケンス図。監視開始から疎通確認、障害診断、停止まで |
| [window-state.mmd](window-state.mmd) | UML状態図。最小化によるトレイ格納、ダブルクリックによる復元、終了 |

クラス図は主要なメンバーのみを載せています。`Settings` は `MainForm` 内の非公開recordです。`PingService.CheckAsync` の戻り値は図中で `Task` と省略し、実際は `(int Received, string Detail)` のタプルを返す非同期メソッドです。標準ライブラリのクラスは依存先として一部だけ記載しています。

監視間隔は検査開始から正確に60秒ごとではなく、各検査と必要な診断の終了後に60秒待機する方式です。TXTには宛先ごとの応答数・詳細を、CSVには4宛先のOK/FAILを保存します。診断情報は障害の初回検出時に保存します。

設定は `%LOCALAPPDATA%\WillPingApp\settings.json`、ログは既定で `%LOCALAPPDATA%\WillPingApp\NetworkMonitorLogs` に保存します。ログ保存先は監視停止中に変更でき、次回起動時にも引き継がれます。
