﻿using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Celeste.Mod.MaxHelpingHand.Entities {
    // The only thing more cursed than sideways jumpthrus, are *moving* sideways jumpthrus.
    // ... Sideways Jumpthrus x Multi-Node Moving Platform might be one of the most cursed mashups yet.
    [CustomEntity("MaxHelpingHand/SidewaysMovingPlatform")]
    [TrackedAs(typeof(SidewaysJumpThru))]
    public class SidewaysMovingPlatform : SidewaysJumpThru {
        // this variable is private, static, and never modified: so we only need reflection once to get it!
        private static readonly HashSet<Actor> solidRiders = (HashSet<Actor>) typeof(Solid).GetField("riders", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

        // settings
        private readonly EntityData thisEntityData;
        private readonly Vector2 thisOffset;
        private readonly string texture;
        private readonly bool left;

        private MTexture[] textures;

        // solid used internally to push/squash/carry the player around
        private Solid playerInteractingSolid;

        // the moving platform that makes that moving platform move :theoreticalwoke:
        private MultiNodeMovingPlatform animatingPlatform;
        private bool spawnedByOtherPlatform = false;

        public SidewaysMovingPlatform(EntityData data, Vector2 offset)
            : base(data.Position + offset, data.Height, !data.Bool("left"), "MaxHelpingHand/invisible", animationDelay: 0f, allowClimbing: true, allowWallJumping: true, letSeekersThrough: true) {

            thisEntityData = data;
            thisOffset = offset;
            texture = data.Attr("texture", "default");
            left = data.Bool("left");

            // this solid will be made solid only when moving the player with the platform, so that the player gets squished and can climb the platform properly.
            playerInteractingSolid = new Solid(Position, Width, Height, safe: false);
            playerInteractingSolid.Collidable = false;
            playerInteractingSolid.Visible = false;
            if (!left) {
                playerInteractingSolid.Position.X += 3f;
            }
        }

        public override void Added(Scene scene) {
            base.Added(scene);


            // add the hidden solid to the scene as well.
            scene.Add(playerInteractingSolid);

            // load the texture.
            MTexture fullTexture = GFX.Game["objects/woodPlatform/" + texture];
            textures = new MTexture[fullTexture.Width / 8];
            for (int i = 0; i < textures.Length; i++) {
                textures[i] = fullTexture.GetSubtexture(i * 8, 0, 8, 8);
            }

            if (spawnedByOtherPlatform) {
                // this platform was spawned by another platform that spawned the moving platform. so don't manage the static mover!
                return;
            }

            // add a multi-node moving platform, pass the platform settings to it, and attach the bumper to it.
            StaticMover staticMover = new StaticMoverWithLiftSpeed() {
                OnMove = move => SidewaysJumpthruOnMove(this, playerInteractingSolid, left, move),
                OnSetLiftSpeed = liftSpeed => playerInteractingSolid.LiftSpeed = liftSpeed
            };
            Add(staticMover);
            animatingPlatform = new MultiNodeMovingPlatform(thisEntityData, thisOffset, otherPlatform => {
                if (otherPlatform != animatingPlatform) {
                    // another multi-node moving platform was spawned (because of the "count" setting), spawn another platform...
                    SidewaysMovingPlatform otherSidewaysPlatform = new SidewaysMovingPlatform(thisEntityData, thisOffset);
                    otherSidewaysPlatform.spawnedByOtherPlatform = true;
                    Scene.Add(otherSidewaysPlatform);

                    // ... and attach it to that new platform.
                    StaticMover otherStaticMover = new StaticMoverWithLiftSpeed() {
                        OnMove = move => SidewaysJumpthruOnMove(otherSidewaysPlatform, otherSidewaysPlatform.playerInteractingSolid, otherSidewaysPlatform.left, move),
                        OnSetLiftSpeed = liftSpeed => otherSidewaysPlatform.playerInteractingSolid.LiftSpeed = liftSpeed
                    };
                    otherSidewaysPlatform.Add(otherStaticMover);
                    otherPlatform.AnimateObject(otherStaticMover, forcedTrackOffset: new Vector2(Width + 4f, Height) / 2f);
                }
            });
            animatingPlatform.AnimateObject(staticMover, forcedTrackOffset: new Vector2(Width + 4f, Height) / 2f);
            scene.Add(animatingPlatform);
        }

        // called when the platform moves, with the move amount
        public static void SidewaysJumpthruOnMove(Entity platform, Solid playerInteractingSolid, bool left, Vector2 move) {
            if (platform.Scene == null) {
                // the platform isn't in the scene yet (initial offset is applied by the moving platform), so don't do collide checks and just move.
                platform.Position += move;
                playerInteractingSolid.MoveHNaive(move.X);
                playerInteractingSolid.MoveVNaive(move.Y);
                return;
            }

            bool playerHasToMove = false;

            if (platform.CollideCheckOutside<Player>(platform.Position + move) && (Math.Sign(move.X) == (left ? -1 : 1))) {
                // the platform is pushing the player horizontally, so we should have the solid push the player.
                playerHasToMove = true;
            }
            if (platform.Collidable && GetPlayerClimbing(platform, left) != null) {
                // player is climbing the platform, so the solid should carry the player with the platform
                playerHasToMove = true;
            }

            // move the platform..
            platform.Position += move;

            // back up the riders, because we don't want to mess up the static variable by moving a solid while moving another solid.
            HashSet<Actor> ridersBackup = new HashSet<Actor>(solidRiders);
            solidRiders.Clear();

            // make the hidden solid collidable if it needs to push the player.
            playerInteractingSolid.Collidable = playerHasToMove;

            // determine who is riding the platform, we will need that later.
            List<Actor> platformRiders = new List<Actor>();
            if (playerInteractingSolid.Collidable) {
                foreach (Actor entity in platform.Scene.Tracker.GetEntities<Actor>()) {
                    if (entity.IsRiding(playerInteractingSolid)) {
                        platformRiders.Add(entity);
                    }
                }
            }

            // move the hidden solid, keeping its lift speed. If it is solid, it will push the player and carry them if they climb the platform.
            Vector2 liftSpeed = playerInteractingSolid.LiftSpeed;
            playerInteractingSolid.MoveH(move.X, liftSpeed.X);
            playerInteractingSolid.MoveV(move.Y, liftSpeed.Y);
            playerInteractingSolid.Collidable = false;

            // restore the riders; skip those that were also riding the platform, to avoid a double move.
            solidRiders.Clear();
            foreach (Actor rider in ridersBackup) {
                if (!platformRiders.Contains(rider)) {
                    solidRiders.Add(rider);
                }
            }
        }

        // variant on Solid.GetPlayerClimbing() that also checks for the jumpthru orientation.
        public static Player GetPlayerClimbing(Entity platform, bool left) {
            foreach (Player player in platform.Scene.Tracker.GetEntities<Player>()) {
                if (player.StateMachine.State == 1) {
                    if (!left && player.Facing == Facings.Left && platform.CollideCheckOutside(player, platform.Position + Vector2.UnitX)) {
                        return player;
                    }
                    if (left && player.Facing == Facings.Right && platform.CollideCheckOutside(player, platform.Position - Vector2.UnitX)) {
                        return player;
                    }
                }
            }
            return null;
        }

        public override void Render() {
            base.Render();

            if (left) {
                float rotation = (float) (-Math.PI / 2);
                textures[0].Draw(Position, new Vector2(Height, 0f), Color.White, 1f, rotation);
                for (int i = 16; i < Height; i += 8) {
                    textures[1].Draw(Position + new Vector2(0f, i), Vector2.Zero, Color.White, 1f, rotation);
                }
                textures[3].Draw(Position, new Vector2(8f, 0f), Color.White, 1f, rotation);
                textures[2].Draw(Position + new Vector2(0f, Height / 2f + 4f), Vector2.Zero, Color.White, 1f, rotation);
            } else {
                float rotation = (float) (Math.PI / 2);
                textures[0].Draw(Position, new Vector2(0f, 8f), Color.White, 1f, rotation);
                for (int i = 8; i < Height - 8f; i += 8) {
                    textures[1].Draw(Position + new Vector2(8f, i), Vector2.Zero, Color.White, 1f, rotation);
                }
                textures[3].Draw(Position + new Vector2(8f, Height - 8f), Vector2.Zero, Color.White, 1f, rotation);
                textures[2].Draw(Position + new Vector2(8f, Height / 2f - 4f), Vector2.Zero, Color.White, 1f, rotation);
            }
        }
    }
}
