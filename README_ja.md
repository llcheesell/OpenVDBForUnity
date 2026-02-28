# OpenVDBForUnity

[OpenVDB](http://www.openvdb.org/) ボリュームを Unity でインポート・レンダリングするためのプラグインです。

![gif](https://github.com/karasusan/OpenVDBForUnity/wiki/images/CloudSample.gif)

## 必要環境

- Unity 6 (6000.x) 以降
- **HDRP 17.x**（High Definition Render Pipeline）または Built-in Render Pipeline

## 対応プラットフォーム

| プラットフォーム | プラグイン | 状態 |
|----------|--------|--------|
| Windows x86_64 | `openvdbi.dll` | ビルド済み同梱 |
| Linux x86_64 | `libopenvdbi.so` | ビルド済み同梱 |
| macOS | `libopenvdbi.bundle` | ソースからビルド |

## クイックスタート

### 1. プロジェクトを開く

`OpenVDBForUnity/` フォルダを Unity 6 以降で開きます。Windows および Linux 用のネイティブプラグインはリポジトリに同梱されています。

### 2. VDB ファイルをインポート

1. `.vdb` ファイルを `Assets/` フォルダに配置
2. Unity が ScriptedImporter 経由でファイルを自動検出し、以下を生成:
   - VDB ボリュームからサンプリングした 3D テクスチャ
   - バウンディングボックス用メッシュ
   - 適切なボリュームレンダリングシェーダーを使用したマテリアル

### 3. 結果を確認

`Assets/Scenes/Test.unity` を開くか、新しいシーンを作成します。インポートした VDB アセットをシーンにドラッグすると、レイマーチングでボリュームがレンダリングされます。

## 統合ボリュームコンポーネント

`OpenVDBVolume` コンポーネントは、すべてのボリュームレンダリングの統一エントリポイントです。Inspector のドロップダウンから **Classic** と **Realtime** レンダリングモードを切り替えられます。

### レンダリングモード

| モード | 説明 | 最適な用途 |
|------|------|----------|
| **Realtime**（デフォルト） | オキュパンシーグリッド、適応ステッピング、テンポラルジッター、HG 位相関数による最適化レイマーチング | インタラクティブ / リアルタイムアプリケーション |
| **Classic** | HDRP ライトバッファ統合による従来型レイマーチング | オフライン / 最高品質レンダリング |

モードを切り替えると、コンポーネントが自動的に適切なシェーダーを適用し、Inspector には該当モードのパラメータのみを表示します。

### 共有機能（両モード共通）

すべての機能は `shader_feature_local` によるキーワードゲートで、無効時の GPU コストはゼロです。

| 機能 | 説明 |
|------|------|
| **Directional Light** | ライト方向に沿った Beer-Lambert シャドウマーチング |
| **Ambient Light** | 環境光カラーと密度を設定可能 |
| **Light Influence** | ディレクショナルおよびアンビエントライトの影響度乗数 |
| **Color Ramp** | グラデーションベースの密度→カラーマッピング（256x1 テクスチャにベイク） |
| **Spot Lights** | Unity スポットライト最大 2 灯（距離減衰 + コーン減衰） |
| **Shadow Casting** | ボリュームが他のメッシュにシャドウを投影（GPU 負荷高） |
| **Auto Sync Light** | メインのディレクショナルライトの方向と色を自動取得 |

### Realtime 専用機能

| 機能 | 説明 |
|------|------|
| **Quality Presets** | Low / Medium / High / Ultra / Custom プリセット |
| **Empty Space Skipping** | DDA トラバーサル用のスパースオキュパンシーグリッド（コンピュートシェーダー） |
| **Temporal Jitter** | TAA 統合用のピクセル単位レイ開始ノイズ |
| **Adaptive Stepping** | 距離ベースの可変ステップサイズ |
| **Henyey-Greenstein Phase** | 異方性前方/後方散乱 |
| **Multi-Scatter Approximation** | 薄いボリューム用のパウダーエフェクト |

### 深度オプション（HDRP）

- **Write Depth** -- `SV_Depth` 経由で最初のヒットボクセルの深度を書き込み、ポストプロセッシングとの正しい連携を実現
- **Clip Against Scene Depth** -- 不透明ジオメトリ表面でレイを停止

## レンダーパイプライン対応

### HDRP（推奨）

- **Realtime**: `OpenVDB/Realtime/HDRP` -- ShadowCaster パスを含む完全な機能セット
- **Classic**: `OpenVDB/HDRP/Standard` -- HDRP ライトバッファ統合

### Built-in Render Pipeline

- **Realtime**: `OpenVDB/Realtime/Standard` -- `_LightColor0` 統合による完全な機能セット
- **Classic**: `OpenVDB/Standard` -- ShadowCaster パス、Light/Ambient Influence 対応のボリュームレンダリング

## タイムライン / シーケンス再生

VDB アニメーションシーケンスは以下で再生可能:

- **OpenVDBSequencePlayer** -- 連続 `.vdb` ファイル再生用のランタイムコンポーネント
- **Timeline 統合** -- Unity Timeline での再生制御用カスタム Timeline トラック（`OpenVDBTimelineTrack`）

## ネイティブプラグインのビルド

ネイティブプラグインは OpenVDB C++ ライブラリを P/Invoke 経由で Unity から使用するためのラッパーです。

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

# Unity プロジェクトへインストール
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

# Unity プロジェクトへインストール
cp openvdbi/libopenvdbi.bundle \
  ../../OpenVDBForUnity/Assets/OpenVDB/Scripts/Plugins/x86_64/
```

### Windows

```powershell
# vcpkg 経由で依存パッケージをインストール
vcpkg install openvdb:x64-windows

# ビルド
cd Plugin
mkdir build && cd build
cmake .. -DBUILD_SHARED_LIBS=ON -DCMAKE_BUILD_TYPE=Release ^
  -DCMAKE_TOOLCHAIN_FILE=[vcpkg root]/scripts/buildsystems/vcpkg.cmake
cmake --build . --config Release

# Unity プロジェクトへインストール（DLL と依存ファイル）
copy openvdbi\Release\openvdbi.dll ^
  ..\..\OpenVDBForUnity\Assets\OpenVDB\Scripts\Plugins\x86_64\
```

> **注意:** Windows では `openvdbi.dll` と共に以下のランタイム DLL も必要です: `openvdb.dll`, `tbb12.dll`, `blosc.dll`, `lz4.dll`, `zlib1.dll`, `zstd.dll`, `Imath-3_2.dll`。これらはビルド済みパッケージに同梱されています。

## プロジェクト構成

```
OpenVDBForUnity/
├── Plugin/                          # C++ ネイティブプラグインソース
│   ├── openvdbi/                    # OpenVDB ラッパーライブラリ
│   │   ├── Importer/               # VDB 読み込み・ボリュームサンプリング
│   │   └── Foundation/             # メモリ管理、ログユーティリティ
│   └── CMakeLists.txt              # CMake ビルド設定
├── OpenVDBForUnity/                 # Unity プロジェクト
│   └── Assets/
│       └── OpenVDB/
│           ├── Scripts/
│           │   ├── OpenVDBVolume.cs  # 統合ボリュームコンポーネント（Classic + Realtime）
│           │   ├── Importer/        # ランタイム C# スクリプト（P/Invoke、メッシュ、テクスチャ）
│           │   ├── HDRP/            # HDRP 固有コンポーネント（LocalVolumetricFog ブリッジ）
│           │   ├── Realtime/        # Realtime ヘルパー（オキュパンシーグリッド、品質プリセット）
│           │   ├── Plugins/x86_64/  # ネイティブプラグインバイナリ
│           │   ├── Sequence/        # VDB シーケンスプレイヤー
│           │   └── Timeline/        # Timeline 統合
│           ├── Editor/
│           │   ├── OpenVDBVolumeEditor.cs  # 統合 Inspector エディタ
│           │   ├── Importer/        # .vdb ファイル用 ScriptedImporter
│           │   ├── HDRP/            # Classic HDRP シェーダー GUI
│           │   ├── Realtime/        # Realtime シェーダー GUI
│           │   └── Sequence/        # シーケンスエディタ
│           └── Shaders/
│               ├── HDRP/            # Classic HDRP ボリュームシェーダー
│               ├── Realtime/        # Realtime ボリュームシェーダー（HDRP + Standard）
│               ├── VolumeStandard.shader  # Built-in RP Classic シェーダー
│               └── *.cginc          # Built-in RP シェーダーインクルード
└── THIRD_PARTY_LICENSES.md          # 同梱ネイティブ DLL のライセンス
```

## 旧コンポーネントからの移行

レガシーの `OpenVDBHDRPVolume` および `OpenVDBRealtimeVolume` コンポーネントは削除されました。統合された `OpenVDBVolume` コンポーネントを使用してください:

1. GameObject に `OpenVDBVolume` コンポーネントを追加
2. **Render Mode** を選択（Classic または Realtime）
3. Volume Texture を割り当て、パラメータを設定

## 変更履歴

### dev/feature-update (最新)

- **統合アーキテクチャ** -- Classic と Realtime レンダラーを単一の `OpenVDBVolume` コンポーネントに統合。共有機能セットとモード別 Inspector UI を提供
- **クロスパイプライン スポットライト** -- 最大 2 灯のスポットライト（スムーズな二次距離減衰 + コーン減衰）。HDRP と Built-in RP の両方で動作
- **Standard RP ライトカラー** -- Realtime シェーダーがハードコード白ではなく Unity フォワードレンダリングの `_LightColor0` を読み取るように改善
- **HDRP 互換性** -- オブジェクト→ワールド変換に `UNITY_MATRIX_M` を使用し、HDRP と Built-in RP 両方のシェーダーコンパイルに対応
- **キーワードゲート機能** -- すべての共有機能が `shader_feature_local` を使用し、無効時の GPU コストはゼロ
- **シャドウキャスティング** -- 全レンダーパイプラインで専用 ShadowCaster パスによるボリューム影生成。密度閾値設定でバウンディングボックスアーティファクトを防止
- **カラーランプ** -- グラデーションベースの密度→カラーマッピング（256x1 ルックアップテクスチャにベイク）
- **クオリティプリセット** -- Low / Medium / High / Ultra プリセットでワンクリック設定
- **空間スキップ** -- コンピュートシェーダー生成のオキュパンシーグリッドによる DDA トラバーサルで高速スパースボリュームレンダリング
- **レガシー削除** -- 非推奨の `OpenVDBHDRPVolume`、`OpenVDBRealtimeVolume`、および孤立プロトタイプ（BrickMap、TemporalReprojection）を削除
- **Classic Standard RP** -- Light Influence / Ambient Influence ユニフォームおよび ShadowCaster パスを追加
- **リファクタリング** -- ライト検索のキャッシュ化、スポットライト同期とオキュパンシーグリッド生成の重複排除

## ライセンス

MIT

同梱サードパーティライブラリのライセンスは [THIRD_PARTY_LICENSES.md](THIRD_PARTY_LICENSES.md) を参照してください。
