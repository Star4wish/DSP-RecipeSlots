using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace DSPRecipeSlots
{
    /// <summary>
    /// M2：把原版写死 6 格的「输入需求匹配」全部改为按 needs.Length 循环，
    /// 从而让 needs 数组可以按配方原料数（>6，最多 12+）全长填充，
    /// 彻底去掉路线 A 的「轮转广告占空比损失」。
    ///
    /// 改动点：
    ///  1. <see cref="AssemblerNeedsResizePatch"/>：配方变化时把 needs 扩容到原料数；
    ///  2. <see cref="NeedsUtil"/>：Contains/IndexOf 按长度循环；
    ///  3. CargoPath.TryPickItem / TryPickItemAtRear / CanPickItemFromRear：传送带取料匹配；
    ///  4. StorageComponent.TakeTailItems：箱子取料匹配；
    ///  5. StationComponent.TakeItem：物流站取料匹配。
    /// 原版方法只在 needs.Length &gt; 6 时被接管，≤6 时完全走原版，零行为变化。
    /// </summary>
    internal static class NeedsUtil
    {
        public static bool Contains(int[] needs, int item)
        {
            if (needs == null) return false;
            for (int i = 0; i < needs.Length; i++)
                if (needs[i] == item) return true;
            return false;
        }

        public static int IndexOf(int[] needs, int item)
        {
            if (needs == null) return -1;
            for (int i = 0; i < needs.Length; i++)
                if (needs[i] == item) return i;
            return -1;
        }

        /// <summary>与 AssemblerComponent 内部 split_inc 等价。</summary>
        public static int SplitInc(ref int n, ref int m, int p)
        {
            if (n == 0) return 0;
            int num = m / n;
            int num2 = m - num * n;
            n -= p;
            num2 -= n;
            num = (num2 > 0) ? (num * p + num2) : (num * p);
            m -= num;
            return num;
        }

        internal static bool Enabled()
        {
            try { return Plugin.EnableNeedsLoop == null || Plugin.EnableNeedsLoop.Value; }
            catch { return true; }
        }
    }

    // ───────────────────────── 1) needs 扩容 ─────────────────────────

    [HarmonyPatch(typeof(AssemblerComponent), nameof(AssemblerComponent.SetRecipe))]
    internal static class AssemblerNeedsResizePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref AssemblerComponent __instance)
        {
            try
            {
                var data = __instance.recipeExecuteData;
                if (data == null || data.requires == null) return;
                int need = Math.Max(6, data.requires.Length);
                if (__instance.needs == null || __instance.needs.Length != need)
                    __instance.needs = new int[need];
            }
            catch (Exception ex) { Plugin.Log.LogError("[m2] resize needs: " + ex); }
        }
    }

    // ───────────────────────── 2) 传送带取料 ─────────────────────────

    internal static class CargoPathFields
    {
        internal static readonly AccessTools.FieldRef<CargoPath, int> BufLen =
            AccessTools.FieldRefAccess<CargoPath, int>("bufferLength");
        internal static readonly AccessTools.FieldRef<CargoPath, int> UpdLen =
            AccessTools.FieldRefAccess<CargoPath, int>("updateLen");
    }

    [HarmonyPatch]
    internal static class CargoPathTryPickItemPatch
    {
        private static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(CargoPath), "TryPickItem", new Type[]
            {
                typeof(int), typeof(int), typeof(int), typeof(int[]),
                typeof(byte).MakeByRefType(), typeof(byte).MakeByRefType()
            });
        }

        [HarmonyPrefix]
        private static bool Prefix(CargoPath __instance, int index, int length, int filter, int[] needs,
            out byte stack, out byte inc, ref int __result)
        {
            stack = 1;
            inc = 0;
            if (needs == null || needs.Length <= 6 || !NeedsUtil.Enabled()) return true;
            try
            {
                var buffer = __instance.buffer;
                var cargo = __instance.cargoContainer;
                lock (buffer)
                {
                    int bl = CargoPathFields.BufLen(__instance);
                    if (index < 0) index = 0;
                    else if (index >= bl) index = bl - 1;
                    int end = index + length;
                    if (end > bl) end = bl;
                    for (int i = index; i < end; i++)
                    {
                        if (buffer[i] < 246) continue;
                        i += 250 - buffer[i];
                        int ci = buffer[i + 1] - 1 + (buffer[i + 2] - 1) * 100
                                 + (buffer[i + 3] - 1) * 10000 + (buffer[i + 4] - 1) * 1000000;
                        int item = cargo.cargoPool[ci].item;
                        stack = cargo.cargoPool[ci].stack;
                        inc = cargo.cargoPool[ci].inc;
                        if ((filter == 0 || item == filter) && NeedsUtil.Contains(needs, item))
                        {
                            Array.Clear(buffer, i - 4, 10);
                            int n3 = i + 5 + 1;
                            if (CargoPathFields.UpdLen(__instance) < n3) CargoPathFields.UpdLen(__instance) = n3;
                            cargo.RemoveCargo(ci);
                            __result = item;
                            return false;
                        }
                        __result = 0;
                        return false;
                    }
                }
                __result = 0;
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[m2] CargoPath.TryPickItem: " + ex);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(CargoPath), nameof(CargoPath.TryPickItemAtRear))]
    internal static class CargoPathTryPickItemAtRearPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(CargoPath __instance, int[] needs, out int needIdx,
            out byte stack, out byte inc, ref int __result)
        {
            needIdx = -1;
            stack = 1;
            inc = 0;
            if (needs == null || needs.Length <= 6 || !NeedsUtil.Enabled()) return true;
            try
            {
                var buffer = __instance.buffer;
                var cargo = __instance.cargoContainer;
                lock (buffer)
                {
                    int bl = CargoPathFields.BufLen(__instance);
                    int pos = bl - 5 - 1;
                    if (pos >= 0 && pos < buffer.Length && buffer[pos] == 250)
                    {
                        int ci = buffer[pos + 1] - 1 + (buffer[pos + 2] - 1) * 100
                                 + (buffer[pos + 3] - 1) * 10000 + (buffer[pos + 4] - 1) * 1000000;
                        int item = cargo.cargoPool[ci].item;
                        stack = cargo.cargoPool[ci].stack;
                        inc = cargo.cargoPool[ci].inc;
                        int idx = NeedsUtil.IndexOf(needs, item);
                        if (idx >= 0)
                        {
                            Array.Clear(buffer, pos - 4, 10);
                            int n3 = pos + 5 + 1;
                            if (CargoPathFields.UpdLen(__instance) < n3) CargoPathFields.UpdLen(__instance) = n3;
                            cargo.RemoveCargo(ci);
                            needIdx = idx;
                            __result = item;
                            return false;
                        }
                    }
                }
                __result = 0;
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[m2] CargoPath.TryPickItemAtRear: " + ex);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(CargoPath), nameof(CargoPath.CanPickItemFromRear))]
    internal static class CargoPathCanPickItemFromRearPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(CargoPath __instance, int[] needs, ref bool __result)
        {
            if (needs == null || needs.Length <= 6 || !NeedsUtil.Enabled()) return true;
            try
            {
                var buffer = __instance.buffer;
                var cargo = __instance.cargoContainer;
                int bl = CargoPathFields.BufLen(__instance);
                int pos = bl - 5 - 1;
                if (pos >= 0 && pos < buffer.Length && buffer[pos] == 250)
                {
                    int ci = buffer[pos + 1] - 1 + (buffer[pos + 2] - 1) * 100
                             + (buffer[pos + 3] - 1) * 10000 + (buffer[pos + 4] - 1) * 1000000;
                    int item = cargo.cargoPool[ci].item;
                    if (item != 0 && NeedsUtil.Contains(needs, item))
                    {
                        __result = true;
                        return false;
                    }
                }
                __result = false;
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[m2] CanPickItemFromRear: " + ex);
                return true;
            }
        }
    }

    // ───────────────────────── 3) 箱子取料 ─────────────────────────

    [HarmonyPatch]
    internal static class StorageTakeTailItemsPatch
    {
        private static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(StorageComponent), "TakeTailItems", new Type[]
            {
                typeof(int).MakeByRefType(), typeof(int).MakeByRefType(), typeof(int[]),
                typeof(int).MakeByRefType(), typeof(bool)
            });
        }

        [HarmonyPrefix]
        private static bool Prefix(StorageComponent __instance, ref int itemId, ref int count, int[] needs,
            out int inc, bool useBan, ref bool __result)
        {
            inc = 0;
            if (needs == null || needs.Length <= 6 || !NeedsUtil.Enabled()) return true;
            try
            {
                if (count == 0)
                {
                    itemId = 0;
                    count = 0;
                    __result = false;
                    return false;
                }
                bool result = false;
                int num = count;
                count = 0;
                var grids = __instance.grids;
                int start = useBan ? (__instance.size - __instance.bans - 1) : (__instance.size - 1);
                if (start >= grids.Length) start = grids.Length - 1;
                for (int i = start; i >= 0; i--)
                {
                    if (grids[i].itemId == 0 || grids[i].count == 0) continue;
                    if (itemId != 0 && grids[i].itemId != itemId) continue;
                    result = true;
                    if (!NeedsUtil.Contains(needs, grids[i].itemId)) continue;
                    itemId = grids[i].itemId;
                    if (grids[i].count > num)
                    {
                        inc += NeedsUtil.SplitInc(ref grids[i].count, ref grids[i].inc, num);
                        count += num;
                        num = 0;
                        break;
                    }
                    inc += grids[i].inc;
                    count += grids[i].count;
                    num -= grids[i].count;
                    grids[i].itemId = grids[i].filter;
                    grids[i].count = 0;
                    grids[i].inc = 0;
                    if (grids[i].filter == 0) grids[i].stackSize = 0;
                }
                if (count == 0)
                {
                    itemId = 0;
                    count = 0;
                }
                else
                {
                    __instance.lastFullItem = -1;
                    __instance.NotifyStorageChange();
                }
                __result = result;
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[m2] TakeTailItems: " + ex);
                return true;
            }
        }
    }

    // ───────────────────────── 4) 物流站取料 ─────────────────────────

    [HarmonyPatch]
    internal static class StationTakeItemPatch
    {
        private static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(StationComponent), "TakeItem", new Type[]
            {
                typeof(int).MakeByRefType(), typeof(int).MakeByRefType(), typeof(int[]),
                typeof(int).MakeByRefType()
            });
        }

        [HarmonyPrefix]
        private static bool Prefix(StationComponent __instance, ref int _itemId, ref int _count, int[] _needs,
            out int _inc)
        {
            _inc = 0;
            if (_needs == null || _needs.Length <= 6 || !NeedsUtil.Enabled()) return true;
            try
            {
                if (_itemId > 0 && _count > 0 && NeedsUtil.Contains(_needs, _itemId))
                {
                    var storage = __instance.storage;
                    if (storage != null)
                    {
                        lock (storage)
                        {
                            for (int i = 0; i < storage.Length; i++)
                            {
                                if (storage[i].itemId == _itemId && storage[i].count > 0)
                                {
                                    _count = (_count < storage[i].count) ? _count : storage[i].count;
                                    _itemId = storage[i].itemId;
                                    _inc = NeedsUtil.SplitInc(ref storage[i].count, ref storage[i].inc, _count);
                                    return false;
                                }
                            }
                        }
                    }
                }
                _itemId = 0;
                _count = 0;
                _inc = 0;
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[m2] StationComponent.TakeItem: " + ex);
                return true;
            }
        }
    }
    // ───────────────────────── 5) 机器/研究站作为取料源（PickFrom 内联 6 项匹配）─────────────────────────

    /// <summary>
    /// 原版 PickFrom 的「装配器 / 研究站作为源」分支把 needs 展开成 6 项内联比较，
    /// 目标原料 >6 时第 7 种以后的产物永远匹配不上。这里仅在 needs.Length &gt; 6 且源是
    /// 装配器/研究站时接管，其余源（传送带/箱子/物流站）继续走原版（其内部已由上面几个补丁修好）。
    /// </summary>
    [HarmonyPatch]
    internal static class PickFromIntM2Patch
    {
        private static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(PlanetFactory), "PickFrom", new Type[]
            {
                typeof(int), typeof(int), typeof(int), typeof(int[]),
                typeof(byte).MakeByRefType(), typeof(byte).MakeByRefType()
            });
        }

        [HarmonyPrefix]
        private static bool Prefix(PlanetFactory __instance, int entityId, int offset, int filter, int[] needs,
            out byte stack, out byte inc, ref int __result)
        {
            stack = 1; inc = 0;
            if (needs == null || needs.Length <= 6 || !NeedsUtil.Enabled()) return true;
            try
            {
                if (entityId <= 0 || entityId >= __instance.entityCursor) return true;
                int aid = __instance.entityPool[entityId].assemblerId;
                int lid = __instance.entityPool[entityId].labId;
                if (aid <= 0 && lid <= 0) return true;
                lock (__instance.entityMutexs[entityId])
                {
                    int[] products; int[] produced;
                    if (aid > 0)
                    {
                        ref AssemblerComponent a = ref __instance.factorySystem.assemblerPool[aid];
                        products = a.recipeExecuteData?.products;
                        produced = a.produced;
                    }
                    else
                    {
                        ref LabComponent l = ref __instance.factorySystem.labPool[lid];
                        products = l.recipeExecuteData?.products;
                        produced = l.produced;
                    }
                    __result = M2Pick(products, produced, filter, needs);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[m2] PickFrom(int): " + ex);
                return true;
            }
        }

        internal static int M2Pick(int[] products, int[] produced, int filter, int[] needs)
        {
            if (products == null || produced == null) return 0;
            int n = products.Length;
            for (int i = 0; i < n; i++)
            {
                int id = products[i];
                if (id <= 0 || produced[i] <= 0) continue;
                if (filter != 0 && filter != id) continue;
                if (!NeedsUtil.Contains(needs, id)) continue;
                produced[i]--;
                return id;
            }
            return 0;
        }
    }

    [HarmonyPatch]
    internal static class PickFromUIntM2Patch
    {
        private const uint AssemblerTag = 0x02000000u;
        private const uint LabTag = 0x07000000u;

        private static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(PlanetFactory), "PickFrom", new Type[]
            {
                typeof(uint), typeof(int), typeof(int), typeof(int[]),
                typeof(byte).MakeByRefType(), typeof(byte).MakeByRefType()
            });
        }

        [HarmonyPrefix]
        private static bool Prefix(PlanetFactory __instance, uint ioTargetTypedId, int offset, int filter, int[] needs,
            out byte stack, out byte inc, ref int __result)
        {
            stack = 1; inc = 0;
            if (needs == null || needs.Length <= 6 || !NeedsUtil.Enabled()) return true;
            try
            {
                uint tag = ioTargetTypedId & 0xFF000000u;
                if (tag != AssemblerTag && tag != LabTag) return true;
                int num = (int)(ioTargetTypedId & 0xFFFFFF);
                int[] products; int[] produced;
                if (tag == AssemblerTag)
                {
                    if (num <= 0 || num >= __instance.factorySystem.assemblerCursor) return true;
                    ref AssemblerComponent a = ref __instance.factorySystem.assemblerPool[num];
                    if (a.id <= 0) return true;
                    products = a.recipeExecuteData?.products;
                    produced = a.produced;
                    lock (__instance.entityMutexs[a.entityId])
                        __result = PickFromIntM2Patch.M2Pick(products, produced, filter, needs);
                }
                else
                {
                    if (num <= 0 || num >= __instance.factorySystem.labCursor) return true;
                    ref LabComponent l = ref __instance.factorySystem.labPool[num];
                    if (l.id <= 0) return true;
                    products = l.recipeExecuteData?.products;
                    produced = l.produced;
                    lock (__instance.entityMutexs[l.entityId])
                        __result = PickFromIntM2Patch.M2Pick(products, produced, filter, needs);
                }
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[m2] PickFrom(uint): " + ex);
                return true;
            }
        }
    }
}



