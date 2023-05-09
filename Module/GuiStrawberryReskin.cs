﻿using Monocle;
using MonoMod.Cil;
using System;
using System.Collections;

namespace Celeste.Mod.MaxHelpingHand.Module {
    public static class GuiStrawberryReskin {
        private static bool isFileSelect;

        public static void Load() {
            IL.Celeste.StrawberriesCounter.Render += modStrawberrySkin;
            IL.Celeste.OuiChapterPanel.DrawCheckpoint += modStrawberrySkin;
            On.Celeste.OuiFileSelect.Enter += onFileSelectEnter;
            On.Celeste.OuiFileSelect.Leave += onFileSelectLeave;
        }

        public static void Unload() {
            IL.Celeste.StrawberriesCounter.Render -= modStrawberrySkin;
            IL.Celeste.OuiChapterPanel.DrawCheckpoint -= modStrawberrySkin;
            On.Celeste.OuiFileSelect.Enter -= onFileSelectEnter;
            On.Celeste.OuiFileSelect.Leave -= onFileSelectLeave;
        }

        private static void modStrawberrySkin(ILContext il) {
            // replace the strawberry, then the golden berry icon.
            ILCursor iLCursor = new ILCursor(il);
            replaceStrawberrySprite(iLCursor, "strawberry");
            iLCursor.Index = 0;
            replaceStrawberrySprite(iLCursor, "goldberry");
        }

        private static void replaceStrawberrySprite(ILCursor iLCursor, string name) {
            while (iLCursor.TryGotoNext(MoveType.After, instr => instr.MatchLdstr($"collectables/{name}"))) {
                Logger.Log("MaxHelpingHand/GuiStrawberryReskin", $"Changing {name} icon w/ custom one at {iLCursor.Index} in IL for {iLCursor.Method.FullName}");
                iLCursor.EmitDelegate<Func<string, string>>(orig => {
                    if (isFileSelect) {
                        return orig;
                    }

                    // try figuring out the current area, and if we can't, bail out!
                    AreaKey? area = (Engine.Scene as Overworld)?.GetUI<OuiChapterPanel>()?.Area;
                    if (area == null) {
                        area = (Engine.Scene as Level)?.Session?.Area;
                    }
                    if (area == null) {
                        return orig;
                    }

                    if (GFX.Gui.Has($"MaxHelpingHand/{area.Value.SID}_{name}")) {
                        return $"MaxHelpingHand/{area.Value.SID}_{name}";
                    }
                    if (GFX.Gui.Has($"MaxHelpingHand/{area.Value.LevelSet}/{name}")) {
                        return $"MaxHelpingHand/{area.Value.LevelSet}/{name}";
                    }
                    return orig;
                });
            }
        }

        private static IEnumerator onFileSelectEnter(On.Celeste.OuiFileSelect.orig_Enter orig, OuiFileSelect self, Oui from) {
            isFileSelect = true;
            return orig(self, from);
        }

        private static IEnumerator onFileSelectLeave(On.Celeste.OuiFileSelect.orig_Leave orig, OuiFileSelect self, Oui next) {
            yield return new SwapImmediately(orig(self, next));
            isFileSelect = false;
        }
    }
}
