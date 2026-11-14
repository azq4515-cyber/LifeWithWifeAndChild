using System;
using System.Collections.Generic;
using StardewValley;
using StardewValley.Characters;
using LifeWithWifeAndChild.Core;

namespace LifeWithWifeAndChild.Abilities
{
    /// <summary>
    /// 阶段 6（成年）：代售。
    ///
    /// 设计取舍（TECH-DESIGN §7.3 的"不真卖"精神 + 一个更安全的落点）：
    /// 目标容器是**出货箱**，不是家庭箱。原因：家庭箱是伴侣 Keg 产出的存放处，
    /// 孩子若自动清空它会把玩家存的酒卖掉——那是明显的负体验。出货箱里的东西玩家
    /// 本来就是拿去卖的，孩子的作用是"当天就卖、不用等过夜"，代价是折价（默认 90%）。
    ///
    /// 实现上是真的把物品从箱子里取走并加钱，所以不存在重复结算：
    /// 晚上原版的出货结算看到的是空箱子。
    /// </summary>
    internal sealed class SellProduceAbility : IChildAbility
    {
        public string Id => "sell_produce";

        public int MinStage => (int)Stage.Adult;

        public bool CanRun(Child child, Farmer player, ChildState state)
        {
            return ModEntry.Config.MaxSellItems > 0 && Game1.getFarm() != null;
        }

        public int Run(Child child, Farmer player, ChildState state)
        {
            var farm = Game1.getFarm();
            if (farm == null) return 0;

            var bin = farm.getShippingBin(player);
            if (bin is not IList<Item> items || items.Count == 0) return 0;

            var limit = Math.Max(0, ModEntry.Config.MaxSellItems);
            var multiplier = ModEntry.Config.SellPriceMultiplier;

            var sold = 0;
            var gold = 0;
            var keep = new List<Item>();
            var taken = new List<Item>();

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];

                if (item == null)
                {
                    keep.Add(null!);
                    continue;
                }

                if (sold >= limit)
                {
                    keep.Add(item);
                    continue;
                }

                int pricePerUnit;
                try
                {
                    pricePerUnit = item.sellToStorePrice();
                }
                catch
                {
                    pricePerUnit = -1;
                }

                if (pricePerUnit <= 0)
                {
                    // 卖不出钱的（垃圾/任务物品）留给原版出货逻辑处理
                    keep.Add(item);
                    continue;
                }

                var stack = Math.Max(1, item.Stack);
                gold += (int)Math.Round(pricePerUnit * stack * multiplier);
                sold++;
                taken.Add(item);
            }

            if (sold == 0) return 0;

            try
            {
                bin.OverwriteWith(keep);
            }
            catch (Exception ex)
            {
                // 覆盖失败说明状态不可信：把金退回不卖，绝不留下"没扣物品却加了钱"
                ModLog.Error("sell_produce: 清空出货箱失败，本次不结算。", ex);
                return 0;
            }

            try
            {
                Game1.player.Money += gold;
                Game1.playSound("sell");
            }
            catch (Exception ex)
            {
                ModLog.Error("sell_produce: 加钱失败。", ex);
            }

            ModLog.Info($"sell_produce: 代售 {sold} 件，进账 {gold}g（折价 {multiplier:P0}）。");
            LastGold = gold;
            return sold;
        }

        internal int LastGold { get; private set; }

        public string Describe(int count)
        {
            return I18n.Get("hud.ability.sell", new { count, gold = LastGold });
        }
    }
}
