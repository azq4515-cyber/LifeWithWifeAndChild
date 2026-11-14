using System;
using System.Collections.Generic;
using StardewModdingAPI;
using LifeWithWifeAndChild.Core;

namespace LifeWithWifeAndChild.Integrations
{
    /// <summary>
    /// 已知冲突 mod 的启动检测（DESIGN.md §8 / TECH-DESIGN §10）。
    /// 命中后禁用本 mod 的成长与出门逻辑，避免"双实体/年龄错乱"这类不可恢复的存档问题。
    /// </summary>
    internal static class ModConflictDetector
    {
        private static readonly string[] KnownConflictIds =
        {
            "Pathoschild.ChildToNPC",
            "Aedenthorn.ChildToNPC",
            "Rafseazz.RSVCCP", // 仅记录，不视为硬冲突
        };

        private static readonly string[] KnownConflictNameFragments =
        {
            "child to npc",
            "childtonpc",
            "immersive family",
        };

        internal static bool HasBlockingConflict { get; private set; }

        internal static void Run(IModHelper helper, IMonitor monitor)
        {
            try
            {
                foreach (var mod in helper.ModRegistry.GetAll())
                {
                    if (mod == null) continue;
                    if (mod.Manifest?.UniqueID == Constants.UniqueId) continue;

                    var id = mod.Manifest?.UniqueID ?? string.Empty;
                    var name = mod.Manifest?.Name ?? string.Empty;

                    foreach (var known in KnownConflictIds)
                    {
                        if (string.Equals(id, known, StringComparison.OrdinalIgnoreCase))
                        {
                            Block(monitor, name);
                            return;
                        }
                    }

                    var lowered = name.ToLowerInvariant();
                    foreach (var fragment in KnownConflictNameFragments)
                    {
                        if (lowered.Contains(fragment))
                        {
                            Block(monitor, name);
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("ModConflictDetector.Run failed.", ex);
            }
        }

        private static void Block(IMonitor monitor, string modName)
        {
            HasBlockingConflict = true;

            // 注意：这里**不改** config。改了以后玩家一旦用 GMCM 保存配置，
            // 禁用状态就会被写进 config.json，卸掉冲突 mod 之后也恢复不了。
            monitor.Log(
                Constants.LogPrefix + $" 检测到可能冲突的 mod「{modName}」：本次会话已禁用成长推进、上学与伴侣代收，避免双实体/年龄错乱。",
                LogLevel.Alert);
        }

        internal static List<string> Describe()
        {
            var lines = new List<string>();
            lines.Add(HasBlockingConflict ? "冲突检测: 命中（成长已禁用）" : "冲突检测: 未命中");
            return lines;
        }
    }
}
