using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Insectum.Game
{
    public static class ForestGenerator
    {
        public const int BoardRadius = 2;
        private const int GenerationAttempts = 4;
        private static readonly Dictionary<WorldId, ForestCertificates> Certificates = new();

        public static LevelLayout Generate(int runSeed, int levelNumber) =>
            Generate(runSeed, levelNumber, WorldId.Forest);

        internal static LevelLayout Generate(int runSeed, int levelNumber, WorldId world)
        {
            if (!Certificates.TryGetValue(world, out ForestCertificates certificates))
            {
                certificates = ForestCertificates.Load(world);
                Certificates.Add(world, certificates);
            }
            return Generate(runSeed, levelNumber, certificates, world);
        }

        internal static LevelLayout Generate(int runSeed, int levelNumber, ForestCertificates verified, WorldId world = WorldId.Forest)
        {
            var seeds = new LevelSeeds(runSeed, levelNumber);
            var random = new SeededRandom(seeds.Layout);
            for (int attempt = 0; attempt < GenerationAttempts; attempt++)
            {
                int band = random.Next(3) - 1;
                bool mirrored = random.Next(2) == 1;
                int variants = random.Next(1 << RouteLength(band, hasPocket: true));
                LevelLayout layout = Build(runSeed, levelNumber, band, mirrored, variants, hasPocket: true, world);
                if (verified.Contains(Signature(layout)))
                    return layout;
            }

            return Fallback(runSeed, levelNumber, verified, world);
        }

        internal static LevelLayout Fallback(int runSeed, int levelNumber, ForestCertificates verified, WorldId world = WorldId.Forest)
        {
            LevelLayout layout = Build(runSeed, levelNumber, 0, false, 0, hasPocket: false, world);
            if (verified.Fallback != Signature(layout))
                throw new InvalidOperationException($"The baked {world} fallback does not match its verified terrain.");
            return layout;
        }

        internal static int RouteLength(int band, bool hasPocket) =>
            MaximumQ(band) - MinimumQ(band) + (hasPocket ? 0 : 1);

        private static int MinimumQ(int r) => Mathf.Max(-BoardRadius, -BoardRadius - r);
        private static int MaximumQ(int r) => Mathf.Min(BoardRadius, BoardRadius - r);

        internal static LevelLayout Build(int runSeed, int levelNumber, int band, bool mirrored, int variants, bool hasPocket,
            WorldId world = WorldId.Forest)
        {
            var tiles = new Dictionary<Vector2Int, TerrainMaterial>();
            var chunks = new List<MacroChunk>(19);
            var checkpoints = new List<RouteCheckpoint>();
            var markers = new List<LevelMarker>();
            var seeds = new LevelSeeds(runSeed, levelNumber);
            BiomeTemplates templates = BiomeTemplates.For(world);
            var encounterRandom = new SeededRandom(seeds.Encounters);
            var first = new Vector2Int(MinimumQ(band), band);
            var finish = first + Vector2Int.right * (RouteLength(band, hasPocket) - 1);
            var pocket = finish + Vector2Int.up;

            // The path is fixed before fitting templates or consuming encounter RNG.
            for (int r = -BoardRadius; r <= BoardRadius; r++)
            {
                for (int q = MinimumQ(r); q <= MaximumQ(r); q++)
                {
                    var coordinate = new Vector2Int(q, r);
                    Vector2Int center = ForestTemplates.Global(coordinate, Vector2Int.zero, mirrored);
                    Vector2Int boardCoordinate = mirrored ? new Vector2Int(-r, -q) : coordinate;
                    bool main = r == band && q >= first.x && q <= finish.x;
                    string template;
                    MacroPort ports;
                    if (main)
                    {
                        int index = q - first.x;
                        bool birch = (variants & (1 << index)) != 0;
                        ports = coordinate == first ? MacroPort.None : MacroPort.UpperLeft;
                        if (coordinate != finish)
                            ports |= MacroPort.LowerRight;
                        else if (hasPocket)
                            ports |= MacroPort.UpperRight;

                        bool pocketStairs = hasPocket && coordinate == finish;
                        template = templates.DescentId(ports, birch);
                        templates.StampDescent(tiles, coordinate, mirrored, birch, pocketStairs);
                        if (index > 0)
                        {
                            Vector2 landing = ForestTemplates.Landing(coordinate, ForestTemplates.Entry, mirrored);
                            checkpoints.Add(new RouteCheckpoint(landing, boardCoordinate,
                                ForestTemplates.Reflect(MacroPort.UpperLeft, mirrored), template));
                        }

                        Vector2Int encounterLedge = encounterRandom.Next(2) == 0 ? new Vector2Int(0, 0) : new Vector2Int(2, -1);
                        markers.Add(new LevelMarker(LevelMarkerKind.Encounter,
                            ForestTemplates.Landing(coordinate, encounterLedge, mirrored), boardCoordinate));
                        if (index == 1)
                        {
                            markers.Add(new LevelMarker(LevelMarkerKind.Shop,
                                ForestTemplates.Landing(coordinate, ForestTemplates.Entry, mirrored), boardCoordinate));
                        }
                    }
                    else if (hasPocket && coordinate == pocket)
                    {
                        template = templates.PocketId;
                        ports = MacroPort.LowerLeft;
                        templates.StampPocket(tiles, coordinate, mirrored);
                        Vector2 landing = ForestTemplates.Landing(coordinate, ForestTemplates.PocketLanding, mirrored);
                        markers.Add(new LevelMarker(LevelMarkerKind.Branch, landing, boardCoordinate));
                        markers.Add(new LevelMarker(LevelMarkerKind.Trap,
                            landing + Vector2.right * HexGrid.ColumnSpacing * (mirrored ? -1f : 1f) * templates.TrapOffset,
                            boardCoordinate));
                        if (templates.Shortcut is Vector2Int shortcut)
                            markers.Add(new LevelMarker(LevelMarkerKind.Shortcut,
                                HexGrid.Center(ForestTemplates.Global(coordinate, shortcut, mirrored)), boardCoordinate));
                    }
                    else
                    {
                        template = templates.ClosedId;
                        ports = MacroPort.None;
                        templates.StampClosed(tiles, coordinate, mirrored);
                    }

                    chunks.Add(new MacroChunk(boardCoordinate, center, template, ForestTemplates.Reflect(ports, mirrored), main));
                }
            }

            Vector2 spawn = ForestTemplates.Landing(first, ForestTemplates.Entry, mirrored);
            Vector2 exit = ForestTemplates.Landing(finish, ForestTemplates.Exit, mirrored) + Vector2.up * 0.55f;
            return new LevelLayout(runSeed, levelNumber, seeds, tiles, chunks, checkpoints, markers, spawn, exit,
                BoundsOf(tiles), mirrored ? -1 : 1, !hasPocket, world);
        }

        private static Rect BoundsOf(IReadOnlyDictionary<Vector2Int, TerrainMaterial> tiles)
        {
            Vector2 min = Vector2.positiveInfinity;
            Vector2 max = Vector2.negativeInfinity;
            foreach (Vector2Int cell in tiles.Keys)
            {
                Vector2 center = HexGrid.Center(cell);
                min = Vector2.Min(min, center);
                max = Vector2.Max(max, center);
            }

            return Rect.MinMaxRect(min.x - 2f, min.y - 2f, max.x + 2f, max.y + 4f);
        }

        internal static string Signature(LevelLayout layout)
        {
            ulong hash = 14695981039346656037UL;
            var cells = new List<Vector2Int>(layout.Tiles.Keys);
            cells.Sort(HexGrid.Compare);
            foreach (Vector2Int cell in cells)
            {
                AddHash(ref hash, cell.x);
                AddHash(ref hash, cell.y);
                AddHash(ref hash, (int)layout.Tiles[cell]);
            }

            foreach (MacroChunk chunk in layout.Chunks)
            {
                AddHash(ref hash, (int)chunk.Ports);
                foreach (char character in chunk.TemplateId)
                    AddHash(ref hash, character);
            }

            foreach (RouteCheckpoint checkpoint in layout.Checkpoints)
            {
                AddHash(ref hash, BitConverter.SingleToInt32Bits(checkpoint.Landing.x));
                AddHash(ref hash, BitConverter.SingleToInt32Bits(checkpoint.Landing.y));
            }

            AddHash(ref hash, BitConverter.SingleToInt32Bits(layout.Spawn.x));
            AddHash(ref hash, BitConverter.SingleToInt32Bits(layout.Spawn.y));
            AddHash(ref hash, BitConverter.SingleToInt32Bits(layout.Exit.x));
            AddHash(ref hash, BitConverter.SingleToInt32Bits(layout.Exit.y));
            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        private static void AddHash(ref ulong hash, int value) =>
            hash = unchecked((hash ^ (uint)value) * 1099511628211UL);

        internal sealed class ForestCertificates
        {
            private readonly HashSet<string> layouts = new(StringComparer.Ordinal);
            internal string Fallback { get; private set; }

            internal ForestCertificates(string source)
            {
                foreach (string raw in source.Split('\n'))
                {
                    string line = raw.Trim();
                    if (line.StartsWith("fallback ", StringComparison.Ordinal))
                        Fallback = line.Substring(9);
                    else if (line.Length != 0)
                        layouts.Add(line);
                }
            }

            internal bool Contains(string signature) => layouts.Contains(signature);

            internal static ForestCertificates Load(WorldId world = WorldId.Forest)
            {
                var asset = Resources.Load<TextAsset>($"{world}Certificates");
                if (asset == null)
                    throw new InvalidOperationException($"The baked {world} route certificate is missing.");
                return new ForestCertificates(asset.text);
            }
        }
    }
}
