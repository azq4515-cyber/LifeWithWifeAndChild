using System;
using System.Collections.Generic;
using StardewValley;
using StardewValley.Characters;

namespace LifeWithWifeAndChild.Core
{
    /// <summary>
    /// 孩子名字 → 资产键的规范化与冲突检测（TECH-DESIGN §7.2）。
    ///
    /// 关键事实（已从 IL 确认）：原版 <c>NPC.getMasterScheduleRawData()</c> 用的键是
    /// <c>"Characters\\schedules\\" + NPC.Name</c>，也就是**原始名字**，不是我们自选的键。
    /// 因此这里不做"改名式规范化"，只做三件事：
    ///   1. 判断名字能不能安全用作资产键（含路径分隔符/控制字符就不行）；
    ///   2. 判断会不会撞上原版村民或另一个孩子的键；
    ///   3. 给出提示原因，失败则让"上学"降级为"留家"。
    /// </summary>
    internal static class NameKey
    {
        private static readonly char[] InvalidChars = { '\0', '\n', '\r', '\t' };

        /// <summary>孩子用于资产键的名字（原版就是这么拼的：原始的 name.Value）。</summary>
        internal static string Resolve(Child child)
        {
            if (child == null) return string.Empty;

            try
            {
                // Child.translateName() => name.Value.TrimEnd()，原版资产键用的就是这个
                return (child.Name ?? string.Empty).Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>名字能否作为资产键使用。失败时给出面向玩家的原因键。</summary>
        internal static bool IsUsable(string name, out string reasonKey)
        {
            reasonKey = string.Empty;

            if (string.IsNullOrWhiteSpace(name))
            {
                reasonKey = "school.disabled.empty_name";
                return false;
            }

            if (name.IndexOfAny(InvalidChars) >= 0)
            {
                reasonKey = "school.disabled.invalid_name";
                return false;
            }

            // 资产键会与 "Characters\\schedules\\" 直接拼接：出现 ".." 或带类型的斜杠会串到别的资产上
            if (name.Contains("..") || name.Contains("\\"))
            {
                reasonKey = "school.disabled.invalid_name";
                return false;
            }

            return true;
        }

        /// <summary>与村民或另一个孩子重名则返回冲突对象的名字。</summary>
        internal static string? FindConflict(Child self, string name)
        {
            try
            {
                foreach (var npc in Utility.getAllVillagers())
                {
                    if (npc == null || ReferenceEquals(npc, self)) continue;

                    string otherName;
                    try
                    {
                        otherName = npc.Name;
                    }
                    catch
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(otherName) && string.Equals(otherName.Trim(), name, StringComparison.OrdinalIgnoreCase))
                        return otherName;
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("NameKey.FindConflict(villagers) failed.", ex);
            }

            try
            {
                foreach (var sibling in ChildRegistry.All())
                {
                    if (sibling == null || ReferenceEquals(sibling, self)) continue;

                    var otherName = Resolve(sibling);
                    if (!string.IsNullOrWhiteSpace(otherName) && string.Equals(otherName, name, StringComparison.OrdinalIgnoreCase))
                        return otherName;
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("NameKey.FindConflict(children) failed.", ex);
            }

            return null;
        }

        /// <summary>综合判断：能用、没冲突。失败原因写进 reasonKey。</summary>
        internal static bool CanUseAssetKeys(Child child, out string reasonKey)
        {
            reasonKey = string.Empty;
            var name = Resolve(child);

            if (!IsUsable(name, out reasonKey))
                return false;

            var conflict = FindConflict(child, name);
            if (conflict != null)
            {
                reasonKey = "school.disabled.name_conflict";
                return false;
            }

            return true;
        }

        /// <summary>日程/对话资产名。</summary>
        internal static string ScheduleAssetName(Child child)
        {
            return Constants.AssetSchedulesPrefix + Resolve(child);
        }

        internal static string DialogueAssetName(Child child)
        {
            return Constants.AssetDialoguePrefix + Resolve(child);
        }
    }
}
