using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace DSPRecipeSlots
{
    /// <summary>
    /// M4：研究站 / 科技 >6 矩阵（消耗种类 >6）。
    ///
    /// 机制要点（源码依据）：
    ///  - 矩阵槽位映射是 `matrixPoints[itemId - matrixIds[0]] = tech.ItemPoints[j]`（GameTick / GameData.Import 各一处），
    ///    所以额外矩阵必须**紧接 6006 连续**（如 6007、6008）；
    ///  - 消费循环本身是 `for i &lt; matrixServed.Length`（full.cs:56202-56223），扩容即生效；
    ///  - 真正写死 6 的地方：`UpdateNeedsResearch`（需求广播）、`InternalUpdateResearch` 瓶颈判定 6 段、
    ///    `InsertInto` 研究分支 `itemId-6001 in [0,6)`、`UpdateOutputToNext` 6 段（链式转移，暂不扩）。
    ///
    /// 本补丁：
    ///  1. 启动时按配置把 `LabComponent.matrixIds / TechProto.matrixIds / LabComponent.matrixPoints` 扩到 N；
    ///  2. `UpdateNeedsResearch` 后置：按 matrixIds.Length 广播需求（并把旧存档的 6 长数组扩到 N）；
    ///  3. `UpdateNeedsAssemble` 后置：研究站跑 >6 输入的普通配方时复用「需求轮转」；
    ///  4. `InsertInto`（int/uint）研究分支：按 matrixIds 查表受理第 7+ 种矩阵；
    ///  5. `InternalUpdateResearch` 前置：把「每种矩阵至少 1 个」的瓶颈判定扩展到全部矩阵。
    /// </summary>
    internal static class LabPatches
    {
        // ───────────────────────── 启动扩展 ─────────────────────────

        // ───────────────────────── 启动扩展（发布态也生效） ─────────────────────────

        /// <summary>
        /// 矩阵数组扩展必须在 VFPreload 预载结束后执行，且**不能**依赖测试配方开关
        /// （Test.EnableTestRecipe 只控制开发用测试内容；正式内容 MOD 靠 Lab.ExtraMatrixIds 生效）。
        /// </summary>
        [HarmonyPatch(typeof(VFPreload), "InvokeOnLoadWorkEnded")]
        [HarmonyPriority(Priority.Last)]
        internal static class LabInitPatch
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
                try { ExtendMatrices(); }
                catch (Exception ex) { Plugin.Log.LogError("[lab] ExtendMatrices failed: " + ex); }
            }
        }

        internal static void ExtendMatrices()
        {
            string cfg = Plugin.LabExtraMatrixIds.Value;
            if (string.IsNullOrWhiteSpace(cfg)) return;
            var wanted = new List<int>();
            foreach (string part in cfg.Split(','))
            {
                int id;
                if (int.TryParse(part.Trim(), out id) && id > 0) wanted.Add(id);
            }
            if (wanted.Count == 0) return;

            var curIds = LabComponent.matrixIds;
            if (curIds == null || curIds.Length == 0) return;

            // 支持任意额外矩阵 ID（不要求紧跟 6006 连续）：下标换算由 RemapMatrixPoints 负责。
            var extra = new List<int>();
            foreach (int id in wanted)
            {
                if (Array.IndexOf(curIds, id) >= 0) continue;
                if (extra.Contains(id)) continue;
                if (LDB.items.Select(id) == null)
                    Plugin.Log.LogWarning("[lab] extra matrix item " + id + " not in LDB yet (will be registered later?)");
                extra.Add(id);
            }
            if (extra.Count == 0) return;

            int newLen = curIds.Length + extra.Count;
            var newIds = new int[newLen];
            Array.Copy(curIds, newIds, curIds.Length);
            for (int i = 0; i < extra.Count; i++) newIds[curIds.Length + i] = extra[i];

            LabComponent.matrixIds = newIds;
            TechProto.matrixIds = newIds;

            var oldPoints = LabComponent.matrixPoints;
            var newPoints = new int[newLen];
            if (oldPoints != null) Array.Copy(oldPoints, newPoints, Math.Min(oldPoints.Length, newLen));
            LabComponent.matrixPoints = newPoints;

            Plugin.Log.LogInfo("[lab] matrix extended: ids=[" + string.Join(",", newIds) + "] pointsLen=" + newLen);
        }

        /// <summary>
        /// 原版在两处用 `itemId - matrixIds[0]` 把科技消耗的矩阵映射到 matrixPoints 下标；
        /// 额外矩阵 ID 不连续时（例如 6010）会映射错位，这里按 matrixIds 实际下标重算。
        /// 只在矩阵数 &gt; 6 时生效，≤6 时原版映射本来就正确。
        /// </summary>
        internal static void RemapMatrixPoints(int techId)
        {
            var ids = LabComponent.matrixIds;
            var pts = LabComponent.matrixPoints;
            if (ids == null || pts == null || ids.Length <= 6) return;
            Array.Clear(pts, 0, pts.Length);
            if (techId <= 0) return;
            var tech = LDB.techs.Select(techId);
            if (tech == null || tech.Items == null) return;
            for (int j = 0; j < tech.Items.Length; j++)
            {
                int idx = Array.IndexOf(ids, tech.Items[j]);
                if (idx >= 0 && idx < pts.Length) pts[idx] = tech.ItemPoints[j];
            }
        }

        [HarmonyPatch(typeof(FactorySystem), nameof(FactorySystem.GameTickLabResearchMode))]
        internal static class LabMatrixRemapTickPatch
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
                try
                {
                    var history = GameMain.history;
                    RemapMatrixPoints(history != null ? history.currentTech : 0);
                }
                catch (Exception ex) { Plugin.Log.LogError("[lab] remap tick: " + ex); }
            }
        }

        [HarmonyPatch(typeof(GameHistoryData), nameof(GameHistoryData.Import))]
        internal static class LabMatrixRemapImportPatch
        {
            [HarmonyPostfix]
            private static void Postfix(GameHistoryData __instance)
            {
                try { RemapMatrixPoints(__instance != null ? __instance.currentTech : 0); }
                catch (Exception ex) { Plugin.Log.LogError("[lab] remap import: " + ex); }
            }
        }

        // ───────────────────────── needs 扩容（M2） ─────────────────────────

        /// <summary>研究站普通配方模式：needs 按原料数扩容（原版固定 6 格）。</summary>
        [HarmonyPatch(typeof(LabComponent), nameof(LabComponent.SetFunction))]
        internal static class LabNeedsResizePatch
        {
            [HarmonyPostfix]
            private static void Postfix(ref LabComponent __instance)
            {
                try
                {
                    if (__instance.researchMode) return;
                    var data = __instance.recipeExecuteData;
                    if (data == null || data.requires == null) return;
                    int need = Math.Max(6, data.requires.Length);
                    if (__instance.needs == null || __instance.needs.Length != need)
                        __instance.needs = new int[need];
                }
                catch (Exception ex) { Plugin.Log.LogError("[m2] lab resize needs: " + ex); }
            }
        }

        // ───────────────────────── 需求广播 ─────────────────────────

        [HarmonyPatch(typeof(LabComponent), nameof(LabComponent.UpdateNeedsResearch))]
        internal static class LabUpdateNeedsResearchPatch
        {
            [HarmonyPostfix]
            private static void Postfix(ref LabComponent __instance)
            {
                try
                {
                    var ids = LabComponent.matrixIds;
                    var served = __instance.matrixServed;
                    var inc = __instance.matrixIncServed;
                    if (ids == null || served == null) return;
                    int n = ids.Length;
                    if (__instance.needs == null || __instance.needs.Length != n)
                        __instance.needs = new int[n];
                    if (served.Length != n)
                    {
                        var ns = new int[n];
                        Array.Copy(served, ns, Math.Min(served.Length, n));
                        __instance.matrixServed = ns;
                        served = ns;
                    }
                    if (inc == null || inc.Length != n)
                    {
                        var ni = new int[n];
                        if (inc != null) Array.Copy(inc, ni, Math.Min(inc.Length, n));
                        __instance.matrixIncServed = ni;
                    }
                    var needs = __instance.needs;
                    for (int i = 0; i < n; i++)
                        needs[i] = (served[i] < 36000) ? ids[i] : 0;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError("[lab] UpdateNeedsResearch patch: " + ex);
                }
            }
        }

        /// <summary>研究站跑 &gt;6 输入普通配方时的需求轮转（与 AssemblerComponent 同策略）。</summary>
        [HarmonyPatch(typeof(LabComponent), nameof(LabComponent.UpdateNeedsAssemble))]
        internal static class LabUpdateNeedsAssemblePatch
        {
            [HarmonyPostfix]
            private static void Postfix(ref LabComponent __instance)
            {
                var data = __instance.recipeExecuteData;
                if (data == null) return;
                var req = data.requires;
                var needs = __instance.needs;
                var served = __instance.served;
                if (req == null || needs == null || served == null) return;
                int n = req.Length;
                if (n <= 6) return;

                int buffer = ((data.timeSpend > 5400000) ? 6 : (3 * ((__instance.speedOverride + 5001) / 10000) + 3));
                int cap = needs.Length;
                int slot = 0;
                if (cap >= n)
                {
                    // M2：needs 已按原料数扩容 → 直接按索引全量填充，无轮转占空比损失
                    for (int i = 0; i < n; i++)
                        needs[i] = (i < served.Length && served[i] < buffer) ? req[i] : 0;
                    for (int i = n; i < cap; i++) needs[i] = 0;
                    return;
                }
                int rot = (int)(GameMain.gameTick % n);
                if (rot < 0) rot += n;
                for (int k = 0; k < n && slot < cap; k++)
                {
                    int i = rot + k;
                    if (i >= n) i -= n;
                    if (i < served.Length && served[i] < buffer) needs[slot++] = req[i];
                }
                for (; slot < cap; slot++) needs[slot] = 0;
            }
        }

        // ───────────────────────── 研究站普通模式产物 ─────────────────────────

        /// <summary>
        /// 原版 `InternalUpdateAssemble` 的返回值被 `_lab_produce_parallel` 当作
        /// `matrixShaderStates[]` 下标使用（原版假设产物一定是 6001..6006 矩阵）：
        ///   return (uint)(products[0] - matrixIds[0] + 1);
        /// 产出额外矩阵（如 6010）时会得到 10 → 越界崩溃。这里按 matrixIds 实际下标重映射并夹紧。
        /// </summary>
        [HarmonyPatch(typeof(LabComponent), nameof(LabComponent.InternalUpdateAssemble))]
        internal static class LabInternalUpdateAssemblePatch
        {
            private const int ShaderStateLen = 10; // LabComponent.matrixShaderStates.Length

            [HarmonyPostfix]
            private static void Postfix(ref LabComponent __instance, ref uint __result)
            {
                try
                {
                    if (__result == 0u) return;
                    var data = __instance.recipeExecuteData;
                    if (data == null || data.products == null || data.products.Length == 0) return;
                    int idx = Array.IndexOf(LabComponent.matrixIds, data.products[0]);
                    if (idx >= 0) __result = (uint)(idx + 1);
                    if (__result >= ShaderStateLen) __result = 0u;
                }
                catch (Exception ex) { Plugin.Log.LogError("[lab] InternalUpdateAssemble patch: " + ex); }
            }
        }

        // ───────────────────────── 研究进度瓶颈（>6 矩阵） ─────────────────────────

        [HarmonyPatch(typeof(LabComponent), nameof(LabComponent.InternalUpdateResearch))]
        internal static class LabInternalUpdateResearchPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(ref LabComponent __instance, ref uint __result)
            {
                try
                {
                    var served = __instance.matrixServed;
                    var points = LabComponent.matrixPoints;
                    if (served == null || points == null) return true;
                    int n = Math.Min(served.Length, points.Length);
                    for (int i = 0; i < n; i++)
                    {
                        // 与原版等价：某矩阵有需求（points>0）但存量不足 1 个 → 停机
                        if (points[i] > 0 && served[i] < points[i])
                        {
                            __instance.replicating = false;
                            __result = 0u;
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError("[lab] InternalUpdateResearch patch: " + ex);
                }
                return true;
            }
        }
        // ───────────────────────── 研究站链式转移（>6 矩阵） ─────────────────────────

        /// <summary>
        /// 原版 `UpdateOutputToNext` 的矩阵分支是 6 段写死（6001..6006 逐槽），
        /// 扩到 7+ 矩阵后第 7 种不会在叠站之间传递。这里只接管「矩阵长度 &gt; 6」的情况，
        /// 其余（普通配方 served 分支）继续走原版（其循环本身就是 served.Length）。
        /// </summary>
        [HarmonyPatch(typeof(LabComponent), nameof(LabComponent.UpdateOutputToNext))]
        internal static class LabUpdateOutputToNextPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(ref LabComponent __instance, LabComponent[] labPool)
            {
                try
                {
                    var ms = __instance.matrixServed;
                    if (ms == null || ms.Length <= 6) return true; // 交给原版
                    int nxt = __instance.nextLabId;
                    if (nxt <= 0 || nxt >= labPool.Length) return true;
                    var next = labPool[nxt];
                    if (next.id == 0 || next.id != nxt) { __instance.nextLabId = 0; return false; }
                    if (next.needs == null || __instance.recipeId != next.recipeId || __instance.techId != next.techId) return false;
                    var nms = next.matrixServed;
                    var inc = __instance.matrixIncServed;
                    var ninc = next.matrixIncServed;
                    var ids = LabComponent.matrixIds;
                    if (nms == null || inc == null || ninc == null || ids == null) return false;

                    int n = Math.Min(Math.Min(ms.Length, nms.Length), Math.Min(ids.Length, next.needs.Length));
                    lock (ms)
                    {
                        lock (nms)
                        {
                            for (int i = 0; i < n; i++)
                            {
                                if (next.needs[i] != ids[i]) continue;
                                if (ms[i] < 7200) continue;
                                int amount = (ms[i] - 7200) / 3600 * 3600;
                                if (amount > 36000) amount = 36000;
                                int moved = SplitInc(ref ms[i], ref inc[i], amount);
                                ninc[i] += moved;
                                nms[i] += amount;
                            }
                        }
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError("[lab] UpdateOutputToNext patch: " + ex);
                    return true;
                }
            }

            private static int SplitInc(ref int n, ref int m, int p)
            {
                int num = m / n;
                int num2 = m - num * n;
                n -= p;
                num2 -= n;
                m -= (num2 > 0) ? (num * p + num2) : (num * p);
                return num;
            }
        }
    }
}


