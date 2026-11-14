using System;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Objects;
using LifeWithWifeAndChild.Core;
using SObject = StardewValley.Object;

namespace LifeWithWifeAndChild.Commands
{
    /// <summary>
    /// 测试用场景搭建指令：把"要跑 42 天才能到达的条件"压缩成一条命令。
    ///
    /// 这些指令**只用于测试**，全部挂在 <c>EnableDebugCommands</c> 开关下。
    /// 它们不需要游戏内时间流逝，也不会写存档以外的任何状态。
    /// </summary>
    internal static class TestSetupCommands
    {
        internal static void Register(IModHelper helper)
        {
            helper.ConsoleCommands.Add(
                "lwwc_baby",
                "测试用：在农舍里生成一个阶段 0 的孩子。lwwc_baby [名字] [female]",
                (_, args) => Run(() => CreateBaby(args)));

            helper.ConsoleCommands.Add(
                "lwwc_hearts",
                "测试用：把配偶好感设为指定心数（0-14）。lwwc_hearts <n>",
                (_, args) => Run(() => SetHearts(args)));

            helper.ConsoleCommands.Add(
                "lwwc_day",
                "测试用：把日期改成当月某一天（1-28），用于快速到周一。lwwc_day <n>",
                (_, args) => Run(() => SetDay(args)));

            helper.ConsoleCommands.Add(
                "lwwc_kegs",
                "测试用：在玩家周围生成若干个「已成熟、装着酒」的酿酒桶。lwwc_kegs <n>",
                (_, args) => Run(() => SpawnKegs(args)));
        }

        private static void Run(Action action)
        {
            if (!Context.IsWorldReady)
            {
                ModLog.Info("世界还没加载，这个指令需要先进入存档。");
                return;
            }

            try
            {
                action();
            }
            catch (Exception ex)
            {
                ModLog.Error("test setup command failed.", ex);
                ModLog.Info("指令执行失败：" + ex.Message);
            }
        }

        // ---- lwwc_baby ----
        private static void CreateBaby(string[] args)
        {
            var home = Utility.getHomeOfFarmer(Game1.player);
            if (home == null)
            {
                ModLog.Info("找不到玩家的农舍（需要房子至少升级到有婴儿床）。");
                return;
            }

            var name = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]) ? args[0].Trim() : "Baby";
            var female = args.Length > 1 && string.Equals(args[1], "female", StringComparison.OrdinalIgnoreCase);

            // 名字本身也是被测对象：非法/重名的名字要能复现"上学被禁用"的提示
            if (!NameKey.IsUsable(name, out var reason))
            {
                ModLog.Info($"这个名字不能用作资产键（{reason}），换一个再试。");
                return;
            }

            var child = new Child(name, !female, false, Game1.player);

            var spot = home.GetChildBedSpot(home.getChildrenCount());
            var tile = spot != Point.Zero ? Utility.PointToVector2(spot) : new Vector2(31f, 14f);

            child.Position = tile * 64f;
            child.DefaultMap = Constants.HomeLocation;
            child.DefaultPosition = child.Position;

            home.addCharacter(child);
            child.currentLocation = home; // addCharacter 只进集合，不设 currentLocation

            var state = ChildState.Get(child);
            state.ForceStage(Stage.Baby);
            state.MarkMigrated();
            state.ApplyVanillaAge();

            ModLog.Info($"已在 {home.NameOrUniqueName} 生成孩子 '{name}'（阶段 0）。用 lwwc_info 查看，lwwc_stage 4 可以直接跳到学龄。");
        }

        // ---- lwwc_hearts ----
        private static void SetHearts(string[] args)
        {
            var spouse = Game1.player.spouse;

            if (string.IsNullOrWhiteSpace(spouse))
            {
                ModLog.Info("玩家还没结婚，没有配偶好感可设。");
                return;
            }

            if (args.Length < 1 || !int.TryParse(args[0], out var hearts))
            {
                ModLog.Info("用法: lwwc_hearts <0-14>");
                return;
            }

            hearts = Math.Max(0, Math.Min(14, hearts));

            if (!Game1.player.friendshipData.TryGetValue(spouse, out var friendship) || friendship == null)
            {
                ModLog.Info($"找不到与 {spouse} 的好感数据。");
                return;
            }

            friendship.Points = hearts * 250;
            ModLog.Info($"已把与 {spouse} 的好感设为 {hearts} 心（{friendship.Points} 点）。");
        }

        // ---- lwwc_day ----
        private static void SetDay(string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out var day))
            {
                ModLog.Info("用法: lwwc_day <1-28>");
                return;
            }

            day = Math.Max(1, Math.Min(28, day));

            Game1.dayOfMonth = day;
            Game1.Date.DayOfMonth = day;

            ModLog.Info($"已把日期改为 {Game1.currentSeason} {Game1.dayOfMonth}（{Game1.Date.DayOfWeek}）。注意：这是调试用的日期跳变，节日判断可能不同步。");
        }

        // ---- lwwc_kegs ----
        private static void SpawnKegs(string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out var count))
            {
                ModLog.Info("用法: lwwc_kegs <n>");
                return;
            }

            count = Math.Max(1, Math.Min(30, count));

            var location = Game1.player.currentLocation;
            if (location == null)
            {
                ModLog.Info("当前地图不可用。");
                return;
            }

            var origin = Game1.player.Tile;
            var placed = 0;

            for (var dx = -4; dx <= 4 && placed < count; dx++)
            {
                for (var dy = -4; dy <= 4 && placed < count; dy++)
                {
                    var tile = new Vector2((int)origin.X + dx, (int)origin.Y + dy);

                    if (!location.isTileOnMap(tile)) continue;
                    if (location.objects.ContainsKey(tile)) continue;
                    if (location.terrainFeatures.ContainsKey(tile)) continue;

                    try
                    {
                        var keg = ItemRegistry.Create<SObject>("(BC)12");
                        keg.TileLocation = tile;

                        // 装一瓶「已成熟」的酒：这正是代收要处理的状态
                        keg.heldObject.Value = ItemRegistry.Create<SObject>("(O)348");
                        keg.readyForHarvest.Value = true;
                        keg.MinutesUntilReady = 0;

                        location.objects.Add(tile, keg);
                        placed++;
                    }
                    catch (Exception ex)
                    {
                        ModLog.Error("生成 Keg 失败 @" + tile + ".", ex);
                        break;
                    }
                }
            }

            ModLog.Info($"已在 {location.NameOrUniqueName} 生成 {placed} 个成熟 Keg。用 lwwc_chest 放家庭箱，再 lwwc_keg 代收。");
        }
    }
}
