using System;
using HarmonyLib;

namespace DSPRecipeSlots
{
    /// <summary>
    /// 输入链补丁（路线 A：需求轮转 + 受理循环化）。
    /// 注意：不要整方法替换 AssemblerComponent.InternalUpdate（星环/POR 用 CodeMatcher 锚定了它）。
    /// </summary>
    internal static class Patches
    {
        [HarmonyPatch(typeof(AssemblerComponent), nameof(AssemblerComponent.UpdateNeeds))]
        internal static class AssemblerUpdateNeedsPatch
        {
            private static long _hit;

            [HarmonyPostfix]
            private static void Postfix(ref AssemblerComponent __instance)
            {
                var data = __instance.recipeExecuteData;
                if (data == null) return;
                var req = data.requires;
                var needs = __instance.needs;
                var served = __instance.served;
                var counts = data.requireCounts;
                if (req == null || needs == null || served == null || counts == null) return;
                int n = req.Length;
                if (n <= 6) return; // 原版行为，不动

                int buffer = __instance.speedOverride * 180 / data.timeSpend + 1;
                if (buffer < 2) buffer = 2;

                int cap = needs.Length;
                if (cap <= 0) return;
                int slot = 0;
                if (cap >= n)
                {
                    // needs 已经足够长（干净实现路线）：直接全量填充
                    for (int i = 0; i < n && slot < cap; i++)
                    {
                        if (i < served.Length && i < counts.Length && served[i] < counts[i] * buffer)
                            needs[slot++] = req[i];
                    }
                }
                else
                {
                    // 路线 A：needs 仍是 6 格，按 gameTick 轮转，保证每种原料都有机会被广告。
                    int rot = (int)(GameMain.gameTick % n);
                    if (rot < 0) rot += n;
                    for (int k = 0; k < n && slot < cap; k++)
                    {
                        int i = rot + k;
                        if (i >= n) i -= n;
                        if (i < served.Length && i < counts.Length && served[i] < counts[i] * buffer)
                            needs[slot++] = req[i];
                    }
                }
                for (; slot < cap; slot++) needs[slot] = 0;

                if (Plugin.Verbose.Value && (_hit++ % 120) == 0)
                    Plugin.Log.LogInfo("[needs] recipe=" + __instance.recipeId + " reqLen=" + n
                        + " needs=" + Join(needs) + " served=" + Join(served));
            }
        }

        [HarmonyPatch]
        internal static class InsertIntoIntPatch
        {
            private static System.Reflection.MethodBase TargetMethod()
            {
                return AccessTools.Method(typeof(PlanetFactory), "InsertInto",
                    new Type[] { typeof(int), typeof(int), typeof(int), typeof(byte), typeof(byte), typeof(byte).MakeByRefType() });
            }

            [HarmonyPrefix]
            private static bool Prefix(PlanetFactory __instance, int entityId, int itemId, byte itemCount, byte itemInc,
                out byte remainInc, ref int __result)
            {
                remainInc = itemInc;
                if (entityId <= 0 || entityId >= __instance.entityPool.Length) return true;
                if (__instance.entityPool[entityId].beltId > 0) return true; // 传送带交给原版
                int assemblerId = __instance.entityPool[entityId].assemblerId;
                if (assemblerId > 0)
                {
                    ref AssemblerComponent a = ref __instance.factorySystem.assemblerPool[assemblerId];
                    if (a.recipeExecuteData == null) { __result = 0; return false; }
                    int[] requires = a.recipeExecuteData.requires;
                    if (requires == null || requires.Length <= 6) return true; // <=6 原版逻辑等价，放行

                    lock (__instance.entityMutexs[entityId])
                    {
                        for (int i = 0; i < requires.Length; i++)
                        {
                            if (requires[i] != itemId) continue;
                            a.served[i] += itemCount;
                            a.incServed[i] += itemInc;
                            remainInc = 0;
                            __result = itemCount;
                            if (Plugin.Verbose.Value)
                                Plugin.Log.LogInfo("[insert-int] eid=" + entityId + " item=" + itemId + " slot=" + i + " served=" + a.served[i]);
                            return false;
                        }
                    }
                    __result = 0;
                    return false;
                }
                int labId = __instance.entityPool[entityId].labId;
                if (labId > 0) return HandleLab(__instance, entityId, labId, itemId, itemCount, itemInc, out remainInc, ref __result);
                return true; // 其它建筑交给原版
            }
        }

        /// <summary>研究站受理：研究模式按 matrixIds 查表（支持 &gt;6 矩阵）；普通配方 &gt;6 输入时按 requires 循环。</summary>
        private static bool HandleLab(PlanetFactory factory, int entityId, int labId, int itemId, byte itemCount, byte itemInc,
            out byte remainInc, ref int __result)
        {
            remainInc = itemInc;
            var pool = factory.factorySystem != null ? factory.factorySystem.labPool : null;
            if (pool == null || labId >= pool.Length) { __result = 0; return false; }
            ref LabComponent lab = ref pool[labId];
            if (lab.id <= 0) { __result = 0; return false; }
            lock (factory.entityMutexs[entityId])
            {
                if (lab.researchMode)
                {
                    int idx = Array.IndexOf(LabComponent.matrixIds, itemId);
                    if (idx >= 0 && lab.matrixServed != null && idx < lab.matrixServed.Length)
                    {
                        lab.matrixServed[idx] += 3600 * itemCount;
                        lab.matrixIncServed[idx] += 3600 * itemInc;
                        remainInc = 0;
                        __result = itemCount;
                        return false;
                    }
                    __result = 0;
                    return false;
                }
                var data = lab.recipeExecuteData;
                int[] requires = data != null ? data.requires : null;
                if (requires != null && requires.Length > 6 && lab.served != null)
                {
                    for (int i = 0; i < requires.Length && i < lab.served.Length; i++)
                    {
                        if (requires[i] != itemId) continue;
                        lab.served[i] += itemCount;
                        lab.incServed[i] += itemInc;
                        remainInc = 0;
                        __result = itemCount;
                        return false;
                    }
                    __result = 0;
                    return false;
                }
            }
            return true; // 普通配方 <=6 输入：交回原版
        }

