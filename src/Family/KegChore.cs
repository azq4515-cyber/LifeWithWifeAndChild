using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Objects;
using StardewValley.Pathfinding;
using LifeWithWifeAndChild.Commute;
using LifeWithWifeAndChild.Core;

namespace LifeWithWifeAndChild.Family
{
    /// <summary>
    /// 周末收桶家务（DESIGN.md §6.2，取代原"伴侣周一代收"）。
    ///
    /// 要求是"要有动作"，所以不是批量搬运，而是：**走到桶边 → 播动作 → 搬进家庭箱 → 下一个**。
    /// 状态全部写进 modData，中途存档/退出不会把状态搞坏。
    ///
    /// 两个刻意的限制：
    /// 1. 只处理 **农场** 上的桶（棚屋/姜岛一律不碰）——"看得见"是这个功能的前提；
    /// 2. 玩家不在农场时 **暂停**（非当前地图的 NPC 不更新，静默完成等于骗人）。
    /// </summary>
    internal static class KegChore
    {
        private const string StateIdle = "idle";
        private const string StateWalking = "walking";
        private const string StateDone = "done";

        private const int WalkTimeoutTicks = 3;
        private const int WorkEndMinute = 1700;

        internal static void OnTimeChanged(int timeOfDay)
        {
            if (!Context.IsMainPlayer) return;
            if (!ModEntry.Config.EnableWeekendKegChore) return;
            if (!IsWeekend()) return;
            if (Game1.isFestival()) return;
            if (timeOfDay < ModEntry.Config.KegChoreStartHour * 100) return;

            var farm = Game1.getFarm();
            if (farm == null) return;

            foreach (var child in ChildRegistry.All())
            {
                try
                {
                    var state = ChildState.Get(child);

                    if (!state.IsManaged) continue;
                    if (state.Stage < Stage.Teen) continue;
                    if (state.LessonLocation.Length > 0) continue; // 今天要上课（周末不会，防御）
                    if (state.ChoreState == StateDone) continue;

                    EnsureDay(state);

                    if (timeOfDay >= WorkEndMinute)
                    {
                        Finish(child, state, "hud.chore.done_time");
                        continue;
                    }

                    // 玩家不在农场 → 暂停，等玩家回来继续
                    if (!ReferenceEquals(Game1.currentLocation, farm)) continue;

                    EnsureOnFarm(child, state, farm);

                    if (!ReferenceEquals(child.currentLocation, farm)) continue;

                    Tick(child, state, farm);
                }
                catch (Exception ex)
                {
                    ModLog.Error("KegChore.OnTimeChanged failed for '" + child?.Name + "'.", ex);
                }
            }
        }

        /// <summary>调试指令：立刻跑一轮（不受周末/时段限制）。</summary>
        internal static string ForceOnce()
        {
            var farm = Game1.getFarm();
            if (farm == null) return "农场不可用。";

            var reported = new List<string>();

            foreach (var child in ChildRegistry.All())
            {
                var state = ChildState.Get(child);
                if (!state.IsManaged || state.Stage < Stage.Teen) continue;

                EnsureDay(state);
                state.ChoreState = StateIdle;

                if (!ReferenceEquals(child.currentLocation, farm))
                    EnsureOnFarm(child, state, farm);

                for (var i = 0; i < 3; i++) Tick(child, state, farm);

                reported.Add($"'{child.Name}' 状态={state.ChoreState} 已收={state.ChoreCount}");
            }

            if (reported.Count == 0) return "没有阶段 5 以上的孩子。";
            return string.Join("；", reported);
        }

        // ---- 状态 ----

        private static bool IsWeekend()
        {
            var day = Game1.Date.DayOfWeek;
            return day == DayOfWeek.Saturday || day == DayOfWeek.Sunday;
        }

        private static void EnsureDay(ChildState state)
        {
            var today = Game1.Date.TotalDays;
            if (state.ChoreDay == today) return;

            state.ChoreDay = today;
            state.ChoreState = StateIdle;
            state.ChoreTarget = Vector2.Zero;
            state.ChoreCount = 0;
            state.ChoreWalkTicks = 0;
            state.SetChoreSkipped(Array.Empty<string>());
        }

