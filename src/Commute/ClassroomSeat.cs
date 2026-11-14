using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Objects;
using LifeWithWifeAndChild.Core;

namespace LifeWithWifeAndChild.Commute
{
    /// <summary>
    /// 课堂座位（DESIGN.md §4.3）。
    ///
    /// 规则：**离讲课人最近的桌子旁边的空格**。
    /// 桌子的判定直接用原版 <see cref="Furniture.IsTable"/>（furniture_type 5 / 11），
    /// 不去猜"看起来像桌子"的东西；没有桌子时退化为"她旁边的空格"。
    ///
    /// 注意：原版没有任何 NPC 坐姿能力（Furniture / MapSeat 的座位都只认 Farmer id），
    /// 所以"坐着"靠位置 + 朝向 + 静止来近似，真正的坐姿需要一张 16×32 的儿童坐姿图。
    /// </summary>
    internal static class ClassroomSeat
    {
        /// <summary>找座位；找不到返回 null。</summary>
        internal static Vector2? Find(GameLocation? location, Vector2 anchorTile)
        {
            if (location == null) return null;

            try
            {
                var best = FindTableSeat(location, anchorTile) ?? FindBeside(location, anchorTile);
                return best;
            }
            catch (Exception ex)
            {
                ModLog.Error("ClassroomSeat.Find failed.", ex);
                return null;
            }
        }

        private static Vector2? FindTableSeat(GameLocation location, Vector2 anchorTile)
        {
            var tables = new List<Furniture>();

            try
            {
                foreach (var furniture in location.furniture)
                {
                    if (furniture != null && furniture.IsTable()) tables.Add(furniture);
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("ClassroomSeat: 读取家具失败。", ex);
                return null;
            }

            Vector2? best = null;
            var bestDistance = float.MaxValue;

            foreach (var table in tables)
            {
                try
                {
                    var origin = table.TileLocation;
                    var width = Math.Max(1, table.getTilesWide());
                    var height = Math.Max(1, table.getTilesHigh());

                    // 桌沿向外一格的相邻格
                    for (var x = (int)origin.X - 1; x <= (int)origin.X + width; x++)
                    {
                        for (var y = (int)origin.Y - 1; y <= (int)origin.Y + height; y++)
                        {
                            var insideX = x >= (int)origin.X && x < (int)origin.X + width;
                            var insideY = y >= (int)origin.Y && y < (int)origin.Y + height;
                            if (insideX && insideY) continue; // 桌子自己占的格子

                            var tile = new Vector2(x, y);
                            if (!IsStandable(location, tile)) continue;

                            var distance = Vector2.Distance(tile, anchorTile);
                            if (distance >= bestDistance) continue;

                            bestDistance = distance;
                            best = tile;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModLog.Error("ClassroomSeat: 处理某张桌子失败。", ex);
                }
            }

            return best;
        }

        /// <summary>没有桌子时的兜底：从锚点向外一圈圈找最近的可站格。</summary>
        private static Vector2? FindBeside(GameLocation location, Vector2 anchorTile)
        {
            for (var radius = 1; radius <= 8; radius++)
            {
                Vector2? best = null;
                var bestDistance = float.MaxValue;

                for (var dx = -radius; dx <= radius; dx++)
                {
                    for (var dy = -radius; dy <= radius; dy++)
                    {
                        // 只看这一圈的边，避免重复检查内圈
                        if (Math.Abs(dx) != radius && Math.Abs(dy) != radius) continue;

                        var tile = new Vector2(anchorTile.X + dx, anchorTile.Y + dy);
                        if (!IsStandable(location, tile)) continue;

                        var distance = Vector2.Distance(tile, anchorTile);
                        if (distance >= bestDistance) continue;

                        bestDistance = distance;
                        best = tile;
                    }
                }

                if (best.HasValue) return best;
            }

            return null;
        }

        private static bool IsStandable(GameLocation location, Vector2 tile)
        {
            try
            {
                if (!location.isTileOnMap(tile)) return false;
                if (location.objects.ContainsKey(tile)) return false;
                if (location.GetFurnitureAt(tile) != null) return false;
                if (!location.isTilePassable(tile)) return false;

                foreach (var character in location.characters)
                {
                    if (character != null && character.TilePoint.X == (int)tile.X && character.TilePoint.Y == (int)tile.Y)
                        return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>调试：把课堂里的桌子/候选座位/最终选中摊平成人能读的行。</summary>
        internal static List<string> Describe(GameLocation? location, Vector2 anchorTile)
        {
            var lines = new List<string>();
            if (location == null)
            {
                lines.Add("课堂地图不可用");
                return lines;
            }

            try
            {
                var tables = 0;
                foreach (var furniture in location.furniture)
                {
                    if (furniture == null || !furniture.IsTable()) continue;

                    tables++;
                    lines.Add($"  桌子 @ {furniture.TileLocation} {furniture.getTilesWide()}x{furniture.getTilesHigh()} ({furniture.QualifiedItemId})");
                }

                if (tables == 0) lines.Add("  (这张图里没有桌子)");

                var seat = Find(location, anchorTile);
                lines.Add(seat.HasValue
                    ? $"  选中座位: {seat.Value}（距锚点 {Vector2.Distance(seat.Value, anchorTile):F1}）"
                    : "  选中座位: 无");
            }
            catch (Exception ex)
            {
                ModLog.Error("ClassroomSeat.Describe failed.", ex);
            }

            return lines;
        }
    }
}