        [HarmonyPatch]
        internal static class InsertIntoUIntPatch
        {
            // EFactoryIOTargetType: Belt=0x01000000, Assembler=0x02000000, ..., Lab=0x07000000
            private const uint AssemblerTag = 33554432u; // 0x02000000
            private const uint LabTag = 117440512u;      // 0x07000000

            private static System.Reflection.MethodBase TargetMethod()
            {
                return AccessTools.Method(typeof(PlanetFactory), "InsertInto",
                    new Type[] { typeof(uint), typeof(int), typeof(int), typeof(byte), typeof(byte), typeof(byte).MakeByRefType() });
            }

            [HarmonyPrefix]
            private static bool Prefix(PlanetFactory __instance, uint ioTargetTypedId, int itemId, byte itemCount, byte itemInc,
                out byte remainInc, ref int __result)
            {
                remainInc = itemInc;
                uint tag = ioTargetTypedId & 0xFF000000u;
                if (tag == LabTag)
                {
                    int labNum = (int)(ioTargetTypedId & 0xFFFFFF);
                    var labs = __instance.factorySystem != null ? __instance.factorySystem.labPool : null;
                    if (labs == null || labNum <= 0 || labNum >= labs.Length) { __result = 0; return false; }
                    return HandleLab(__instance, labs[labNum].entityId, labNum, itemId, itemCount, itemInc, out remainInc, ref __result);
                }
                if (tag != AssemblerTag) return true;
                int num = (int)(ioTargetTypedId & 0xFFFFFF);
                var pool = __instance.factorySystem != null ? __instance.factorySystem.assemblerPool : null;
                if (pool == null || num <= 0 || num >= pool.Length) { __result = 0; return false; }
                ref AssemblerComponent a = ref pool[num];
                if (a.id <= 0) { __result = 0; return false; }
                if (a.recipeExecuteData == null) { __result = 0; return false; }
                int[] requires = a.recipeExecuteData.requires;
                if (requires == null || requires.Length <= 6) return true;

                int entityId = a.entityId;
                if (entityId <= 0 || entityId >= __instance.entityMutexs.Length) return true;
                lock (__instance.entityMutexs[entityId])
                {
                    for (int i = 0; i < requires.Length; i++)
                    {
                        if (requires[i] != itemId) continue;
                        a.served[i] += itemCount;
                        a.incServed[i] += itemInc;
                        remainInc = 0;
                        __result = itemCount;
                        if (Plugin.Verbose.Value)
                            Plugin.Log.LogInfo("[insert-uint] assembler=" + num + " item=" + itemId + " slot=" + i + " served=" + a.served[i]);
                        return false;
                    }
                }
                __result = 0;
                return false;
            }
        }

        [HarmonyPatch(typeof(PowerSystem), "GameTick",
            new Type[] { typeof(long), typeof(bool), typeof(bool), typeof(int) })]

#if !SHIP
        internal static class PowerSystemTestFeedPatch
        {
            [HarmonyPostfix]
            private static void Postfix(PowerSystem __instance)
            {
                if (!Plugin.EnableTestPowerHack.Value) return;
                try
                {
                    var factory = __instance.factory;
                    if (factory == null || factory.factorySystem == null) return;
                    var fs = factory.factorySystem;
                    var serves = __instance.networkServes;
                    if (serves == null || serves.Length == 0) return;
                    var pool = fs.assemblerPool;
                    for (int i = 1; i < fs.assemblerCursor; i++)
                    {
                        if (pool[i].id != i) continue;
                        var data = pool[i].recipeExecuteData;
                        if (data == null) continue;
                        // 只给「超出原版槽位」的测试配方灌电，避免影响正常产线
                        if (data.requires.Length <= 6 && data.products.Length <= 2) continue;
                        int pcId = pool[i].pcId;
                        if (pcId <= 0 || pcId >= __instance.consumerPool.Length) continue;
                        int nid = __instance.consumerPool[pcId].networkId;
                        if (nid >= 0 && nid < serves.Length) serves[nid] = 1f;
                    }
                    var labs = fs.labPool;
                    for (int i = 1; i < fs.labCursor; i++)
                    {
                        if (labs[i].id != i) continue;
                        if (labs[i].researchMode)
                        {
                            // 研究模式没有 recipeExecuteData，按 needs（=matrixIds 长度）判断是否 >6 矩阵测试
                            if (labs[i].needs == null || labs[i].needs.Length <= 6) continue;
                        }
                        else
                        {
                            var data = labs[i].recipeExecuteData;
                            if (data == null || data.requires.Length <= 6) continue;
                        }
                        int pcId = labs[i].pcId;
                        if (pcId <= 0 || pcId >= __instance.consumerPool.Length) continue;
                        int nid = __instance.consumerPool[pcId].networkId;
                        if (nid >= 0 && nid < serves.Length) serves[nid] = 1f;
                    }
                }
                catch (Exception)
                {
                    // 测试辅助开关，绝不因为供电注入影响游戏
                }
            }
        }


#endif
        private static string Join(int[] a)
        {
            if (a == null) return "null";
            var sb = new System.Text.StringBuilder();
            sb.Append('[');
            for (int i = 0; i < a.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(a[i]);
            }
            sb.Append(']');
            return sb.ToString();
        }
    }
}


