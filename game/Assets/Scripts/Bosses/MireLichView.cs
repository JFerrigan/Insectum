using System.Collections.Generic;
using UnityEngine;

namespace Insectum.Game
{
    internal sealed class MireLichView : IBossView
    {
        private sealed class Droplet
        {
            internal SpriteRenderer Art;
            internal Vector2 Position;
            internal Vector2 Velocity;
            internal float Size;
            internal int Age;
            internal bool Splatter;
        }

        private static readonly Sprite[] frames = new Sprite[MireLichArt.FramesPerStage * 3];
        private static readonly Sprite[] masks = new Sprite[frames.Length];
        private static readonly Sprite[] effects = new Sprite[4];
        private static Texture2D atlas;
        private static Texture2D glowAtlas;
        private static Texture2D fxAtlas;
        private static Material material;
        private static readonly Color White = Color.white;
        private static readonly Color Dull = new Color32(179, 169, 169, 255);
        private static readonly Color Dark = new Color32(98, 78, 95, 255);

        private readonly MireLich queen;
        private readonly Transform parent;
        private readonly SpriteRenderer body;
        private readonly SpriteRenderer glow;
        private readonly SpriteRenderer shadow;
        private readonly SpriteRenderer marker;
        private readonly MaterialPropertyBlock wash = new();
        private readonly BossFx dust;
        private readonly List<SpriteRenderer> hazardArt = new();
        private readonly List<Droplet> droplets = new();
        private SeededRandom random = new(0x1D70C0Du);
        private int tick;
        private int flash;

        internal MireLichView(Transform parent, MireLich queen)
        {
            this.parent = parent;
            this.queen = queen;
            material ??= new Material(Resources.Load<Shader>("BossFlash"));
            body = ForestArt.Place(parent, queen.Name, queen.Position, Frame(queen.ArtFrame), 5);
            body.sharedMaterial = material;
            glow = ForestArt.Place(body.transform, "Warning glow", Vector2.zero, null, 6);
            glow.sharedMaterial = material;
            shadow = ForestArt.Place(parent, "Queen shadow", queen.Position, BossFx.Shadow, 3);
            marker = ForestArt.Place(parent, "Locked ground shadow", queen.Position, BossFx.Shadow, 4);
            dust = new BossFx(parent);
            queen.Damage.Resolved += (_, result) =>
            {
                if (result == DamageOutcome.Applied)
                    flash = 4;
            };
            Update();
        }

        internal static Sprite Portrait => Frame((int)MireLichPose.Hover0);

        private static Sprite Frame(int index, bool mask = false)
        {
            Sprite[] cache = mask ? masks : frames;
            Texture2D texture = mask
                ? glowAtlas ??= ForestArt.Load("SwampBossGlow")
                : atlas ??= ForestArt.Load("SwampBoss");
            return cache[index] ??= ForestArt.Frame(texture, index % MireLichArt.Columns, index / MireLichArt.Columns,
                MireLichArt.Width, MireLichArt.Height, new Vector2(0.5f, MireLichArt.GroundPixels / (float)MireLichArt.Height));
        }

        private static Sprite Effect(int index) => effects[index] ??= ForestArt.Frame(
            fxAtlas ??= ForestArt.Load("SwampBossFx"), index, 0, 32, 32, Vector2.one * 0.5f);

        public void Update()
        {
            tick++;
            int frame = queen.ArtFrame;
            body.sprite = Frame(frame);
            body.flipX = queen.Facing < 0;
            Vector2 position = queen.Position;
            if (queen.CurrentAct == MireLich.Act.Swell)
                position.x += (tick % 4 < 2 ? -1f : 1f) * queen.ActProgress * 0.18f;
            body.transform.position = PixelViewport.Snap(position);

            body.color = queen.CurrentAct == MireLich.Act.Fade
                ? Color.Lerp(White, Dark, queen.DeathFinished ? 1f : queen.ActProgress)
                : queen.Opening ? Dull : White;
            SetWash(body, new Color(1f, 0.99f, 0.98f, flash > 0 || queen.Flash ? 0.88f : 0f));
            flash = Mathf.Max(0, flash - 1);

            glow.enabled = queen.CurrentAct == MireLich.Act.Warning && !queen.Flash
                || queen.CurrentAct is MireLich.Act.Swell or MireLich.Act.Stagger;
            if (glow.enabled)
            {
                glow.sprite = Frame(frame, mask: true);
                glow.flipX = body.flipX;
                Color color = queen.CurrentAct == MireLich.Act.Swell ? new Color32(255, 77, 112, 255)
                    : queen.CurrentMove is MireLich.Move.Bubbles or MireLich.Move.Larvae
                    ? new Color32(172, 247, 120, 255) : new Color32(255, 147, 75, 255);
                SetWash(glow, color);
                glow.color = new Color(1f, 1f, 1f, 0.26f + 0.21f * Mathf.Sin(tick * 0.39f));
            }

            DrawShadows();
            DrawHazards();
            foreach (BossCue cue in queen.Cues)
            {
                if (cue.Kind == BossCueKind.Shatter)
                    Burst(cue.Position);
                else if (cue.Kind is BossCueKind.Thud or BossCueKind.Land)
                    dust.Dust(cue.Position, 0, 4);
                else if (cue.Kind == BossCueKind.WingBeat)
                    dust.Dust(cue.Position, cue.Direction, cue.Direction == 0 ? 2 : 7, 3f);
            }
            queen.ClearCues();
            dust.Step();
            StepDroplets();
        }

