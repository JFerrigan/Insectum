using System;
using System.Collections.Generic;
using UnityEngine;

namespace Insectum.Game
{
    // Purely cosmetic "inclusions" drawn over terrain tiles: fossils, roots, veins, pebbles...
    // The atlas is painted procedurally once per world so no art files are needed.
    internal sealed class FlavorSet
    {
        public Texture2D Texture;
        public readonly Dictionary<TerrainMaterial, List<Rect>> Small = new();
        public readonly Dictionary<TerrainMaterial, List<Rect>> Large = new();
    }

    internal static class FlavorArt
    {
        public const int SmallSize = 16;
        public const int LargeWidth = 48;
        public const int LargeHeight = 32;
        private const int AtlasSize = 512;

        private static readonly Dictionary<WorldId, FlavorSet> Sets = new();

        internal static FlavorSet Get(WorldId world)
        {
            if (!Sets.TryGetValue(world, out FlavorSet set) || set.Texture == null)
                Sets[world] = set = Build(world);
            return set;
        }

        // ---------- palette ----------
        private static readonly Color32 Bone = new(222, 212, 184, 255);
        private static readonly Color32 BoneShade = new(160, 148, 120, 255);
        private static readonly Color32 Fungus = new(226, 220, 204, 200);
        private static readonly Color32 FungusDim = new(190, 182, 166, 130);
        private static readonly Color32 Root = new(104, 72, 48, 255);
        private static readonly Color32 RootLight = new(140, 102, 70, 255);
        private static readonly Color32 Burrow = new(30, 20, 18, 220);
        private static readonly Color32 Stone = new(128, 124, 118, 255);
        private static readonly Color32 StoneLight = new(168, 164, 156, 255);
        private static readonly Color32 StoneDark = new(82, 78, 74, 255);
        private static readonly Color32 Quartz = new(236, 236, 240, 255);
        private static readonly Color32 Gold = new(232, 190, 70, 255);
        private static readonly Color32 Garnet = new(150, 30, 46, 255);
        private static readonly Color32 Rust = new(150, 72, 34, 255);
        private static readonly Color32 RustLight = new(196, 110, 52, 255);
        private static readonly Color32 Steel = new(120, 128, 138, 255);
        private static readonly Color32 SteelLight = new(170, 178, 188, 255);
        private static readonly Color32 SteelDark = new(70, 76, 84, 255);
        private static readonly Color32 Crack = new(20, 16, 18, 200);
        private static readonly Color32 Leaf = new(96, 80, 36, 255);
        private static readonly Color32 Reed = new(108, 112, 54, 255);
        private static readonly Color32 Bubble = new(150, 170, 150, 170);
        private static readonly Color32 Clay1 = new(156, 82, 60, 200);
        private static readonly Color32 Clay2 = new(120, 120, 128, 200);
        private static readonly Color32 Vein = new(150, 40, 120, 230);
        private static readonly Color32 VeinLight = new(214, 90, 180, 255);
        private static readonly Color32 Glow = new(120, 240, 200, 255);
        private static readonly Color32 GlowDim = new(60, 160, 150, 220);
        private static readonly Color32 Chitin = new(46, 40, 34, 255);
        private static readonly Color32 ChitinShine = new(110, 120, 90, 255);
        private static readonly Color32 Shell = new(200, 186, 160, 255);
        private static readonly Color32 Fossil = new(60, 50, 44, 230);
        private static readonly Color32 FossilLight = new(150, 136, 116, 230);
        private static readonly Color32 BogWood = new(58, 40, 26, 255);

        // ---------- painter ----------
        private sealed class Painter
        {
            private readonly Color32[] pixels;
            private readonly int ox, oy, w, h;
            public readonly System.Random Rng;

            public Painter(Color32[] pixels, int ox, int oy, int w, int h, int seed)
            {
                this.pixels = pixels; this.ox = ox; this.oy = oy; this.w = w; this.h = h;
                Rng = new System.Random(seed);
            }

            public int W => w;
            public int H => h;

