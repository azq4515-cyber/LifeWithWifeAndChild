using System;
using StardewModdingAPI;

namespace LifeWithWifeAndChild.Integrations
{
    /// <summary>
    /// GMCM 的软依赖接口（不安装就没有这个 API，注册会被跳过）。
    /// 只声明我们真正用到的几个方法，避免把整个 API 搬进来。
    /// </summary>
    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

        void AddSectionTitle(IManifest mod, Func<string> text, Func<string>? tooltip = null);

        void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string>? tooltip = null);

        void AddNumberOption(IManifest mod, Func<int> getValue, Action<int> setValue, Func<string> name, Func<string>? tooltip = null, int? min = null, int? max = null, int? interval = null, Func<int, string>? formatValue = null);

        void AddNumberOption(IManifest mod, Func<float> getValue, Action<float> setValue, Func<string> name, Func<string>? tooltip = null, float? min = null, float? max = null, float? interval = null, Func<float, string>? formatValue = null);
    }

    /// <summary>通用配置菜单接入（可选）。</summary>
    internal static class GenericModConfigMenu
    {
        internal static void Register(IModHelper helper, IManifest manifest, ModConfig config, Action save)
        {
            try
            {
                var api = helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
                if (api == null) return;

                api.Register(manifest, () => { }, save);

                api.AddSectionTitle(manifest, () => Core.I18n.Get("config.section.growth"));
                api.AddNumberOption(manifest, () => config.GrowthSpeed, value => config.GrowthSpeed = value,
                    () => Core.I18n.Get("config.growth_speed.name"), () => Core.I18n.Get("config.growth_speed.tooltip"), 1, 60, 1);
                api.AddNumberOption(manifest, () => config.ChildWorkHour, value => config.ChildWorkHour = value,
                    () => Core.I18n.Get("config.work_hour.name"), () => Core.I18n.Get("config.work_hour.tooltip"), 6, 22, 1);

                api.AddSectionTitle(manifest, () => Core.I18n.Get("config.section.features"));
                api.AddBoolOption(manifest, () => config.EnableChildWork, v => config.EnableChildWork = v,
                    () => Core.I18n.Get("config.child_work.name"), () => Core.I18n.Get("config.child_work.tooltip"));
                api.AddBoolOption(manifest, () => config.EnableLesson, v => config.EnableLesson = v,
                    () => Core.I18n.Get("config.lesson.name"), () => Core.I18n.Get("config.lesson.tooltip"));
                api.AddBoolOption(manifest, () => config.EnableSeat, v => config.EnableSeat = v,
                    () => Core.I18n.Get("config.seat.name"), () => Core.I18n.Get("config.seat.tooltip"));
                api.AddBoolOption(manifest, () => config.EnableWeekendKegChore, v => config.EnableWeekendKegChore = v,
                    () => Core.I18n.Get("config.chore.name"), () => Core.I18n.Get("config.chore.tooltip"));

                api.AddSectionTitle(manifest, () => Core.I18n.Get("config.section.balance"));
                api.AddNumberOption(manifest, () => config.KegChoreStartHour, v => config.KegChoreStartHour = v,
                    () => Core.I18n.Get("config.chore_hour.name"), () => Core.I18n.Get("config.chore_hour.tooltip"), 6, 22, 1);
                api.AddNumberOption(manifest, () => config.MaxKegsPerDay, v => config.MaxKegsPerDay = v,
                    () => Core.I18n.Get("config.max_kegs.name"), () => Core.I18n.Get("config.max_kegs.tooltip"), 0, 200, 1);
                api.AddNumberOption(manifest, () => config.MaxWaterTiles, v => config.MaxWaterTiles = v,
                    () => Core.I18n.Get("config.max_water.name"), () => Core.I18n.Get("config.max_water.tooltip"), 0, 100, 1);
                api.AddNumberOption(manifest, () => config.MaxHarvestCrops, v => config.MaxHarvestCrops = v,
                    () => Core.I18n.Get("config.max_harvest.name"), () => Core.I18n.Get("config.max_harvest.tooltip"), 0, 200, 1);
                api.AddNumberOption(manifest, () => config.MaxSellItems, v => config.MaxSellItems = v,
                    () => Core.I18n.Get("config.max_sell.name"), () => Core.I18n.Get("config.max_sell.tooltip"), 0, 100, 1);
                api.AddNumberOption(manifest, () => config.SellPriceMultiplier, v => config.SellPriceMultiplier = v,
                    () => Core.I18n.Get("config.sell_multiplier.name"), () => Core.I18n.Get("config.sell_multiplier.tooltip"), 0.05f, 1f, 0.05f);

                api.AddSectionTitle(manifest, () => Core.I18n.Get("config.section.debug"));
                api.AddBoolOption(manifest, () => config.VerboseLogging, v => config.VerboseLogging = v,
                    () => Core.I18n.Get("config.verbose.name"), () => Core.I18n.Get("config.verbose.tooltip"));
            }
            catch (Exception ex)
            {
                Core.ModLog.Error("GenericModConfigMenu registration failed (ignored).", ex);
            }
        }
    }
}
