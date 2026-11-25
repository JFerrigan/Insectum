using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Insectum.Game
{
    [Flags]
    public enum MacroPort
    {
        None = 0,
        UpperRight = 1 << 0,
        LowerRight = 1 << 1,
        Bottom = 1 << 2,
        LowerLeft = 1 << 3,
        UpperLeft = 1 << 4,
        Top = 1 << 5
    }

    public enum LevelVariant
    {
        Standard,
        Night,
        HeavySpawn,
        DoubleBoss
    }

    public readonly struct LevelSeeds
    {
        public uint Layout { get; }
        public uint Encounters { get; }
        public uint Loot { get; }
        public uint Variants { get; }

        public LevelSeeds(int runSeed, int levelNumber)
        {
            uint key = SeededRandom.Mix(unchecked((uint)runSeed + (uint)levelNumber * 0x9E3779B9u));
            Layout = SeededRandom.Mix(key ^ 0xDB4F0B91u);
            Encounters = SeededRandom.Mix(key ^ 0xBBE05633u);
            Loot = SeededRandom.Mix(key ^ 0xA0F2EC75u);
            Variants = SeededRandom.Mix(key ^ 0xA54FF53Au);
        }
    }

    public struct SeededRandom
    {
        private uint state;

        public SeededRandom(uint seed) => state = seed;

        public uint NextUInt()
        {
            state = unchecked(state + 0x9E3779B9u);
            return Mix(state);
        }

        public int Next(int count)
        {
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            return (int)(NextUInt() % (uint)count);
        }

        internal static uint Mix(uint value)
        {
            unchecked
            {
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                return value ^ (value >> 16);
            }
        }
    }

    public readonly struct MacroChunk
    {
        public Vector2Int Coordinate { get; }
        public Vector2Int CenterCell { get; }
        public string TemplateId { get; }
        public MacroPort Ports { get; }
        public bool MainRoute { get; }
        public bool IsBranch => !MainRoute && Ports != MacroPort.None;

        internal MacroChunk(Vector2Int coordinate, Vector2Int centerCell, string templateId, MacroPort ports, bool mainRoute)
        {
            Coordinate = coordinate;
            CenterCell = centerCell;
            TemplateId = templateId;
            Ports = ports;
            MainRoute = mainRoute;
        }

        public bool Contains(Vector2Int cell) => HexGrid.Distance(CenterCell, cell) <= HexGrid.MacroRadius;
    }

    public readonly struct RouteCheckpoint
    {
        public Vector2 Landing { get; }
        public Vector2Int Chunk { get; }
        public MacroPort EntryPort { get; }
        public string TemplateId { get; }

        internal RouteCheckpoint(Vector2 landing, Vector2Int chunk, MacroPort entryPort, string templateId)
        {
            Landing = landing;
            Chunk = chunk;
            EntryPort = entryPort;
            TemplateId = templateId;
        }
    }

    public enum LevelMarkerKind
    {
        Encounter,
        Trap,
        Shop,
        Branch,
        Boss,
        Shortcut,
        Treasure
    }

    public readonly struct LevelMarker
    {
        public LevelMarkerKind Kind { get; }
        public Vector2 Position { get; }
        public Vector2Int Chunk { get; }
        // For an encounter a room placed itself: the enemy it asked for, or empty for the world's usual pick.
        public string Enemy { get; }

        internal LevelMarker(LevelMarkerKind kind, Vector2 position, Vector2Int chunk, string enemy = "")
        {
            Kind = kind;
            Position = position;
            Chunk = chunk;
            Enemy = enemy ?? "";
        }
    }

    public sealed class LevelLayout
    {
        public int RunSeed { get; }
        public int LevelNumber { get; }
        public WorldId World { get; }
        public int WorldLevel => RunWorlds.LocalLevel(LevelNumber);
        public LevelSeeds Seeds { get; }
        public LevelVariant Variant { get; }
        public IReadOnlyDictionary<Vector2Int, TerrainMaterial> Tiles { get; }
        public IReadOnlyList<MacroChunk> Chunks { get; }
        public IReadOnlyList<RouteCheckpoint> Checkpoints { get; }
        public IReadOnlyList<LevelMarker> Markers { get; }
        public Vector2 Spawn { get; }
        public Vector2 Exit { get; }
        public Rect Bounds { get; }
        // Where the camera's view must stay, for levels that show a fixed hall; null lets it follow freely.
        public Rect? CameraBounds { get; }
        public float KillHeight => Bounds.yMin - 2f;
        public int RouteDirection { get; }
        public bool UsedFallback { get; }
        internal ForestTunnelSpace Tunnel { get; }

        internal LevelLayout(int runSeed, int levelNumber, LevelSeeds seeds, Dictionary<Vector2Int, TerrainMaterial> tiles,
            List<MacroChunk> chunks, List<RouteCheckpoint> checkpoints, List<LevelMarker> markers,
            Vector2 spawn, Vector2 exit, Rect bounds, int routeDirection, bool usedFallback, WorldId world = WorldId.Forest,
            ForestTunnelSpace tunnel = null, Rect? cameraBounds = null)
        {
            RunSeed = runSeed;
            LevelNumber = levelNumber;
            World = world;
            Seeds = seeds;
            Variant = RunWorlds.ChooseVariant(seeds, WorldLevel);
            Tiles = new ReadOnlyDictionary<Vector2Int, TerrainMaterial>(tiles);
            Chunks = chunks.AsReadOnly();
            Checkpoints = checkpoints.AsReadOnly();
            Markers = markers.AsReadOnly();
            Spawn = spawn;
            Exit = exit;
            Bounds = bounds;
            CameraBounds = cameraBounds;
            RouteDirection = routeDirection;
            UsedFallback = usedFallback;
            Tunnel = tunnel;
        }
    }
}