        private void SetWash(SpriteRenderer renderer, Color color)
        {
            wash.SetColor("_Flash", color);
            renderer.SetPropertyBlock(wash);
        }

        private void DrawShadows()
        {
            Vector2 position = queen.Position;
            Vector2 surface = queen.Surface(position.x, position.y + 0.02f);
            shadow.enabled = !queen.DeathFinished;
            shadow.transform.position = PixelViewport.Snap(surface);
            shadow.transform.localScale = new Vector3(0.62f, 0.59f, 1f);
            shadow.color = new Color(0f, 0f, 0f, 0.40f - 0.16f * Mathf.Clamp01((position.y - surface.y) / 10f));
            marker.enabled = queen.Marker.HasValue;
            if (queen.Marker is not Vector2 locked)
                return;
            marker.transform.position = PixelViewport.Snap(locked);
            bool land = queen.CurrentMove == MireLich.Move.Feed;
            marker.transform.localScale = new Vector3(land ? 0.83f : 0.50f, 0.76f, 1f);
            marker.color = land ? new Color(0.06f, 0.08f, 0.1f, 0.65f)
                : new Color(0.42f, 0.10f, 0.15f, queen.Flash ? 0.86f : 0.58f);
        }

        private void DrawHazards()
        {
            int i = 0;
            foreach (MireLich.Hazard hazard in queen.Hazards)
            {
                if (i == hazardArt.Count)
                    hazardArt.Add(ForestArt.Place(parent, "Queen hazard", Vector2.zero, null, 6));
                SpriteRenderer art = hazardArt[i++];
                art.enabled = true;
                art.color = White;
                art.sortingOrder = 6;
                art.flipX = hazard.Direction.x < 0f;
                art.transform.position = PixelViewport.Snap(hazard.Position);
                art.transform.localScale = Vector3.one;
                switch (hazard.Kind)
                {
                    case MireLich.HazardKind.Glob:
                        art.sprite = Effect(0);
                        art.transform.localScale = Vector3.one * 0.62f;
                        break;
                    case MireLich.HazardKind.Puddle:
                        art.sprite = Effect(1);
                        art.sortingOrder = 4;
                        art.transform.position = PixelViewport.Snap(hazard.Position + Vector2.up * 0.17f);
                        art.transform.localScale = new Vector3(0.8f, 0.40f + 0.05f * Mathf.Sin(hazard.Age * 0.42f), 1f);
                        break;
                    case MireLich.HazardKind.Bubble:
                        art.sprite = Effect(2);
                        art.transform.localScale = Vector3.one * (0.73f + 0.04f * Mathf.Sin(hazard.Age * 0.43f));
                        art.color = new Color(0.94f, 1f, 0.91f, 0.95f);
                        break;
                    case MireLich.HazardKind.Cloud:
                        art.sprite = Effect(2);
                        art.color = new Color(0.62f, 0.92f, 0.50f, 0.75f - hazard.Age * 0.018f);
                        art.transform.localScale = new Vector3(1.03f + hazard.Age * 0.009f, 0.76f, 1f);
                        break;
                    default:
                        art.sprite = Effect(3);
                        if (hazard.Kind == MireLich.HazardKind.LarvaWarning)
                            art.color = queen.Flash ? White
                                : new Color(1f, 0.67f + 0.18f * Mathf.Sin(hazard.Age * 0.64f), 0.39f, 1f);
                        else if (hazard.Kind == MireLich.HazardKind.LarvaDart)
                            art.transform.localScale = new Vector3(1.19f, 0.85f, 1f);
                        break;
                }
            }

            for (; i < hazardArt.Count; i++)
                hazardArt[i].enabled = false;
        }

        private void Burst(Vector2 position)
        {
            for (int i = 0; i < 40; i++)
            {
                float x = (random.Next(1001) - 500) / 100f;
                float y = 1.75f + random.Next(801) / 100f;
                float size = 0.14f + random.Next(23) * 0.01f;
                var art = ForestArt.Place(parent, "Blood droplet", position, Effect(0), 7);
                art.transform.localScale = Vector3.one * size;
                droplets.Add(new Droplet { Art = art, Position = position, Velocity = new Vector2(x, y), Size = size });
            }
        }

        private void StepDroplets()
        {
            for (int i = droplets.Count - 1; i >= 0; i--)
            {
                Droplet bead = droplets[i];
                if (!bead.Splatter)
                {
                    bead.Velocity += Vector2.down * (23f / 60f);
                    Vector2 next = bead.Position + bead.Velocity / 60f;
                    if (queen.SweepTerrain(bead.Position, next, out Vector2 impact))
                    {
                        bead.Splatter = true;
                        bead.Age = 0;
                        bead.Position = impact + Vector2.up * 0.03f;
                        bead.Art.sprite = Effect(1);
                        bead.Art.sortingOrder = 4;
                        bead.Art.transform.localScale = new Vector3(bead.Size * 1.65f, bead.Size * 0.65f, 1f);
                    }
                    else
                        bead.Position = next;
                    bead.Art.transform.position = PixelViewport.Snap(bead.Position);
                }
                else
                    bead.Art.color = new Color(0.79f, 0.57f, 0.64f, 1f - bead.Age / 360f);

                if (++bead.Age < (bead.Splatter ? 360 : 150))
                    continue;
                Object.Destroy(bead.Art.gameObject);
                droplets.RemoveAt(i);
            }
        }
    }
}
