# OpenVDBForUnity

[English](README.md)

Unity用の[OpenVDB](http://www.openvdb.org/)ボリュームインポート・レンダリングプラグインです。

![gif](https://github.com/karasusan/OpenVDBForUnity/wiki/images/CloudSample.gif)

## 要件

- Unity 2022.3 LTS 以降
- **HDRP 14.x** (High Definition Render Pipeline) または Built-in Render Pipeline

## 対応プラットフォーム

| プラットフォーム | プラグイン | 状態 |
|----------|--------|--------|
| Windows x86_64 | `openvdbi.dll` | ビルド済み同梱 |
| Linux x86_64 | `libopenvdbi.so` | ビルド済み同梱 |
| macOS | `libopenvdbi.bundle` | ソースからビルド |

## クイックスタート

### 1. プロジェクトを開く

`OpenVDBForUnity/` フォルダをUnity 2022.3以降で開いてください。Windows・Linux用のネイティブプラグインはリポジトリに同梱されています。

### 2. VDBファイルのインポート

1. `.vdb` ファイルを `Assets/` フォルダに配置
2. ScriptedImporterにより自動的に以下が生成されます：
   - VDBボリュームからサンプリングした3Dテクスチャ
   - バウンディングボックス用メッシュ
   - 適切なボリュームレンダリングシェーダーを使用したマテリアル

### 3. 結果の確認

`Assets/Scenes/Test.unity` を開くか、新しいシーンを作成してください。インポートしたVDBアセットをシーンにドラッグします。ボリュームはレイマーチングで描画されます。

## レンダーパイプライン対応

### HDRP（推奨）

`OpenVDB/HDRP/Standard` シェーダーはHDRP 14.x向けで、以下の機能を提供します：

| プロパティ | 説明 | デフォルト |
|----------|-------------|---------|
| Volume | VDBデータの3Dテクスチャ | - |
| Intensity | レンダリング強度 | 0.3 |
| Step Distance | レイマーチのステップサイズ | 0.01 |
| Shadow Steps | シャドウサンプリングステップ数 | 32 |
| Shadow Density | シャドウ色 | (0.4, 0.4, 0.4) |
| Shadow Threshold | シャドウカットオフ | 0.001 |
| Ambient Color | アンビエントライト色 | (0.4, 0.4, 0.5) |
| Ambient Density | アンビエントライト強度 | 0.2 |
| Culling | フェースカリングモード | Off |
| Enable Directional Light | ディレクショナルライト使用 | On |
| Enable Ambient Light | アンビエントライト使用 | On |
| Auto HDRP Light | HDRPライトバッファからライトデータを読み取り | On |
| **Write Depth** | SV_Depthでボクセル精度のデプス書き込み | On |
| **Clip Against Scene Depth** | 不透明ジオメトリでレイを停止 | On |

#### デプスオプション

- **Write Depth（ボクセル精度）** -- 最初にヒットしたボクセルのデプスを `SV_Depth` 経由でデプスバッファに書き込みます。ポストプロセスエフェクト（被写界深度、フォグなど）との正しい連携に必要です。
- **Clip Against Scene Depth** -- 不透明デプスバッファを読み取り、シーンジオメトリの表面でボリュームのレイマーチを停止します。VDBボリュームが不透明オブジェクトの背後に描画されるのを防ぎます。

#### スポットライト対応

HDRPモードでは、ボリュームはシーン内のスポットライトの影響を受けることができます。スポットライトの位置、方向、コーン角度、距離減衰を考慮した正確なライティングが適用されます。

### Built-in Render Pipeline

`OpenVDB/Standard` シェーダーは、Built-in Render Pipeline向けの基本的なボリュームレンダリングを提供します。

## タイムライン / シーケンス再生

VDBアニメーションシーケンスは以下の方法で再生できます：

- **OpenVDBSequencePlayer** -- 連続する `.vdb` ファイルを再生するランタイムコンポーネント
- **タイムライン統合** -- Unity Timelineでの再生制御用カスタムTrack (`OpenVDBTimelineTrack`)

## ネイティブプラグインのビルド

ネイティブプラグインはOpenVDB C++ライブラリをP/Invoke経由でUnityから使用するためのラッパーです。

### Linux (Ubuntu/Debian)

```bash
# 依存パッケージのインストール
sudo apt install cmake libopenvdb-dev libtbb-dev libimath-dev libboost-iostreams-dev

# ビルド
cd Plugin
mkdir build && cd build
cmake .. -DBUILD_SHARED_LIBS=ON -DCMAKE_BUILD_TYPE=Release \
  -DCMAKE_MODULE_PATH="/usr/lib/x86_64-linux-gnu/cmake/OpenVDB"
cmake --build . -j$(nproc)

# Unityプロジェクトにインストール
cp openvdbi/libopenvdbi.so \
  ../../OpenVDBForUnity/Assets/OpenVDB/Scripts/Plugins/x86_64/
```

### macOS

```bash
# 依存パッケージのインストール
brew install openvdb imath cmake

# ビルド
cd Plugin
mkdir build && cd build
cmake .. -DBUILD_SHARED_LIBS=ON -DCMAKE_BUILD_TYPE=Release
cmake --build . -j$(sysctl -n hw.ncpu)

# Unityプロジェクトにインストール
cp openvdbi/libopenvdbi.bundle \
  ../../OpenVDBForUnity/Assets/OpenVDB/Scripts/Plugins/x86_64/
```

### Windows

```powershell
# vcpkg経由で依存パッケージをインストール
vcpkg install openvdb:x64-windows

# ビルド
cd Plugin
mkdir build && cd build
cmake .. -DBUILD_SHARED_LIBS=ON -DCMAKE_BUILD_TYPE=Release ^
  -DCMAKE_TOOLCHAIN_FILE=[vcpkg root]/scripts/buildsystems/vcpkg.cmake
cmake --build . --config Release

# Unityプロジェクトにインストール（DLLと依存ファイル）
copy openvdbi\Release\openvdbi.dll ^
  ..\..\OpenVDBForUnity\Assets\OpenVDB\Scripts\Plugins\x86_64\
```

> **Note:** Windows環境では `openvdbi.dll` と共に以下のランタイムDLLが必要です：`openvdb.dll`, `tbb12.dll`, `blosc.dll`, `lz4.dll`, `zlib1.dll`, `zstd.dll`, `Imath-3_2.dll`。これらはビルド済みパッケージに同梱されています。

## ライセンス

MIT

バンドルされたサードパーティライブラリのライセンスは [THIRD_PARTY_LICENSES.md](THIRD_PARTY_LICENSES.md) を参照してください。