        /// <summary>孩子还在屋里就先出门（门廊在农场地图上）。</summary>
        private static void EnsureOnFarm(Child child, ChildState state, Farm farm)
        {
            try
            {
                if (ReferenceEquals(child.currentLocation, farm)) return;
                if (!ChildRegistry.IsAtHome(child)) return;

                var home = ChildRegistry.HomeOf(child);
                if (home == null) return;

                var porch = home.getPorchStandingSpot();
                if (porch == Point.Zero) return;

                var tile = Utility.PointToVector2(porch);
                child.controller = null;
                child.temporaryController = null;
                Game1.warpCharacter(child, farm, tile);
            }
            catch (Exception ex)
            {
                ModLog.Error("KegChore.EnsureOnFarm failed.", ex);
            }
        }

        // ---- 主循环 ----

        private static void Tick(Child child, ChildState state, Farm farm)
        {
            var chest = FamilyChest.Find();

            if (chest == null)
            {
                if (state.ChoreState != StateDone)
                    Game1.addHUDMessage(new HUDMessage(I18n.Get("hud.chore.no_chest"), HUDMessage.error_type));

                state.ChoreState = StateDone;
                return;
            }

            var limit = Math.Max(1, ModEntry.Config.MaxKegsPerDay);
            if (state.ChoreCount >= limit)
            {
                Finish(child, state, "hud.chore.done_limit");
                return;
            }

            if (state.ChoreState == StateWalking)
            {
                Step(child, state, farm, chest);
                return;
            }

            // idle：挑下一个桶
            var kegTile = FindNextKeg(farm, state, child.Tile);
            if (!kegTile.HasValue)
            {
                Finish(child, state, "hud.chore.done");
                return;
            }

            var stand = FindStandTile(farm, kegTile.Value, child.Tile, state, child);
            if (!stand.HasValue)
            {
                Skip(state, kegTile.Value);
                return;
            }

            state.ChoreTarget = stand.Value;
            state.ChoreWalkTicks = 0;

            var controller = new PathFindController(child, farm, new Point((int)stand.Value.X, (int)stand.Value.Y), 2);

            if (controller.pathToEndPoint == null || controller.pathToEndPoint.Count == 0)
            {
                ModLog.Trace($"chore: '{child.Name}' 走不到 {stand.Value}，跳过这个桶 {kegTile.Value}。");
                Skip(state, kegTile.Value);
                return;
            }

            child.controller = controller;
            state.ChoreState = StateWalking;
        }

        private static void Step(Child child, ChildState state, Farm farm, Chest chest)
        {
            var target = state.ChoreTarget;
            if (target == Vector2.Zero)
            {
                state.ChoreState = StateIdle;
                return;
            }

            var targetPoint = new Point((int)target.X, (int)target.Y);

            if (child.TilePoint == targetPoint)
            {
                Harvest(child, state, farm, chest, target);
                state.ChoreTarget = Vector2.Zero;
                state.ChoreState = StateIdle;
                return;
            }

            if (child.controller != null)
            {
                state.ChoreWalkTicks = state.ChoreWalkTicks + 1;

                if (state.ChoreWalkTicks <= WalkTimeoutTicks) return;

                ModLog.Trace($"chore: '{child.Name}' 走 {target} 超时，跳过这个桶。");
                Skip(state, target);
                return;
            }

            // 控制器没了但没到目标：放弃这一个
            Skip(state, target);
        }

        private static void Harvest(Child child, ChildState state, Farm farm, Chest chest, Vector2 stand)
        {
            try
            {
                var keg = FindKegBeside(farm, stand);
                if (keg == null) return;

                if (!farm.objects.TryGetValue(keg.Value, out var obj) || obj == null) return;
                if (obj.QualifiedItemId != KegCollector.KegQualifiedId) return;
                if (!obj.readyForHarvest.Value) return;

                var held = obj.heldObject.Value;
                if (held == null) return;

                if (!FamilyChest.TryAdd(chest, held))
                {
                    Game1.addHUDMessage(new HUDMessage(I18n.Get("hud.chore.full", new { child = child.Name }), HUDMessage.error_type));
                    state.ChoreState = StateDone;
                    return;
                }

                obj.heldObject.Value = null;
                obj.readyForHarvest.Value = false;
                obj.MinutesUntilReady = 0;

                state.ChoreCount = state.ChoreCount + 1;
                PlayHarvestAction(child);

                ModLog.Trace($"chore: '{child.Name}' 收了 {keg.Value}（今日第 {state.ChoreCount} 个）。");
            }
            catch (Exception ex)
            {
                ModLog.Error("KegChore.Harvest failed.", ex);
            }
        }

