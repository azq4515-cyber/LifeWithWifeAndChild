using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Locations;
using LifeWithWifeAndChild.Core;

namespace LifeWithWifeAndChild.Commute
{
    /// <summary>
    /// 学堂 / 出门相关的纯查询工具：课堂在哪、孩子坐哪、回家落哪。
    /// 不修改任何状态，方便被日程、搬迁、调试指令共用。
    /// </summary>
    internal static class SchoolLocator
    {
        /// <summary>
        /// 当前课堂所在图：社区中心修好后搬到社区中心，否则用博物馆/图书馆。
        /// 返回 null 表示今天没有可用课堂。
        /// </summary>
        internal static string? Current()
        {
            try
            {
                if (Game1.isLocationAccessible(Constants.SchoolLocationCommunity))
                {
                    var cc = Game1.getLocationFromName(Constants.SchoolLocationCommunity);
                    if (cc != null) return cc.NameOrUniqueName;
                }

                var museum = Game1.getLocationFromName(Constants.SchoolLocationVanilla);
                if (museum != null) return museum.NameOrUniqueName;
            }
            catch (Exception ex)
            {
                ModLog.Error("SchoolLocator.Current failed.", ex);
            }

            return null;
        }

        /// <summary>在目标图里找一个孩子能站住的格子（找不到返回 Vector2.Zero）。</summary>
        internal static Vector2 FindOpenTile(Character character, GameLocation location)
        {
            return FindOpenTile(character, location, null);
        }

        /// <summary>同上，但可以指定搜索起点（用于"老师站在孩子旁边"这类需求）。</summary>
        internal static Vector2 FindOpenTile(Character character, GameLocation location, Vector2? preferredStart)
        {
            if (location == null) return Vector2.Zero;

            try
            {
                // 依次尝试多个起点：原版 BFS 只在起点周围能走通时才有解，
                // 起点落在墙上/地图外时它会直接返回 Zero。
                foreach (var start in CandidateStarts(location, preferredStart))
                {
                    var tile = Utility.recursiveFindOpenTileForCharacter(character, location, start, 300, false);
                    if (tile != Vector2.Zero) return tile;
                }

                // 兜底：整图扫描。比"今天就上不了学"好。
                return ScanForStandableTile(location);
            }
            catch (Exception ex)
            {
                ModLog.Error("SchoolLocator.FindOpenTile('" + location.NameOrUniqueName + "') failed.", ex);
                return Vector2.Zero;
            }
        }

        /// <summary>
        /// 候选起点。注意 warp 的两个坐标含义不同：
        /// <c>X/Y</c> 是**本地图**上的门，<c>TargetX/TargetY</c> 是**目标地图**的落点。
        /// 之前错用了 TargetX/TargetY，导致在博物馆里从镇上坐标开始搜，必然搜不到。
        /// </summary>
        private static List<Vector2> CandidateStarts(GameLocation location, Vector2? preferredStart)
        {
            var starts = new List<Vector2>();

            if (preferredStart.HasValue && preferredStart.Value != Vector2.Zero)
                starts.Add(preferredStart.Value);

            try
            {
                if (location.warps != null)
                {
                    foreach (var warp in location.warps)
                    {
                        var tile = new Vector2(warp.X, warp.Y);
                        if (!starts.Contains(tile)) starts.Add(tile);
                    }
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("SchoolLocator.CandidateStarts(warps) failed.", ex);
            }

            try
            {
                starts.Add(new Vector2(location.map.Layers[0].LayerWidth / 2, location.map.Layers[0].LayerHeight / 2));
                starts.Add(new Vector2(1, 1));
            }
            catch
            {
                // 地图没加载好就只用已有候选
            }

            return starts;
        }

        private static Vector2 ScanForStandableTile(GameLocation location)
        {
            try
            {
                var width = location.map.Layers[0].LayerWidth;
                var height = location.map.Layers[0].LayerHeight;

                for (var y = 1; y < height; y++)
                {
                    for (var x = 1; x < width; x++)
                    {
                        var tile = new Vector2(x, y);

                        if (!location.isTileOnMap(tile)) continue;
                        if (location.objects.ContainsKey(tile)) continue;
                        if (location.terrainFeatures.ContainsKey(tile)) continue;
                        if (IsOccupied(location, x, y)) continue;
                        if (!location.isTilePassable(tile)) continue;

                        ModLog.Warn($"SchoolLocator: 在 '{location.NameOrUniqueName}' 用了整图扫描兜底，落点 {tile}。");
                        return tile;
                    }
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("SchoolLocator.ScanForStandableTile failed.", ex);
            }

            return Vector2.Zero;
        }

        private static bool IsOccupied(GameLocation location, int x, int y)
        {
            try
            {
                foreach (var npc in location.characters)
                {
                    if (npc != null && npc.TilePoint.X == x && npc.TilePoint.Y == y) return true;
                }
            }
            catch
            {
                // 集合正在变更时忽略，交给 passable 判断
            }

            return false;
        }

        /// <summary>孩子在家里的落点：孩子已经在家就用原位，否则找一个空位（找不到退到儿童床/门口）。</summary>
        internal static Vector2 HomeSpot(Child child)
        {
            var home = ChildRegistry.HomeOf(child);
            if (home == null) return new Vector2(31, 14);

            try
            {
                if (ReferenceEquals(child.currentLocation, home))
                    return new Vector2((int)(child.Position.X / 64), (int)(child.Position.Y / 64));

                // 原版对 Age 2/3 的孩子每天就是用这个函数在家里找一个落脚点
                var open = home.getRandomOpenPointInHouse(Game1.random, 1, 200);
                if (open != Point.Zero)
                    return Utility.PointToVector2(open);

                var bedSpot = home.GetChildBedSpot(ChildIndex(child));
                if (bedSpot != Point.Zero)
                    return Utility.PointToVector2(bedSpot);

                var entry = home.getEntryLocation();
                if (entry != Point.Zero)
                    return Utility.PointToVector2(entry);
            }
            catch (Exception ex)
            {
                ModLog.Error("SchoolLocator.HomeSpot failed.", ex);
            }

            return new Vector2(31, 14);
        }

        /// <summary>复刻原版 Child.GetChildIndex，但不受"孩子是否在农舍集合里"影响。</summary>
        internal static int ChildIndex(Child child)
        {
            try
            {
                var parentId = child.idOfParent.Value;
                var siblings = new List<Child>();

                foreach (var other in ChildRegistry.All())
                {
                    if (other != null && other.idOfParent.Value == parentId) siblings.Add(other);
                }

                siblings.Sort((a, b) => a.daysOld.Value.CompareTo(b.daysOld.Value));
                siblings.Reverse();

                var index = siblings.IndexOf(child);
                if (index >= 0) return index;
            }
            catch (Exception ex)
            {
                ModLog.Error("SchoolLocator.ChildIndex failed.", ex);
            }

            return Math.Max(0, (int)child.Gender);
        }

        internal static FarmHouse? Home(Child child)
        {
            return ChildRegistry.HomeOf(child);
        }
    }
}
