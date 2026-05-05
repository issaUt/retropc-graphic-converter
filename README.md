# RetroPC Graphic Converter GUI

レトロPC向け画像変換スクリプトをWindows GUIから実行し、元画像・変換後PNG・生成ファイルを確認するためのVisual Studioソリューションです。

## Repository Layout

```text
MzGraphicConv/
  MzGraphConvApp/
    MzGraphConvApp.sln
    MzRubyConvGui/       # 現在のRuby変換GUI
```

現在の開発対象は `MzGraphConvApp/MzRubyConvGui` です。旧サンプルプロジェクト `MzGraphConvApp/MzGraphConvApp` は削除済みです。

## Requirements

- Windows
- Visual Studio 2022
- .NET SDK 8.0
- Ruby
- 画像変換Rubyスクリプト
  - https://github.com/issaUt/mz-ruby-graphic-core
  - 例: `C:\RetroPC\mz-ruby-graphic-core\pngconvMZ.rb`

Rubyスクリプト本体はこのGUIプロジェクトの外部に置き、GUIの `Settings` タブにある `Script` 欄で指定する運用を想定しています。

## Initial Setup

初回起動後、まず `Settings` タブでRuby実行ファイルと変換スクリプトを指定してください。

![Settings tab](docs/images/settings-ruby-script.svg)

### 1. Ruby

`Ruby` にはRuby実行ファイルを指定します。

RubyにPATHが通っている場合は、既定値のまま `ruby` で動作します。動作しない場合は `参照...` から `ruby.exe` を指定してください。

なお、Ruby本体のインストール方法は  [mz-ruby-graphic-core](https://github.com/issaUt/mz-ruby-graphic-core) の README.md を参照してください。

例:

```text
ruby
```

または:

```text
C:\Ruby32-x64\bin\ruby.exe
```

### 2. Script

`Script` にはRuby変換スクリプト `pngconvMZ.rb` を指定します。

例:

```text
C:\RetroPC\mz-ruby-graphic-core\pngconvMZ.rb
```

このGUIにはRubyスクリプト本体を同梱しません。別途 [mz-ruby-graphic-core](https://github.com/issaUt/mz-ruby-graphic-core) から `pngconvMZ.rb` を取得し、Ruby側READMEに従って必要なgemをインストールしてください。

### 3. Script Version

`Get` ボタンを押すと、指定したスクリプトに対して以下を実行し、バージョン情報を表示します。

```powershell
ruby pngconvMZ.rb --json --info
```

正常に取得できる場合は、以下のように表示されます。

```text
pngconvMZ 0.1.3
```

ここまで確認できれば、`Convert` タブで入力画像、出力先、変換条件を指定して実行できます。

## Build

ソリューションをVisual Studioで開く場合:

```text
MzGraphConvApp/MzGraphConvApp.sln
```

コマンドラインでビルドする場合:

```powershell
dotnet build .\MzGraphConvApp\MzRubyConvGui\MzRubyConvGui.csproj
```

## MzRubyConvGui

主な機能:

- Rubyスクリプトのコマンドライン引数をGUIから指定
- `Settings` タブでRuby実行ファイルと変換スクリプトを指定
- `Settings` タブで変換スクリプトのバージョン情報を確認
- 入力画像 PNG/JPEG、出力フォルダ、ベース名の指定と履歴保存
- PNGのみ出力の指定
- 変換後PNG/BRD/BSD/Paletteファイルの一覧表示
- 元画像とPreview画像の比較表示
- 拡大ウィンドウでの同期ズーム/同期パン
- split320x200出力のUpper/Lower比較表示
- Preview表示のアスペクト補正切替
- 実行中キャンセル
- 上書き確認
- D88ディスクイメージ生成
- `BRD/BSDファイルを残す` チェックによる sidecar ファイル保持切替
- D88内部ファイル名の長さ事前チェック

ユーザー設定は以下に保存されます。

```text
%LOCALAPPDATA%\MzRubyConvGui\settings.json
```

この設定ファイルは個人環境依存のためGit管理対象外です。

## 注意事項

本ツールは個人開発のソフトウェアです。動作確認は可能な範囲で行っていますが、すべての環境での動作を保証するものではありません。

生成されたファイルの利用や、実機・エミュレータでの読み込みは、利用者ご自身の責任で行ってください。重要なデータを扱う場合は、事前にバックアップを取ることをおすすめします。

## D88 Support

`Convert` タブで `D88ディスクイメージを生成` を有効にすると、変換後の `BRD` / `BSD` をD88ディスクイメージへ追加できます。

- `BRD/BSDファイルを残す` にチェックがある場合:
  - D88追加後も `BRD` / `BSD` を出力フォルダへ残します。
- チェックがない場合:
  - D88追加後に `BRD` / `BSD` を削除します。

D88ファイル名は `Output Dir` と `Base Name` から自動決定されます。

## File Naming Notes

512色モードでは固定色出力ファイル名に `_FR` / `_FG` / `_FB` を使用します。

例:

- `sample_FR.png`
- `sample_FR.brd`
- `sample_FR.bas.bsd`

`split320x200` では upper / lower に加えて、結合BSDファイルとして `_c.bas.bsd` を使用します。

例:

- `sample_FR_u.brd`
- `sample_FR_l.brd`
- `sample_FR_c.bas.bsd`

## D88 Name Length Limit

D88内部ファイル名は安全側の運用として **16 bytes以内** を前提にしています。
GUIは変換開始前にこの条件をチェックし、長すぎる場合は `Base Name` を短くするよう警告します。
