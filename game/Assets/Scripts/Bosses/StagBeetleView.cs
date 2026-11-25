using System.Collections.Generic;
using UnityEngine;

namespace Insectum.Game
{
    // Draws Old Oakhorn from its logic each fixed step: the frame for its act, a jaw glow while it winds
    // up, a white wash on hits and the last frames before an attack, a dull tint during openings, and the
    // markers that say where an attack will land.
    internal sealed class StagBeetleView
    {
        // Frame ranges in ForestBoss.png, in the order ArtSource/boss_forest_pixels.py draws them.
        private static class Anims
        {
            internal const int Idle = 0, Walk = 4, Turn = 10, ChargeWindup = 12, ChargeRun = 18, Stuck = 22, Wrench = 26,
                Snap = 29, Rear = 33, WingOpen = 39, Hover = 43, Dive = 47, Land = 49, Dash = 52, Roar = 54, Death = 58,
                Slam = 68;
            internal const int Count = 73;
        }

        private const int Columns = 16;
        private const int CellWidth = 128, CellHeight = 112;
        private const float GroundPixels = 12f;
        private static readonly Color Amber = new Color32(255, 170, 64, 255);
        private static readonly Color MarkerRed = new Color32(240, 72, 56, 255);
        private static readonly Sprite[] frames = new Sprite[Anims.Count];
        private static readonly Sprite[] glows = new Sprite[Anims.Count];
        private static Texture2D bodyAtlas;
        private static Texture2D glowAtlas;

        private readonly StagBeetle beetle;
        private readonly Transform root;
        private readonly SpriteRenderer body;
        private readonly SpriteRenderer glow;
        private readonly Material bodyMaterial;
        private readonly SpriteRenderer shadow;
        private readonly SpriteRenderer target;
        private readonly SpriteRenderer[] waves = new SpriteRenderer[2];
        private readonly List<SpriteRenderer> shards = new();
        private readonly List<SpriteRenderer> shardShadows = new();
        private readonly Transform parent;
        private readonly BossFx fx;
        private int tick;
        private int hitFlash;

        internal StagBeetleView(Transform parent, StagBeetle beetle)
        {
            this.beetle = beetle;
            this.parent = parent;
            root = new GameObject(beetle.Name).transform;
            root.SetParent(parent, false);
            var flash = Resources.Load<Shader>("BossFlash");
            bodyMaterial = new Material(flash);
            body = ForestArt.Place(root, "Body", Vector2.zero, Frame(0), 5);
            body.sharedMaterial = bodyMaterial;
            glow = ForestArt.Place(root, "Jaw glow", Vector2.zero, Glow(0), 6);
            glow.sharedMaterial = new Material(flash);
            glow.sharedMaterial.SetColor("_Flash", Amber);
            glow.enabled = false;
            shadow = ForestArt.Place(parent, "Beetle shadow", Vector2.zero, BossFx.Shadow, 3);
            shadow.color = new Color(0f, 0f, 0f, 0.35f);
            target = ForestArt.Place(parent, "Dive target", Vector2.zero, BossFx.Ring, 3);
            target.enabled = false;
            for (int i = 0; i < waves.Length; i++)
            {
                waves[i] = ForestArt.Place(parent, "Shockwave", Vector2.zero, BossFx.Wave(0), 6);
                waves[i].enabled = false;
            }
            fx = new BossFx(parent);
            beetle.Damage.Resolved += (_, outcome) =>
            {
                if (outcome == DamageOutcome.Applied)
                    hitFlash = 4;
            };
            Update();
        }

        private static Sprite Frame(int index) =>
            frames[index] ??= ForestArt.Frame(bodyAtlas ??= ForestArt.Load("ForestBoss"), index % Columns, index / Columns,
                CellWidth, CellHeight, new Vector2(0.5f, GroundPixels / CellHeight));

        private static Sprite Glow(int index) =>
            glows[index] ??= ForestArt.Frame(glowAtlas ??= ForestArt.Load("ForestBossGlow"), index % Columns, index / Columns,
                CellWidth, CellHeight, new Vector2(0.5f, GroundPixels / CellHeight));

        internal void Update()
        {
            tick++;
            if (hitFlash > 0)
                hitFlash--;
            // Quiver a pixel side to side once it has risen for a body slam.
            bool quiver = beetle.CurrentAct == StagBeetle.Act.SlamWindup && beetle.ActProgress >= 0.4f;
            root.position = PixelViewport.Snap(beetle.Position + Vector2.right * (quiver ? (tick % 4 < 2 ? 1f : -1f) / 16f : 0f));
            int index = FrameIndex();
            body.sprite = Frame(index);
            body.flipX = beetle.Facing < 0;
            glow.sprite = Glow(index);
            glow.flipX = body.flipX;

            StagBeetle.Glow telegraph = beetle.CurrentGlow;
            glow.enabled = telegraph == StagBeetle.Glow.Amber;
            glow.color = new Color(1f, 1f, 1f, 0.45f + 0.3f * Mathf.Sin(tick * 0.5f));
            float wash = hitFlash > 0 ? 0.9f : telegraph == StagBeetle.Glow.Flash ? 0.75f : 0f;
            bodyMaterial.SetColor("_Flash", new Color(1f, 0.97f, 0.9f, wash));
            body.color = BodyTint();

            DrawShadow();
            DrawMarkers();
            DrawHazards();
            PlayCues();
            fx.Step();
        }

