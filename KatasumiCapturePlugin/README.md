# かたすみキャプチャ（開発ビルド）

YMM4 ツールプラグイン「かたすみキャプチャ」のソースです。ユーザー向けの説明・インストール・アンインストール・ライセンス・利用規約は、リポジトリ直下の [README.md](../README.md) を参照してください。

## ビルド要件

- .NET SDK（YMM4 が要求するランタイムに合わせたバージョン。本プロジェクトは `net10.0-windows` をターゲットにしています）
- インストール済みの YMM4（API 参照用 DLL）

## ビルド手順

1. `ClipboardToTimelinePlugin.csproj` の `<YMM4_PATH>` を、ご利用の YMM4 インストールフォルダに合わせて変更します。
2. YMM4 を終了した状態でビルドすると、`AfterTargets="Build"` により `user\plugin\ClipboardToTimelinePlugin` へコピーされます。起動中は DLL がロックされるため失敗することがあります。
3. コマンド例:

   ```bash
   dotnet build -c Release
   ```

   YMM4 起動中にビルドだけ通したい場合:

   ```bash
   dotnet build -c Release -p:SkipPluginCopy=true
   ```

4. 手動配置する場合は、`bin\Release\net10.0-windows10.0.19041.0\` に出力されたファイル一式を  
   `[YMM4]\user\plugin\ClipboardToTimelinePlugin\` にコピーします。

## アセンブリ名について

プラグインフォルダ名は現在 **ClipboardToTimelinePlugin** です（`AssemblyName`）。リポジトリ名を `KatasumiCapture` にしていても、手動インストール・アンインストールのパスはこのフォルダ名を指します。

## 変更履歴

[CHANGELOG.md](../CHANGELOG.md) を参照してください。
