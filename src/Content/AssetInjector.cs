using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using LifeWithWifeAndChild.Commute;
using LifeWithWifeAndChild.Core;

namespace LifeWithWifeAndChild.Content
{
    /// <summary>
    /// 所有资产编辑的统一入口（TECH-DESIGN §2.4）。
    ///
    /// 两条铁律：
    ///   1. 只做**增量**（追加键 / 插入条目），绝不整表替换原版数据；
    ///   2. 动态键（孩子名）在请求发生时按**当前活着的孩子**现算，不依赖启动时的一次性字典。
    ///
    /// v1.1 起**不再修改任何原版 NPC 的日程**（老师/伴侣的注入已全部删除）。
    /// </summary>
    internal static class AssetInjector
    {
        internal static void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            try
            {
                var name = e.NameWithoutLocale;

                if (name.IsEquivalentTo(Constants.AssetBigCraftables, false))
                {
                    e.Edit(BigCraftableContent.Apply, AssetEditPriority.Default);
                    return;
                }

                // 孩子相关的动态资产
                foreach (var child in ChildRegistry.All())
                {
                    var childName = NameKey.Resolve(child);
                    if (childName.Length == 0) continue;

                    if (name.IsEquivalentTo(Constants.AssetSchedulesPrefix + childName, false))
                    {
                        // 撞名的孩子绝不抢原版 NPC 的资产
                        if (NameKey.FindConflict(child, childName) != null) return;

                        e.LoadFrom(() => ScheduleBuilder.BuildScheduleAsset(child), AssetLoadPriority.Exclusive);
                        return;
                    }

                    if (name.IsEquivalentTo(Constants.AssetDialoguePrefix + childName, false))
                    {
                        if (NameKey.FindConflict(child, childName) != null) return;

                        e.LoadFrom(() => DialogueContent.Build(child, ChildState.Get(child)), AssetLoadPriority.Exclusive);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("AssetInjector failed for '" + e.Name + "'.", ex);
            }
        }

        /// <summary>让某个资产重新走一遍请求流程（孩子名字确定后 / 阶段变化后）。</summary>
        internal static void InvalidateChildAssets(StardewValley.Characters.Child child)
        {
            try
            {
                ModEntry.Helper.GameContent.InvalidateCache(NameKey.ScheduleAssetName(child));
                ModEntry.Helper.GameContent.InvalidateCache(NameKey.DialogueAssetName(child));
            }
            catch (Exception ex)
            {
                ModLog.Error("AssetInjector.InvalidateChildAssets failed.", ex);
            }
        }
    }
}
