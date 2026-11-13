#if !SHIP
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace DSPRecipeSlots
{
    /// <summary>
    /// 开发用测试内容（Test.EnableTestRecipe）：
    ///  - 新物品：原生矩阵（6010，粉色图标，矩阵类）、树叶（6101，绿色图标，材料）
    ///  - 7000：7 矿 → 2×铁块（制造台）
    ///  - 7001：铁矿 → 铁块/磁铁/钢材/铜块（制造台，多产物）
    ///  - 7002：7 矿 → 电磁矩阵（研究站普通配方）
    ///  - 7003：铁矿+铜矿+煤矿+石矿+硅石+钛石+木材+树叶 → 原生矩阵（研究站）
    ///  - 7004：铁块+铜块+高能石墨+石材+高纯硅块+钛块+钛合金+框架材料 → 4 爪 + 3 传送带（制造台）
    ///  - 7005：水+原油+精炼油+氢+重氢+硫酸+反物质 → 6 矿 + 木材 + 树叶（化工厂）
    ///  - 7006：4 爪 + 3 传送带 → 水+原油+精炼油+氢+重氢+硫酸+反物质（熔炉）
    ///  - 7100：测试科技，7 种矩阵各 10 个（ItemPoints=36），HashNeeded=1000
    /// 仅用于实机验证；正式内容 MOD 应继续用 LDBTool/CommonAPI 注册。
    /// </summary>
    [HarmonyPatch(typeof(VFPreload), "InvokeOnLoadWorkEnded")]
    [HarmonyPriority(Priority.Last)]
    internal static class TestContent
    {
        public const int PinkMatrixId = 6010;   // 原生矩阵（矩阵类，粉色图标）
        public const int LeafId = 6101;         // 树叶（材料，绿色图标）
        public const int TestTechId = 7100;     // 测试科技

        [HarmonyPostfix]
        private static void Postfix()
        {
            if (!Plugin.EnableTestRecipe.Value) return;
            try
            {
                // 1) 物品（必须先于配方注册，InitRecipeItems 会查物品表）
                EnsureItem(PinkMatrixId, "原生矩阵", 6001, EItemType.Matrix, true);
                EnsureItem(LeafId, "树叶", 1031, EItemType.Material, false);
                ItemProto.InitItemIds();
                ItemProto.InitItemIndices();

                // 2) 原有测试配方
                int baseId = Plugin.TestRecipeId.Value;
                RegisterRecipe(baseId,
                    new[] { 1001, 1002, 1003, 1004, 1005, 1006, 1007 },
                    new[] { 1, 1, 1, 1, 1, 1, 1 },
                    new[] { 1101 },
                    new[] { 2 },
                    "test-7in-1out");
                RegisterRecipe(baseId + 1,
                    new[] { 1001 },
                    new[] { 1 },
                    new[] { 1101, 1102, 1103, 1104 },
                    new[] { 1, 1, 1, 1 },
                    "test-1in-4out");
                RegisterRecipe(baseId + 2,
                    new[] { 1001, 1002, 1003, 1004, 1005, 1006, 1007 },
                    new[] { 1, 1, 1, 1, 1, 1, 1 },
                    new[] { 6001 },
                    new[] { 1 },
                    "test-lab-7in",
                    ERecipeType.Research);

                // 3) 用户指定的 4 条跨建筑测试配方
                // 研究站：8 输入 → 原生矩阵
                RegisterRecipe(baseId + 3,
                    new[] { 1001, 1002, 1006, 1005, 1003, 1004, 1030, LeafId },
                    new[] { 1, 1, 1, 1, 1, 1, 1, 1 },
                    new[] { PinkMatrixId },
                    new[] { 1 },
                    "test-lab-8in-pink-matrix",
                    ERecipeType.Research);
                // 制造台：8 输入 → 7 产物
                RegisterRecipe(baseId + 4,
                    new[] { 1101, 1104, 1109, 1108, 1105, 1106, 1107, 1125 },
                    new[] { 1, 1, 1, 1, 1, 1, 1, 1 },
                    new[] { 2011, 2012, 2013, 2014, 2001, 2002, 2003 },
                    new[] { 1, 1, 1, 1, 1, 1, 1 },
                    "test-assemble-8in-7out",
                    ERecipeType.Assemble);
                // 化工厂：7 输入 → 8 产物
                RegisterRecipe(baseId + 5,
                    new[] { 1000, 1007, 1114, 1120, 1121, 1116, 1122 },
                    new[] { 1, 1, 1, 1, 1, 1, 1 },
                    new[] { 1001, 1002, 1006, 1005, 1003, 1004, 1030, LeafId },
                    new[] { 1, 1, 1, 1, 1, 1, 1, 1 },
                    "test-chemical-7in-8out",
                    ERecipeType.Chemical);
                // 熔炉：7 输入 → 7 产物
                RegisterRecipe(baseId + 6,
                    new[] { 2011, 2012, 2013, 2014, 2001, 2002, 2003 },
                    new[] { 1, 1, 1, 1, 1, 1, 1 },
                    new[] { 1008, 1007, 1114, 1120, 1121, 1116, 1122 },
                    new[] { 1, 1, 1, 1, 1, 1, 1 },
                    "test-smelt-7in-7out",
                    ERecipeType.Smelt);

                // 4) 12 原料 / 12 产物 压力测试配方（验证 UI 满 6 换行与 12 槽引擎侧）
                int[] twelveIn = { 1001, 1002, 1003, 1004, 1005, 1006, 1030, LeafId, 1101, 1104, 1109, 1114 };
                int[] oneEach12 = { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
                int[] twelveOut = { 2011, 2012, 2013, 2014, 2001, 2002, 2003, 1201, 1102, 1103, 1108, 1125 };
                RegisterRecipe(baseId + 7, twelveIn, oneEach12, new[] { 1101 }, new[] { 1 },
                    "test-12in-1out", ERecipeType.Assemble);
                RegisterRecipe(baseId + 8, new[] { 1001 }, new[] { 1 }, twelveOut, oneEach12,
                    "test-1in-12out", ERecipeType.Assemble);
                RegisterRecipe(baseId + 9, twelveIn, oneEach12, twelveOut, oneEach12,
                    "test-12in-12out", ERecipeType.Assemble);

                // 5) 测试科技（7 矩阵 × 10 个 / 1000 哈希）
                RegisterTech(TestTechId);

                // 6) 矩阵数组扩展（非连续 ID 也可以，映射修正补丁负责下标换算）
                LabPatches.ExtendMatrices();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("[test-content] register failed: " + ex);
            }
        }

        // ───────────────────────── 新档自动解锁测试配方 ─────────────────────────

        /// <summary>新游戏开始时把测试配方加入已解锁集合，这样配方选择器里能直接选到。</summary>
        [HarmonyPatch(typeof(GameHistoryData), nameof(GameHistoryData.SetForNewGame))]
        internal static class UnlockTestRecipesPatch
        {
            [HarmonyPostfix]
            private static void Postfix(GameHistoryData __instance)
            {
                if (!Plugin.EnableTestRecipe.Value) return;
                try
                {
                    if (__instance == null || __instance.recipeUnlocked == null) return;
                    int baseId = Plugin.TestRecipeId.Value;
                    for (int i = 0; i < 10; i++) __instance.recipeUnlocked.Add(baseId + i);
                    Plugin.Log.LogInfo("[test-content] new game: unlocked test recipes " + baseId + ".." + (baseId + 9));
                }
                catch (Exception ex) { Plugin.Log.LogError("[test-content] unlock recipes failed: " + ex); }
            }
        }

        // ───────────────────────── 物品 ─────────────────────────

        private static void EnsureItem(int id, string name, int templateId, EItemType type, bool pinkIcon)
        {
            var set = LDB.items;
            if (set.Select(id) != null)
            {
                Plugin.Log.LogInfo("[test-item] id=" + id + " already exists (" + set.Select(id).Name + "), skip");
                return;
            }
            var template = set.Select(templateId);
            if (template == null)
            {
                Plugin.Log.LogWarning("[test-item] template " + templateId + " not found for " + name);
                return;
            }
            var proto = new ItemProto();
            foreach (var f in typeof(ItemProto).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                try { f.SetValue(proto, f.GetValue(template)); } catch { }
            }
            proto.ID = id;
            proto.Name = name;
            proto.SID = name;
            proto.Type = type;
            proto.SubID = 0;
            proto.GridIndex = 0;
            proto.IconPath = string.Empty;
            proto.IconTag = string.Empty;
            proto.CanBuild = false;
            proto.IsEntity = false;
            proto.ModelIndex = 0;
            proto.ModelCount = 0;
            proto.prefabDesc = null;
            proto.Description = name + " (DSP-RecipeSlots dev)";
            if (type == EItemType.Material || type == EItemType.Matrix) proto.StackSize = 100;

            var old = set.dataArray;
            var arr = new ItemProto[old.Length + 1];
            Array.Copy(old, arr, old.Length);
            arr[old.Length] = proto;
            set.dataArray = arr;
            set.OnAfterDeserialize();
            int idx = Array.IndexOf(set.dataArray, proto);
            proto.Preload(idx);

            Sprite icon = pinkIcon ? MakePinkIcon() : template.iconSprite;
            SetItemIcon(proto, icon);
            Plugin.Log.LogInfo("[test-item] registered id=" + id + " name=" + name + " type=" + type
                + " index=" + proto.index + " icon=" + (icon != null));
        }

        private static void SetItemIcon(ItemProto proto, Sprite sprite)
        {
            if (sprite == null) return;
            try
            {
                var f = typeof(ItemProto).GetField("_iconSprite", BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null) f.SetValue(proto, sprite);
            }
            catch (Exception ex) { Plugin.Log.LogWarning("[test-item] set icon failed: " + ex.Message); }
        }

        private static Sprite MakePinkIcon()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            var inner = new Color(1f, 0.55f, 0.8f, 1f);
            var outer = new Color(0.85f, 0.12f, 0.5f, 1f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float r = Vector2.Distance(new Vector2(x, y), center);
                    if (r > 30f) { tex.SetPixel(x, y, new Color(0f, 0f, 0f, 0f)); continue; }
                    float t = Mathf.Clamp01(r / 30f);
                    var c = Color.Lerp(inner, outer, t);
                    if (r > 27f) c = Color.Lerp(c, Color.white, 0.7f); // 外圈亮边
                    tex.SetPixel(x, y, c);
                }
            }
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 64f);
        }

        // ───────────────────────── 科技 ─────────────────────────

        private static void RegisterTech(int id)
        {
            var set = LDB.techs;
            if (set.Select(id) != null)
            {
                Plugin.Log.LogInfo("[test-tech] id=" + id + " already exists, skip");
                return;
            }
            var template = set.Select(1001);
            if (template == null) { Plugin.Log.LogWarning("[test-tech] template tech 1001 not found"); return; }
            var proto = new TechProto();
            foreach (var f in typeof(TechProto).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                try { f.SetValue(proto, f.GetValue(template)); } catch { }
            }
            proto.ID = id;
            proto.Name = "测试科技-七糖";
            proto.Desc = "蓝糖+红糖+黄糖+粉糖+紫糖+绿糖+白糖 各 10 个，1000 哈希（DSP-RecipeSlots dev）";
            proto.Conclusion = string.Empty;
            proto.Published = true;
            proto.IsLabTech = true;
            proto.IsHiddenTech = false;
            proto.IsObsolete = false;
            proto.PreTechs = new int[0];
            proto.PreTechsImplicit = new int[0];
            proto.PreTechsMax = false;
            proto.PreItem = new int[0];
            proto.Items = new[] { 6001, 6002, 6003, PinkMatrixId, 6004, 6005, 6006 };
            proto.ItemPoints = new[] { 36, 36, 36, 36, 36, 36, 36 };
            proto.HashNeeded = 1000;
            proto.Level = 0;
            proto.MaxLevel = 0;
            proto.LevelCoef1 = 0;
            proto.LevelCoef2 = 0;
            proto.UnlockRecipes = new int[0];
            proto.UnlockFunctions = new int[0];
            proto.UnlockValues = new double[0];
            proto.AddItems = new int[0];
            proto.AddItemCounts = new int[0];
            proto.PropertyOverrideItems = new int[0];
            proto.PropertyItemCounts = new int[0];
            proto.IconPath = string.Empty;
            proto.IconTag = string.Empty;
            proto.Position = new Vector2(template.Position.x, template.Position.y - 3f);

            var old = set.dataArray;
            var arr = new TechProto[old.Length + 1];
            Array.Copy(old, arr, old.Length);
            arr[old.Length] = proto;
            set.dataArray = arr;
            set.OnAfterDeserialize();
            proto.Preload();
            proto.Preload2();
            // 重建全部科技的 pre/post 数组，让科技树连线/依赖查询包含新科技
            foreach (var t in set.dataArray)
            {
                if (t == null || t == proto) continue;
                try { t.Preload2(); } catch { }
            }
            // 科技图标用粉色矩阵图标
            try
            {
                var pink = LDB.items.Select(PinkMatrixId);
                var f = typeof(TechProto).GetField("_iconSprite", BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null && pink != null) f.SetValue(proto, pink.iconSprite);
            }
            catch { }
            Plugin.Log.LogInfo("[test-tech] registered id=" + id + " name=" + proto.Name
                + " items=" + string.Join(",", proto.Items) + " points=" + string.Join(",", proto.ItemPoints)
                + " hash=" + proto.HashNeeded + " position=" + proto.Position);
        }

        // ───────────────────────── 配方 ─────────────────────────

        private static void RegisterRecipe(int id, int[] items, int[] counts, int[] results, int[] resultCounts, string name,
            ERecipeType type = ERecipeType.Assemble)
        {
            if (id <= 0 || id > 32767)
            {
                Plugin.Log.LogError("[test-recipe] invalid id " + id + " (must be < 12000 for IconSet, <= 32767 for save)");
                return;
            }
            if (LDB.recipes.Select(id) != null)
            {
                Plugin.Log.LogWarning("[test-recipe] id " + id + " already exists, skip");
                return;
            }

            var proto = new RecipeProto
            {
                ID = id,
                Type = type,
                Handcraft = false,
                Explicit = true,
                TimeSpend = 60,
                Items = items,
                ItemCounts = counts,
                Results = results,
                ResultCounts = resultCounts,
                GridIndex = 0,
                Name = name,
                Description = name + " (DSP-RecipeSlots dev)",
                IconPath = string.Empty,
                IconTag = string.Empty,
                NonProductive = true,
            };

            var set = LDB.recipes;
            var old = set.dataArray;
            int n = old.Length;
            var arr = new RecipeProto[n + 1];
            Array.Copy(old, arr, n);
            arr[n] = proto;
            set.dataArray = arr;
            set.OnAfterDeserialize();   // 重建 dataIndices / name / sid
            proto.Preload(n);           // 初始化 name / icon / productive / preTech

            RecipeProto.InitRecipeItems(); // 重建运行时表（含新配方）

            Plugin.Log.LogInfo("[test-recipe] registered id=" + id + " name=" + name
                + " items=" + items.Length + " results=" + results.Length
                + " (recipeExecuteData=" + (RecipeProto.recipeExecuteData.ContainsKey(id) ? "ok" : "MISSING") + ")");
        }
    }
}
#endif


