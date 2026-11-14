using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using LifeWithWifeAndChild.Core;

namespace LifeWithWifeAndChild.Spouse
{
    /// <summary>
    /// 阶段剧情场景（DESIGN.md §3 的"一个事件"）。
    ///
    /// 实现选择（与 TECH-DESIGN §7.6 的偏离，见 docs/milestones.md）：
    /// 没有走 <c>Data/Events/&lt;location&gt;</c>，而是在玩家空闲时用多页对话播放一段"场景"。
    /// 理由是硬性的：事件脚本里要引用演员名，而孩子的名字是玩家输入的，
    /// 事件资产键与演员都无法在写死的事件脚本里表达；同时 v1 没有新美术。
    /// 用对话链 + createQuestionDialogue 可以在零格式风险下交付同样的内容（含分支选择）。
    /// </summary>
    internal static class StageScenes
    {
        private static readonly Queue<Dialogue> Pending = new Queue<Dialogue>();
        private static Action? afterQueue;
        private static bool waitingForDialogueEnd;

        internal static void OnDayStarted(Child child, ChildState state)
        {
            // 场景在玩家空闲时由 Tick 推进
        }

        /// <summary>每秒调用一次：先播队列，再看有没有新场景要排队。</summary>
        internal static void Tick()
        {
            try
            {
                if (Pending.Count > 0)
                {
                    DrainQueue();
                    return;
                }

                // 最后一句已经画出来了：等玩家把对话读完，再做"场景之后"的事（弹选择框等）。
                // 直接在画完那一刻调用会让 createQuestionDialogue 撞上还没关掉的对话框。
                if (waitingForDialogueEnd)
                {
                    if (Game1.dialogueUp || Game1.activeClickableMenu != null) return;

                    waitingForDialogueEnd = false;
                    var done = afterQueue;
                    afterQueue = null;
                    done?.Invoke();
                    return;
                }

                TryQueueNext();
            }
            catch (Exception ex)
            {
                ModLog.Error("StageScenes.Tick failed.", ex);
                Pending.Clear();
                afterQueue = null;
                waitingForDialogueEnd = false;
            }
        }

        private static void DrainQueue()
        {
            if (Pending.Count == 0) return;
            if (!I18nExtensions.PlayerIsFree()) return;

            var page = Pending.Dequeue();
            Game1.DrawDialogue(page);

            if (Pending.Count == 0)
                waitingForDialogueEnd = true;
        }

        private static void TryQueueNext()
        {
            if (!I18nExtensions.PlayerIsFree()) return;

            foreach (var child in ChildRegistry.All())
            {
                var state = ChildState.Get(child);
                var pending = state.PendingEvents;
                if (pending.Count == 0) continue;

                QueueScene(child, state, pending[0]);
                return;
            }
        }

        /// <summary>播放指定场景（调试指令 <c>lwwc_event</c> 也走这里）。</summary>
        internal static void PlayNow(Child child, ChildState state, string sceneId)
        {
            try
            {
                QueueScene(child, state, sceneId);

                // 调试时立刻尝试推进一次
                if (Pending.Count > 0 && I18nExtensions.PlayerIsFree())
                    DrainQueue();
            }
            catch (Exception ex)
            {
                ModLog.Error("StageScenes.PlayNow failed.", ex);
            }
        }

        private static void QueueScene(Child child, ChildState state, string sceneId)
        {
            var tokens = new { child = child.Name };

            var childKey = "scene." + sceneId;
            if (!I18nExtensions.Exists(childKey))
            {
                ModLog.Warn("scene '" + sceneId + "' 没有对应文本，跳过。");
                state.RemoveEvent(sceneId);
                return;
            }

            state.RemoveEvent(sceneId);
            Pending.Clear();
            waitingForDialogueEnd = false;
            afterQueue = () => AfterScene(child, state, sceneId);

            // 伴侣先说话（同一张图里才播，避免"隔空对话"）
            var spouse = I18nExtensions.SafeSpouse();
            var parentKey = childKey + ".parent";

            if (spouse != null
                && I18nExtensions.Exists(parentKey)
                && ReferenceEquals(spouse.currentLocation, child.currentLocation))
            {
                Pending.Enqueue(new Dialogue(spouse, parentKey, I18n.Get(parentKey, tokens)));
            }

            Pending.Enqueue(new Dialogue(child, childKey, I18n.Get(childKey, tokens)));

            ModLog.Info("scene: '" + sceneId + "' queued for '" + child.Name + "'.");
        }