        private static void PlayHarvestAction(Child child)
        {
            try
            {
                child.faceDirection(2);

                // 原地做一小段"搬运"动画（儿童精灵表没有专门动作，用行走帧近似）
                child.Sprite.setCurrentAnimation(new List<FarmerSprite.AnimationFrame>
                {
                    new FarmerSprite.AnimationFrame(0, 110),
                    new FarmerSprite.AnimationFrame(1, 110),
                    new FarmerSprite.AnimationFrame(2, 110),
                    new FarmerSprite.AnimationFrame(1, 110),
                });
                child.Sprite.loop = false;

                child.doEmote(20);
                Game1.playSound("coin");
            }
            catch (Exception ex)
            {
                ModLog.Error("KegChore.PlayHarvestAction failed.", ex);
            }
        }

        private static void Finish(Child child, ChildState state, string messageKey)
        {
            try
            {
                if (state.ChoreState == StateDone) return;

                state.ChoreState = StateDone;

                var message = state.ChoreCount > 0
                    ? I18n.Get(messageKey, new { child = child.Name, count = state.ChoreCount })
                    : I18n.Get("hud.chore.nothing", new { child = child.Name });

                Game1.addHUDMessage(new HUDMessage(message, state.ChoreCount > 0 ? HUDMessage.newQuest_type : HUDMessage.error_type));
                ModLog.Info($"chore: '{child.Name}' 收工，共 {state.ChoreCount} 个桶。");
            }
            catch (Exception ex)
            {
                ModLog.Error("KegChore.Finish failed.", ex);
            }
        }

        // ---- 查找 ----

        private static Vector2? FindNextKeg(Farm farm, ChildState state, Vector2 from)
        {
            var skipped = state.ChoreSkipped;
            Vector2? best = null;
            var bestDistance = float.MaxValue;

            foreach (var pair in farm.objects.Pairs)
            {
                var obj = pair.Value;
                if (obj == null) continue;
                if (obj.QualifiedItemId != KegCollector.KegQualifiedId) continue;
                if (!obj.readyForHarvest.Value) continue;
                if (obj.heldObject.Value == null) continue;

                var key = (int)pair.Key.X + "," + (int)pair.Key.Y;
                if (skipped.Contains(key)) continue;

                var distance = Vector2.Distance(pair.Key, from);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = pair.Key;
            }

            return best;
        }

        private static Vector2? FindStandTile(Farm farm, Vector2 kegTile, Vector2 from, ChildState state, Child child)
        {
            Vector2? best = null;
            var bestDistance = float.MaxValue;

            var offsets = new[]
            {
                new Point(0, -1), new Point(0, 1), new Point(-1, 0), new Point(1, 0),
            };

            foreach (var offset in offsets)
            {
                var tile = new Vector2(kegTile.X + offset.X, kegTile.Y + offset.Y);

                if (!farm.isTileOnMap(tile)) continue;
                if (farm.objects.ContainsKey(tile)) continue;
                if (!farm.isTilePassable(tile)) continue;

                var occupied = false;
                foreach (var npc in farm.characters)
                {
                    if (npc == null || ReferenceEquals(npc, child)) continue;
                    if (npc.TilePoint.X == (int)tile.X && npc.TilePoint.Y == (int)tile.Y) { occupied = true; break; }
                }
                if (occupied) continue;

                var distance = Vector2.Distance(tile, from);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = tile;
            }

            return best;
        }

        private static Vector2? FindKegBeside(GameLocation farm, Vector2 stand)
        {
            var offsets = new[]
            {
                new Point(0, -1), new Point(0, 1), new Point(-1, 0), new Point(1, 0),
            };

            foreach (var offset in offsets)
            {
                var tile = new Vector2(stand.X + offset.X, stand.Y + offset.Y);
                if (!farm.objects.TryGetValue(tile, out var obj) || obj == null) continue;
                if (obj.QualifiedItemId != KegCollector.KegQualifiedId) continue;
                return tile;
            }

            return null;
        }

        private static void Skip(ChildState state, Vector2 tile)
        {
            var skipped = state.ChoreSkipped;
            var key = (int)tile.X + "," + (int)tile.Y;
            if (skipped.Contains(key)) return;

            skipped.Add(key);
            state.SetChoreSkipped(skipped);

            state.ChoreState = StateIdle;
            state.ChoreTarget = Vector2.Zero;
            state.ChoreWalkTicks = 0;
        }
    }
}
