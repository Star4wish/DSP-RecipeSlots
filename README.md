# DSP-RecipeSlots

Break the vanilla "max 6 ingredients / products per recipe" limit in **Dyson Sphere Program**, with matching UI support for assemblers, the tech tree and research labs.

- Game version: `0.10.35.29151` (EA)
- Runtime: BepInEx 5.4.17 + HarmonyX
- Plugin version: `1.2.0`
- Status: single-player chain fully verified in game; multiplayer checked by static analysis + load-level coexistence (two-client test still pending)

---

## English

### Features

| Feature | Description |
|---|---|
| **>6 ingredients** | Recipes may have more than 6 `Items`; assemblers / smelters / chemical plants accept and consume them (since 1.2.0 with no rotation duty-cycle loss) |
| **>6 products** | Recipes may have more than 2 `Results`; the assembler window shows dynamic product slots, click to take |
| **Labs with >6 matrices** | Matrix labs can consume a 7th+ matrix; `matrixIds/matrixPoints` are extended from config, and need broadcast / bottleneck gate / accept / chained-lab transfer are all looped |
| **Labs with >6 ingredients** | A matrix lab running an `ERecipeType.Research` recipe also supports 7+ ingredients |
| **UI support** | Multi-row ingredient/product layout with auto window height, tech-tree cost icons, tech tooltip cost row, 3×3 matrix grid in the lab window, item/recipe tooltip icon rows |

### How it works

Vanilla hard-codes 6 in the input chain (need broadcast → inserter picking → machine accept). This plugin:

1. fills the `needs` array to the recipe's ingredient count (since 1.2.0; 1.0–1.1 rotated a 6-slot window);
2. rewrites all 6-item hard-coded matching in the 4 pick sources (belt / storage / station / machine-to-machine) into length-based loops;
3. loops the assembler branch of `PlanetFactory.InsertInto(int/uint)` over `requires.Length`;
4. extends lab matrix arrays and loops broadcast / bottleneck / accept / chained transfer;
5. clones vanilla controls to extend the UI.

### Install

1. Install BepInEx 5.4.17 (r2modman / Thunderstore resolves it automatically);
2. Put `DSP-RecipeSlots.dll` into `<game>/BepInEx/plugins/DSP-RecipeSlots/`;
3. Start the game. `DSP-RecipeSlots 1.2.0 loaded` in the log means success.

### Config (`BepInEx/config/star4wish.dsp.recipeslots.cfg`)

| Key | Default | Description |
|---|---|---|
| `Core.EnablePatches` | `true` | Master switch for gameplay patches (`false` = attribution / compatibility A/B) |
| `Core.EnableNeedsLoop` | `true` | 1.2.0 full-loop implementation; `false` falls back to the 1.1 6-slot rotation |
| `Lab.ExtraMatrixIds` | empty | Extra matrix item IDs, comma separated (e.g. `6010`). **No contiguity required** (the plugin remaps by actual index). The content mod must register the items first |
| `Debug.Verbose` | `false` | Patch-hit diagnostics |

> The release build contains **no** test recipes, test tech or file debug channel; they only exist in dev builds (`build.ps1` without `-Release`).

### Known limitations

- Recipe / item / tech IDs must be **< 12000** (`IconSet` index arrays are fixed at 12000).
- Serialized assembler `recipeId` is a `short`: IDs must be ≤ 32767.
- Removing the plugin falls back to vanilla safely: saves load, but machines lose their recipe (LDBTool warning + "missing recipe" idle, products lost), and autosave keeps that state.
- **Nebula multiplayer**: `needs` is not synchronized (recomputed locally each tick); `Served/IncServed` use variable-length arrays and products/matrices sync by index. Static analysis + load-level coexistence verified; **all clients must install the same plugin version**. Two-client test still pending.
- **Orbital Ring (POR)**: POR 1.0.22 cannot start a new game on 0.10.35 in our environment (its own `GameLoader.FixedUpdate` NRE, reproducible with our patches disabled), so POR coexistence could not be tested end to end. The plugin does not touch `AssemblerComponent.InternalUpdate` / `FactorySystem.GameTick` / `GameLogic._assembler_parallel`.
- Lab 9-matrix UI: fixed 3×3 grid (9 cells, 7 active + 2 reserved); configure 9 matrix item IDs in `Lab.ExtraMatrixIds` to use all cells.
- Not covered: ejector/silo bullet matching; station input-slot emptiness check (sums `needs[0..5]`). Both are unrelated to recipe ingredients or have negligible impact.

---

## 中文

### 功能