        /// <summary>场景播完后处理分支（DESIGN.md §3 的"第一天上学的选择"/"梦想·叛逆"）。</summary>
        private static void AfterScene(Child child, ChildState state, string sceneId)
        {
            try
            {
                if (sceneId == "first_school") AskSchoolChoice(child, state);
                else if (sceneId == "dream") AskDreamChoice(child, state);
            }
            catch (Exception ex)
            {
                ModLog.Error("StageScenes.AfterScene failed.", ex);
            }
        }

        private static void AskSchoolChoice(Child child, ChildState state)
        {
            if (state.HasFlag("school_choice_done")) return;
            if (!I18nExtensions.PlayerIsFree()) return;
            if (Game1.currentLocation == null) return;

            state.AddFlag("school_choice_done");

            var question = I18n.Get("choice.school.question", new { child = child.Name });
            var responses = new[]
            {
                new Response("lwwc_school_yes", I18n.Get("choice.school.yes")),
                new Response("lwwc_school_later", I18n.Get("choice.school.later")),
            };

            Game1.currentLocation.createQuestionDialogue(
                question,
                responses,
                (who, answer) =>
                {
                    try
                    {
                        if (answer == "lwwc_school_later")
                        {
                            state.AddFlag("school_delayed");
                            state.LessonLocation = string.Empty;
                            Commute.CommuteService.ForceReturnHome(child, "玩家选择晚点再上学");
                            Game1.addHUDMessage(new HUDMessage(I18n.Get("hud.school_delayed"), HUDMessage.newQuest_type));
                        }
                        else
                        {
                            state.AddFlag("school_started");
                            Game1.addHUDMessage(new HUDMessage(I18n.Get("hud.school_started"), HUDMessage.newQuest_type));
                        }
                    }
                    catch (Exception ex)
                    {
                        ModLog.Error("school choice callback failed.", ex);
                    }
                },
                child);
        }

        private static void AskDreamChoice(Child child, ChildState state)
        {
            if (state.HasFlag("dream_choice_done")) return;
            if (!I18nExtensions.PlayerIsFree()) return;
            if (Game1.currentLocation == null) return;

            state.AddFlag("dream_choice_done");

            var question = I18n.Get("choice.dream.question", new { child = child.Name });
            var responses = new[]
            {
                new Response("lwwc_dream", I18n.Get("choice.dream.dream")),
                new Response("lwwc_rebel", I18n.Get("choice.dream.rebel")),
            };

            Game1.currentLocation.createQuestionDialogue(
                question,
                responses,
                (who, answer) =>
                {
                    try
                    {
                        state.AddFlag(answer == "lwwc_rebel" ? "branch_rebel" : "branch_dream");
                        Game1.addHUDMessage(new HUDMessage(
                            I18n.Get("hud.branch_chosen", new { child = child.Name }),
                            HUDMessage.newQuest_type));
                    }
                    catch (Exception ex)
                    {
                        ModLog.Error("dream choice callback failed.", ex);
                    }
                },
                child);
        }

        /// <summary>孩子被右键说话时用的台词键（随阶段、分支、是否在上课变化）。</summary>
        internal static string DialogueKey(ChildState state)
        {
            var stage = (int)state.Stage;

            // 上课中：单独一套台词（不打断课堂气氛）
            if (state.LessonLocation.Length > 0
                && state.LessonStart > 0
                && Game1.timeOfDay >= state.LessonStart
                && Game1.timeOfDay < state.LessonEnd
                && I18nExtensions.Exists("dialogue.in_class"))
            {
                return "dialogue.in_class";
            }

            if (state.HasFlag("branch_rebel") && stage >= 5) return "dialogue.stage" + stage + ".rebel";
            if (state.HasFlag("branch_dream") && stage >= 5) return "dialogue.stage" + stage + ".dream";

            var key = "dialogue.stage" + stage;
            return I18nExtensions.Exists(key) ? key : "dialogue.fallback";
        }
    }
}
