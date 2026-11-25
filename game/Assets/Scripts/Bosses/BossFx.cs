using System.Collections.Generic;
using UnityEngine;

namespace Insectum.Game
{
    // Shared boss effect sprites from BossFx.png (made by ArtSource/boss_forest_pixels.py) and a small
    // pool of short-lived particles: dust, sparks and chips that fly, fall and fade on the fixed step.
    internal sealed class BossFx
    {
        internal enum Kind
        {
            Dust,
            Spark,
            Chip
        }

        private struct Particle
        {
            internal SpriteRenderer Art;
            internal Kind Kind;
            internal Vector2 Position;
            internal Vector2 Velocity;
            internal float Gravity;
            internal int Age;
            internal int Life;
            internal float Spin;
        }

        private static Texture2D atlas;
        private static readonly Sprite[] dust = new Sprite[4];
        private static readonly Sprite[] sparks = new Sprite[3];
        private static Sprite shard;
        private static Sprite shadow;
        private static Sprite ring;
        private static readonly Sprite[] waves = new Sprite[2];

        private static Texture2D Atlas => atlas ??= ForestArt.Load("BossFx");

        internal static Sprite DustFrame(int frame) =>
            dust[frame] ??= ForestArt.Frame(Atlas, frame, 0, 32, 32, new Vector2(0.5f, 0.25f));

        internal static Sprite SparkFrame(int frame) =>
            sparks[frame] ??= ForestArt.Frame(Atlas, 4 + frame, 0, 32, 32, Vector2.one * 0.5f);

        internal static Sprite Shard => shard ??= ForestArt.Frame(Atlas, 7, 0, 32, 32, Vector2.one * 0.5f);

        // Row 1 holds 64 x 16 cells, two rows of 16 below the 32-pixel first row.
        internal static Sprite Shadow => shadow ??= ForestArt.Frame(Atlas, 0, 2, 64, 16, Vector2.one * 0.5f);

        internal static Sprite Ring => ring ??= ForestArt.Frame(Atlas, 1, 2, 64, 16, Vector2.one * 0.5f);

        internal static Sprite Wave(int frame) =>
            waves[frame] ??= ForestArt.Frame(Atlas, 2 + frame, 2, 64, 16, new Vector2(0.5f, 1f / 16f));

        private readonly Transform parent;
        private readonly List<Particle> live = new();
        private readonly Stack<SpriteRenderer> spare = new();
        private SeededRandom random = new(0x2545F491u);

        internal BossFx(Transform parent) => this.parent = parent;

        internal void Spawn(Kind kind, Vector2 position, Vector2 velocity, float gravity = 0f, int life = 20)
        {
            SpriteRenderer art = spare.Count > 0 ? spare.Pop() : ForestArt.Place(parent, "Boss fx", position, null, 7);
            art.gameObject.SetActive(true);
            art.color = Color.white;
            art.transform.localScale = Vector3.one;
            art.transform.rotation = Quaternion.identity;
            live.Add(new Particle
            {
                Art = art, Kind = kind, Position = position, Velocity = velocity, Gravity = gravity, Life = life,
                Spin = kind == Kind.Chip ? Spread(540f) : 0f
            });
            Draw(live[live.Count - 1]);
        }

        // A puff of dust drifting along the ground.
        internal void Dust(Vector2 position, int direction, int count = 1, float speed = 0.9f)
        {
            for (int i = 0; i < count; i++)
                Spawn(Kind.Dust, position + new Vector2(Spread(0.5f), Spread(0.1f)),
                    new Vector2(direction * speed * (0.6f + Unit() * 0.8f) + Spread(0.6f), 0.4f + Unit() * 0.5f),
                    0f, 20 + random.Next(10));
        }

        // Bark chips flung up and away, falling back with gravity.
        internal void Chips(Vector2 position, int direction, int count)
        {
            for (int i = 0; i < count; i++)
                Spawn(Kind.Chip, position, new Vector2(direction * (1.5f + Unit() * 3f) + Spread(1.5f), 3f + Unit() * 4f),
                    26f, 40 + random.Next(20));
        }

        internal void Spark(Vector2 position) => Spawn(Kind.Spark, position, Vector2.zero, 0f, 9);

        internal void Step()
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                Particle particle = live[i];
                particle.Age++;
                particle.Velocity += Vector2.down * (particle.Gravity * PlayerMotor.StepSeconds);
                particle.Position += particle.Velocity * PlayerMotor.StepSeconds;
                if (particle.Kind == Kind.Dust)
                    particle.Velocity *= 0.93f;
                if (particle.Age >= particle.Life)
                {
                    particle.Art.gameObject.SetActive(false);
                    spare.Push(particle.Art);
                    live.RemoveAt(i);
                    continue;
                }
                live[i] = particle;
                Draw(particle);
            }
        }

        private static void Draw(Particle particle)
        {
            float t = particle.Age / (float)particle.Life;
            SpriteRenderer art = particle.Art;
            art.transform.position = PixelViewport.Snap(particle.Position);
            switch (particle.Kind)
            {
                case Kind.Dust:
                    art.sprite = DustFrame(Mathf.Min(3, (int)(t * 4f)));
                    art.color = new Color(1f, 1f, 1f, 1f - t * 0.6f);
                    break;
                case Kind.Spark:
                    art.sprite = SparkFrame(Mathf.Min(2, (int)(t * 3f)));
                    break;
                case Kind.Chip:
                    art.sprite = Shard;
                    art.transform.localScale = Vector3.one * 0.5f;
                    art.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Round(particle.Age * particle.Spin / 60f / 45f) * 45f);
                    art.color = new Color(1f, 1f, 1f, t > 0.75f ? (1f - t) * 4f : 1f);
                    break;
            }
        }

        private float Unit() => random.Next(1000) / 1000f;

        private float Spread(float amount) => (Unit() * 2f - 1f) * amount;
    }
}
