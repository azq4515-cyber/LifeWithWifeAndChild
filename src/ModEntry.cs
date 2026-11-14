using System;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using LifeWithWifeAndChild.Abilities;
using LifeWithWifeAndChild.Commute;
using LifeWithWifeAndChild.Content;
using LifeWithWifeAndChild.Core;
using LifeWithWifeAndChild.Commands;
using LifeWithWifeAndChild.Family;
using LifeWithWifeAndChild.Growth;
using LifeWithWifeAndChild.Integrations;
using LifeWithWifeAndChild.Spouse;

namespace LifeWithWifeAndChild
{
    /// <summary>SMAPI 入口：装配事件、补丁、指令与配置。</summary>
    public sealed class ModEntry : Mod
    {
        internal static ModConfig Config { get; private set; } = new ModConfig();

        /// <summary>静态入口：补丁层与各 service 都通过它取 helper。刻意隐藏 <see cref="Mod.Helper"/>。</summary>
        internal static new IModHelper Helper { get; private set; } = null!;

        /// <summary>多人联机：v1 明确不支持（DESIGN.md §7），检测到就只保留指令与 UI。</summary>
        internal static bool MultiplayerBlocked { get; private set; }

        /// <summary>任何一条"全局停用"命中时，成长 / 上课 / 收桶一律不跑。</summary>
        internal static bool FeaturesBlocked => MultiplayerBlocked || ModConflictDetector.HasBlockingConflict;

        private Harmony? harmony;

        public override void Entry(IModHelper helper)
        {
            Helper = helper;

            try
            {
                Config = helper.ReadConfig<ModConfig>();
                Config.Sanitize();
            }
            catch (Exception ex)
            {
                Monitor.Log(Constants.LogPrefix + " 读取 config.json 失败，使用默认配置。", LogLevel.Warn);
                ModLog.Error("ReadConfig failed.", ex);
                Config = new ModConfig();
            }

            ModLog.Init(Monitor, Config.VerboseLogging);

            ApplyHarmonyPatches();

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.DayEnding += OnDayEnding;
            helper.Events.GameLoop.TimeChanged += OnTimeChanged;
            helper.Events.GameLoop.OneSecondUpdateTicked += OnOneSecondUpdateTicked;
            helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
            helper.Events.Content.AssetRequested += AssetInjector.OnAssetRequested;

            if (Config.EnableDebugCommands)
                DebugCommands.Register(helper);

            Monitor.Log(
                $"{Constants.LogPrefix} loaded v{ModManifest.Version} (GrowthSpeed={Config.GrowthSpeed}, work={Config.EnableChildWork}, lesson={Config.EnableLesson}, chore={Config.EnableWeekendKegChore}).",
                LogLevel.Info);
        }

        // ---- Harmony ----

        private void ApplyHarmonyPatches()
        {
            try
            {
                harmony = new Harmony(ModManifest.UniqueID);
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                Monitor.Log(Constants.LogPrefix + " Harmony 补丁已应用。", LogLevel.Trace);
            }
            catch (Exception ex)
            {
                ModLog.Error("Harmony PatchAll failed; the mod will run without patches.", ex);
                Monitor.Log(Constants.LogPrefix + " 补丁应用失败：成长接管与孩子对话将不可用，其余功能照常。", LogLevel.Alert);
            }
        }

        // ---- 事件 ----

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            ModConflictDetector.Run(Helper, Monitor);
            GenericModConfigMenu.Register(Helper, ModManifest, Config, () => Helper.WriteConfig(Config));
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            try
            {
                MultiplayerBlocked = Context.IsMultiplayer;

                if (MultiplayerBlocked)
                {
                    Monitor.Log(Constants.LogPrefix + " 检测到多人联机：v1 不支持，成长推进、上课与收桶已停用。", LogLevel.Alert);
                }

                if (!Context.IsMainPlayer) return;

                SaveMigration.EnsureAll();
                CommuteService.OnSaveLoaded();

                ModLog.Info($"save loaded: {ChildRegistry.All().Count} 个孩子。");
            }
            catch (Exception ex)
            {
                ModLog.Error("OnSaveLoaded failed.", ex);
            }
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            if (FeaturesBlocked) return;
            GrowthService.OnDayStarted();
        }

        private void OnDayEnding(object? sender, DayEndingEventArgs e)
        {
            if (FeaturesBlocked) return;
            GrowthService.OnDayEnding();
        }

        private void OnTimeChanged(object? sender, TimeChangedEventArgs e)
        {
            if (FeaturesBlocked) return;

            try
            {
                CommuteService.OnTimeChanged(e.NewTime);
                SeatService.OnTimeChanged(e.NewTime);
                KegChore.OnTimeChanged(e.NewTime);

                // 孩子能力：到达配置时刻后结算一次（孩子必须在家；在外的会在回家后补）
                if (e.NewTime >= Config.ChildWorkHour * 100)
                    AbilityService.RunDailyWork();
            }
            catch (Exception ex)
            {
                ModLog.Error("OnTimeChanged failed.", ex);
            }
        }

        private void OnOneSecondUpdateTicked(object? sender, OneSecondUpdateTickedEventArgs e)
        {
            if (FeaturesBlocked) return;
            if (!Context.IsWorldReady) return;

            StageScenes.Tick();
        }

        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            MultiplayerBlocked = false;
        }
    }
}
