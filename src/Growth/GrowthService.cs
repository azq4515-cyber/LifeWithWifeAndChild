using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using LifeWithWifeAndChild.Core;
using LifeWithWifeAndChild.Spouse;

namespace LifeWithWifeAndChild.Growth
{
    /// <summary>
    /// 成长状态机（TECH-DESIGN §5.2 / §6.3）：
    /// 推进只在 <c>DayEnding</c> 发生（"睡觉结算"），用 lwwc/lastGrowthDay 做幂等。
    /// </summary>
    internal static class GrowthService
    {
        // ---- DayStarted ----
        internal static void OnDayStarted()
        {
            if (!Context.IsMainPlayer) return;

            try
            {
                SaveMigration.EnsureAll();

                foreach (var child in ChildRegistry.All())
                {
                    var state = ChildState.Get(child);
                    state.WorkDoneToday = false;

                    state.ApplyVanillaAge();
                    Commute.CommuteService.OnDayStarted(child, state);
                    StageScenes.OnDayStarted(child, state);
                }

                Family.FamilyChestService.OnDayStarted();
            }
            catch (Exception ex)
            {
                ModLog.Error("GrowthService.OnDayStarted failed.", ex);
            }
        }

        // ---- DayEnding ----
        internal static void OnDayEnding()
        {
            if (!Context.IsMainPlayer) return;

            try
            {
                var today = Game1.Date.TotalDays;

                foreach (var child in ChildRegistry.All())
                {
                    var state = ChildState.Get(child);

                    // 存档前必须让孩子回家：原版 dayUpdate 与存档归属都假设孩子在家
                    Commute.CommuteService.ForceReturnHome(child, "DayEnding 归位");
                    Commute.CommuteService.ClearSchedule(child);

                    if (!state.IsManaged) continue;
                    if (state.LastGrowthDay == today) continue; // 幂等

                    state.LastGrowthDay = today;

                    if (state.Stage >= Stage.Adult) continue;

                    var speed = Math.Max(1, ModEntry.Config.GrowthSpeed);
                    state.DaysInStage = state.DaysInStage + 1;

                    if (state.DaysInStage >= speed)
                    {
                        state.DaysInStage = 0;
                        Advance(child, state, state.Stage + 1);
                    }
                    else
                    {
                        ModLog.Trace($"growth: '{child.Name}' stage={(int)state.Stage} daysInStage={state.DaysInStage}/{speed}.");
                    }
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("GrowthService.OnDayEnding failed.", ex);
            }
        }

        /// <summary>推进一个孩子到指定阶段（调试指令也走这里）。</summary>
        internal static void Advance(Child child, ChildState state, Stage next)
        {
            next = StageExtensions.Clamp((int)next);
            var from = state.Stage;
            if (next == from) return;

            // 写前校验：孩子还在、有农场主、不在 cutscene
            if (child == null || !ChildRegistry.All().Contains(child))
            {
                ModLog.Warn("growth: child vanished before stage change; skipping.");
                return;
            }

            state.Stage = next;
            state.ApplyVanillaAge();

            var flag = StageTable.StageMailFlag(next);
            try
            {
                if (!Game1.player.mailReceived.Contains(flag))
                    Game1.player.mailReceived.Add(flag);
            }
            catch (Exception ex)
            {
                ModLog.Error("growth: failed to write mail flag " + flag + ".", ex);
            }

            var scene = StageTable.StageSceneId(next);
            if (scene != null) state.QueueEvent(scene);

            ModLog.Info($"growth: '{child.Name}' {(int)from}({I18n.Get(from.NameKey())}) -> {(int)next}({I18n.Get(next.NameKey())}).");

            try
            {
                if (Context.IsMainPlayer)
                {
                    Game1.addHUDMessage(new HUDMessage(
                        I18n.Get("hud.stage_up", new { child = child.Name, stage = I18n.Get(next.NameKey()) }),
                        HUDMessage.newQuest_type));
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("growth: stage-up HUD failed.", ex);
            }
        }

        // ---- 调试：强制设置阶段 ----
        internal static void SetStage(Child child, Stage stage)
        {
            var state = ChildState.Get(child);
            stage = StageExtensions.Clamp((int)stage);

            state.ForceStage(stage);
            state.DaysInStage = 0;
            state.ApplyVanillaAge();

            ModLog.Info($"debug: '{child.Name}' forced to stage {(int)stage}.");
            Commute.CommuteService.RefreshToday(child);
        }

        internal static List<string> Describe()
        {
            return SaveMigration.Describe();
        }
    }
}