| 功能 | 说明 |
|---|---|
| **>6 种原料** | 配方 `Items` 可超过 6 项；制造台/熔炉/化工厂等装配类建筑可正常投料、生产（1.2.0 起无轮转占空比损失） |
| **>6 种产物** | 配方 `Results` 可超过 2 项；装配台 UI 动态生成产物槽，可点击取出 |
| **研究站 >6 矩阵** | 研究站可消耗第 7+ 种矩阵；`matrixIds/matrixPoints` 按配置扩展，需求广播/瓶颈判定/受理/叠站转移全部循环化 |
| **研究站 >6 输入普通配方** | 矩阵研究站跑 `ERecipeType.Research` 配方时支持 7+ 原料 |
| **UI 适配** | 装配台产物/原料多行、窗口自适应高度、科技树成本图标、科技 tooltip 成本行、研究站 3×3 九宫格矩阵槽、物品/配方 tooltip 图标行 |

### 工作原理

原版在「输入需求广播 → 分拣器按需抓料 → 机器受理」链上把 6 写死。本插件：

1. `needs` 数组按配方原料数**全长填充**（1.2.0 起；1.0–1.1 为 6 格轮转）；
2. 分拣器取料的 4 类来源（传送带 / 箱子 / 物流站 / 机器直送）的 6 项硬编码匹配全部改为按长度循环；
3. `PlanetFactory.InsertInto(int/uint)` 的装配器分支改为按 `requires.Length` 循环受理；
4. `LabComponent` 系列矩阵数组扩展 + 广播/瓶颈/受理/叠站转移循环化；
5. UI 补丁克隆原生控件动态扩展。

### 安装

1. 安装 BepInEx 5.4.17（r2modman / Thunderstore 会自动处理依赖）；
2. 将 `DSP-RecipeSlots.dll` 放到 `<游戏目录>/BepInEx/plugins/DSP-RecipeSlots/`；
3. 启动游戏。日志出现 `DSP-RecipeSlots 1.2.0 loaded` 即成功。

### 配置（`BepInEx/config/star4wish.dsp.recipeslots.cfg`）

| 配置键 | 默认 | 说明 |
|---|---|---|
| `Core.EnablePatches` | `true` | 玩法补丁总开关（`false` 用于排查兼容性/归因） |
| `Core.EnableNeedsLoop` | `true` | 1.2.0 全循环化实现；`false` 回退到 1.1 的 6 格轮转方案 |
| `Lab.ExtraMatrixIds` | 空 | 额外矩阵物品 ID（逗号分隔，如 `6010`）。**不必连续**：映射由插件按 `matrixIds` 实际下标修正。内容 MOD 需先注册对应物品 |
| `Debug.Verbose` | `false` | 补丁命中诊断日志 |

> 发布版**不包含**任何测试配方、测试科技、文件调试通道；这些只在开发构建（`build.ps1` 不带 `-Release`）中存在。

### 已知限制

- **配方/物品/科技 ID 必须 < 12000**：`IconSet` 索引数组定长 12000；超出会在开新档时崩。
- **装配器存档 `recipeId` 为 `short`**：ID 需 ≤ 32767。
- **去掉插件回退原版**：存档可正常载入，但机器会丢失配方（LDBTool 警告 + 机器显示「缺少配方」停机、产物丢失），自动存档会保留该状态。
- **联机（Nebula）**：`needs` 不在同步包内（各端每 tick 本地重算），`Served/IncServed` 变长数组、产物/矩阵按索引同步。静态分析 + 本体加载级共存已验证；**要求所有客户端安装同一插件版本**。真正的双端实测待补。
- **星环/POR**：本机 POR 1.0.22 在 0.10.35 下无法开新档（其自身 `GameLoader.FixedUpdate` NRE，关闭本插件补丁仍复现），故 POR 共存未能实测；代码面不修改 POR 依赖的 `AssemblerComponent.InternalUpdate` / `FactorySystem.GameTick` / `GameLogic._assembler_parallel`。
- **研究站 9 矩阵**：UI 固定 3×3（9 格，7 个有效 + 2 个预留空格）；要 9 格全用需在 `Lab.ExtraMatrixIds` 配齐 9 个矩阵物品。
- **未覆盖的 6 项硬编码**：弹射器/发射井子弹匹配、物流站输入槽判空（用 `needs[0..5]` 求和）。均与配方原料无关或影响极小。

---

## Build (developers)

```powershell
# Dev build (test recipes + debug channel, for in-game verification)
powershell -ExecutionPolicy Bypass -File build.ps1

# Release build (test content and debug channel stripped) + r2modman/Thunderstore zip
powershell -ExecutionPolicy Bypass -File build.ps1 -Release -Package
```

Outputs: `bin/ReleasePkg/DSP-RecipeSlots.dll`, `dist/DSP_RecipeSlots-<version>.zip` (contains `manifest.json` / `icon.png` / `README.md` / `CHANGELOG.md` / `BepInEx/plugins/DSP-RecipeSlots/`) and a `.sha256` file.

## Credits

- BepInEx / HarmonyX
- No other hard dependencies
