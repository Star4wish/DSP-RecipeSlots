# Changelog

Bilingual: each version lists the English summary first, then the Chinese details.
双语说明：每个版本先列英文摘要，再列中文详情。

## 1.2.0

### English
- **M2 – full looping of the input needs chain**: the `needs` array is now filled to the recipe's ingredient count (no more 6-slot rotation), so there is no advertising duty-cycle loss. All hard-coded 6-item matches were rewritten to length-based loops: `CargoPath.TryPickItem` / `TryPickItemAtRear` / `CanPickItemFromRear`, `StorageComponent.TakeTailItems`, `StationComponent.TakeItem`, and the assembler/lab branches of `PlanetFactory.PickFrom(int/uint)`. Lab normal recipes got the same treatment. `Core.EnableNeedsLoop=false` falls back to the 1.1 rotation.
- Verified: a 12-ingredient recipe receives all 12 ingredients from belts / storage / machines and runs 10 consecutive production cycles (12 products each); 8-ingredient and vanilla recipes still work.
- **Release build and packaging**: new `SHIP` compile symbol. `build.ps1 -Release` produces a release DLL with test content, debug channel, test power patch and `Test.*` configs stripped. `build.ps1 -Release -Package` produces a r2modman / Thunderstore compatible `dist/DSP_RecipeSlots-<version>.zip` plus `.sha256`. Dev builds (`bin/Dev`) keep everything for in-game verification.

### 中文
- **M2：输入需求「全循环化」，去掉 6 格轮转的占空比损失**：`needs` 按配方原料数全长填充，取料匹配全部按长度循环（传送带 3 处、箱子、物流站、机器直送 2 个重载），研究站普通配方同步处理；`Core.EnableNeedsLoop=false` 可回退 1.1 轮转。
- 实测：12 原料配方从传送带/箱子/机器四种来源全部取到第 7–12 种原料，连续生产 10 轮（每轮 12 产物）；8 输入与原版配方回归正常。
- **发布构建与打包**：新增 `SHIP` 编译开关；`build.ps1 -Release` 产出剔除测试内容/调试通道/测试供电/`Test.*` 的发布版 DLL；`build.ps1 -Release -Package` 一键产出 r2modman / Thunderstore 兼容 zip 与 `.sha256`。开发构建（`bin/Dev`）保留全部调试能力。

## 1.1.4

### English
- Product row capacity changed from 4 to 6; the multi-row layout still triggers when ingredients > 6 or products > 4.
- Added 12-ingredient / 12-product stress-test recipes (7007–7009) and verified them in game.

### 中文
- 产物每行容量 4 → 6；多行 UI 触发条件保持「原料 > 6 或 产物 > 4」。
- 新增 12 原料 / 12 产物压力测试配方（7007–7009）并实机验证。

## 1.1.3

### English
- Production window: ingredients now on top (6/row), products below (4/row at the time), speed display centered in between with downward arrows; counts moved to the bottom-right inside each circle; fixed slot coordinate offset (`produce` container offset -100) that overlapped the inventory panel.
- Lab window: 3×3 matrix grid is now auto-scaled and centered inside the left area of the (unchanged) vanilla window, so the right-side hints no longer overlap.

### 中文
- 生产窗口：原料在上（6/行）、产物在下、速度居中且箭头朝下；数量统一到圆圈右下角内侧；修正槽位坐标偏移（`produce` 容器 -100）导致的清单重叠。
- 研究站：3×3 网格在窗口左侧可用区域内自动缩放居中，右侧提示不再重叠。

## 1.1.1 / 1.1.2

### English
- 1.1.1: first version of the production-window multi-row layout (ingredients 6/row, products 4/row, auto window height).
- 1.1.2: lab matrix slots fixed to a 3×3 grid (9 cells, 2 reserved for future 9-matrix tech); fixed crashes when a lab produced an extra matrix, non-contiguous extra matrix ID mapping, and tech `Level/MaxLevel` convention.

### 中文
- 1.1.1：生产窗口多行布局初版（原料 6/行、产物 4/行、窗口自动加高）。
- 1.1.2：研究站矩阵槽固定 3×3 九格（预留 2 格）；修复研究站产出额外矩阵越界崩溃、非连续矩阵 ID 映射、科技 `Level/MaxLevel` 约定等问题。

## 1.1.0

### English
- Added multi-row layout for production windows and a GenesisBook-inspired 3×3 lab matrix grid.
- Added dev test content: pink native matrix item 6010, leaf item 6101, recipes 7003–7006 and test tech 7100.

### 中文
- 新增生产窗口多行布局、参考创世之书的研究站 3×3 九宫格。
- 新增开发测试内容：粉色原生矩阵 6010、树叶 6101、配方 7003–7006、测试科技 7100。

## 1.0.0

### English
- First usable release: >6 ingredients, >6 products, labs with >6 matrices, tech-tree/Tooltip support for >6 costs, vanilla rollback verified, Nebula load-level coexistence verified.

### 中文
- 首个可用版本：>6 输入、>6 产物、研究站 >6 矩阵、科技树/tooltip >6 消耗、原版回退与 Nebula 加载级共存均已验证。
