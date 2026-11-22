#if !SHIP
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace DSPRecipeSlots
{
    /// <summary>
    /// 开发期文件命令通道（只在本机 dev 开关打开时工作）。
    /// 每秒轮询 &lt;插件目录&gt;\_dev\devcmd.txt，逐行执行，结果追加到 _dev\devout.txt 并删除命令文件。
    /// 这样无需键盘鼠标即可实机验证：开新档、放建筑、设配方、投料、打印装配器状态。
    ///
    /// 命令：
    ///   gamestate
    ///   newgame [seed] [starCount] [resourceMultiplier]
    ///   place &lt;itemId&gt; [offsetX] [offsetZ]   （直接 AddEntityDataWithComponents，不消耗物品）
    ///   build &lt;itemId&gt;                        （完整 BuildFinally 链路，会走建造流程）
    ///   setrecipe &lt;entityId&gt; &lt;recipeId&gt;
    ///   insert &lt;entityId&gt; &lt;itemId&gt; [count]     （走 PlanetFactory.InsertInto，测补丁受理）
    ///   dump &lt;entityId&gt;                     （打印装配器 needs/served/produced/cycleCount）
    ///   entities [count]
    ///   recipeinfo &lt;recipeId&gt;
    /// </summary>
    internal static class DevChannel
    {
        internal static string DevDir;
        private static BepInEx.Configuration.ConfigEntry<bool> _enabled;
        private static float _nextPoll;

        internal static void Init(BepInEx.Configuration.ConfigFile config)
        {
            _enabled = config.Bind("Dev", "EnableCommandChannel", false,
                "开发期文件命令通道（_dev/devcmd.txt），仅本机调试用。");
            if (!_enabled.Value) return; // 发布态：不创建 _dev 目录、不轮询
            string pluginDir;
            try { pluginDir = Path.GetDirectoryName(typeof(DevChannel).Assembly.Location); }
            catch { pluginDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BepInEx", "plugins", "DSP-RecipeSlots"); }
            DevDir = Path.Combine(pluginDir ?? ".", "_dev");
            try
            {
                Directory.CreateDirectory(DevDir);
                Plugin.Log.LogInfo("[dev] command channel: " + DevDir);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("[dev] cannot create " + DevDir + " : " + ex.Message);
            }
        }

        internal static void Tick()
        {
            if (_enabled == null || !_enabled.Value) return;
            float now = Time.realtimeSinceStartup;
            if (now < _nextPoll) return;
            _nextPoll = now + 1f;
            string cmdPath = Path.Combine(DevDir, "devcmd.txt");
            if (!File.Exists(cmdPath)) return;

            string[] lines;
            try
            {
                lines = File.ReadAllLines(cmdPath, Encoding.UTF8);
                File.Delete(cmdPath);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("[dev] read devcmd failed: " + ex.Message);
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("===== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " tick=" + GameMain.gameTick + " =====");
            foreach (string raw in lines)
            {
                string line = (raw ?? "").Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                sb.AppendLine("> " + line);
                try { Execute(line, sb); }
                catch (Exception ex) { sb.AppendLine("  [exception] " + ex.GetType().Name + ": " + ex.Message); }
            }
            sb.AppendLine();
            try { File.AppendAllText(Path.Combine(DevDir, "devout.txt"), sb.ToString(), new UTF8Encoding(false)); }
            catch (Exception ex) { Plugin.Log.LogWarning("[dev] write devout failed: " + ex.Message); }
        }

        private static void Execute(string line, StringBuilder o)
        {
            string[] p = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            string cmd = p[0].ToLowerInvariant();
            switch (cmd)
            {
                case "gamestate": GameState(o); break;
                case "newgame": NewGame(p, o); break;
                case "place": Place(p, o); break;
                case "build": Build(p, o); break;
                case "setrecipe": SetRecipe(p, o); break;
                case "insert": Insert(p, o); break;
                case "dump": Dump(p, o); break;
                case "entities": Entities(p, o); break;
                case "recipeinfo": RecipeInfo(p, o); break;
                case "tickinfo": TickInfo(o); break;
                case "force": Force(p, o); break;
                case "runbg": Application.runInBackground = true; o.AppendLine("  runInBackground=true"); break;
                case "itemid": ItemId(p, o); break;
                case "picktest": PickTest(p, o); break;
                case "pickwith": PickWith(p, o); break;
                case "save": SaveGame(p, o); break;
                case "load": LoadGame(p, o); break;
                case "prefab": Prefab(p, o); break;
                case "uiopen": UiOpen(p, o); break;
                case "uiclose": UiClose(o); break;
                case "uidump": UiDump(p, o); break;
                case "uifields": UiFields(o); break;
                case "uitake": o.AppendLine("  uitake index=" + p[1] + " ok=" + AssemblerUiPatch.ClickProduct(int.Parse(p[1]))); break;
                case "inv": Inv(p, o); break;
                case "labtest": LabTest(p, o); break;
                case "labdump": LabDump(p, o); break;
                case "setlabrecipe": SetLabRecipe(p, o); break;
                case "techtree": TechTreeCmd(p, o); break;
                case "uilabdump": UiLabDump(o); break;
                case "tiprecipe": TipRecipe(p, o); break;
                case "techtipui": TechTipUi(p, o); break;
                case "techtipclose": TechTipClose(o); break;
                case "labchain": LabChain(p, o); break;
                case "itemat": ItemAt(p, o); break;
                case "itemscan": ItemScan(p, o); break;
                case "techinfo": TechInfo(p, o); break;
                case "techstate": TechStateCmd(p, o); break;
                case "setresearch": SetResearch(p, o); break;
                case "setlabresearch": SetLabResearch(p, o); break;
                case "unlockrecipes": UnlockRecipes(p, o); break;
                case "techpos": TechPos(p, o); break;
                case "technode": TechNode(p, o); break;
                case "uislots": AssemblerUiPatch.DumpSlots(o); break;
                case "uiscreen": UiScreen(o); break;
                case "pkg": Pkg(p, o); break;
                case "uipatches": UiPatches(o); break;
                case "uikids": UiKids(p, o); break;
                case "autofeed": AutoFeed(p, o); break;
                default: o.AppendLine("  unknown command: " + cmd); break;
            }
        }

        private static PlanetFactory Factory()
        {
            var planet = GameMain.localPlanet;
            return planet != null ? planet.factory : null;
        }

        private static void GameState(StringBuilder o)
        {
            o.AppendLine("  IsMenuDemo=" + DSPGame.IsMenuDemo + " GameMain.isNull=" + GameMain.isNull
                + " data=" + (GameMain.data != null) + " gameTick=" + GameMain.gameTick
                + " isRunning=" + GameMain.isRunning + " isLoading=" + GameMain.isLoading);
            o.AppendLine("  localPlanet=" + (GameMain.localPlanet != null ? GameMain.localPlanet.id.ToString() : "null")
                + " mainPlayer=" + (GameMain.mainPlayer != null));
            if (GameMain.localPlanet != null)
            {
                o.AppendLine("  planet.factoryLoaded=" + GameMain.localPlanet.factoryLoaded
                    + " factory=" + (GameMain.localPlanet.factory != null)
                    + " entityCursor=" + (GameMain.localPlanet.factory != null ? GameMain.localPlanet.factory.entityCursor : -1)
                    + " assemblerCursor=" + (GameMain.localPlanet.factory != null ? GameMain.localPlanet.factory.factorySystem.assemblerCursor : -1));
                if (GameMain.mainPlayer != null)
                    o.AppendLine("  player.position=" + GameMain.mainPlayer.position.ToString("F2"));
            }
        }

        private static void NewGame(string[] p, StringBuilder o)
        {
            if (GameMain.instance == null) { o.AppendLine("  game instance not ready"); return; }
            int seed = p.Length > 1 ? int.Parse(p[1]) : 12345678;
            int starCount = p.Length > 2 ? int.Parse(p[2]) : 64;
            float res = p.Length > 3 ? float.Parse(p[3]) : 1f;
            var desc = new GameDesc();
            desc.SetForNewGame(UniverseGen.algoVersion, seed, starCount, 1, res);
            o.AppendLine("  newgame seed=" + seed + " stars=" + starCount + " res=" + res + " -> StartGameSkipPrologue");
            DSPGame.StartGameSkipPrologue(desc);
        }

        private static Vector3 SurfacePos(PlanetData planet, Player player)
        {
            Vector3 dir = player.position.normalized;
            float height = planet.radius;
            try
            {
                float h = planet.data.QueryModifiedHeight(dir * planet.radius);
                if (h > 1f && h < planet.radius * 3f) height = h;
            }
            catch { }
            return dir * (height + 0.3f);
        }

        private static void Place(string[] p, StringBuilder o)
        {
            var planet = GameMain.localPlanet;
            var factory = Factory();
            if (factory == null) { o.AppendLine("  no local factory"); return; }
            int itemId = int.Parse(p[1]);
            var proto = LDB.items.Select(itemId);
            if (proto == null) { o.AppendLine("  item not found: " + itemId); return; }
            var player = GameMain.mainPlayer;
            Vector3 pos = SurfacePos(planet, player);
            Quaternion rot = Quaternion.LookRotation(Vector3.forward, pos.normalized);
            // 可选偏移（沿表面切向），便于放多个建筑
            if (p.Length > 3)
            {
                float ox = float.Parse(p[2]);
                float oz = float.Parse(p[3]);
                Vector3 tangent = Vector3.Cross(pos.normalized, Vector3.forward).normalized;
                Vector3 tangent2 = Vector3.Cross(pos.normalized, tangent).normalized;
                pos = pos + (tangent * ox + tangent2 * oz);
                pos = pos.normalized * (pos.magnitude);
            }
            EntityData entity = default;
            entity.protoId = (short)itemId;
            entity.modelIndex = (short)proto.ModelIndex;
            entity.pos = pos;
            entity.rot = rot;
            entity.alt = pos.magnitude;
            entity.tilt = 0;
            entity.localized = true;
            int eid = factory.AddEntityDataWithComponents(entity, 0);
            o.AppendLine("  place item=" + itemId + "(" + proto.Name + ") -> eid=" + eid);
            if (eid > 0)
            {
                var e = factory.entityPool[eid];
                o.AppendLine("  components: assembler=" + e.assemblerId + " inserter=" + e.inserterId
                    + " powerGen=" + e.powerGenId + " powerCon=" + e.powerConId + " storage=" + e.storageId
                    + " lab=" + e.labId + " belt=" + e.beltId);
            }
        }

        private static void Build(string[] p, StringBuilder o)
        {
            var planet = GameMain.localPlanet;
            var factory = Factory();
            var player = GameMain.mainPlayer;
            if (factory == null || player == null) { o.AppendLine("  no local factory/player"); return; }
            int itemId = int.Parse(p[1]);
            var proto = LDB.items.Select(itemId);
            if (proto == null) { o.AppendLine("  item not found: " + itemId); return; }
            Vector3 pos = SurfacePos(planet, player);
            Quaternion rot = Quaternion.LookRotation(Vector3.forward, pos.normalized);
            PrebuildData pre = default;
            pre.protoId = (short)itemId;
            pre.modelIndex = (short)proto.ModelIndex;
            pre.pos = pos;
            pre.rot = rot;
            pre.pos2 = pos;
            pre.rot2 = rot;
            pre.itemRequired = 0;
            pre.id = 0;
            int pid = factory.AddPrebuildDataWithComponents(pre);
            o.AppendLine("  prebuild -> " + pid);
            if (pid <= 0) { o.AppendLine("  prebuild failed"); return; }
            try
            {
                factory.BuildFinally(player, pid, true, true);
                o.AppendLine("  BuildFinally ok");
            }
            catch (Exception ex)
            {
                o.AppendLine("  BuildFinally exception: " + ex.GetType().Name + " " + ex.Message);
                EntityData e2 = default;
                e2.protoId = (short)itemId; e2.modelIndex = (short)proto.ModelIndex; e2.pos = pos; e2.rot = rot;
                e2.alt = pos.magnitude; e2.localized = true;
                int eid = factory.AddEntityDataWithComponents(e2, pid);
                o.AppendLine("  fallback entity -> " + eid);
            }
        }

        private static void SetRecipe(string[] p, StringBuilder o)
        {
            var factory = Factory();
            if (factory == null) { o.AppendLine("  no local factory"); return; }
            int eid = int.Parse(p[1]);
            int rid = int.Parse(p[2]);
            if (eid <= 0 || eid >= factory.entityCursor || factory.entityPool[eid].id != eid)
            { o.AppendLine("  entity not found: " + eid); return; }
            int aid = factory.entityPool[eid].assemblerId;
            if (aid <= 0) { o.AppendLine("  not an assembler: eid=" + eid); return; }
            var recipe = LDB.recipes.Select(rid);
            if (recipe == null) { o.AppendLine("  recipe not found: " + rid); return; }
            ref AssemblerComponent a = ref factory.factorySystem.assemblerPool[aid];
            a.SetRecipe(rid, factory.entitySignPool);
            o.AppendLine("  setrecipe eid=" + eid + " aid=" + aid + " recipe=" + rid + "(" + recipe.name + ")"
                + " type=" + recipe.Type + " items=" + recipe.Items.Length + " needsLen=" + (a.needs == null ? -1 : a.needs.Length)
                + " servedLen=" + (a.served == null ? -1 : a.served.Length) + " pcId=" + a.pcId);
        }

        private static void Insert(string[] p, StringBuilder o)
        {
            var factory = Factory();
            if (factory == null) { o.AppendLine("  no local factory"); return; }
            int eid = int.Parse(p[1]);
            int itemId = int.Parse(p[2]);
            int count = p.Length > 3 ? int.Parse(p[3]) : 1;
            byte remainInc;
            int inserted = factory.InsertInto(eid, 0, itemId, (byte)count, 1, out remainInc);
            o.AppendLine("  insert eid=" + eid + " item=" + itemId + " x" + count + " -> inserted=" + inserted + " remainInc=" + remainInc);
            int aid = factory.entityPool[eid].assemblerId;
            if (aid > 0)
            {
                ref AssemblerComponent a = ref factory.factorySystem.assemblerPool[aid];
                o.AppendLine("  served=[" + Join(a.served) + "] needs=[" + Join(a.needs) + "] produced=[" + Join(a.produced) + "]");
            }
        }

        private static void Dump(string[] p, StringBuilder o)
        {
            var factory = Factory();
            if (factory == null) { o.AppendLine("  no local factory"); return; }
            int eid = p.Length > 1 ? int.Parse(p[1]) : 0;
            if (eid <= 0 || eid >= factory.entityCursor || factory.entityPool[eid].id != eid)
            { o.AppendLine("  entity not found: " + eid); return; }
            int aid = factory.entityPool[eid].assemblerId;
            if (aid <= 0) { o.AppendLine("  not an assembler: eid=" + eid); return; }
            ref AssemblerComponent a = ref factory.factorySystem.assemblerPool[aid];
            var recipe = a.recipeId > 0 ? LDB.recipes.Select(a.recipeId) : null;
            int pcId = a.pcId;
            int nid = -1; float serve = -1f;
            try
            {
                var ps = factory.powerSystem;
                nid = ps.consumerPool[pcId].networkId;
                serve = ps.networkServes[nid];
            }
            catch { }
            o.AppendLine("  gameTick=" + GameMain.gameTick + " eid=" + eid + " aid=" + aid + " pcId=" + pcId + " networkId=" + nid + " networkServe=" + serve);
            o.AppendLine("  recipeId=" + a.recipeId + "(" + (recipe != null ? recipe.name : "-") + ") recipeType=" + a.recipeType
                + " speed=" + a.speed + " speedOverride=" + a.speedOverride + " replicating=" + a.replicating
                + " time=" + a.time + " timeSpend=" + (a.recipeExecuteData != null ? a.recipeExecuteData.timeSpend : -1)
                + " cycleCount=" + a.cycleCount);
            if (a.recipeExecuteData != null)
                o.AppendLine("  requires=[" + Join(a.recipeExecuteData.requires) + "] requireCounts=[" + Join(a.recipeExecuteData.requireCounts) + "]");
            o.AppendLine("  needs=[" + Join(a.needs) + "]");
            o.AppendLine("  served=[" + Join(a.served) + "] incServed=[" + Join(a.incServed) + "]");
            o.AppendLine("  produced=[" + Join(a.produced) + "]");
            if (recipe != null)
                o.AppendLine("  recipe.Items=[" + Join(recipe.Items) + "] recipe.Results=[" + Join(recipe.Results) + "]");
        }

        private static void Entities(string[] p, StringBuilder o)
        {
            var factory = Factory();
            if (factory == null) { o.AppendLine("  no local factory"); return; }
            int limit = p.Length > 1 ? int.Parse(p[1]) : 20;
            int printed = 0;
            for (int i = 1; i < factory.entityCursor && printed < limit; i++)
            {
                var e = factory.entityPool[i];
                if (e.id != i) continue;
                o.AppendLine("  eid=" + i + " proto=" + e.protoId + " assembler=" + e.assemblerId + " inserter=" + e.inserterId
                    + " belt=" + e.beltId + " storage=" + e.storageId + " powerCon=" + e.powerConId + " powerGen=" + e.powerGenId);
                printed++;
            }
        }

        private static void TickInfo(StringBuilder o)
        {
            o.AppendLine("  isPaused=" + GameMain.isPaused + " isFullscreenPaused=" + GameMain.isFullscreenPaused
                + " isRunning=" + GameMain.isRunning + " isLoading=" + GameMain.isLoading
                + " timeScale=" + Time.timeScale.ToString("F2") + " runInBackground=" + Application.runInBackground
                + " gameTick=" + GameMain.gameTick + " frame=" + Time.frameCount);
            o.AppendLine("  isMenuDemo=" + DSPGame.IsMenuDemo + " sandboxTools=" + GameMain.sandboxToolsEnabled);
            var planet = GameMain.localPlanet;
            if (planet == null) { o.AppendLine("  localPlanet=null"); return; }
            o.AppendLine("  planet.id=" + planet.id + " factoryLoaded=" + planet.factoryLoaded
                + " loaded=" + planet.loaded + " factory=" + (planet.factory != null));
            if (planet.factory == null) return;
            var f = planet.factory;
            o.AppendLine("  localLoadedPlanetFactory==factory: " + (GameMain.data != null && GameMain.data.localLoadedPlanetFactory == f)
                + " factoriesLen=" + (GameMain.data != null && GameMain.data.factories != null ? GameMain.data.factories.Length : -1));
            try
            {
                o.AppendLine("  factorySystem.assemblerCount=" + f.factorySystem.assemblerCount
                    + " assemblerCursor=" + f.factorySystem.assemblerCursor
                    + " pool0.id=" + f.factorySystem.assemblerPool[0].id
                    + " pool1.id=" + f.factorySystem.assemblerPool[1].id
                    + " pool1.time=" + f.factorySystem.assemblerPool[1].time);
            }
            catch (Exception ex) { o.AppendLine("  pool probe failed: " + ex.Message); }
        }

        private static void Force(string[] p, StringBuilder o)
        {
            var factory = Factory();
            if (factory == null) { o.AppendLine("  no local factory"); return; }
            int eid = p.Length > 1 ? int.Parse(p[1]) : 1;
            int times = p.Length > 2 ? int.Parse(p[2]) : 5;
            int aid = factory.entityPool[eid].assemblerId;
            if (aid <= 0) { o.AppendLine("  not an assembler"); return; }
            ref AssemblerComponent a = ref factory.factorySystem.assemblerPool[aid];
            var productRegister = new int[20000];
            var consumeRegister = new int[20000];
            for (int i = 0; i < times; i++)
            {
                if (a.recipeId != 0)
                {
                    a.UpdateNeeds();
                    a.InternalUpdate(1f, productRegister, consumeRegister);
                }
            }
            o.AppendLine("  force x" + times + " aid=" + aid + " replicating=" + a.replicating
                + " time=" + a.time + " cycleCount=" + a.cycleCount + " served=[" + Join(a.served) + "]"
                + " needs=[" + Join(a.needs) + "] produced=[" + Join(a.produced) + "]");
        }

        private static void ItemId(string[] p, StringBuilder o)
        {
            if (p.Length < 2) { o.AppendLine("  usage: itemid <namePart>"); return; }
            string key = p[1];
            int n = 0;
            foreach (var item in LDB.items.dataArray)
            {
                if (item == null) continue;
                if ((item.Name != null && item.Name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                    || (item.name != null && item.name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    o.AppendLine("  item id=" + item.ID + " Name=" + item.Name + " name=" + item.name
                        + " IsEntity=" + item.IsEntity + " ModelIndex=" + item.ModelIndex);
                    if (++n >= 25) break;
                }
            }
            if (n == 0) o.AppendLine("  no item matched: " + key);
        }

        /// <summary>
        /// 复现分拣器的抓料调用：factory.PickFrom(src, offset, filter, needs, out stack, out inc)。
        /// needs 取目标装配器当前的 6 格广告数组，因此这里验证的正是「分拣器从箱子/传送带按 needs 挑料」的链路。
        /// </summary>
        private static void PickTest(string[] p, StringBuilder o)
        {
            var factory = Factory();
            if (factory == null) { o.AppendLine("  no local factory"); return; }
            int dstEid = int.Parse(p[1]);
            int srcEid = int.Parse(p[2]);
            int times = p.Length > 3 ? int.Parse(p[3]) : 8;
            int aid = factory.entityPool[dstEid].assemblerId;
            if (aid <= 0) { o.AppendLine("  dst is not an assembler: " + dstEid); return; }
            ref AssemblerComponent a = ref factory.factorySystem.assemblerPool[aid];
            int[] needs = a.needs;
            o.AppendLine("  dst=" + dstEid + " src=" + srcEid + " needs=[" + Join(needs) + "]");
            for (int i = 0; i < times; i++)
            {
                byte stack, inc;
                int item = factory.PickFrom(srcEid, 0, 0, needs, out stack, out inc);
                o.AppendLine("  pick[" + i + "] item=" + item + " stack=" + stack + " inc=" + inc
                    + " (needs=[" + Join(needs) + "])");
                if (item == 0) break;
            }
        }

        /// <summary>用显式 needs 数组（6 格）调 PickFrom，验证任意物品（含第 7 种）都能被 needs 过滤命中。</summary>
        private static void PickWith(string[] p, StringBuilder o)
        {
            var factory = Factory();
            if (factory == null) { o.AppendLine("  no local factory"); return; }
            int srcEid = int.Parse(p[1]);
            int[] needs = new int[6];
            for (int i = 0; i < 6; i++)
                needs[i] = p.Length > 2 + i ? int.Parse(p[2 + i]) : 0;
            byte stack, inc;
            int item = factory.PickFrom(srcEid, 0, 0, needs, out stack, out inc);
            o.AppendLine("  pickwith src=" + srcEid + " needs=[" + Join(needs) + "] -> item=" + item + " stack=" + stack);
        }

        /// <summary>打印建筑 prefab 的关键字段（插入口数量、是否装配器/分拣器、速度等）。</summary>
        private static void Prefab(string[] p, StringBuilder o)
        {
            int itemId = int.Parse(p[1]);
            var proto = LDB.items.Select(itemId);
            if (proto == null) { o.AppendLine("  item not found: " + itemId); return; }
            var pd = proto.prefabDesc;
            o.AppendLine("  item=" + itemId + "(" + proto.Name + ") modelIndex=" + proto.ModelIndex + " isEntity=" + proto.IsEntity
                + " prefabDesc=" + (pd != null));
            if (pd == null) return;
            o.AppendLine("  slotPoses=" + (pd.slotPoses == null ? -1 : pd.slotPoses.Length)
                + " portPoses=" + (pd.portPoses == null ? -1 : pd.portPoses.Length)
                + " isInserter=" + pd.isInserter + " isAssembler=" + pd.isAssembler
                + " isBelt=" + pd.isBelt + " isStation=" + pd.isStation);
            o.AppendLine("  assemblerRecipeType=" + pd.assemblerRecipeType + " assemblerSpeed=" + pd.assemblerSpeed
                + " inserterGrade=" + pd.inserterGrade);
        }

        private static UIAssemblerWindow AssemblerWindow()
        {
            var root = UIRoot.instance;
            if (root == null || root.uiGame == null) return null;
            return root.uiGame.assemblerWindow;
        }


        private static void UiOpen(string[] p, StringBuilder o)
        {
            int eid = int.Parse(p[1]);
            var player = GameMain.mainPlayer;
            if (player == null || player.controller == null) { o.AppendLine("  player not ready"); return; }
            var factory = Factory();
            if (factory == null || eid <= 0 || eid >= factory.entityCursor || factory.entityPool[eid].id != eid)
            { o.AppendLine("  entity not found: " + eid); return; }
            try
            {
                player.controller.actionInspect.SetInspectee(EObjectType.None, eid);
                var w = AssemblerWindow();
                o.AppendLine("  uiopen eid=" + eid + " windowActive=" + (w != null && w.gameObject.activeInHierarchy)
                    + " assemblerId=" + (w != null ? w.assemblerId : -1));
            }
            catch (Exception ex) { o.AppendLine("  uiopen exception: " + ex.GetType().Name + " " + ex.Message); }
        }

        /// <summary>打印 UIAssemblerWindow 关键字段对应的 GameObject 路径 / 激活 / Rect 信息，用于设计动态槽。</summary>
        private static void UiFields(StringBuilder o)
        {
            var w = AssemblerWindow();
            if (w == null) { o.AppendLine("  assembler window null"); return; }
            o.AppendLine("  window: active=" + w.gameObject.activeInHierarchy + " mbActive=" + w.active
                + " assemblerId=" + w.assemblerId + " sizeDelta=" + w.windowTrans.sizeDelta.ToString("F0"));
            PrintField(o, "windowTrans", w.windowTrans);
            PrintField(o, "recipeGroup", w.recipeGroup);
            PrintField(o, "produceGroup", w.produceGroup);
            PrintField(o, "productGroup", w.productGroup);
            PrintField(o, "productButton0", w.productButton0);
            PrintField(o, "productButton1", w.productButton1);
            PrintField(o, "productIcon0", w.productIcon0);
            PrintField(o, "productIcon1", w.productIcon1);
            PrintField(o, "productProgress0", w.productProgress0);
            PrintField(o, "productProgress1", w.productProgress1);
            PrintField(o, "extraProductProgress0", w.extraProductProgress0);
            PrintField(o, "extraProductProgress1", w.extraProductProgress1);
            PrintField(o, "productCountText0", w.productCountText0);
            PrintField(o, "productCountText1", w.productCountText1);
            PrintField(o, "speedGroup", w.speedGroup);
            PrintField(o, "servingGroup", w.servingGroup);
            PrintField(o, "servingBox", w.servingBox);
        }

        private static void PrintField(StringBuilder o, string name, UnityEngine.Object obj)
        {
            var comp = obj as Component;
            if (comp == null)
            {
                o.AppendLine("  " + name + " = " + (obj == null ? "null" : obj.name));
                return;
            }
            var rt = comp.transform as RectTransform;
            string extra = "";
            if (rt != null)
            {
                extra = " anchorMin=" + rt.anchorMin.ToString("F2") + " anchorMax=" + rt.anchorMax.ToString("F2")
                    + " pivot=" + rt.pivot.ToString("F2");
                var corners = new Vector3[4];
                rt.GetWorldCorners(corners);
                extra += " wMin=" + corners[0].ToString("F0") + " wMax=" + corners[2].ToString("F0");
            }
            o.AppendLine("  " + name + " -> path=" + PathOf(comp.transform)
                + " activeSelf=" + comp.gameObject.activeSelf
                + (rt != null ? " pos=" + rt.anchoredPosition.ToString("F0") + " size=" + rt.sizeDelta.ToString("F0") : "")
                + extra
                + " components=" + string.Join(",", System.Array.ConvertAll(comp.GetComponents<Component>(), c => c == null ? "null" : c.GetType().Name)));
        }

        private static string PathOf(Transform t)
        {
            var sb = new StringBuilder(t.name);
            var p = t.parent;
            while (p != null) { sb.Insert(0, p.name + "/"); p = p.parent; }
            return sb.ToString();
        }

        private static void UiClose(StringBuilder o)
        {
            var root = UIRoot.instance;
            var uiGame = root != null ? root.uiGame : null;
            if (uiGame == null) { o.AppendLine("  uiGame not ready"); return; }
            try
            {
                uiGame.ShutAssemblerWindow();
                o.AppendLine("  uiclose windowActive=" + uiGame.assemblerWindow.gameObject.activeSelf);
            }
            catch (Exception ex) { o.AppendLine("  uiclose exception: " + ex.Message); }
        }

        /// <summary>打印窗口内所有 Text 的屏幕坐标（像素，左上原点），用于把截图里的可疑文字对回控件。</summary>
        private static void UiScreen(StringBuilder o)
        {
            var w = AssemblerWindow();
            if (w == null) { o.AppendLine("  assembler window null"); return; }
            var canvas = w.GetComponentInParent<Canvas>();
            Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
            var texts = w.GetComponentsInChildren<Text>(true);
            o.AppendLine("  uiscreen texts=" + texts.Length + " cam=" + (cam != null ? cam.name : "null"));
            foreach (var t in texts)
            {
                if (t == null) continue;
                var rt = t.rectTransform;
                var c = new Vector3[4];
                rt.GetWorldCorners(c);
                Vector2 p0 = RectTransformUtility.WorldToScreenPoint(cam, c[0]);
                Vector2 p2 = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
                o.AppendLine("  '" + t.text + "' on=" + t.gameObject.activeInHierarchy
                    + " path=" + PathOf(t.transform)
                    + " xy=(" + (p0.x).ToString("F0") + "," + (Screen.height - p2.y).ToString("F0") + ")"
                    + "-(" + (p2.x).ToString("F0") + "," + (Screen.height - p0.y).ToString("F0") + ")");
            }
        }

        /// <summary>增删玩家背包行数（模拟背包扩容，验证「窗口高度跟随物品栏行数」）。</summary>
        private static void Pkg(string[] p, StringBuilder o)
        {
            var player = GameMain.mainPlayer;
            if (player == null || player.package == null) { o.AppendLine("  no player package"); return; }
            int rows = p.Length > 1 ? int.Parse(p[1]) : 4;
            int cols = player.GetPackageColumnCount();
            player.package.SetSize(rows * cols + 1);
            o.AppendLine("  pkg size=" + player.package.size + " cols=" + cols
                + " rows=" + ((player.package.size - 1) / cols + 1)
                + " uiRowCount=" + (UIRoot.instance != null && UIRoot.instance.uiGame != null && UIRoot.instance.uiGame.assemblerWindow != null
                    ? UIRoot.instance.uiGame.assemblerWindow.playerInventory.rowCount.ToString() : "?"));
        }

        /// <summary>列出 UIAssemblerWindow 上被哪些 Harmony 补丁改过（定位第三方 MOD 冲突）。</summary>
        private static void UiPatches(StringBuilder o)
        {
            var t = typeof(UIAssemblerWindow);
            var flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var m in t.GetMethods(flags))
            {
                var info = HarmonyLib.Harmony.GetPatchInfo(m);
                if (info == null) continue;
                var owners = new List<string>();
                if (info.Prefixes != null) foreach (var x in info.Prefixes) owners.Add("pre:" + x.owner);
                if (info.Postfixes != null) foreach (var x in info.Postfixes) owners.Add("post:" + x.owner);
                if (info.Transpilers != null) foreach (var x in info.Transpilers) owners.Add("trans:" + x.owner);
                if (info.Finalizers != null) foreach (var x in info.Finalizers) owners.Add("fin:" + x.owner);
                o.AppendLine("  " + m.Name + " <- " + string.Join(", ", owners.ToArray()));
            }
        }

        /// <summary>列出某个 UI 节点的直接子物体（默认 productGroup=circle-back），用于排查被塞进来的克隆体。</summary>
        private static void UiKids(string[] p, StringBuilder o)
        {
            string path = p.Length > 1
                ? string.Join(" ", p, 1, p.Length - 1)
                : "UI Root/Overlay Canvas/In Game/Windows/Assembler Window/produce/circle-back";
            var go = GameObject.Find(path);
            if (go == null) { o.AppendLine("  no object: " + path); return; }
            o.AppendLine("  kids of '" + go.name + "' n=" + go.transform.childCount + " activeSelf=" + go.activeSelf);
            for (int i = 0; i < go.transform.childCount; i++)
            {
                var c = go.transform.GetChild(i);
                var crt = c as RectTransform;
                o.AppendLine("    [" + i + "] " + c.name + " activeSelf=" + c.gameObject.activeSelf
                    + (crt != null ? " pos=" + crt.anchoredPosition.ToString("F0") : ""));
            }
        }

        private static void UiDump(string[] p, StringBuilder o)
        {
            string path = null;
            int maxDepth = 5;
            if (p.Length > 1)
            {
                int last = p.Length - 1;
                int d;
                if (int.TryParse(p[last], out d)) { maxDepth = d; last--; }
                if (last >= 1) path = string.Join(" ", p, 1, last);
            }
            GameObject go = null;
            if (!string.IsNullOrEmpty(path))
            {
                go = GameObject.Find(path);
            }
            else
            {
                var root = UIRoot.instance;
                var uiGame = root != null ? root.uiGame : null;
                if (uiGame != null && uiGame.assemblerWindow != null) go = uiGame.assemblerWindow.gameObject;
            }
            if (go == null) { o.AppendLine("  no ui object: " + (path ?? "(assembler window)")); return; }
            o.AppendLine("  dump '" + go.name + "' depth=" + maxDepth + " activeInHierarchy=" + go.activeInHierarchy);
            DumpTransform(go.transform, 1, maxDepth, o);
        }

        private static void DumpTransform(Transform t, int depth, int maxDepth, StringBuilder o)
        {
            var rt = t as RectTransform;
            string rect = rt != null ? " pos=" + rt.anchoredPosition.ToString("F0") + " size=" + rt.sizeDelta.ToString("F0") : "";
            o.AppendLine(new string(' ', depth * 2) + t.name + " active=" + t.gameObject.activeSelf + rect);
            foreach (var c in t.GetComponents<Component>())
            {
                if (c == null) continue;
                string info = c.GetType().Name;
                var img = c as Image;
                if (img != null) info += " sprite=" + (img.sprite != null ? img.sprite.name : "null");
                var txt = c as Text;
                if (txt != null) info += " text='" + txt.text + "'";
                var btn = c as Button;
                if (btn != null) info += " onClick=" + btn.onClick.GetPersistentEventCount();
                o.AppendLine(new string(' ', depth * 2 + 2) + "- " + info);
            }
            if (depth >= maxDepth) return;
            for (int i = 0; i < t.childCount; i++) DumpTransform(t.GetChild(i), depth + 1, maxDepth, o);
        }

        private static void Inv(string[] p, StringBuilder o)
        {
            var player = GameMain.mainPlayer;
            if (player == null || player.package == null) { o.AppendLine("  player package null"); return; }
            int itemId = int.Parse(p[1]);
            int count = 0;
            var inv = player.package;
            for (int i = 0; i < inv.size; i++)
            {
                if (inv.grids[i].itemId == itemId) count += inv.grids[i].count;
            }
            o.AppendLine("  inventory item=" + itemId + " count=" + count);
        }

        /// <summary>研究站 &gt;6 矩阵引擎单元测试：需求广播 / 瓶颈门控 / 第 7 种矩阵消耗。</summary>
        private static void LabTest(String[] p, StringBuilder o)
        {
            var factory = Factory();
            if (factory == null) { o.AppendLine("  no local factory"); return; }
            int eid = int.Parse(p[1]);
            int labId = factory.entityPool[eid].labId;
            if (labId <= 0) { o.AppendLine("  not a lab: eid=" + eid); return; }
            ref LabComponent lab = ref factory.factorySystem.labPool[labId];
            int techId = p.Length > 2 ? int.Parse(p[2]) : 1002;
            var tech = LDB.techs.Select(techId);
            if (tech == null) { o.AppendLine("  tech not found: " + techId); return; }

            lab.SetFunction(true, 0, techId, factory.entitySignPool);
            var ids = LabComponent.matrixIds;
            o.AppendLine("  matrixIds=[" + Join(ids) + "] matrixServedLen=" + (lab.matrixServed == null ? -1 : lab.matrixServed.Length)
                + " needsLen=" + (lab.needs == null ? -1 : lab.needs.Length) + " matrixPointsLen=" + LabComponent.matrixPoints.Length);

            if (lab.matrixServed != null) Array.Clear(lab.matrixServed, 0, lab.matrixServed.Length);
            lab.UpdateNeedsResearch();
            o.AppendLine("  published needs=[" + Join(lab.needs) + "]");

            // 模拟「科技要求第 7 种矩阵」：matrixPoints[6]=1
            var points = LabComponent.matrixPoints;
            if (points.Length <= 6) { o.AppendLine("  no extra matrix configured (Lab.ExtraMatrixIds empty) — skip >6 test"); return; }
            points[6] = 1;

            for (int i = 0; i < 6 && i < lab.matrixServed.Length; i++) lab.matrixServed[i] = 7200;
            var ts = new TechState(false, tech.Level, tech.MaxLevel, 0L, tech.GetHashNeeded(tech.Level), 0);
            var consume = new int[20000];
            int hashed = 0; long upoint = 0L; long hashReg = 0L;
            int zero = 0;
            for (int k = 0; k < 5; k++)
            {
                uint st = lab.InternalUpdateResearch(1f, 1f, consume, ref ts, ref hashed, ref upoint, ref hashReg);
                if (st == 0) zero++;
            }
            o.AppendLine("  GATE test (7th missing): zeroReturns=" + zero + " hashUploaded=" + ts.hashUploaded + " (expect zeroReturns=5, hash=0)");

            long up0 = ts.hashUploaded;
            lab.matrixServed[6] = 7200;
            for (int k = 0; k < 30; k++)
                lab.InternalUpdateResearch(1f, 1f, consume, ref ts, ref hashed, ref upoint, ref hashReg);
            o.AppendLine("  RUN test: hashUploaded " + up0 + " -> " + ts.hashUploaded
                + " consumedExtra=" + consume[ids[6]] + " matrixServed=[" + Join(lab.matrixServed) + "]");
        }

        private static void LabDump(String[] p, StringBuilder o)
        {
            var factory = Factory();
            if (factory == null) { o.AppendLine("  no local factory"); return; }
            int eid = int.Parse(p[1]);
            int labId = factory.entityPool[eid].labId;
            if (labId <= 0) { o.AppendLine("  not a lab: eid=" + eid); return; }
            ref LabComponent lab = ref factory.factorySystem.labPool[labId];
            o.AppendLine("  eid=" + eid + " labId=" + labId + " id=" + lab.id + " researchMode=" + lab.researchMode
                + " techId=" + lab.techId + " recipeId=" + lab.recipeId + " replicating=" + lab.replicating);
            o.AppendLine("  matrixIds=[" + Join(LabComponent.matrixIds) + "] matrixPoints=[" + Join(LabComponent.matrixPoints) + "]");
            o.AppendLine("  matrixServed=[" + Join(lab.matrixServed) + "] needs=[" + Join(lab.needs) + "]");
            o.AppendLine("  served=[" + Join(lab.served) + "] produced=[" + Join(lab.produced) + "]");
        }

        private static void SetLabRecipe(string[] p, StringBuilder o)
        {
            var factory = Factory();
            if (factory == null) { o.AppendLine("  no local factory"); return; }
            int eid = int.Parse(p[1]);
            int rid = int.Parse(p[2]);
            int labId = factory.entityPool[eid].labId;
            if (labId <= 0) { o.AppendLine("  not a lab: " + eid); return; }
            var recipe = LDB.recipes.Select(rid);
            if (recipe == null) { o.AppendLine("  recipe not found: " + rid); return; }
            ref LabComponent lab = ref factory.factorySystem.labPool[labId];
            lab.SetFunction(false, rid, 0, factory.entitySignPool);
            o.AppendLine("  setlabrecipe eid=" + eid + " lab=" + labId + " recipe=" + rid + "(" + recipe.name + ")"
                + " type=" + recipe.Type + " items=" + recipe.Items.Length + " -> lab.recipeId=" + lab.recipeId
                + " servedLen=" + (lab.served == null ? -1 : lab.served.Length) + " needsLen=" + (lab.needs == null ? -1 : lab.needs.Length));
        }

        private static void TechTreeCmd(string[] p, StringBuilder o)
        {
            var root = UIRoot.instance;
            var uiGame = root != null ? root.uiGame : null;
            var tt = uiGame != null ? uiGame.techTree : null;
            if (tt == null) { o.AppendLine("  techTree null"); return; }
            string sub = p.Length > 1 ? p[1].ToLowerInvariant() : "info";
            if (sub == "open")
            {
                tt._Open();
                o.AppendLine("  techTree open active=" + tt.active);
                return;
            }
            if (sub == "close")
            {
                tt._Close();
                o.AppendLine("  techTree close active=" + tt.active);
                return;
            }
            if (sub == "pad")
            {
                int techId = int.Parse(p[2]);
                int extra = p.Length > 3 ? int.Parse(p[3]) : 2;
                var tech = LDB.techs.Select(techId);
                if (tech == null) { o.AppendLine("  tech not found: " + techId); return; }
                int n = Math.Max(6, tech.Items.Length) + (tech.Items.Length > 6 ? 0 : extra);
                var items = new int[n];
                var points = new int[n];
                for (int i = 0; i < n; i++)
                {
                    items[i] = 6001 + i;
                    points[i] = 3600;
                }
                tech.Items = items;
                tech.ItemPoints = points;
                tech.Preload2();
                o.AppendLine("  pad tech=" + techId + " items=" + tech.Items.Length + " itemArray=" + (tech.itemArray == null ? -1 : tech.itemArray.Length));
                return;
            }
            var node = tt.nodePrefab;
            o.AppendLine("  nodePrefab=" + (node != null));
            if (node == null) return;
            o.AppendLine("  costIcons=" + (node.costIcons == null ? -1 : node.costIcons.Length)
                + " costIconBds=" + (node.costIconBds == null ? -1 : node.costIconBds.Length)
                + " costButtons=" + (node.costButtons == null ? -1 : node.costButtons.Length)
                + " costCountTexts=" + (node.costCountTexts == null ? -1 : node.costCountTexts.Length)
                + " pCostIcons=" + (node.pCostIcons == null ? -1 : node.pCostIcons.Length));
            if (node.costGroup != null)
            {
                o.AppendLine("  costGroup=" + PathOf(node.costGroup));
                DumpTransform(node.costGroup, 1, 3, o);
            }
        }

        private static void UiLabDump(StringBuilder o)
        {
            var root = UIRoot.instance;
            var uiGame = root != null ? root.uiGame : null;
            var lw = uiGame != null ? uiGame.labWindow : null;
            if (lw == null) { o.AppendLine("  labWindow null"); return; }
            o.AppendLine("  labWindow active=" + lw.active + " size=" + lw.windowTrans.sizeDelta.ToString("F0"));
            o.AppendLine("  itemButtons=" + (lw.itemButtons == null ? -1 : lw.itemButtons.Length)
                + " itemIcons=" + (lw.itemIcons == null ? -1 : lw.itemIcons.Length)
                + " itemPercents=" + (lw.itemPercents == null ? -1 : lw.itemPercents.Length)
                + " itemCountTexts=" + (lw.itemCountTexts == null ? -1 : lw.itemCountTexts.Length)
                + " itemLocks=" + (lw.itemLocks == null ? -1 : lw.itemLocks.Length)
                + " itemIncs=" + (lw.itemIncs == null ? -1 : lw.itemIncs.Length));
            o.AppendLine("  matrixIds=" + Join(LabComponent.matrixIds) + " matrixPoints=" + Join(LabComponent.matrixPoints));
            if (lw.itemButtons != null && lw.itemButtons.Length > 0 && lw.itemButtons[0] != null)
            {
                var t = lw.itemButtons[0].transform;
                o.AppendLine("  itemButtons[0] path=" + PathOf(t) + " pos=" + ((RectTransform)t).anchoredPosition.ToString("F0"));
                if (t.parent != null) DumpTransform(t.parent, 1, 2, o);
            }
        }

        /// <summary>用 UIItemTip 预制体里的 recipeEntry 构造一条配方行，验证图标数量/激活数（不依赖鼠标悬停）。</summary>
        private static void TipRecipe(string[] p, StringBuilder o)
        {
            int rid = int.Parse(p[1]);
            var recipe = LDB.recipes.Select(rid);
            if (recipe == null) { o.AppendLine("  recipe not found: " + rid); return; }
            var cfg = Configs.builtin;
            if (cfg == null || cfg.uiItemTipPrefab == null || cfg.uiItemTipPrefab.recipeEntry == null)
            { o.AppendLine("  uiItemTipPrefab/recipeEntry not available"); return; }
            var root = UIRoot.instance;
            if (root == null) { o.AppendLine("  UIRoot null"); return; }
            var entry = cfg.uiItemTipPrefab.recipeEntry;
            var go = UnityEngine.Object.Instantiate<GameObject>(entry.gameObject, root.transform);
            go.name = "recipeslots-tip-test";
            var inst = go.GetComponent<UIRecipeEntry>();
            inst.SetRecipe(recipe);
            int active = 0;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < inst.icons.Length; i++)
            {
                if (inst.icons[i] == null) continue;
                bool act = inst.icons[i].gameObject.activeSelf;
                if (act) active++;
                if (act) sb.Append(i).Append('=').Append(inst.countTexts[i] != null ? inst.countTexts[i].text : "?").Append(' ');
            }
            o.AppendLine("  recipe=" + rid + " results=" + recipe.Results.Length + " items=" + recipe.Items.Length
                + " iconsArray=" + inst.icons.Length + " activeIcons=" + active);
            o.AppendLine("  active=" + sb.ToString());
            UnityEngine.Object.Destroy(go);
        }

        /// <summary>直接在屏幕指定位置显示某科技的 tooltip（不走鼠标悬停），用于验证 >6 消耗行布局。</summary>
        private static void TechTipUi(string[] p, StringBuilder o)
        {
            int techId = int.Parse(p[1]);
            float x = p.Length > 2 ? float.Parse(p[2]) : 600f;
            float y = p.Length > 3 ? float.Parse(p[3]) : 760f;
            var tech = LDB.techs.Select(techId);
            if (tech == null) { o.AppendLine("  tech not found: " + techId); return; }
            var root = UIRoot.instance;
            var q = root != null && root.uiGame != null ? root.uiGame.researchQueue : null;
            if (q == null || q.techTip == null) { o.AppendLine("  researchQueue/techTip null"); return; }
            var tip = q.techTip;
            tip.SetTechProto(tech, 0);
            tip.panelRect.SetParent(root.buttonTipTransform, false);
            tip.panelRect.anchoredPosition = new Vector2(x, y);
            tip._Open();
            o.AppendLine("  techtipui tech=" + techId + " items=" + (tech.itemArray == null ? -1 : tech.itemArray.Length)
                + " panelSize=" + tip.panelRect.sizeDelta.ToString("F0")
                + " costGroup=" + (tip.costGroup != null ? tip.costGroup.sizeDelta.ToString("F0") : "null")
                + " costIcons=" + (tip.costIcons == null ? -1 : tip.costIcons.Length));
        }

        private static void TechTipClose(StringBuilder o)
        {
            var root = UIRoot.instance;
            var q = root != null && root.uiGame != null ? root.uiGame.researchQueue : null;
            if (q == null || q.techTip == null) { o.AppendLine("  techTip null"); return; }
            q.techTip._Close();
            o.AppendLine("  techtipclose ok");
        }

        /// <summary>双研究站链式转移测试：lab1 → lab2 传矩阵（含第 7 种）。</summary>
        private static void LabChain(string[] p, StringBuilder o)
        {
            var factory = Factory();
            if (factory == null) { o.AppendLine("  no local factory"); return; }
            int eid1 = int.Parse(p[1]);
            int eid2 = int.Parse(p[2]);
            int a1 = factory.entityPool[eid1].labId;
            int a2 = factory.entityPool[eid2].labId;
            if (a1 <= 0 || a2 <= 0) { o.AppendLine("  need two labs: a1=" + a1 + " a2=" + a2); return; }
            ref LabComponent l1 = ref factory.factorySystem.labPool[a1];
            ref LabComponent l2 = ref factory.factorySystem.labPool[a2];
            l1.SetFunction(true, 0, 1001, factory.entitySignPool);
            l2.SetFunction(true, 0, 1001, factory.entitySignPool);
            l1.nextLabId = a2;
            var ids = LabComponent.matrixIds;
            if (l1.matrixServed == null || l2.matrixServed == null) { o.AppendLine("  matrixServed null"); return; }
            for (int i = 0; i < l1.matrixServed.Length; i++) l1.matrixServed[i] = 7200 + 72000;
            for (int i = 0; i < l1.matrixIncServed.Length; i++) l1.matrixIncServed[i] = 0;
            Array.Clear(l2.matrixServed, 0, l2.matrixServed.Length);
            Array.Clear(l2.matrixIncServed, 0, l2.matrixIncServed.Length);
            for (int i = 0; i < l2.needs.Length && i < ids.Length; i++) l2.needs[i] = ids[i];
            o.AppendLine("  before: lab1=[" + Join(l1.matrixServed) + "] lab2=[" + Join(l2.matrixServed) + "]");
            l1.UpdateOutputToNext(factory.factorySystem.labPool);
            o.AppendLine("  after : lab1=[" + Join(l1.matrixServed) + "] lab2=[" + Join(l2.matrixServed) + "]"
                + " lab2inc=[" + Join(l2.matrixIncServed) + "]");
            if (l1.matrixServed.Length > 6)
                o.AppendLine("  7th matrix: lab1=" + l1.matrixServed[6] + " lab2=" + l2.matrixServed[6]
                    + " (expect lab2 > 0 if transferred)");
        }

        private static void ItemAt(string[] p, StringBuilder o)
        {
            int id = int.Parse(p[1]);
            var proto = LDB.items.Select(id);
            if (proto == null) { o.AppendLine("  item " + id + " not found"); return; }
            o.AppendLine("  item id=" + id + " Name=" + proto.Name + " name=" + proto.name
                + " Type=" + proto.Type + " IsEntity=" + proto.IsEntity + " index=" + proto.index
                + " icon=" + (proto.iconSprite != null));
        }

        private static void ItemScan(string[] p, StringBuilder o)
        {
            int from = int.Parse(p[1]);
            int to = int.Parse(p[2]);
            int n = 0;
            for (int id = from; id <= to; id++)
            {
                var proto = LDB.items.Select(id);
                if (proto == null) continue;
                o.AppendLine("  item " + id + " = " + proto.Name + " type=" + proto.Type);
                if (++n >= 80) break;
            }
            if (n == 0) o.AppendLine("  no items in [" + from + "," + to + "]");
        }

        private static void TechInfo(string[] p, StringBuilder o)
        {
            int id = int.Parse(p[1]);
            var t = LDB.techs.Select(id);
            if (t == null) { o.AppendLine("  tech " + id + " not found"); return; }
            o.AppendLine("  tech id=" + id + " name=" + t.Name + " IsLabTech=" + t.IsLabTech + " Published=" + t.Published
                + " Level=" + t.Level + "/" + t.MaxLevel + " HashNeeded=" + t.HashNeeded);
            o.AppendLine("  Items=[" + Join(t.Items) + "] ItemPoints=[" + Join(t.ItemPoints) + "] PreTechs=[" + Join(t.PreTechs) + "]");
            o.AppendLine("  Position=" + t.Position + " preTechArray=" + (t.preTechArray == null ? -1 : t.preTechArray.Length)
                + " postTechArray=" + (t.postTechArray == null ? -1 : t.postTechArray.Length));
        }

        private static void TechStateCmd(string[] p, StringBuilder o)
        {
            int id = int.Parse(p[1]);
            var history = GameMain.history;
            if (history == null) { o.AppendLine("  history null"); return; }
            TechState ts;
            if (!history.techStates.TryGetValue(id, out ts)) { o.AppendLine("  techState " + id + " missing"); return; }
            o.AppendLine("  techState id=" + id + " unlocked=" + ts.unlocked + " level=" + ts.curLevel + "/" + ts.maxLevel
                + " hash=" + ts.hashUploaded + "/" + ts.hashNeeded + " tick=" + ts.unlockTick);
            o.AppendLine("  currentTech=" + history.currentTech
                + " matrixIds=[" + Join(LabComponent.matrixIds) + "]"
                + " matrixPoints=[" + Join(LabComponent.matrixPoints) + "]");
        }

        private static void SetResearch(string[] p, StringBuilder o)
        {
            int techId = int.Parse(p[1]);
            var history = GameMain.history;
            if (history == null) { o.AppendLine("  history null"); return; }
            // 直接改 currentTech 会被科技队列每 tick 重置；必须走入队 API。
            bool can = history.CanEnqueueTech(techId);
            if (can) history.EnqueueTech(techId);
            TechState ts;
            bool has = history.techStates.TryGetValue(techId, out ts);
            o.AppendLine("  setresearch tech=" + techId + " canEnqueue=" + can + " currentTech=" + history.currentTech
                + " queue0=" + (history.techQueue != null && history.techQueue.Length > 0 ? history.techQueue[0] : -1)
                + " stateExists=" + has + (has ? " hashNeeded=" + ts.hashNeeded : ""));
        }

        private static void SetLabResearch(string[] p, StringBuilder o)
        {
            var factory = Factory();
            if (factory == null) { o.AppendLine("  no local factory"); return; }
            int eid = int.Parse(p[1]);
            int techId = int.Parse(p[2]);
            int labId = (eid > 0 && eid < factory.entityCursor) ? factory.entityPool[eid].labId : 0;
            if (labId <= 0) { o.AppendLine("  not a lab: " + eid); return; }
            ref LabComponent lab = ref factory.factorySystem.labPool[labId];
            lab.SetFunction(true, 0, techId, factory.entitySignPool);
            o.AppendLine("  setlabresearch eid=" + eid + " lab=" + labId + " researchMode=" + lab.researchMode
                + " techId=" + lab.techId + " needsLen=" + (lab.needs == null ? -1 : lab.needs.Length)
                + " matrixServedLen=" + (lab.matrixServed == null ? -1 : lab.matrixServed.Length)
                + " needs=[" + Join(lab.needs) + "]");
        }

        private static void UnlockRecipes(string[] p, StringBuilder o)
        {
            var history = GameMain.history;
            if (history == null) { o.AppendLine("  history null"); return; }
            var field = typeof(GameHistoryData).GetField("recipeUnlocked");
            var set = field != null ? field.GetValue(history) as System.Collections.Generic.HashSet<int> : null;
            if (set == null) { o.AppendLine("  recipeUnlocked not accessible"); return; }
            for (int i = 1; i < p.Length; i++)
            {
                foreach (string part in p[i].Split(','))
                {
                    int id;
                    if (int.TryParse(part.Trim(), out id) && id > 0)
                    {
                        set.Add(id);
                        o.AppendLine("  unlocked recipe " + id + " = " + (set.Contains(id)));
                    }
                }
            }
        }

        private static void TechPos(string[] p, StringBuilder o)
        {
            int id = int.Parse(p[1]);
            var t = LDB.techs.Select(id);
            if (t == null) { o.AppendLine("  tech " + id + " not found"); return; }
            if (p.Length >= 4)
            {
                t.Position = new Vector2(float.Parse(p[2]), float.Parse(p[3]));
            }
            o.AppendLine("  tech " + id + " Position=" + t.Position);
        }

        private static void TechNode(string[] p, StringBuilder o)
        {
            int id = int.Parse(p[1]);
            var root = UIRoot.instance;
            var uiGame = root != null ? root.uiGame : null;
            var tt = uiGame != null ? uiGame.techTree : null;
            if (tt == null) { o.AppendLine("  techTree null"); return; }
            if (tt.nodes == null) { o.AppendLine("  techTree.nodes null"); return; }
            UITechNode node;
            if (!tt.nodes.TryGetValue(id, out node) || node == null) { o.AppendLine("  node missing for tech " + id); return; }
            var rt = node.transform as RectTransform;
            o.AppendLine("  node tech=" + id + " active=" + node.gameObject.activeSelf
                + " parent=" + node.transform.parent.name
                + " pos=" + (rt != null ? rt.anchoredPosition.ToString("F0") : "?")
                + " size=" + (rt != null ? rt.sizeDelta.ToString("F0") : "?"));
            if (p.Length >= 3 && p[2] == "center" && rt != null)
            {
                var parent = node.transform.parent as RectTransform;
                if (parent != null)
                {
                    parent.anchoredPosition = -rt.anchoredPosition;
                    o.AppendLine("  centered parent=" + parent.name + " anchor=" + parent.anchoredPosition.ToString("F0"));
                }
            }
        }

        /// <summary>
        /// M2 端到端验证：循环执行「分拣器取料（PickFrom，用目标 needs）→ 投料（InsertInto）」，
        /// 用于验证 needs 全长后 >6 原料能被持续喂入并完成生产。
        /// </summary>
        private static void AutoFeed(string[] p, StringBuilder o)
        {
            var factory = Factory();
            if (factory == null) { o.AppendLine("  no local factory"); return; }
            int dstEid = int.Parse(p[1]);
            int srcEid = int.Parse(p[2]);
            int times = p.Length > 3 ? int.Parse(p[3]) : 100;
            int aid = factory.entityPool[dstEid].assemblerId;
            if (aid <= 0) { o.AppendLine("  dst not assembler: " + dstEid); return; }
            ref AssemblerComponent a = ref factory.factorySystem.assemblerPool[aid];
            int picked = 0, fed = 0;
            var hist = new System.Collections.Generic.Dictionary<int, int>();
            for (int i = 0; i < times; i++)
            {
                byte stack, inc;
                int item = factory.PickFrom(srcEid, 0, 0, a.needs, out stack, out inc);
                if (item <= 0) break;
                picked++;
                if (!hist.ContainsKey(item)) hist[item] = 0;
                hist[item]++;
                byte remainInc;
                int ins = factory.InsertInto(dstEid, 0, item, (byte)stack, (byte)inc, out remainInc);
                if (ins > 0) fed++;
                if (a.recipeId != 0) a.InternalUpdate(1f, new int[20000], new int[20000]);
            }
            o.AppendLine("  autofeed dst=" + dstEid + " src=" + srcEid + " times=" + times
                + " picked=" + picked + " fed=" + fed);
            o.AppendLine("  pickedByItem=" + JoinDict(hist));
            o.AppendLine("  machine: cycleCount=" + a.cycleCount + " replicating=" + a.replicating
                + " served=[" + Join(a.served) + "] produced=[" + Join(a.produced) + "]");
        }

        private static string JoinDict(System.Collections.Generic.Dictionary<int, int> d)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            bool first = true;
            foreach (var kv in d)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append(kv.Key).Append(':').Append(kv.Value);
            }
            sb.Append('}');
            return sb.ToString();
        }
        private static void SaveGame(string[] p, StringBuilder o)
        {
            string name = p.Length > 1 ? p[1] : "recipeslots-test";
            try
            {
                bool ok = GameSave.SaveCurrentGame(name);
                o.AppendLine("  save '" + name + "' -> " + ok);
            }
            catch (Exception ex) { o.AppendLine("  save exception: " + ex.GetType().Name + " " + ex.Message); }
        }

        private static void LoadGame(string[] p, StringBuilder o)
        {
            string name = p.Length > 1 ? p[1] : "recipeslots-test";
            try
            {
                bool ok = GameSave.LoadCurrentGame(name);
                o.AppendLine("  load '" + name + "' -> " + ok);
            }
            catch (Exception ex) { o.AppendLine("  load exception: " + ex.GetType().Name + " " + ex.Message); }
        }

        private static void RecipeInfo(string[] p, StringBuilder o)
        {
            int rid = int.Parse(p[1]);
            var r = LDB.recipes.Select(rid);
            if (r == null) { o.AppendLine("  recipe not found: " + rid); return; }
            o.AppendLine("  id=" + rid + " name=" + r.name + " type=" + r.Type + " time=" + r.TimeSpend
                + " items=[" + Join(r.Items) + "] counts=[" + Join(r.ItemCounts) + "] results=[" + Join(r.Results) + "] resultCounts=[" + Join(r.ResultCounts) + "]");
            o.AppendLine("  recipeExecuteData=" + (RecipeProto.recipeExecuteData.ContainsKey(rid) ? "ok" : "MISSING")
                + " preTech=" + (r.preTech != null ? r.preTech.ID.ToString() : "null"));
        }

        private static string Join(int[] a)
        {
            if (a == null) return "null";
            var sb = new StringBuilder("[");
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

#else
namespace DSPRecipeSlots
{
    /// <summary>发布版：调试命令通道整体剔除（不创建 _dev 目录、不读文件）。</summary>
    internal static class DevChannel
    {
        internal static void Init(BepInEx.Configuration.ConfigFile config) { }
        internal static void Tick() { }
    }
}
#endif


