using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Locations;

namespace LifeWithWifeAndChild.Core
{
    /// <summary>
    /// 找孩子的唯一入口。
    /// 注意：<c>Game1.player.getChildren()</c> 取的是 FarmHouse.characters，
    /// 孩子出门上学时不在那个集合里，所以这里必须扫全地图（TECH-DESIGN §7.2 去重的前提）。
    /// </summary>
    internal static class ChildRegistry
    {
        internal static List<Child> All()
        {
            var children = new List<Child>();

            try
            {
                AddFrom(Game1.getLocationFromName(Constants.HomeLocation), children);

                var locations = Game1.locations;
                if (locations != null)
                {
                    foreach (var location in locations)
                        AddFrom(location, children);
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("ChildRegistry.All failed.", ex);
            }

            return children;
        }

        private static void AddFrom(GameLocation? location, List<Child> children)
        {
            if (location == null) return;

            try
            {
                foreach (var npc in location.characters)
                {
                    if (npc is Child child && !children.Contains(child))
                        children.Add(child);
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("ChildRegistry.AddFrom('" + location.NameOrUniqueName + "') failed.", ex);
            }
        }

        /// <summary>孩子所属的农舍（找不到就退回 FarmHouse）。</summary>
        internal static FarmHouse? HomeOf(Child child)
        {
            try
            {
                var owner = Game1.GetPlayer(child.idOfParent.Value) ?? Game1.player;
                if (owner != null)
                {
                    var home = Utility.getHomeOfFarmer(owner);
                    if (home != null) return home;
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("HomeOf failed, falling back to FarmHouse.", ex);
            }

            return Game1.getLocationFromName(Constants.HomeLocation) as FarmHouse;
        }

        /// <summary>孩子当前是否在农舍里（判定"在家/在外"的唯一物理依据）。</summary>
        internal static bool IsAtHome(Child child)
        {
            return child?.currentLocation is FarmHouse;
        }

        /// <summary>
        /// 找出所有地图里的这个孩子实例（正常情况下应当只有一个）。
        /// 用于读档后去重（DESIGN.md §8 的 B 方案风险）。
        /// </summary>
        internal static List<GameLocation> LocateAll(Child child)
        {
            var found = new List<GameLocation>();

            try
            {
                var locations = Game1.locations;
                if (locations == null) return found;

                foreach (var location in locations)
                {
                    if (location?.characters == null) continue;
                    foreach (var npc in location.characters)
                    {
                        if (ReferenceEquals(npc, child))
                        {
                            found.Add(location);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("ChildRegistry.LocateAll failed.", ex);
            }

            return found;
        }

        /// <summary>调试/日志用：孩子当前所在图名。</summary>
        internal static string DescribeLocation(Child child)
        {
            try
            {
                if (child?.currentLocation == null) return "<null>";
                return child.currentLocation.NameOrUniqueName;
            }
            catch
            {
                return "<error>";
            }
        }
    }
}
