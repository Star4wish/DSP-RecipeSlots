using System;
using HarmonyLib;

namespace DSPRecipeSlots
{
    /// <summary>
    /// 兼容性防御：`GameLogic.Update` 第一行是 `mainPlayer.packageUtility.Count()`。
    /// 在某些 MOD 的读档/开档流程中（日志里可见 [DSP-MechDawn] 自动载入存档），
    /// `GameMain.mainPlayer` 尚未创建时 `GameLogic.Update` 就已经被调了一帧，
    /// 于是抛出 `NullReferenceException ... GameLogic.Update (IL_0006)`，游戏弹出红色错误报告。
    ///
    /// 这里只做“主玩家未就绪时跳过本帧”的保护（data/mainPlayer/packageUtility 任一为空即跳过），
    /// 正常帧行为完全不变，也不修改任何游戏状态。
    /// </summary>
    [HarmonyPatch(typeof(GameLogic), nameof(GameLogic.Update))]
    [HarmonyPriority(Priority.First)]
    internal static class GameLogicUpdateGuardPatch
    {
        private static int _skipped;

        [HarmonyPrefix]
        private static bool Prefix(GameLogic __instance)
        {
            try
            {
                if (__instance == null) return Skip("GameLogic null");
                var data = __instance.data;
                if (data == null) return Skip("data null");
                var player = data.mainPlayer;
                if (player == null) return Skip("mainPlayer null");
                if (player.packageUtility == null) return Skip("packageUtility null");
                return true;
            }
            catch
            {
                // 任何异常都不让主循环继续崩：本帧跳过即可
                return Skip("guard exception");
            }
        }

        private static bool Skip(string reason)
        {
            if (_skipped < 3) Plugin.Log.LogInfo("[guard] skip GameLogic.Update frame: " + reason);
            _skipped++;
            return false;
        }
    }
}
