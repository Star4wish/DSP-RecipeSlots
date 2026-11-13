using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace DSPRecipeSlots
{
    /// <summary>
    /// M4 UI：研究站矩阵槽扩展 + 研发界面 3×3 九宫格（参考创世之书 GenesisBook）。
    ///
    /// 原版 `UILabWindow` 的数组都是预制体定长 6，且 `_OnUpdate` 在研究方法/默认分支里
    /// 以 `itemButtons.Length` 为循环边界同时索引 `matrixProtos[m]` / `matrixRecipes[m]` /
    /// `lab.matrixServed[m]`（长度均为矩阵数）。因此想显示 9 格，必须保证
    /// 「原版函数体看到的数组长度 ≤ 矩阵数」，否则越界。
    ///
    /// 做法：
    ///  1. `_OnInit` 前缀：把数组克隆扩到 max(9, matrixIds.Length)，并缓存完整数组；
    ///  2. `_OnUpdate` 前缀：把实例上的数组**临时裁剪**到矩阵数（原版安全运行）；
    ///  3. `_OnUpdate` Finalizer：恢复完整 9 格数组（异常时也能恢复）；
    ///  4. `_OnUpdate` 后置：3×3 九宫格排布；超出矩阵数的格子显示为「空锁定格」；
    ///     隐藏原版六边形连线，窗口加高到 3 行所需高度。
    /// </summary>
    internal static class LabUiPatch
    {
        private const int SlotCount = 9;
        /// <summary>九宫格间距：原版矩阵容器为 286×277，3×90+96×0.86 可完整放入并居中。</summary>




        private sealed class Full
        {
            public UIButton[] Buttons;
            public Image[] Icons;
            public Image[] Percents;
            public Text[] Counts;
            public Image[] Locks;
            public Image[] Incs;
        }

        private static readonly Dictionary<UILabWindow, Full> Cache = new Dictionary<UILabWindow, Full>();
        private static FieldInfo _protosField;
        private static FieldInfo _recipesField;
        private static bool _err;


        [HarmonyPatch(typeof(UILabWindow), "_OnInit")]
        internal static class OnInitPatch
        {
            [HarmonyPrefix]
            private static void Prefix(UILabWindow __instance)
            {
                try { Ensure(__instance); }
                catch (Exception ex) { Log(ex); }
            }
        }

        [HarmonyPatch(typeof(UILabWindow), "_OnUpdate")]
        internal static class OnUpdatePatch
        {
            [HarmonyPrefix]
            private static void Prefix(UILabWindow __instance)
            {
                try { Trim(__instance); }
                catch (Exception ex) { Log(ex); }
            }

            [HarmonyFinalizer]
            private static void Finalizer(UILabWindow __instance)
            {
                try { Restore(__instance); }
                catch (Exception ex) { Log(ex); }
            }

            [HarmonyPostfix]
            private static void Postfix(UILabWindow __instance)
            {
                try { Arrange(__instance); }
                catch (Exception ex) { Log(ex); }
            }
        }

        private static void Log(Exception ex)
        {
            if (_err) return;
            _err = true;
            Plugin.Log.LogError("[lab-ui] " + ex);
        }

        // ───────────────────────── 数组扩展 / 裁剪 / 恢复 ─────────────────────────

        private static void Ensure(UILabWindow w)
        {
            if (w.itemButtons == null || w.itemButtons.Length == 0) return;
            var ids = LabComponent.matrixIds;
            int matrixLen = ids != null ? ids.Length : 6;
            int need = Math.Max(SlotCount, matrixLen);

            Full full;
            if (Cache.TryGetValue(w, out full) && full != null && full.Buttons != null)
            {
                if (full.Buttons.Length >= need) return;
            }

            int old = w.itemButtons.Length;
            int target = Math.Max(need, old);
            var buttons = new UIButton[target];
            var icons = new Image[target];
            var percents = new Image[target];
            var counts = new Text[target];
            var locks = new Image[target];
            var incs = new Image[target * 3];
            for (int i = 0; i < old; i++)
            {
                buttons[i] = w.itemButtons[i];
                icons[i] = Get(w.itemIcons, i);
                percents[i] = Get(w.itemPercents, i);
                counts[i] = w.itemCountTexts != null && i < w.itemCountTexts.Length ? w.itemCountTexts[i] : null;
                locks[i] = Get(w.itemLocks, i);
                for (int k = 0; k < 3; k++)
                    if (w.itemIncs != null && i * 3 + k < w.itemIncs.Length) incs[i * 3 + k] = w.itemIncs[i * 3 + k];
            }

            if (old > 0 && w.itemButtons[0] != null)
            {
                var parent = w.itemButtons[0].transform.parent;
                var template = w.itemButtons[0].gameObject;
                for (int i = old; i < target; i++)
                {
                    var go = UnityEngine.Object.Instantiate<GameObject>(template, parent);
                    go.name = "circle-ext-" + i;
                    buttons[i] = go.GetComponent<UIButton>();
                    var t = go.transform;
                    icons[i] = ChildImage(t, "icon");
                    percents[i] = ChildImage(t, "fg");
                    counts[i] = ChildText(t, "cnt-text");
                    locks[i] = ChildImage(t, "locked");
                    for (int k = 0; k < 3; k++) incs[i * 3 + k] = ChildImage(t, "icon/inc-" + (k + 1));
                }
            }

            full = new Full
            {
                Buttons = buttons,
                Icons = icons,
                Percents = percents,
                Counts = counts,
                Locks = locks,
                Incs = incs,
            };
            Cache[w] = full;
            Apply(w, full);
            Plugin.Log.LogInfo("[lab-ui] matrix slots ready: " + old + " -> " + target
                + " (matrixIds=" + matrixLen + ")");
        }

        private static void Apply(UILabWindow w, Full full)
        {
            w.itemButtons = full.Buttons;
            w.itemIcons = full.Icons;
            w.itemPercents = full.Percents;
            w.itemCountTexts = full.Counts;
            w.itemLocks = full.Locks;
            w.itemIncs = full.Incs;
        }

        private static int VanillaBound(UILabWindow w, Full full)
        {
            if (_protosField == null) _protosField = AccessTools.Field(typeof(UILabWindow), "matrixProtos");
            if (_recipesField == null) _recipesField = AccessTools.Field(typeof(UILabWindow), "matrixRecipes");
            int bound = 0;
            if (_protosField != null)
            {
                var protos = _protosField.GetValue(w) as ItemProto[];
                if (protos != null) bound = protos.Length;
            }
            if (_recipesField != null)
            {
                var recipes = _recipesField.GetValue(w) as RecipeProto[];
                if (recipes != null) bound = bound == 0 ? recipes.Length : Math.Min(bound, recipes.Length);
            }
            if (bound <= 0)
            {
                var ids = LabComponent.matrixIds;
                bound = ids != null ? ids.Length : 6;
            }
            return Math.Min(bound, full.Buttons.Length);
        }

        /// <summary>把实例数组临时裁剪到原版可安全索引的长度（其余槽位由我们在 Postfix 里画）。</summary>
        private static void Trim(UILabWindow w)
        {
            Full full;
            if (!Cache.TryGetValue(w, out full) || full == null) return;
            int bound = VanillaBound(w, full);
            if (bound >= full.Buttons.Length) { Apply(w, full); return; }

            var buttons = new UIButton[bound];
            var icons = new Image[bound];
            var percents = new Image[bound];
            var counts = new Text[bound];
            var locks = new Image[bound];
            var incs = new Image[bound * 3];
            Array.Copy(full.Buttons, buttons, bound);
            Array.Copy(full.Icons, icons, bound);
            Array.Copy(full.Percents, percents, bound);
            Array.Copy(full.Counts, counts, bound);
            Array.Copy(full.Locks, locks, bound);
            Array.Copy(full.Incs, incs, bound * 3);
            w.itemButtons = buttons;
            w.itemIcons = icons;
            w.itemPercents = percents;
            w.itemCountTexts = counts;
            w.itemLocks = locks;
            w.itemIncs = incs;
        }

        private static void Restore(UILabWindow w)
        {
            Full full;
            if (Cache.TryGetValue(w, out full) && full != null) Apply(w, full);
        }

        // ───────────────────────── 3×3 排列 ─────────────────────────

        private static void Arrange(UILabWindow w)
        {
            Full full;
            if (!Cache.TryGetValue(w, out full) || full == null) return;
            if (w.windowTrans == null) return;
            var ids = LabComponent.matrixIds;
            int matrixLen = ids != null ? ids.Length : 6;

            var lines = w.transform.Find("matrix-group/matrix/lines");
            if (lines != null && lines.gameObject.activeSelf) lines.gameObject.SetActive(false);

            // 3×3 紧凑排布（间距 = 槽位 96），再由 CenterGrid 按可用区域缩放并居中
            for (int i = 0; i < full.Buttons.Length; i++)
            {
                var b = full.Buttons[i];
                if (b == null) continue;
                var go = b.gameObject;
                if (!go.activeSelf) go.SetActive(true);
                var rt = b.transform as RectTransform;
                if (rt != null)
                {
                    rt.anchoredPosition = new Vector2((i % 3 - 1) * 96f, (1 - i / 3) * 96f);
                    if (rt.localScale != Vector3.one) rt.localScale = Vector3.one;
                }

                bool active = i < matrixLen;
                if (!active)
                {
                    // 未来 9 矩阵预留位：显示为空锁定格
                    if (full.Icons[i] != null) { full.Icons[i].sprite = null; full.Icons[i].enabled = false; }
                    if (full.Percents[i] != null) full.Percents[i].fillAmount = 0f;
                    if (full.Counts[i] != null) full.Counts[i].text = "";
                    if (full.Locks[i] != null) full.Locks[i].enabled = true;
                    for (int k = 0; k < 3; k++)
                        if (full.Incs[i * 3 + k] != null) full.Incs[i * 3 + k].enabled = false;
                    if (b.button != null) b.button.interactable = false;
                    b.tips.itemId = 0;
                }
            }

            CenterGrid(w, full);
            // 窗口尺寸保持原版：右侧速度/状态圈/提示的排版依赖原版大小，改尺寸会互相重叠。
        }

        /// <summary>
        /// 把 3×3 网格按「窗口左侧可用区域」缩放并居中。
        /// 全部在窗口局部坐标（UI 单位）里计算，再换算回世界坐标赋值——避免受 Canvas 世界缩放影响。
        /// </summary>
        private static void CenterGrid(UILabWindow w, Full full)
        {
            var matrix = w.transform.Find("matrix-group/matrix") as RectTransform;
            if (matrix == null || full.Buttons == null || full.Buttons.Length == 0) return;
            if (matrix.localScale != Vector3.one) matrix.localScale = Vector3.one;

            var win = w.windowTrans;
            var rect = win.rect;

            // 右侧内容（选择模式提示 / 速度 / 状态圈）的左边界——换算到窗口局部坐标
            float contentLeft = rect.xMax;
            var parent = matrix.parent;
            var c = new Vector3[4];
            for (int i = 0; i < parent.childCount; i++)
            {
                var t = parent.GetChild(i) as RectTransform;
                if (t == null || t == matrix) continue;
                if (!t.gameObject.activeInHierarchy) continue;
                if (t.name == "lines") continue;
                t.GetWorldCorners(c);
                var lo = win.InverseTransformPoint(c[0]);
                var hi = win.InverseTransformPoint(c[2]);
                if (hi.x - lo.x < 1f || hi.y - lo.y < 1f) continue;      // 跳过零尺寸节点
                if (lo.x > rect.xMin + rect.width * 0.35f)               // 只考虑右半区内容
                    contentLeft = Mathf.Min(contentLeft, lo.x);
            }

            float areaLeft = rect.xMin + 18f;
            float areaRight = Mathf.Min(contentLeft - 16f, rect.xMax - 18f);
            if (areaRight < areaLeft + 60f) areaRight = rect.xMax - 18f;
            float areaTop = rect.yMax - 56f;   // 让开标题栏
            float areaBottom = rect.yMin + 14f;
            float availW = Mathf.Max(60f, areaRight - areaLeft);
            float availH = Mathf.Max(60f, areaTop - areaBottom);

            float cell = Mathf.Clamp(Mathf.Min(availW / 3f, availH / 3f), 46f, 96f);
            float gridW = cell * 3f, gridH = cell * 3f;
            float ox = areaLeft + (availW - gridW) * 0.5f;
            float oyTop = areaTop - (availH - gridH) * 0.5f;
            float s = cell / 96f;
            var scale = new Vector3(s, s, 1f);

            for (int i = 0; i < full.Buttons.Length; i++)
            {
                var b = full.Buttons[i];
                if (b == null) continue;
                var rt = b.transform as RectTransform;
                if (rt == null) continue;
                int col = i % 3, row = i / 3;
                var localTarget = new Vector3(ox + (col + 0.5f) * cell, oyTop - (row + 0.5f) * cell, 0f);
                var target = win.TransformPoint(localTarget);
                if ((rt.position - target).sqrMagnitude > 0.25f) rt.position = target;
                if ((rt.localScale - scale).sqrMagnitude > 1e-6f) rt.localScale = scale;
            }
        }

        private static Image Get(Image[] arr, int i) => arr != null && i < arr.Length ? arr[i] : null;

        private static Image ChildImage(Transform t, string path)
        {
            var c = t.Find(path);
            return c != null ? c.GetComponent<Image>() : null;
        }

        private static Text ChildText(Transform t, string path)
        {
            var c = t.Find(path);
            return c != null ? c.GetComponent<Text>() : null;
        }
    }
}
