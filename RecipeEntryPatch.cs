using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace DSPRecipeSlots
{
    /// <summary>
    /// M3 收尾：物品/配方 tooltip 的配方行（UIRecipeEntry）原来一行最多 7 个图标
    /// （`if (num >= 7) break` 两处），6 原料 + 2 产物就会静默丢一个。
    /// 这里整方法替换为「按 Results+Items 总数动态扩展图标数组」，不做数量上限；
    /// UIItemTip.SetTip 本身已按 `num22*40+30` 为 >7 图标加宽面板，无需再改。
    /// </summary>
    [HarmonyPatch(typeof(UIRecipeEntry), nameof(UIRecipeEntry.SetRecipe))]
    internal static class RecipeEntryPatch
    {
        private static bool _err;

        [HarmonyPrefix]
        private static bool Prefix(UIRecipeEntry __instance, RecipeProto recipe)
        {
            try
            {
                if (recipe == null) return true;
                Ensure(__instance, recipe.Results.Length + recipe.Items.Length);
                Fill(__instance, recipe);
                return false;
            }
            catch (Exception ex)
            {
                if (!_err)
                {
                    _err = true;
                    Plugin.Log.LogError("[recipe-entry] " + ex);
                }
                return true; // 回退原版（最多 7 个，但不会崩）
            }
        }

        private static void Ensure(UIRecipeEntry e, int need)
        {
            if (e.icons == null || e.icons.Length == 0) return;
            if (need <= e.icons.Length) return;
            int old = e.icons.Length;
            var icons = new Image[need];
            var counts = new Text[need];
            Array.Copy(e.icons, icons, old);
            Array.Copy(e.countTexts, counts, Math.Min(e.countTexts.Length, old));

            var iconTemplate = e.icons[0];
            Transform iconParent = iconTemplate.transform.parent;
            bool countInside = e.countTexts != null && e.countTexts.Length > 0 && e.countTexts[0] != null
                && e.countTexts[0].transform.IsChildOf(iconTemplate.transform);
            Transform countParent = (!countInside && e.countTexts != null && e.countTexts.Length > 0 && e.countTexts[0] != null)
                ? e.countTexts[0].transform.parent : null;

            for (int i = old; i < need; i++)
            {
                var go = UnityEngine.Object.Instantiate<GameObject>(iconTemplate.gameObject, iconParent);
                go.name = iconTemplate.gameObject.name + "-ext" + i;
                var img = go.GetComponent<Image>();
                if (img == null) img = go.GetComponentInChildren<Image>(true);
                icons[i] = img;
                Text txt = null;
                if (countInside)
                {
                    txt = go.GetComponentInChildren<Text>(true);
                }
                else if (countParent != null)
                {
                    var cgo = UnityEngine.Object.Instantiate<GameObject>(e.countTexts[0].gameObject, countParent);
                    cgo.name = e.countTexts[0].gameObject.name + "-ext" + i;
                    txt = cgo.GetComponent<Text>();
                }
                counts[i] = txt;
                go.SetActive(false);
            }
            e.icons = icons;
            e.countTexts = counts;
        }

        private static void Fill(UIRecipeEntry e, RecipeProto recipe)
        {
            var icons = e.icons;
            var counts = e.countTexts;
            int num = 0;
            float x = 0f;
            for (int i = 0; i < recipe.Results.Length && num < icons.Length; i++)
            {
                var item = LDB.items.Select(recipe.Results[i]);
                if (icons[num] != null) icons[num].sprite = item != null ? item.iconSprite : null;
                if (counts != null && num < counts.Length && counts[num] != null)
                    counts[num].text = recipe.ResultCounts[i].ToString();
                if (icons[num] != null)
                {
                    icons[num].rectTransform.anchoredPosition = new Vector2(x, 0f);
                    icons[num].gameObject.SetActive(true);
                }
                num++;
                x += 40f;
            }
            if (e.arrow != null) e.arrow.anchoredPosition = new Vector2(x, -27f);
            if (e.timeText != null)
            {
                e.timeText.text = recipe.Type == ERecipeType.Fractionate
                    ? (float)recipe.ResultCounts[0] / (float)recipe.ItemCounts[0] * 100f + "%"
                    : (float)recipe.TimeSpend / 60f + " s";
            }
            x += 40f;
            for (int j = 0; j < recipe.Items.Length && num < icons.Length; j++)
            {
                var item = LDB.items.Select(recipe.Items[j]);
                if (icons[num] != null) icons[num].sprite = item != null ? item.iconSprite : null;
                if (counts != null && num < counts.Length && counts[num] != null)
                    counts[num].text = recipe.Type == ERecipeType.Fractionate ? "1" : recipe.ItemCounts[j].ToString();
                if (icons[num] != null)
                {
                    icons[num].rectTransform.anchoredPosition = new Vector2(x, 0f);
                    icons[num].gameObject.SetActive(true);
                }
                num++;
                x += 40f;
            }
            for (int k = num; k < icons.Length; k++)
                if (icons[k] != null) icons[k].gameObject.SetActive(false);
        }
    }
}
