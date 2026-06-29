# LevelName Code Injection 実装メモ

## 目的

`LevelSelector` の既存 `LoadMap.Write(...)` だけでは反映が抜けるケースがあるため、
Cheat Engine スクリプト（AOB + hook + LevelName バッファ差し替え）相当の処理を C# で実装しました。

本実装は **enable/disable 可能なコード注入** で、以下を行います。

1. AOB で注入ポイントを特定
2. リモートメモリを確保（trampoline と LevelName）
3. 注入ポイントを `jmp trampoline` へ差し替え
4. trampoline 内で LevelName を `[rdx+0..38h]` にコピー
5. disable 時に元バイト復元 + メモリ解放

---

## 追加・変更ファイル

- `Memory/LevelNameCodeInjector.cs`（新規）
  - 注入の本体クラス
- `Memory/CrashMemory.cs`
  - `EnableReliableLevelWrite` / `DisableReliableLevelWrite` 追加
  - hook/unhook 時の injector bind/disable/unbind 連携
- `Controls/LevelSelectorControl.cs`
  - レベル lock 時に injector enable
  - unlock/切断時に injector disable
- `Crash.Helper.csproj`
  - `Memory\LevelNameCodeInjector.cs` を Compile 追加

---

## 実装の中核クラス

## `LevelNameCodeInjector`

### 主要責務

- AOB 検索
- モジュール内一意性チェック
- 近傍 `VirtualAllocEx`（rel32 jump 用）
- トランポリン命令列構築
- hook パッチ適用／復元
- `LevelName` 64byte バッファ更新

### シグネチャ

```text
4188004885D274??4983C8FF
```

CE の `41 88 00 48 85 D2 74 ** 49 83 C8 FF` 相当です。

### hook 長

- 6 byte
- 元バイト `41 88 00 48 85 D2` を保存して disable 時に復元

---

## CE スクリプトとの対応

### CE 側

- `alloc(newmem,$1000,INJECT)`
- `alloc(LevelName,64)`
- `INJECT: jmp newmem / nop`
- `[DISABLE]` で `db 41 88 00 48 85 D2`

### C# 側対応

- `VirtualAllocEx(... PAGE_EXECUTE_READWRITE)` で trampoline を確保
- `VirtualAllocEx(... PAGE_READWRITE)` で `LevelName` 64byte を確保
- `WriteProcessMemory` で `E9 rel32 + 90` を書き込み
- disable 時に保存済み元6byteを書き戻し
- `VirtualFreeEx(... MEM_RELEASE)` で解放

---

## トランポリン内容（概念）

1. `push rax`
2. `[moduleBase + 0x01A5C6D8]` を辿り `+0x18` と `r8` を比較
3. 不一致なら original へ
4. 一致時のみ `LevelName` バッファを 8byte * 8回コピー
   - `[rdx + 0x00]`
   - `[rdx + 0x08]`
   - `[rdx + 0x10]`
   - `[rdx + 0x18]`
   - `[rdx + 0x20]`
   - `[rdx + 0x28]`
   - `[rdx + 0x30]`
   - `[rdx + 0x38]`
5. `pop rax`
6. 元命令 `mov [r8], al` / `test rdx, rdx` 実行
7. `jmp return`（`INJECT + 6`）

---

## rel32 と近傍アロケーション

`E9 rel32` は ±2GB 制約があるため、`INJECT` 近傍への確保を優先しています。

- `AllocateNear(...)` で `target ± 0x10000` ステップ探索
- 確保できても rel32 範囲外なら解放して再試行
- すべて失敗したら enable を失敗として返す（安全側）

---

## CrashMemory API

### 追加メソッド

- `bool EnableReliableLevelWrite(string mapValue, out string error)`
- `bool DisableReliableLevelWrite(out string error)`

### 動作

- Hook 中プロセスに injector を bind
- enable で hook をインストール（未導入時）し、`LevelName` を更新
- unhook 時は自動で disable + unbind

---

## LevelSelector への統合

`SetSelectedLevel(bool lockIt)` で:

- `lockIt == true`
  - `memory.EnableReliableLevelWrite(map, out error)` を実行
  - 失敗時は Trace 出力し、既存ルートは継続
- `lockIt == false`
  - `memory.DisableReliableLevelWrite(out error)`

`StopLock()` と `dataControl.EnabledChanged`（未接続時）でも disable 実行して、
パッチ残留を避けています。

---

## エラーハンドリング方針

- AOB 不一致（0件/複数件）は注入しない
- 近傍確保失敗は注入しない
- 書き込み失敗時はエラー文字列を返す
- 部分失敗時も `Disable` で可能な限り復旧

---

## 運用上の注意

1. ゲーム更新で命令列が変わるとシグネチャ再調整が必要
2. 管理者権限・保護機構の状態で API 成否が変わる可能性がある
3. 例外は UI には出さず、現状は Trace 中心
4. 対象が x64 前提（rel32 / 命令列前提）

---

## 学習ポイント（この実装で学べること）

- CE スクリプトを C# に落とし込む手順
- AOB 検索 + モジュール内一意性検証
- リモートプロセスのコード差し替えと復元
- rel32 制約を考慮したメモリ確保戦略
- UI ロジック（LevelSelector）と注入ロジックの責務分離