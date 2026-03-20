# かたすみキャプチャ（開発ビルド）

YMM4 ツールプラグイン「かたすみキャプチャ」のソースです。ユーザー向けの説明・インストール・アンインストール・ライセンス・利用規約は、リポジトリ直下の [README.md](../README.md) を参照してください。

## ビルド要件

- .NET SDK（YMM4 が要求するランタイムに合わせたバージョン。本プロジェクトは `net10.0-windows` をターゲットにしています）
- インストール済みの YMM4（API 参照用 DLL）

## ビルド手順

1. `KatasumiCapturePlugin.csproj` の `<YMM4_PATH>` を、ご利用の YMM4 インストールフォルダに合わせて変更します。
2. YMM4 を終了した状態でビルドすると、`AfterTargets="Build"` により `user\plugin\かたすみキャプチャプラグイン\` へコピーされます。起動中は DLL がロックされるため失敗することがあります。
3. コマンド例:

   ```bash
   dotnet build -c Release
   ```

   YMM4 起動中にビルドだけ通したい場合:

   ```bash
   dotnet build -c Release -p:SkipPluginCopy=true
   ```

4. 手動配置する場合は、**提出用レイアウト**（下記 `dotnet publish`）で生成された `かたすみキャプチャプラグイン` フォルダ内の DLL を  
   `[YMM4]\user\plugin\かたすみキャプチャプラグイン\` にコピーします。

## 提出・リリース用フォルダ構成（`dotnet publish`）

プラグインポータル等への提出用に、`dotnet publish` の完了後に次の構成が **`bin\<構成>\<TFM>\KatasumiCapturePlugin\`** に自動生成されます。

```text
KatasumiCapturePlugin\
  かたすみキャプチャプラグイン\
    KatasumiCapturePlugin.dll
    Readme.txt
  利用規約.txt
  ライセンス.txt
  Readme.txt
```

- ルートと内側の **Readme.txt** は同一内容です（`submit-release-txt\Readme.txt` をコピー）。
- **利用規約.txt**・**ライセンス.txt**・**Readme.txt** の編集元は **`submit-release-txt\`** に置いてください（リポジトリ管理用）。`dotnet publish` 後、同内容が **`bin\...\publish\`** にも同期されます（`publish` だけを編集すると次回 publish で上書きされます）。

コマンド例:

```bash
dotnet publish -c Release -p:SkipPluginCopy=true
```

提出用レイアウトの生成だけオフにする場合:

```bash
dotnet publish -c Release -p:SkipPluginCopy=true -p:SkipSubmitReleaseLayout=true
```

## アセンブリ名とフォルダ名

- **DLL 名**: `KatasumiCapturePlugin.dll`（`AssemblyName`）
- **YMM4 の `user\plugin` 下のフォルダ名**: **かたすみキャプチャプラグイン**（`Ymm4PluginFolderJa`）

## 変更履歴

[CHANGELOG.md](../CHANGELOG.md) を参照してください。
