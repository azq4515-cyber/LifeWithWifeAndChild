using System;
using StardewValley.Characters;
using LifeWithWifeAndChild.Core;

namespace LifeWithWifeAndChild.Growth
{
    /// <summary>
    /// 接管原版 aging（DESIGN.md §3.1 / TECH-DESIGN §7.1）。
    ///
    /// 做法：把 <c>daysOld</c> 当成"原版年龄的投影"，在 <c>Child.dayUpdate</c> 之前写入合成值，
    /// 然后**放行原版逻辑**（不 skip），让 resetForNewDay / 位置重排 / speed / reloadSprite 全部照原版跑。
    /// 这样既保住了原版的全部副作用，又完全控制了推进节奏。
    /// </summary>
    internal static class AgingOverride
    {
        /// <summary>在 <c>Child.dayUpdate</c> 的 Prefix 里调用。返回 false 表示跳过原版方法。</summary>
        internal static bool Apply(Child child)
        {
            if (child == null) return true;

            try
            {
                var state = ChildState.Get(child);

                // 原版 dayUpdate 里对 Age 2/3 有 (currentLocation as FarmHouse) 的硬转型，
                // 孩子在外时会 NRE。这里先把他送回家，再交给原版。
                Commute.CommuteService.ForceReturnHome(child, "dayUpdate 前归位");

                if (!state.IsManaged) return true;

                state.ApplyVanillaAge();

                var synthetic = StageTable.SyntheticDaysOld(state.Stage);
                if (child.daysOld.Value != synthetic)
                {
                    ModLog.Trace($"aging: '{child.Name}' daysOld {child.daysOld.Value} -> {synthetic} (stage {(int)state.Stage}).");
                    child.daysOld.Value = synthetic;
                }

                return true;
            }
            catch (Exception ex)
            {
                ModLog.Error("AgingOverride.Apply failed; letting vanilla run untouched.", ex);
                return true;
            }
        }
    }
}