        private Color BodyTint()
        {
            if (beetle.CurrentAct == StagBeetle.Act.DeathFade || beetle.State == Boss.Mode.Dead)
            {
                float fade = beetle.State == Boss.Mode.Dead ? 1f : beetle.ActProgress;
                return Color.Lerp(Color.white, new Color(0.45f, 0.4f, 0.38f), fade);
            }
            return beetle.Opening ? new Color(0.8f, 0.76f, 0.72f) : Color.white;
        }

        private int FrameIndex()
        {
            float p = beetle.ActProgress;
            int Span(int start, int count) => start + Mathf.Min(count - 1, (int)(p * count));
            switch (beetle.CurrentAct)
            {
                case StagBeetle.Act.IntroRest:
                case StagBeetle.Act.Idle:
                    return Anims.Idle + tick / 10 % 4;
                case StagBeetle.Act.IntroRoar:
                    return p < 0.2f ? Anims.Rear + (p < 0.1f ? 0 : 1) : p < 0.8f ? Anims.Roar + tick / 6 % 4 : Anims.Rear + 4 + (p < 0.9f ? 0 : 1);
                case StagBeetle.Act.Walk:
                    return Anims.Walk + tick / 6 % 6;
                case StagBeetle.Act.Turn:
                    return Span(Anims.Turn, 2);
                case StagBeetle.Act.Roar:
                    return Anims.Roar + tick / 6 % 4;
                case StagBeetle.Act.ChargeWindup:
                    return Span(Anims.ChargeWindup, 6);
                case StagBeetle.Act.ChargeRun:
                    return Anims.ChargeRun + tick / 3 % 4;
                case StagBeetle.Act.ChargeSkid:
                    return Anims.ChargeRun;
                case StagBeetle.Act.Stuck:
                    return Anims.Stuck + tick / 3 % 4;
                case StagBeetle.Act.Wrench:
                    return Span(Anims.Wrench, 3);
                case StagBeetle.Act.ChargeRecover:
                    return p < 0.5f ? Anims.Wrench + 2 : Anims.Idle;
                case StagBeetle.Act.SnapWindup:
                    return Anims.Snap + (p < 0.5f ? 0 : 1);
                case StagBeetle.Act.Snap:
                    return Anims.Snap + 2;
                case StagBeetle.Act.SnapRecover:
                    return p < 0.7f ? Anims.Snap + 3 : Anims.Idle;
                case StagBeetle.Act.RearWindup:
                    return Anims.Rear + (p < 0.5f ? 0 : 1);
                case StagBeetle.Act.RearSnap:
                    return Anims.Rear + (p < 0.4f ? 2 : 3);
                case StagBeetle.Act.RearRecover:
                    return Anims.Rear + (p < 0.5f ? 4 : 5);
                case StagBeetle.Act.SlamWindup:
                    return Anims.Slam + (p < 0.4f ? 0 : 1);
                case StagBeetle.Act.Slam:
                    return Anims.Slam + 2;
                case StagBeetle.Act.SlamRecover:
                    return Anims.Slam + (p < 0.4f ? 2 : p < 0.7f ? 3 : 4);
                case StagBeetle.Act.WingOpen:
                    return Span(Anims.WingOpen, 4);
                case StagBeetle.Act.LiftOff:
                case StagBeetle.Act.HoverTrack:
                case StagBeetle.Act.DiveLock:
                case StagBeetle.Act.GoreRise:
                case StagBeetle.Act.SoftDrop:
                    return Anims.Hover + tick / 2 % 4;
                case StagBeetle.Act.DiveDrop:
                    return Anims.Dive + tick / 2 % 2;
                case StagBeetle.Act.Land:
                case StagBeetle.Act.SoftLand:
                    return p < 0.3f ? Anims.Land + Mathf.Min(2, (int)(p / 0.3f * 3f)) : Anims.Idle;
                case StagBeetle.Act.GoreAim:
                case StagBeetle.Act.GoreDash:
                    return Anims.Dash + tick / 2 % 2;
                case StagBeetle.Act.DeathFall:
                    return Anims.Dive;
                case StagBeetle.Act.DeathKneel:
                    return Span(Anims.Death, 5);
                case StagBeetle.Act.DeathPause:
                    return Anims.Death + 4;
                case StagBeetle.Act.DeathDrop:
                    return Span(Anims.Death + 5, 4);
                default:
                    return Anims.Death + 9;
            }
        }

