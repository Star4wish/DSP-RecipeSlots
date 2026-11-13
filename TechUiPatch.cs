using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace DSPRecipeSlots
{
    /// <summary>
    /// M4 UI：科技 &gt;6 种消耗时，科技树节点（UITechNode）与科技 tooltip（UITechTip）的成本图标
    /// 数组都是预制体定长 6（`costIcons/costIconBds/costButtons/costCountTexts/pCostIcons`），
    /// 原版填充循环 `for l &lt; techProto.itemArray.Length` 无上界 → 第 7 种会越界崩。
    ///
    /// 做法：在 `UpdateInfoComplete()` 前把数组克隆扩到 itemArray.Length，
    /// 克隆体沿用模板的位置 + 等距步进，并默认隐藏（原方法会激活用到的）。
    /// </summary>
    internal static class TechUiPatch
    {
        private static bool _err;

        [HarmonyPatch(typeof(UITechNode), nameof(UITechNode.UpdateInfoComplete))]
        internal static class NodePatch
        {
            [HarmonyPrefix]
            private static void Prefix(UITechNode __instance)
            {
                try
                {
                    var tech = __instance.techProto;
                    if (tech == null || tech.itemArray == null) return;
                    int need = tech.itemArray.Length;
                    __instance.costIcons = Ensure(__instance.costIcons, need, "cost-icon");
                    __instance.costIconBds = Ensure(__instance.costIconBds, need, "cost-icon-bd");
                    __instance.costButtons = Ensure(__instance.costButtons, need, "cost-button");
                    __instance.costCountTexts = Ensure(__instance.costCountTexts, need, "cost-count");
                    __instance.pCostIcons = Ensure(__instance.pCostIcons, need, "pcost-icon");
                    __instance.pCostCountTexts = Ensure(__instance.pCostCountTexts, need, "pcost-count");
                }
                catch (Exception ex) { Log(ex); }
            }

            /// <summary>方法体最后会重算 costBg/分组尺寸，必须在 Postfix 里再加宽。</summary>
            [HarmonyPostfix]
            private static void Postfix(UITechNode __instance)
            {
                try
                {
                    var tech = __instance.techProto;
                    if (tech == null || tech.itemArray == null) return;
                    int need = tech.itemArray.Length;
                    if (need <= 6) return;
                    Widen(__instance.costGroup, __instance.costBg, null, need);
                    Widen(__instance.pCostGroup, null, null, need);
                }
                catch (Exception ex) { Log(ex); }
            }
        }

        [HarmonyPatch(typeof(UITechTip), nameof(UITechTip.UpdateInfoComplete))]
        internal static class TipPatch
        {
            [HarmonyPrefix]
            private static void Prefix(UITechTip __instance)
            {
                try
                {
                    var tech = __instance.techProto;
                    if (tech == null || tech.itemArray == null) return;
                    int need = tech.itemArray.Length;
                    __instance.costIcons = Ensure(__instance.costIcons, need, "tip-cost-icon");
                    __instance.costButtons = Ensure(__instance.costButtons, need, "tip-cost-button");
                    __instance.costCountTexts = Ensure(__instance.costCountTexts, need, "tip-cost-count");
                }
                catch (Exception ex) { Log(ex); }
            }

            /// <summary>方法体末尾用 min/base/maxWidth 重算 panel 宽，必须在 Postfix 里加宽才能覆盖。</summary>
            [HarmonyPostfix]
            private static void Postfix(UITechTip __instance)
            {
                try
                {
                    var tech = __instance.techProto;
                    if (tech == null || tech.itemArray == null) return;
                    int need = tech.itemArray.Length;
                    if (need <= 6) return;
                    Widen(__instance.costGroup, null, __instance.panelRect, need);
                }
                catch (Exception ex) { Log(ex); }
            }
        }

        /// <summary>面板宽度在 UpdateLayoutDynamic 里按 maxWidth 重算，每帧兜底加宽。</summary>
        [HarmonyPatch(typeof(UITechTip), nameof(UITechTip.UpdateLayoutDynamic))]
        internal static class TipLayoutPatch
        {
            [HarmonyPostfix]
            private static void Postfix(UITechTip __instance)
            {
                try
                {
                    var tech = __instance.techProto;
                    if (tech == null || tech.itemArray == null) return;
                    int need = tech.itemArray.Length;
                    if (need <= 6) return;
                    Widen(__instance.costGroup, null, __instance.panelRect, need);
                }
                catch (Exception ex) { Log(ex); }
            }
        }

        private static T[] Ensure<T>(T[] arr, int need, string tag) where T : Component
        {
            if (arr == null || arr.Length == 0) return arr;
            if (need < arr.Length)
            {
                for (int i = need; i < arr.Length; i++)
                    if (arr[i] != null) arr[i].gameObject.SetActive(false);
                return arr;
            }
            if (need == arr.Length) return arr;

            int old = arr.Length;
            var res = new T[need];
            Array.Copy(arr, res, old);
            var template = arr[0] != null ? arr[0] : arr[old - 1];
            if (template == null) return arr;
            Transform parent = template.transform.parent;
            var rt0 = template.transform as RectTransform;
            Vector2 pos0 = rt0 != null ? rt0.anchoredPosition : Vector2.zero;
            float step = 46f;
            if (old > 1 && arr[1] != null)
            {
                var rt1 = arr[1].transform as RectTransform;
                if (rt1 != null) step = rt1.anchoredPosition.x - pos0.x;
                if (Mathf.Abs(step) < 1f) step = 46f;
            }
            for (int i = old; i < need; i++)
            {
                var go = UnityEngine.Object.Instantiate<GameObject>(template.gameObject, parent);
                go.name = template.gameObject.name + "-ext" + i;
                var rt = go.transform as RectTransform;
                if (rt != null) rt.anchoredPosition = new Vector2(pos0.x + step * i, pos0.y);
                go.SetActive(false);
                var comp = go.GetComponent<T>();
                if (comp == null) comp = go.GetComponentInChildren<T>(true);
                res[i] = comp;
                if (comp == null) Plugin.Log.LogWarning("[tech-ui] clone " + tag + " i=" + i + " missing component " + typeof(T).Name);
            }
            return res;
        }

        private static void Widen(RectTransform group, RectTransform bg, RectTransform panel, int need)
        {
            float w = 46f * need + 10f;
            if (group != null && group.sizeDelta.x < w) group.sizeDelta = new Vector2(w, group.sizeDelta.y);
            if (bg != null && bg.sizeDelta.x < w) bg.sizeDelta = new Vector2(w, bg.sizeDelta.y);
            if (panel != null && panel.sizeDelta.x < w + 28f) panel.sizeDelta = new Vector2(w + 28f, panel.sizeDelta.y);
        }

        private static void Log(Exception ex)
        {
            if (_err) return;
            _err = true;
            Plugin.Log.LogError("[tech-ui] " + ex);
        }
    }
}
