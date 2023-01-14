﻿using Celeste.Mod.Entities;
using Celeste.Mod.MaxHelpingHand.Module;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using System;
using System.Collections;
using System.Linq;

namespace Celeste.Mod.MaxHelpingHand.Triggers {
    // Near copy-paste of Everest's Dialog Cutscene Trigger, that has the following extra features:
    // - allows to start auto-skipping messages (skipping them without pressing Confirm) with {trigger 0} and stop auto-skipping with {trigger 1}
    // - allows to set a custom font for the dialogue
    [CustomEntity("MaxHelpingHand/AutoSkipDialogCutsceneTrigger", "MaxHelpingHand/ExtendedDialogCutsceneTrigger")]
    public class ExtendedDialogCutsceneTrigger : Trigger {
        private readonly string dialogEntry;
        private readonly EntityID id;
        private readonly bool onlyOnce;
        private readonly bool endLevel;
        private readonly int deathCount;
        private readonly string font;

        private bool triggered;

        public ExtendedDialogCutsceneTrigger(EntityData data, Vector2 offset, EntityID entId)
            : base(data, offset) {
            dialogEntry = data.Attr("dialogId");
            onlyOnce = data.Bool("onlyOnce", true);
            endLevel = data.Bool("endLevel", false);
            deathCount = data.Int("deathCount", -1);
            font = data.Attr("font");
            triggered = false;
            id = entId;
        }

        public override void OnEnter(Player player) {
            if (triggered || (Scene as Level).Session.GetFlag("DoNotLoad" + id) ||
                (deathCount >= 0 && SceneAs<Level>().Session.DeathsInCurrentLevel != deathCount)) {

                return;
            }

            triggered = true;

            Scene.Add(new ExtendedDialogCutscene(dialogEntry, player, endLevel, font));

            if (onlyOnce) {
                (Scene as Level).Session.SetFlag("DoNotLoad" + id, true); // Sets flag to not load
            }
        }

        private class ExtendedDialogCutscene : CutsceneEntity {
            private Player player;
            private string dialogID;
            private bool endLevel;
            private string font;

            public ExtendedDialogCutscene(string dialogID, Player player, bool endLevel, string font) {
                this.dialogID = dialogID;
                this.player = player;
                this.endLevel = endLevel;
                this.font = font;
            }

            public override void OnBegin(Level level) {
                Add(new Coroutine(Cutscene(level)));
            }

            private IEnumerator Cutscene(Level level) {
                player.StateMachine.State = 11;
                player.StateMachine.Locked = true;
                player.ForceCameraUpdate = true;

                yield return new SwapImmediately(LoadFontForDurationOfCoroutine(font,
                    fontFace => SayWithDifferentFont(fontFace, dialogID, startSkipping, stopSkipping)));

                EndCutscene(level);
            }

            // this is triggered with {trigger 0}
            private IEnumerator startSkipping() {
                new DynData<Textbox>(Scene.Tracker.GetEntity<Textbox>())["autoPressContinue"] = !MaxHelpingHandModule.Instance.Settings.DisableDialogueAutoSkip;
                yield break;
            }

            // this is triggered with {trigger 1}
            private IEnumerator stopSkipping() {
                new DynData<Textbox>(Scene.Tracker.GetEntity<Textbox>())["autoPressContinue"] = false;
                yield break;
            }

            public override void OnEnd(Level level) {
                player.StateMachine.Locked = false;
                player.StateMachine.State = 0;
                player.ForceCameraUpdate = false;
                if (WasSkipped) {
                    level.Camera.Position = player.CameraTarget;
                }
                if (endLevel) {
                    level.CompleteArea(true, false);
                    player.StateMachine.State = 11;
                    RemoveSelf();
                }
            }
        }

        internal static IEnumerator SayWithDifferentFont(PixelFont font, string dialog, params Func<IEnumerator>[] events) {
            IEnumerator say = Textbox.Say(dialog, events);

            if (font != null && say.MoveNext()) {
                // the MoveNext above added the textbox to the scene, this is time to swap the font!
                Textbox textbox = Engine.Scene.Entities.GetToAdd().OfType<Textbox>().First();
                new DynData<Textbox>(textbox).Get<FancyText.Text>("text").Font = font;

                // pass the value (probably null) that we consumed by calling MoveNext above.
                yield return say.Current;
            }

            // now just exhaust the rest of the coroutine.
            yield return new SwapImmediately(say);
        }

        internal static IEnumerator LoadFontForDurationOfCoroutine(string font, Func<PixelFont, IEnumerator> coroutineProducer) {
            bool unloadFont = false;
            PixelFont fontFace = null;

            if (!string.IsNullOrEmpty(font)) {
                fontFace = Fonts.Get(font);

                if (fontFace == null) {
                    // we are loading a new font for the duration of the coroutine, and we should dispose of it at the end of it.
                    fontFace = Fonts.Load(font);
                    unloadFont = true;
                }
            }

            yield return new SwapImmediately(coroutineProducer(fontFace));

            if (unloadFont) {
                Fonts.Unload(font);
            }
        }
    }
}