        private float Floor => BossArenaLayout.FloorY;

        private void DrawShadow()
        {
            float height = beetle.Position.y - Floor;
            shadow.enabled = beetle.State != Boss.Mode.Dead && beetle.CurrentAct != StagBeetle.Act.DeathFade;
            float spread = Mathf.Clamp(1.1f - height * 0.05f, 0.5f, 1.1f);
            shadow.transform.position = PixelViewport.Snap(new Vector2(beetle.Position.x, Floor + 0.06f));
            shadow.transform.localScale = new Vector3(spread * 1.5f, spread, 1f);
            shadow.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.35f, 0.12f, height / 11f));
        }

        private void DrawMarkers()
        {
            float? dive = beetle.DiveMarker;
            target.enabled = dive.HasValue;
            if (dive.HasValue)
            {
                float strength = dive.Value;
                target.transform.position = PixelViewport.Snap(new Vector2(beetle.Position.x, Floor + 0.12f));
                target.transform.localScale = new Vector3(1.6f - strength * 0.5f, 1f, 1f);
                bool blink = strength >= 1f && tick / 3 % 2 == 0;
                target.color = blink ? Color.white : new Color(MarkerRed.r, MarkerRed.g, MarkerRed.b, 0.35f + strength * 0.65f);
            }
        }

        private void DrawHazards()
        {
            IReadOnlyList<StagBeetle.Shockwave> live = beetle.Shockwaves;
            for (int i = 0; i < waves.Length; i++)
            {
                bool on = i < live.Count;
                waves[i].enabled = on;
                if (!on)
                    continue;
                waves[i].sprite = BossFx.Wave(tick / 4 % 2);
                waves[i].flipX = live[i].Direction < 0;
                waves[i].transform.position = PixelViewport.Snap(new Vector2(live[i].X, Floor));
            }

            IReadOnlyList<StagBeetle.Shard> falling = beetle.Shards;
            while (shards.Count < falling.Count)
            {
                shards.Add(ForestArt.Place(parent, "Bark shard", Vector2.zero, BossFx.Shard, 6));
                SpriteRenderer under = ForestArt.Place(parent, "Shard shadow", Vector2.zero, BossFx.Shadow, 3);
                under.color = new Color(0f, 0f, 0f, 0.4f);
                shardShadows.Add(under);
            }
            for (int i = 0; i < shards.Count; i++)
            {
                bool on = i < falling.Count;
                shards[i].enabled = shardShadows[i].enabled = on;
                if (!on)
                    continue;
                Vector2 at = falling[i].Position;
                shards[i].transform.position = PixelViewport.Snap(at);
                shards[i].transform.rotation = Quaternion.Euler(0f, 0f, tick * 12 % 360 / 45 * 45f);
                float drop = Mathf.Clamp01((at.y - Floor) / 6f);
                shardShadows[i].transform.position = PixelViewport.Snap(new Vector2(at.x, Floor + 0.06f));
                shardShadows[i].transform.localScale = new Vector3(0.45f - drop * 0.15f, 0.6f, 1f);
                shardShadows[i].color = new Color(0f, 0f, 0f, 0.55f - drop * 0.3f);
            }
        }

        private void PlayCues()
        {
            foreach (BossCue cue in beetle.Cues)
            {
                switch (cue.Kind)
                {
                    case BossCueKind.Dust:
                        fx.Dust(cue.Position, cue.Direction);
                        break;
                    case BossCueKind.WallHit:
                        fx.Dust(cue.Position, -cue.Direction, 3, 1.6f);
                        fx.Chips(cue.Position, -cue.Direction, 6);
                        fx.Spark(cue.Position);
                        break;
                    case BossCueKind.Land:
                        fx.Dust(cue.Position + Vector2.left * 1.5f, -1, 3, 2f);
                        fx.Dust(cue.Position + Vector2.right * 1.5f, 1, 3, 2f);
                        break;
                    case BossCueKind.Roar:
                        fx.Dust(beetle.Position + Vector2.left * 2f, -1, 2, 1.2f);
                        fx.Dust(beetle.Position + Vector2.right * 2f, 1, 2, 1.2f);
                        break;
                    case BossCueKind.Clank:
                        fx.Spark(cue.Position);
                        break;
                    case BossCueKind.Thud:
                        fx.Dust(cue.Position + Vector2.left * 2f, -1, 4, 1.8f);
                        fx.Dust(cue.Position + Vector2.right * 2f, 1, 4, 1.8f);
                        break;
                    case BossCueKind.WingBeat:
                        fx.Dust(cue.Position + new Vector2(-cue.Direction, 0f), -cue.Direction, 1, 1.4f);
                        break;
                    case BossCueKind.Shatter:
                        fx.Chips(cue.Position + Vector2.up * 0.2f, 0, 3);
                        fx.Dust(cue.Position, 0, 1, 0.4f);
                        break;
                }
            }
            beetle.ClearCues();
        }
    }
}
