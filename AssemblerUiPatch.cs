using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace DSPRecipeSlots
{
    /// <summary>
    /// 装配台/熔炉/化工厂等生产建筑窗口：
    ///  - 原版只显示产物图标（最多单行），窗口不会为 >4 产物 / >6 原料加高。
    ///  - 这里在「原料 &gt; 6 或 产物 &gt; 4」时切换到多行布局：
    ///      · 新增「原料需求」行（图标 + 需求数量，悬浮有物品 tooltip），原料 6/行；
    ///      · 产物 4/行（每行 72px），窗口按行数加高，「物品清单」区自动下移；
    ///      · 速度/供电区移到右侧。
    ///  - 未超限时保持原版单行布局（产物最多 4 个单行），避免影响普通配方。
    ///
    /// 实机层级（依据 uidump）：
    ///   produce/circle-back = productGroup：整槽模板（bg Image + product-icon + circle-fg
    ///     + extra-circle-fg + cnt-text + -1 后缀第二槽 + stop-btn）。
    ///   克隆 productGroup 作为「槽容器」，隐藏 -1 组与 stop-btn；原料槽额外隐藏进度圈。
    /// </summary>
    [HarmonyPatch(typeof(UIAssemblerWindow), "_OnUpdate")]
    internal static class AssemblerUiPatch
    {
        private sealed class Slot
        {
            public GameObject Root;
            public Image Icon;
            public Text Count;
            public Image Progress;
            public Image ExtraProgress;
            public Button Button;
            public UIButton Tips;
        }

        private sealed class WinState
        {
            public readonly List<Slot> Extras = new List<Slot>();      // 单行模式：产物索引 2..N
            public readonly List<Slot> GridProducts = new List<Slot>(); // 多行模式：全部产物
            public readonly List<Slot> Inputs = new List<Slot>();       // 旧版自建输入行（保留清空）
            public int ServingCols;                                     // 当前 serving-box 列数
            public bool Multi;
        }

        private static readonly Dictionary<UIAssemblerWindow, WinState> Cache = new Dictionary<UIAssemblerWindow, WinState>();
        private static bool _errorLogged;

        private const float SlotSize = 64f;
        private const float RowH = 72f;
        private const int InPerRow = 6;      // 原料每行数量
        private const int OutPerRow = 6;     // 产物每行数量（满 6 个才换行）
        private const int MultiOutTrigger = 4; // 产物 >4 即切换为「原料在上/产物在下」的多行 UI

        [HarmonyPostfix]
        private static void Postfix(UIAssemblerWindow __instance)
        {
            try { Update(__instance); }
            catch (Exception ex)
            {
                if (_errorLogged) return;
                _errorLogged = true;
                Plugin.Log.LogError("[ui] assembler window patch failed: " + ex);
            }
        }

        private static WinState GetState(UIAssemblerWindow w)
        {
            WinState st;
            if (Cache.TryGetValue(w, out st) && st != null) return st;
            st = new WinState();
            Cache[w] = st;
            return st;
        }

        private static void Update(UIAssemblerWindow w)
        {
            int asmId = w.assemblerId;
            var planet = GameMain.localPlanet;
            var factory = planet != null ? planet.factory : null;
            if (factory == null || asmId <= 0) return;
            ref AssemblerComponent a = ref factory.factorySystem.assemblerPool[asmId];
            if (a.id != asmId) return;
            var data = a.recipeExecuteData;
            if (data == null) return;

            int nOut = data.products.Length;
            int nIn = data.requires.Length;
            var st = GetState(w);
            bool multi = nIn > InPerRow || nOut > MultiOutTrigger;
            st.Multi = multi;
            if (multi) ApplyMulti(w, ref a, data, nIn, nOut, st);
            else ApplySingle(w, ref a, data, nIn, nOut, st);
            ClampWindow(w);
        }

        // ───────────────────────── 单行（原版）─────────────────────────

        private static void ApplySingle(UIAssemblerWindow w, ref AssemblerComponent a, RecipeExecuteData data, int nIn, int nOut, WinState st)
        {
            HideAll(st.GridProducts);
            HideAll(st.Inputs);
            if (w.productGroup != null) w.productGroup.gameObject.SetActive(true);
            if (w.servingGroup != null) w.servingGroup.gameObject.SetActive(true);
            if (w.servingBox != null)
            {
                int cols = Mathf.Max(1, nIn);
                if (st.ServingCols != cols || w.servingBox.colCount != cols)
                {
                    st.ServingCols = cols;
                    w.servingBox.colCount = cols;
                    w.servingBox.OnStorageSizeChanged();
                }
            }

            if (nOut > 2) EnsureSlots(w, st.Extras, nOut - 2, false);
            for (int i = 0; i < st.Extras.Count; i++)
            {
                int idx = i + 2;
                var s = st.Extras[i];
                if (s.Root == null) continue;
                if (nOut <= idx) { s.Root.SetActive(false); continue; }
                s.Root.SetActive(true);
                var rt = (RectTransform)s.Root.transform;
                rt.anchoredPosition = new Vector2(19f + SlotSize * idx, 0f);
                rt.sizeDelta = new Vector2(SlotSize, SlotSize);
                FillProduct(s, a, data, idx, w);
            }

            if (w.productGroup != null)
                w.productGroup.sizeDelta = new Vector2(SlotSize * Math.Max(1, nOut), SlotSize);
            float shift = SlotSize * (Math.Max(1, nOut) - 1);
            if (w.speedGroup != null)
                w.speedGroup.anchoredPosition = new Vector2(80f + shift, 0f); // y 必须归零，否则残留多行模式的偏移
            SetArrowDirection(w, false);
            if (w.servingGroup != null)
                w.servingGroup.anchoredPosition = new Vector2(160f + shift, 0f);

            float servingWidth = 50f * Math.Max(1, nIn) + 4f;
            float needed = 160f + shift + servingWidth + 24f;
            float baseWidth = 80f + (w.playerInventory != null ? w.playerInventory.colCount : 10) * 50f;
            float target = Math.Max(baseWidth, needed);
            if (w.windowTrans != null)
                w.windowTrans.sizeDelta = new Vector2(target, 420f);
            if (w.playerInventoryGroup != null)
                w.playerInventoryGroup.anchoredPosition = new Vector2(w.playerInventoryGroup.anchoredPosition.x, -180f);
        }

        // ───────────────────────── 多行（>6 原料 / >4 产物）─────────────────────────

        private static void ApplyMulti(UIAssemblerWindow w, ref AssemblerComponent a, RecipeExecuteData data, int nIn, int nOut, WinState st)
        {
            if (w.productGroup != null) w.productGroup.gameObject.SetActive(false);
            if (w.servingGroup != null) w.servingGroup.gameObject.SetActive(false); // 原料改用自建圆圈格显示
            HideAll(st.Extras);

            int inCols = Mathf.Min(InPerRow, Mathf.Max(1, nIn));
            int inRows = Mathf.CeilToInt(nIn / (float)inCols);
            int outRows = Mathf.Max(1, Mathf.CeilToInt(nOut / (float)OutPerRow));

            // 槽位是 produce 组的子节点：produce(anchor 顶部、高 100) 的中心在窗口 y = -100，
            // 因此「窗口 y」与「槽位 anchoredY」相差 +100。
            const float ProduceCenterOffset = 100f;
            float inputY0 = -75f;
            float lastInputBottom = inputY0 - (inRows - 1) * RowH - SlotSize * 0.5f;
            float speedY = lastInputBottom - 40f;
            float prodY0 = speedY - 70f;
            float contentBottom = prodY0 - (outRows - 1) * RowH - SlotSize * 0.5f;
            float invTopY = contentBottom - 64f;
            float targetH = -invTopY + 204f + 20f;
            float fullW = Mathf.Min(nIn, inCols) * SlotSize;

            // 上方：原料（6/行，圆圈格 + 右下角数量）；箭头朝下表示 原料 → 产物
            EnsureSlots(w, st.Inputs, nIn, true);
            for (int i = 0; i < st.Inputs.Count; i++)
            {
                var s = st.Inputs[i];
                if (s.Root == null) continue;
                if (i >= nIn) { s.Root.SetActive(false); continue; }
                s.Root.SetActive(true);
                var rt = (RectTransform)s.Root.transform;
                rt.sizeDelta = new Vector2(SlotSize, SlotSize);
                float yw = inputY0 - (i / inCols) * RowH;
                rt.anchoredPosition = new Vector2(19f + (i % inCols) * SlotSize, yw + ProduceCenterOffset);
                FillInput(s, a, data, i);
            }

            // 中间：速度显示（居中，箭头朝下）
            float gridW = Mathf.Min(nOut, OutPerRow) * SlotSize;
            float contentW = Mathf.Max(Mathf.Max(fullW, gridW), 120f);
            float centerX = 19f + contentW * 0.5f;
            if (w.speedGroup != null)
            {
                w.speedGroup.anchoredPosition = new Vector2(centerX, speedY + ProduceCenterOffset);
                SetArrowDirection(w, true);
            }

            // 下方：产物（4/行）
            EnsureSlots(w, st.GridProducts, nOut, false);
            for (int i = 0; i < st.GridProducts.Count; i++)
            {
                var s = st.GridProducts[i];
                if (s.Root == null) continue;
                if (i >= nOut) { s.Root.SetActive(false); continue; }
                s.Root.SetActive(true);
                var rt = (RectTransform)s.Root.transform;
                rt.sizeDelta = new Vector2(SlotSize, SlotSize);
                float yw = prodY0 - (i / OutPerRow) * RowH;
                rt.anchoredPosition = new Vector2(19f + (i % OutPerRow) * SlotSize, yw + ProduceCenterOffset);
                FillProduct(s, a, data, i, w);
            }

            float needed = 19f + contentW + 40f;
            float baseWidth = 80f + (w.playerInventory != null ? w.playerInventory.colCount : 10) * 50f;
            float targetW = Mathf.Max(baseWidth, needed);
            if (w.windowTrans != null)
                w.windowTrans.sizeDelta = new Vector2(targetW, targetH);
            if (w.playerInventoryGroup != null)
                w.playerInventoryGroup.anchoredPosition = new Vector2(w.playerInventoryGroup.anchoredPosition.x, invTopY);
        }

        /// <summary>速度区箭头：多行模式下改成朝下（暗示产物 → 速度 → 原料的阅读顺序）。</summary>
        private static void SetArrowDirection(UIAssemblerWindow w, bool down)
        {
            if (w.speedArrows == null) return;
            var want = down ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
            for (int i = 0; i < w.speedArrows.Length; i++)
            {
                var img = w.speedArrows[i];
                if (img == null) continue;
                var t = img.transform;
                if (t.localRotation != want) t.localRotation = want;
            }
        }

        private static void FillProduct(Slot s, AssemblerComponent a, RecipeExecuteData data, int idx, UIAssemblerWindow w)
        {
            int productId = data.products[idx];
            var item = LDB.items.Select(productId);
            if (s.Icon != null) s.Icon.sprite = item != null ? item.iconSprite : null;
            if (s.Count != null) s.Count.text = a.produced[idx].ToString();
            if (s.Button != null) s.Button.interactable = a.produced[idx] > 0;
            if (s.Tips != null && item != null)
            {
                s.Tips.tips.itemId = productId;
                s.Tips.tips.itemInc = 0;
                s.Tips.tips.itemCount = 0;
                s.Tips.tips.type = UIButton.ItemTipType.Item;
                s.Tips.tips.corner = 2;
                s.Tips.tips.delay = 0.2f;
            }
            if (s.Progress != null)
                s.Progress.fillAmount = w.productProgress0 != null ? w.productProgress0.fillAmount : 0f;
            if (s.ExtraProgress != null)
                s.ExtraProgress.fillAmount = w.extraProductProgress0 != null ? w.extraProductProgress0.fillAmount : 0f;
        }

        private static void FillInput(Slot s, AssemblerComponent a, RecipeExecuteData data, int idx)
        {
            int itemId = data.requires[idx];
            var item = LDB.items.Select(itemId);
            if (s.Icon != null) s.Icon.sprite = item != null ? item.iconSprite : null;
            int have = a.served != null && idx < a.served.Length ? a.served[idx] : 0;
            if (s.Count != null) s.Count.text = have.ToString();
            if (s.Button != null) s.Button.interactable = false;
            if (s.Tips != null && item != null)
            {
                int need = data.requireCounts != null && idx < data.requireCounts.Length ? data.requireCounts[idx] : 1;
                s.Tips.tips.itemId = itemId;
                s.Tips.tips.itemInc = 0;
                s.Tips.tips.itemCount = 0;
                s.Tips.tips.type = UIButton.ItemTipType.Item;
                s.Tips.tips.corner = 2;
                s.Tips.tips.delay = 0.2f;
                s.Tips.tips.tipText = need + " / 次";
            }
        }

        // ───────────────────────── 槽克隆 ─────────────────────────

        private static void HideAll(List<Slot> list)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].Root != null) list[i].Root.SetActive(false);
        }

        private static void EnsureSlots(UIAssemblerWindow w, List<Slot> list, int need, bool input)
        {
            if (w.productGroup == null) return;
            Transform parent = w.productGroup.transform.parent;
            while (list.Count < need)
            {
                var go = UnityEngine.Object.Instantiate<GameObject>(w.productGroup.gameObject, parent);
                go.name = (input ? "recipeslots-input-" : "recipeslots-product-") + list.Count;
                var slot = BuildSlot(go, input);
                if (!input)
                {
                    int captured = list.Count;
                    if (slot.Button != null)
                    {
                        slot.Button.onClick.RemoveAllListeners();
                        slot.Button.onClick.AddListener(() => TakeProduct(w, captured));
                    }
                }
                list.Add(slot);
            }
            // 重绑定点击索引（列表可能复用到不同机器窗口，简单起见每次刷新）
            if (!input)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var b = list[i].Button;
                    if (b == null) continue;
                    int captured = i;
                    b.onClick.RemoveAllListeners();
                    b.onClick.AddListener(() => TakeProduct(w, captured));
                }
            }
        }

        private static Slot BuildSlot(GameObject go, bool input)
        {
            var slot = new Slot { Root = go };
            var t = go.transform;
            var rt = t as RectTransform;
            if (rt != null) rt.sizeDelta = new Vector2(SlotSize, SlotSize);
            HideChild(t, "product-icon-1");
            HideChild(t, "circle-fg-1");
            HideChild(t, "extra-circle-fg-1");
            HideChild(t, "cnt-text-1");
            HideChild(t, "stop-btn");
            if (input)
            {
                HideChild(t, "circle-fg");
                HideChild(t, "extra-circle-fg");
            }
            var iconT = t.Find("product-icon");
            if (iconT != null)
            {
                slot.Icon = iconT.GetComponent<Image>();
                slot.Button = iconT.GetComponent<Button>();
                slot.Tips = iconT.GetComponent<UIButton>();
            }
            var countT = t.Find("cnt-text");
            if (countT != null)
            {
                slot.Count = countT.GetComponent<Text>();
                var crt = countT as RectTransform;
                if (crt != null)
                {
                    // 产物与原料统一：数量贴到圆圈的右下角内侧（避免被下一行圆圈背景盖住）
                    crt.anchorMin = new Vector2(1f, 0f);
                    crt.anchorMax = new Vector2(1f, 0f);
                    crt.pivot = new Vector2(1f, 0f);
                    crt.anchoredPosition = new Vector2(-9f, 7f);
                    if (slot.Count != null) slot.Count.alignment = TextAnchor.LowerRight;
                }
                if (input && slot.Button != null) slot.Button.transition = Selectable.Transition.None;
            }
            var progT = t.Find("circle-fg");
            if (progT != null) slot.Progress = progT.GetComponent<Image>();
            var extraT = t.Find("extra-circle-fg");
            if (extraT != null) slot.ExtraProgress = extraT.GetComponent<Image>();
            return slot;
        }

        private static void HideChild(Transform t, string name)
        {
            var c = t.Find(name);
            if (c != null) c.gameObject.SetActive(false);
        }

        // ───────────────────────── 点击取物（调试通道复用）─────────────────────────

        internal static void DumpSlots(System.Text.StringBuilder o)
        {
            foreach (var kv in Cache)
            {
                var w = kv.Key;
                var st = kv.Value;
                if (w == null || st == null) continue;
                o.AppendLine("  assemblerId=" + w.assemblerId + " multi=" + st.Multi);
                AppendRect(o, "window", w.windowTrans);
                AppendRect(o, "productGroup", w.productGroup);
                AppendRect(o, "produce", w.produceGroup);
                AppendRect(o, "playerStorage", w.playerInventoryGroup);
                o.AppendLine("  extras=" + Describe(st.Extras));
                o.AppendLine("  gridProducts=" + Describe(st.GridProducts));
                o.AppendLine("  inputs=" + Describe(st.Inputs));
                var par = w.windowTrans != null ? w.windowTrans.parent as RectTransform : null;
                if (par != null) o.AppendLine("  windowParent rect=" + par.rect.size + " pos=" + par.position);
            }
        }

        private static string Describe(List<Slot> list)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append('[');
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                if (i > 0) sb.Append(',');
                if (s == null || s.Root == null) { sb.Append("null"); continue; }
                var rt = s.Root.transform as RectTransform;
                sb.Append(s.Root.name).Append(s.Root.activeSelf ? "" : "(off)")
                  .Append('@').Append(rt != null ? rt.anchoredPosition.ToString("F0") : "?");
                if (s.Count != null)
                {
                    var crt = s.Count.rectTransform;
                    sb.Append(" cnt='").Append(s.Count.text).Append("'")
                      .Append(s.Count.gameObject.activeSelf ? "" : "(off)")
                      .Append(" a=").Append(crt.anchorMin.ToString("F2"))
                      .Append(" p=").Append(crt.pivot.ToString("F2"))
                      .Append(" pos=").Append(crt.anchoredPosition.ToString("F0"));
                }
                else sb.Append(" cnt=null");
                if (rt != null)
                {
                    var c = new Vector3[4];
                    rt.GetWorldCorners(c);
                    Vector3 sc = c[0];
                    sb.Append(" scr=").Append(sc.ToString("F0"));
                }
            }
            sb.Append(']');
            return sb.ToString();
        }

        private static void AppendRect(System.Text.StringBuilder o, string name, RectTransform rt)
        {
            if (rt == null) { o.AppendLine("  " + name + "=null"); return; }
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            o.AppendLine("  " + name + " pos=" + rt.anchoredPosition.ToString("F0")
                + " size=" + rt.sizeDelta.ToString("F0")
                + " anchor=" + rt.anchorMin.ToString("F2") + "/" + rt.anchorMax.ToString("F2")
                + " pivot=" + rt.pivot.ToString("F2")
                + " wMin=" + corners[0].ToString("F0") + " wMax=" + corners[2].ToString("F0"));
        }

        /// <summary>窗口防出屏：按父容器尺寸把窗口挪回可视区域。</summary>
        internal static void ClampWindow(UIAssemblerWindow w)
        {
            if (w == null || w.windowTrans == null) return;
            var parent = w.windowTrans.parent as RectTransform;
            if (parent == null) return;
            var rt = w.windowTrans;
            float pw = parent.rect.width, ph = parent.rect.height;
            float sw = rt.sizeDelta.x, sh = rt.sizeDelta.y;
            if (rt.anchorMin.x == rt.anchorMax.x && rt.anchorMin.y == rt.anchorMax.y)
            {
                var p = rt.anchoredPosition;
                float minX = -pw * 0.5f + sw * rt.pivot.x;
                float maxX = pw * 0.5f - sw * (1f - rt.pivot.x);
                float minY = -ph * 0.5f + sh * rt.pivot.y;
                float maxY = ph * 0.5f - sh * (1f - rt.pivot.y);
                float nx = Mathf.Clamp(p.x, Mathf.Min(minX, maxX), Mathf.Max(minX, maxX));
                float ny = Mathf.Clamp(p.y, Mathf.Min(minY, maxY), Mathf.Max(minY, maxY));
                if (Mathf.Abs(nx - p.x) > 0.5f || Mathf.Abs(ny - p.y) > 0.5f)
                    rt.anchoredPosition = new Vector2(nx, ny);
            }
        }

        internal static bool ClickProduct(int index)
        {
            var root = UIRoot.instance;
            var w = root != null && root.uiGame != null ? root.uiGame.assemblerWindow : null;
            if (w == null) return false;
            TakeProduct(w, index);
            return true;
        }

        private static void TakeProduct(UIAssemblerWindow w, int index)
        {
            var planet = GameMain.localPlanet;
            var factory = planet != null ? planet.factory : null;
            var player = GameMain.mainPlayer;
            if (factory == null || player == null) return;
            int asmId = w.assemblerId;
            if (asmId <= 0) return;
            ref AssemblerComponent a = ref factory.factorySystem.assemblerPool[asmId];
            if (a.id != asmId || a.recipeExecuteData == null) return;
            var products = a.recipeExecuteData.products;
            if (index < 0 || index >= products.Length || a.produced[index] <= 0) return;
            if (player.inhandItemId > 0) return;
            int added = player.TryAddItemToPackage(products[index], a.produced[index], 0, throwTrash: false);
            factory.factorySystem.assemblerPool[asmId].produced[index] = 0;
            if (added > 0) UIItemup.Up(products[index], added);
        }
    }
}

