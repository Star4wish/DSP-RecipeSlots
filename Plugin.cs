using System;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace DSPRecipeSlots
{
    /// <summary>
    /// DSP-RecipeSlots · 配方原料/产物突破 6 种的专项补丁（PoC 阶段）。
    ///
    /// 当前阶段（M1 / 路线 A）：
    ///  1. AssemblerComponent.UpdateNeeds：对 requires.Length &gt; 6 的配方做「需求轮转」，
    ///     每 tick 只广告 6 种原料，保证第 7+ 种也能被分拣器按 needs 抓取；
    ///  2. PlanetFactory.InsertInto(int/uint)：装配器分支改为按 requires.Length 循环受理，
    ///     让第 7+ 种原料能被真正收下（原版第 7 项直接 return 0）；
    ///  3. PowerSystem.GameTick 后置：测试模式给「跑测试配方的装配器」所在电网灌 1.0 供电，
    ///     避免测试台因为没拉电线而空转（仅 dev 开关，默认开）。
    ///
    /// 配方注册不在本补丁核心内：正式内容 MOD 仍走 LDBTool/CommonAPI 注册；
    /// 这里注册一条 7 输入测试配方用于实机验证（Test.EnableTestRecipe）。
    /// </summary>
    [BepInPlugin(GUID, NAME, VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "star4wish.dsp.recipeslots";
        public const string NAME = "DSP-RecipeSlots";
        public const string VERSION = "1.2.1";

        public static Plugin Instance;
        public static ManualLogSource Log;
        public static Harmony Harmony;

        /// <summary>是否注册 7 输入测试配方（仅开发验证用）。</summary>
#if !SHIP
        public static ConfigEntry<bool> EnableTestRecipe;

        /// <summary>测试配方 ID（必须 &lt;= 32767，否则装配器存档的 recipeId short 会写坏）。</summary>
        public static ConfigEntry<int> TestRecipeId;

        /// <summary>测试用供电注入：给测试配方所在电网灌 1.0 供电（dev only）。</summary>
        public static ConfigEntry<bool> EnableTestPowerHack;
#endif

        /// <summary>是否打印每次补丁命中的诊断日志（调试期开，正常关）。</summary>
        public static ConfigEntry<bool> Verbose;

        /// <summary>
        /// 额外矩阵物品 ID（逗号分隔，必须紧接 6006 连续，如 "6007" / "6007,6008"）。
        /// 用于研究站/科技 &gt;6 种消耗：会扩展 LabComponent/TechProto.matrixIds 与 matrixPoints。
        /// 正式 MOD 需先注册对应物品。
        /// </summary>
        public static ConfigEntry<string> LabExtraMatrixIds;

        /// <summary>是否启用全部玩法补丁（关掉后只剩调试通道，用于兼容性 A/B 归因）。</summary>
        public static ConfigEntry<bool> EnablePatches;
        public static ConfigEntry<bool> EnableNeedsLoop;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            EnablePatches = Config.Bind("Core", "EnablePatches", true,
                "启用全部玩法补丁。false 时只保留调试通道，用于排查兼容性问题（区分「是不是本插件导致」）。");
            EnableNeedsLoop = Config.Bind("Core", "EnableNeedsLoop", true,
                "M2：needs 按配方长度全长填充并把取料匹配改为循环（去掉轮转占空比损失）。关闭则回退到 6 格轮转。");
#if !SHIP
            EnableTestRecipe = Config.Bind("Test", "EnableTestRecipe", false,
                "注册一条 7 输入测试配方（仅开发验证用，正式发布置 false）。");
            TestRecipeId = Config.Bind("Test", "TestRecipeId", 7000,
                "测试配方 ID。必须 1..11999（<12000：IconSet 的 recipeIconIndex/itemIconIndex 数组定长 12000；且装配器存档 recipeId 用 short）。");
            EnableTestPowerHack = Config.Bind("Test", "EnableTestPowerHack", false,
                "测试用：给跑测试配方的装配器所在电网强制 1.0 供电，方便无电线测试。正式发布置 false。");
#endif
            Verbose = Config.Bind("Debug", "Verbose", false,
                "打印补丁命中诊断日志。");
            LabExtraMatrixIds = Config.Bind("Lab", "ExtraMatrixIds", "",
                "额外矩阵物品 ID（逗号分隔，必须紧接 6006 连续，如 6007）。用于研究站/科技 >6 种消耗；留空=不扩展。");

            Harmony = new Harmony(GUID);
            Log.LogInfo("[boot] awake, EnablePatches=" + EnablePatches.Value + " EnableNeedsLoop=" + EnableNeedsLoop.Value);
            if (EnablePatches.Value)
            {
                try
                {
                    // PatchAll() 只扫本程序集，Patches 里的嵌套补丁类与 TestContent 都会被处理
                    Harmony.PatchAll();
                    Log.LogInfo("[boot] PatchAll ok, patched=" + Harmony.GetPatchedMethods().Count());
                }
                catch (Exception ex)
                {
                    Log.LogError("[boot] PatchAll FAILED: " + ex);
                }
            }
            else
            {
                Log.LogWarning("Core.EnablePatches=false，全部玩法补丁已跳过（仅调试通道可用）");
            }

            DevChannel.Init(Config);
            // 无人值守自动化测试必需：失焦时游戏主循环继续推进
            UnityEngine.Application.runInBackground = true;

            Log.LogInfo(NAME + " " + VERSION + " loaded (gameTick hooks armed)");
        }

        private void Update()
        {
            DevChannel.Tick();
        }
    }
}