            public void Set(int x, int y, Color32 c)
            {
                if (x < 0 || y < 0 || x >= w || y >= h || c.a == 0)
                    return;
                pixels[(oy + y) * AtlasSize + ox + x] = c;
            }

            public void Line(float x0, float y0, float x1, float y1, Color32 c, int thick = 1)
            {
                int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0))) + 1;
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
                    int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                    for (int dx = 0; dx < thick; dx++)
                    for (int dy = 0; dy < thick; dy++)
                        Set(x + dx, y + dy, c);
                }
            }

            public void Ellipse(float cx, float cy, float rx, float ry, Color32 c)
            {
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float nx = (x + 0.5f - cx) / rx, ny = (y + 0.5f - cy) / ry;
                    if (nx * nx + ny * ny <= 1f)
                        Set(x, y, c);
                }
            }

            public void Ring(float cx, float cy, float rx, float ry, Color32 c)
            {
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float nx = (x + 0.5f - cx) / rx, ny = (y + 0.5f - cy) / ry;
                    float d = nx * nx + ny * ny;
                    float inner = 1f - 2.2f / Mathf.Max(rx, ry);
                    if (d <= 1f && d >= inner * inner)
                        Set(x, y, c);
                }
            }

            // Shaded pebble/boulder with a highlight top-left and shadow bottom-right.
            public void Rock(float cx, float cy, float rx, float ry, Color32 mid, Color32 light, Color32 dark)
            {
                Ellipse(cx + 0.6f, cy - 0.6f, rx, ry, dark);
                Ellipse(cx, cy, rx, ry, mid);
                Ellipse(cx - rx * 0.3f, cy + ry * 0.3f, rx * 0.45f, ry * 0.4f, light);
            }

            // Random branching walk, used for mycelium, roots and veins.
            public void Walk(float x, float y, float angle, int steps, float wander, float branch, Color32 c,
                int depth = 0, float gravity = 0f)
            {
                for (int i = 0; i < steps; i++)
                {
                    Set(Mathf.RoundToInt(x), Mathf.RoundToInt(y), c);
                    angle += ((float)Rng.NextDouble() - 0.5f) * wander + gravity;
                    x += Mathf.Cos(angle);
                    y += Mathf.Sin(angle);
                    if (depth < 3 && Rng.NextDouble() < branch)
                        Walk(x, y, angle + (Rng.NextDouble() < 0.5 ? 0.9f : -0.9f), steps - i - 1 > 2 ? (steps - i) / 2 : 2,
                            wander, branch, c, depth + 1, gravity);
                }
            }

            public void Jagged(float x0, float y0, float x1, float y1, int segments, float jitter, Color32 c, int thick = 1)
            {
                float px = x0, py = y0;
                for (int i = 1; i <= segments; i++)
                {
                    float t = i / (float)segments;
                    float nx = Mathf.Lerp(x0, x1, t) + (i < segments ? ((float)Rng.NextDouble() - 0.5f) * jitter : 0);
                    float ny = Mathf.Lerp(y0, y1, t) + (i < segments ? ((float)Rng.NextDouble() - 0.5f) * jitter : 0);
                    Line(px, py, nx, ny, c, thick);
                    px = nx; py = ny;
                }
            }

            public void Spiral(float cx, float cy, float maxR, float turns, Color32 c)
            {
                int steps = (int)(turns * 60);
                for (int i = 0; i < steps; i++)
                {
                    float t = i / (float)steps;
                    float a = t * turns * Mathf.PI * 2f;
                    float r = 0.6f + t * maxR;
                    Set(Mathf.RoundToInt(cx + Mathf.Cos(a) * r), Mathf.RoundToInt(cy + Mathf.Sin(a) * r), c);
                }
            }

            public float R(float min, float max) => min + (float)Rng.NextDouble() * (max - min);
        }

        // ---------- decal designs: small (16x16) ----------
        private static void Pebble(Painter p) => p.Rock(8, 8, p.R(3, 5), p.R(2.5f, 3.5f), Stone, StoneLight, StoneDark);

        private static void Pebbles(Painter p)
        {
            for (int i = 0; i < 3; i++)
                p.Rock(p.R(4, 12), p.R(4, 12), p.R(1.5f, 2.5f), p.R(1.2f, 2f), Stone, StoneLight, StoneDark);
        }

        private static void Mycelium(Painter p)
        {
            for (int i = 0; i < 3; i++)
                p.Walk(8, 8, p.R(0, Mathf.PI * 2), 9, 1.2f, 0.18f, i == 0 ? Fungus : FungusDim);
        }

        private static void Rootlets(Painter p) => p.Walk(8, 15, -Mathf.PI / 2, 14, 0.9f, 0.25f, Root);

        private static void WormBurrow(Painter p)
        {
            float a = p.R(0, Mathf.PI);
            for (int i = 0; i < 16; i++)
            {
                float x = 2 + i * 0.8f, y = 8 + Mathf.Sin(i * 0.5f + a) * 3;
                p.Ellipse(x, y, 1.5f, 1.5f, Burrow);
            }
        }

        private static void Seed(Painter p)
        {
            p.Ellipse(8, 8, 2.5f, 1.6f, RootLight);
            p.Line(6, 8, 10, 8, Root);
        }

        private static void ShellFragment(Painter p)
        {
            for (int i = 0; i < 4; i++)
                p.Line(8, 5, 4 + i * 2.6f, 11, i % 2 == 0 ? Shell : BoneShade);
        }

        private static void BeetleHusk(Painter p)
        {
            p.Ellipse(8, 8, 3, 4.5f, Chitin);
            p.Line(8, 4, 8, 12, ChitinShine);
            p.Set(7, 13, ChitinShine);
            p.Set(9, 13, ChitinShine);
            p.Line(5, 6, 3, 4, Chitin);
            p.Line(11, 6, 13, 4, Chitin);
            p.Line(5, 10, 3, 12, Chitin);
            p.Line(11, 10, 13, 12, Chitin);
        }

        private static void Bubbles(Painter p)
        {
            for (int i = 0; i < 4; i++)
            {
                float r = p.R(1.2f, 2.8f);
                p.Ring(p.R(3, 13), p.R(3, 13), r, r, Bubble);
            }
        }

        private static void RottingLeaf(Painter p)
        {
            for (int y = 0; y < 12; y++)
            {
                float half = Mathf.Sin(y / 11f * Mathf.PI) * 4;
                p.Line(8 - half, y + 2, 8 + half, y + 2, Leaf);
            }
            p.Line(8, 1, 8, 14, Root);
            p.Set(6, 7, Burrow);
            p.Set(10, 10, Burrow);
        }

        private static void ReedStems(Painter p)
        {
            for (int i = 0; i < 3; i++)
            {
                int x = 4 + i * 4;
                p.Line(x, 1, x + (i - 1), 15, Reed);
                p.Set(x, 6 + i * 2, RootLight);
            }
        }

        private static void AntGalleries(Painter p)
        {
            p.Ellipse(4, 11, 2.5f, 1.6f, Burrow);
            p.Ellipse(12, 5, 2.5f, 1.6f, Burrow);
            p.Ellipse(11, 12, 2, 1.3f, Burrow);
            p.Line(4, 11, 12, 5, Burrow);
            p.Line(8, 8, 11, 12, Burrow);
        }

        private static void Potsherd(Painter p)
        {
            for (int y = 4; y < 12; y++)
                p.Line(4 + (y - 4) * 0.3f, y, 12 - (y - 4) * 0.2f, y, RustLight);
            p.Line(5, 7, 11, 7, Burrow);
            p.Line(5, 9, 11, 9, Bone);
        }

        private static void Cracks(Painter p)
        {
            p.Jagged(2, p.R(4, 12), 14, p.R(4, 12), 5, 4, Crack);
            p.Jagged(8, 8, p.R(4, 12), 15, 3, 3, Crack);
        }

        private static void Ammonite(Painter p)
        {
            p.Ellipse(8, 8, 6, 6, FossilLight);
            p.Spiral(8, 8, 5.5f, 2.6f, Fossil);
            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2;
                p.Line(8 + Mathf.Cos(a) * 3.5f, 8 + Mathf.Sin(a) * 3.5f, 8 + Mathf.Cos(a) * 6, 8 + Mathf.Sin(a) * 6, Fossil);
            }
        }

        private static void Trilobite(Painter p)
        {
            p.Ellipse(8, 8, 4.5f, 6.5f, FossilLight);
            p.Ellipse(8, 12, 4.5f, 2.2f, Fossil);
            for (int y = 3; y < 11; y += 2)
                p.Line(4, y, 12, y, Fossil);
            p.Line(8, 2, 8, 13, Fossil);
        }

        private static void ShellLayer(Painter p)
        {
            for (int i = 0; i < 5; i++)
                p.Ring(2 + i * 3, 7 + (i % 2), 1.8f, 1.8f, i % 2 == 0 ? Shell : FossilLight);
            p.Line(0, 5, 15, 5, BoneShade);
        }

        private static void QuartzVein(Painter p) => p.Jagged(1, p.R(2, 6), 15, p.R(10, 14), 4, 3, Quartz, 2);

        private static void MicaFlecks(Painter p)
        {
            for (int i = 0; i < 7; i++)
            {
                int x = (int)p.R(2, 14), y = (int)p.R(2, 14);
                p.Set(x, y, i % 2 == 0 ? Quartz : SteelLight);
            }
        }

        private static void GarnetCrystal(Painter p)
        {
            p.Ellipse(8, 8, 3, 3, Garnet);
            p.Set(7, 9, new Color32(220, 90, 100, 255));
            p.Set(8, 9, new Color32(220, 90, 100, 255));
        }

        private static void CrossBedding(Painter p)
        {
            for (int i = 0; i < 4; i++)
                p.Line(i * 4, 2, i * 4 + 6, 14, BoneShade);
            p.Line(0, 2, 15, 2, BoneShade);
        }

        private static void IronConcretion(Painter p)
        {
            p.Ellipse(8, 8, 4.5f, 4f, Rust);
            p.Ring(8, 8, 4.5f, 4f, RustLight);
            p.Set(7, 9, RustLight);
        }

        private static void Rivets(Painter p)
        {
            for (int i = 0; i < 4; i++)
            {
                p.Ellipse(3 + i * 3.4f, 8, 1.2f, 1.2f, SteelLight);
                p.Set(4 + (int)(i * 3.4f), 7, SteelDark);
            }
        }

        private static void RustBloom(Painter p)
        {
            for (int i = 0; i < 4; i++)
                p.Ellipse(p.R(5, 11), p.R(5, 11), p.R(1.5f, 3.5f), p.R(1.5f, 3f), i == 0 ? Rust : RustLight);
            p.Line(8, 8, 8, 1, Rust);
        }

        private static void BoltPlate(Painter p)
        {
            for (int y = 3; y < 13; y++)
                p.Line(3, y, 12, y, Steel);
            p.Line(3, 12, 12, 12, SteelLight);
            p.Line(3, 3, 12, 3, SteelDark);
            p.Set(4, 11, SteelDark); p.Set(11, 11, SteelDark); p.Set(4, 4, SteelDark); p.Set(11, 4, SteelDark);
        }

        private static void Veins(Painter p)
        {
            p.Walk(0, 8, 0, 18, 0.8f, 0.2f, Vein);
            p.Walk(8, 0, Mathf.PI / 2, 10, 0.8f, 0.2f, VeinLight);
        }

        private static void Nodules(Painter p)
        {
            for (int i = 0; i < 3; i++)
            {
                float x = p.R(4, 12), y = p.R(4, 12), r = p.R(1.5f, 2.5f);
                p.Ellipse(x, y, r + 0.8f, r + 0.8f, Vein);
                p.Ellipse(x, y, r, r, VeinLight);
            }
        }

        private static void Eggs(Painter p)
        {
            for (int i = 0; i < 4; i++)
            {
                float x = 4 + (i % 2) * 6 + p.R(-1, 1), y = 4 + (i / 2) * 6 + p.R(-1, 1);
                p.Ellipse(x, y, 2.2f, 2.8f, new Color32(210, 214, 170, 255));
                p.Set((int)x - 1, (int)y + 1, Quartz);
            }
        }

        private static void GlowCrystal(Painter p)
        {
            p.Line(8, 2, 6, 12, GlowDim, 2);
            p.Line(9, 2, 11, 10, Glow, 2);
            p.Set(9, 11, Quartz);
        }

        private static void StalactiteStub(Painter p)
        {
            for (int y = 0; y < 12; y++)
                p.Line(8 - y * 0.35f, 14 - y, 8 + y * 0.35f, 14 - y, y < 2 ? StoneLight : Stone);
        }

        private static void Scarab(Painter p)
        {
            BeetleHusk(p);
            p.Set(8, 9, Gold);
            p.Set(8, 7, Gold);
        }

        private static void BogWoodChip(Painter p)
        {
            for (int i = 0; i < 3; i++)
                p.Line(2, 6 + i * 2, 14, 8 + i * 2, i == 1 ? Root : BogWood, 1);
        }

        private static void BoneShard(Painter p)
        {
            p.Line(3, 5, 12, 11, Bone, 2);
            p.Ellipse(3, 5, 1.8f, 1.8f, Bone);
            p.Ellipse(12, 11, 1.8f, 1.8f, Bone);
            p.Line(4, 5, 12, 10, BoneShade);
        }

        // ---------- decal designs: large (48x32) ----------
        private static void BigBone(Painter p)
        {
            p.Line(8, 10, 40, 22, Bone, 4);
            p.Ellipse(7, 8, 4, 4, Bone); p.Ellipse(9, 13, 3.5f, 3.5f, Bone);
            p.Ellipse(41, 24, 4, 4, Bone); p.Ellipse(39, 19, 3.5f, 3.5f, Bone);
            p.Line(9, 9, 40, 20, BoneShade);
            p.Jagged(20, 15, 22, 19, 2, 1, Crack);
        }

        private static void Ripples(Painter p)
        {
            for (int row = 0; row < 4; row++)
                for (int x = 0; x < 47; x++)
                    p.Set(x, 8 + row * 5 + Mathf.RoundToInt(Mathf.Sin(x * 0.5f + row) * 1.5f), BoneShade);
        }

        private static void MyceliumWeb(Painter p)
        {
            for (int i = 0; i < 7; i++)
                p.Walk(24 + p.R(-4, 4), 16 + p.R(-3, 3), i / 7f * Mathf.PI * 2, 22, 0.9f, 0.14f, i % 2 == 0 ? Fungus : FungusDim);
            p.Ellipse(24, 16, 2, 1.5f, Fungus);
        }

        private static void TreeRoot(Painter p)
        {
            p.Walk(24, 31, -Mathf.PI / 2, 28, 0.6f, 0.08f, Root);
            p.Walk(25, 31, -Mathf.PI / 2 + 0.3f, 24, 0.6f, 0.1f, Root);
            p.Walk(23, 31, -Mathf.PI / 2 - 0.3f, 24, 0.6f, 0.1f, RootLight);
            p.Line(23, 31, 25, 20, Root, 3);
        }

        private static void SunkenBranch(Painter p)
        {
            p.Line(2, 12, 44, 20, BogWood, 3);
            p.Line(18, 15, 26, 26, BogWood, 2);
            p.Line(32, 18, 40, 8, BogWood, 2);
            p.Line(3, 14, 44, 22, Root);
        }

        private static void ClamBed(Painter p)
        {
            for (int i = 0; i < 8; i++)
            {
                float x = p.R(6, 42), y = p.R(6, 26);
                p.Ellipse(x, y, 3.5f, 2.5f, FossilLight);
                for (int r = 0; r < 3; r++)
                    p.Line(x, y - 2, x - 2.5f + r * 2.5f, y + 2, Fossil);
            }
        }

        private static void Boulder(Painter p)
        {
            p.Rock(24, 16, 15, 11, Stone, StoneLight, StoneDark);
            p.Jagged(14, 18, 30, 10, 3, 3, StoneDark);
        }

        private static void RootBall(Painter p)
        {
            p.Ellipse(24, 16, 10, 8, Root);
            for (int i = 0; i < 10; i++)
                p.Walk(24, 16, i / 10f * Mathf.PI * 2, 16, 0.8f, 0.12f, i % 2 == 0 ? Root : RootLight);
            p.Ellipse(22, 18, 4, 3, RootLight);
        }

        private static void ClayLens(Painter p)
        {
            for (int b = 0; b < 5; b++)
                p.Ellipse(24, 16, 20 - b * 3.5f, 10 - b * 2f, b % 2 == 0 ? Clay1 : Clay2);
        }

        private static void FishFossil(Painter p)
        {
            p.Ellipse(22, 16, 15, 7, FossilLight);
            p.Line(7, 16, 38, 16, Fossil);
            for (int x = 11; x < 34; x += 3)
                p.Line(x, 10, x + 1, 22, Fossil);
            p.Ring(11, 17, 2, 2, Fossil);
            p.Line(37, 16, 45, 24, Fossil);
            p.Line(37, 16, 45, 8, Fossil);
            p.Line(45, 8, 45, 24, Fossil);
        }

        private static void FernImprint(Painter p)
        {
            p.Line(4, 4, 44, 28, Fossil);
            for (int i = 1; i < 14; i++)
            {
                float x = 4 + i * 2.9f, y = 4 + i * 1.73f, len = 7 - Mathf.Abs(i - 6) * 0.5f;
                p.Line(x, y, x - len * 0.5f, y + len, Fossil);
                p.Line(x, y, x + len, y - len * 0.4f, Fossil);
            }
        }

        private static void OreVein(Painter p)
        {
            p.Jagged(0, 8, 47, 24, 8, 6, Quartz, 2);
            for (int i = 0; i < 8; i++)
            {
                float t = p.R(0, 1);
                p.Set(Mathf.RoundToInt(t * 47), Mathf.RoundToInt(8 + t * 16 + p.R(-1, 2)), Gold);
            }
        }

        private static void Geode(Painter p)
        {
            p.Ellipse(24, 16, 13, 11, StoneDark);
            p.Ellipse(24, 16, 10.5f, 8.5f, new Color32(120, 84, 160, 255));
            for (int i = 0; i < 20; i++)
            {
                float a = i / 20f * Mathf.PI * 2;
                p.Line(24 + Mathf.Cos(a) * 9.5f, 16 + Mathf.Sin(a) * 7.5f, 24 + Mathf.Cos(a) * 5.5f, 16 + Mathf.Sin(a) * 4f,
                    i % 2 == 0 ? new Color32(190, 150, 230, 255) : Quartz);
            }
            p.Ellipse(24, 16, 4.5f, 3, new Color32(40, 24, 56, 255));
        }

        private static void DinoFootprint(Painter p)
        {
            p.Ellipse(24, 10, 6, 5, Fossil);
            p.Line(24, 12, 24, 28, Fossil, 3);
            p.Line(22, 12, 12, 26, Fossil, 3);
            p.Line(26, 12, 36, 26, Fossil, 3);
        }

        private static void Pipe(Painter p)
        {
            for (int y = 11; y < 21; y++)
                p.Line(0, y, 47, y, y > 17 ? SteelLight : y < 13 ? SteelDark : Steel);
            for (int x = 6; x < 47; x += 14)
            {
                p.Line(x, 9, x, 22, SteelDark, 2);
                p.Ellipse(x + 3, 17, 1.5f, 1.5f, Rust);
            }
        }

        private static void Girder(Painter p)
        {
            p.Line(0, 6, 47, 6, Steel, 3);
            p.Line(0, 24, 47, 24, Steel, 3);
            for (int x = 0; x < 44; x += 8)
            {
                p.Line(x, 8, x + 8, 24, SteelDark, 2);
                p.Ellipse(x + 4, 16, 2, 2, StoneDark);
            }
            p.Line(0, 26, 47, 26, SteelLight);
        }

        private static void Rebar(Painter p)
        {
            p.Jagged(10, 0, 30, 31, 6, 5, Crack, 2);
            for (int i = 0; i < 3; i++)
            {
                int y = 6 + i * 9;
                p.Line(4, y, 44, y + 2, Rust, 2);
                for (int x = 6; x < 44; x += 3)
                    p.Set(x, y + 1 + (x - 4) / 20, RustLight);
            }
        }

        private static void Ribcage(Painter p)
        {
            p.Line(4, 16, 44, 16, VeinLight, 3);
            for (int i = 0; i < 6; i++)
            {
                float x = 10 + i * 5.5f;
                p.Line(x, 16, x - 4, 3, new Color32(230, 200, 220, 255), 2);
                p.Line(x, 18, x - 4, 29, new Color32(230, 200, 220, 255), 2);
            }
        }

        private static void Membrane(Painter p)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i < 7; i++)
                pts.Add(new Vector2(p.R(4, 44), p.R(4, 28)));
            for (int i = 0; i < pts.Count; i++)
            for (int j = i + 1; j < pts.Count; j++)
                if ((pts[i] - pts[j]).sqrMagnitude < 360)
                    p.Line(pts[i].x, pts[i].y, pts[j].x, pts[j].y, Vein);
            foreach (Vector2 pt in pts)
                p.Ellipse(pt.x, pt.y, 1.8f, 1.8f, VeinLight);
        }

        private static void CrystalCluster(Painter p)
        {
            for (int i = 0; i < 6; i++)
            {
                float x = 14 + i * 4 + p.R(-1, 1), a = -0.5f + i * 0.2f;
                float len = p.R(10, 22);
                p.Line(x, 4, x + Mathf.Sin(a) * len, 4 + Mathf.Cos(a) * len, i % 2 == 0 ? Glow : GlowDim, 3);
                p.Set(Mathf.RoundToInt(x + Mathf.Sin(a) * len), Mathf.RoundToInt(4 + Mathf.Cos(a) * len) + 1, Quartz);
            }
            p.Ellipse(24, 4, 14, 3, StoneDark);
        }

        private static void Skull(Painter p)
        {
            p.Ellipse(20, 18, 11, 9, Bone);
            p.Line(28, 12, 42, 10, Bone, 4);
            p.Ellipse(17, 19, 3, 3, Burrow);
            p.Ellipse(26, 16, 2, 1.5f, Burrow);
            for (int x = 30; x < 42; x += 3)
                p.Line(x, 9, x, 6, Bone);
            p.Jagged(12, 22, 22, 26, 3, 2, BoneShade);
        }

        // ---------- catalog ----------
        private delegate void Design(Painter p);

        private static (Design[] small, Design[] large) Catalog(WorldId world, TerrainMaterial m)
        {
            (Design[] small, Design[] large) baseSet = m switch
            {
                TerrainMaterial.Sand => (new Design[] { ShellFragment, Pebbles, BeetleHusk }, new Design[] { BigBone, Ripples }),
                TerrainMaterial.SoftDirt => (new Design[] { Mycelium, Rootlets, WormBurrow, Seed, Mycelium },
                    new Design[] { MyceliumWeb, TreeRoot }),
                TerrainMaterial.Mud => (new Design[] { Bubbles, RottingLeaf, ReedStems }, new Design[] { SunkenBranch, ClamBed }),
                TerrainMaterial.PackedDirt => (new Design[] { Pebble, Pebbles, AntGalleries, Rootlets },
                    new Design[] { Boulder, RootBall }),
                TerrainMaterial.Clay => (new Design[] { Potsherd, Pebble, Cracks }, new Design[] { ClayLens }),
                TerrainMaterial.SoftStone => (new Design[] { Ammonite, Trilobite, ShellLayer },
                    new Design[] { FishFossil, FernImprint }),
                TerrainMaterial.HardStone => (new Design[] { QuartzVein, MicaFlecks, GarnetCrystal },
                    new Design[] { OreVein, Geode }),
                TerrainMaterial.Sandstone => (new Design[] { CrossBedding, IronConcretion }, new Design[] { BigBone, DinoFootprint }),
                TerrainMaterial.Metal => (new Design[] { Rivets, RustBloom }, new Design[] { Pipe, Girder }),
                TerrainMaterial.Reinforced => (new Design[] { BoltPlate, Cracks }, new Design[] { Rebar }),
                TerrainMaterial.AlienOrganic => (new Design[] { Veins, Nodules, Eggs }, new Design[] { Ribcage, Membrane }),
                _ => (Array.Empty<Design>(), Array.Empty<Design>())
            };

            var small = new List<Design>(baseSet.small);
            var large = new List<Design>(baseSet.large);
            bool stone = m is TerrainMaterial.SoftStone or TerrainMaterial.HardStone or TerrainMaterial.Sandstone;
            bool earth = m is TerrainMaterial.SoftDirt or TerrainMaterial.Mud or TerrainMaterial.PackedDirt;
            switch (world)
            {
                case WorldId.Swamp when earth:
                    small.Add(BogWoodChip); small.Add(Bubbles);
                    large.Add(SunkenBranch);
                    break;
                case WorldId.Caves when stone:
                    small.Add(GlowCrystal); small.Add(StalactiteStub);
                    large.Add(CrystalCluster);
                    break;
                case WorldId.Desert when m is TerrainMaterial.Sand or TerrainMaterial.Sandstone or TerrainMaterial.PackedDirt:
                    small.Add(Scarab); small.Add(BoneShard);
                    large.Add(Skull);
                    break;
                case WorldId.Alien when m != TerrainMaterial.Metal && m != TerrainMaterial.Reinforced:
                    small.Add(Veins); small.Add(Nodules);
                    large.Add(Membrane);
                    break;
            }
            return (small.ToArray(), large.ToArray());
        }

        private static FlavorSet Build(WorldId world)
        {
            var pixels = new Color32[AtlasSize * AtlasSize];
            var set = new FlavorSet();
            int x = 1, y = 1, rowHeight = 0, seed = (int)world * 7919 + 17;

            Rect Place(int w, int h, Design design)
            {
                if (x + w + 1 > AtlasSize)
                {
                    x = 1;
                    y += rowHeight + 2;
                    rowHeight = 0;
                }
                if (y + h + 1 > AtlasSize)
                    throw new InvalidOperationException("Flavor atlas overflow");
                design(new Painter(pixels, x, y, w, h, seed++));
                var rect = new Rect((x + 0.5f) / AtlasSize, (y + 0.5f) / AtlasSize, (w - 1f) / AtlasSize, (h - 1f) / AtlasSize);
                x += w + 2;
                rowHeight = Mathf.Max(rowHeight, h);
                return rect;
            }

            foreach (TerrainMaterial m in Enum.GetValues(typeof(TerrainMaterial)))
            {
                (Design[] small, Design[] large) = Catalog(world, m);
                var smallRects = new List<Rect>();
                var largeRects = new List<Rect>();
                // Two randomized takes of each small design so repeats don't look stamped.
                foreach (Design d in small)
                {
                    smallRects.Add(Place(SmallSize, SmallSize, d));
                    smallRects.Add(Place(SmallSize, SmallSize, d));
                }
                foreach (Design d in large)
                    largeRects.Add(Place(LargeWidth, LargeHeight, d));
                set.Small[m] = smallRects;
                set.Large[m] = largeRects;
            }

            var texture = new Texture2D(AtlasSize, AtlasSize, TextureFormat.RGBA32, false)
            {
                name = $"{world} terrain flavor",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            set.Texture = texture;
            return set;
        }
    }
}
