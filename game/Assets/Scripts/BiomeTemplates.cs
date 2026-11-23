using System;
using System.Collections.Generic;
using UnityEngine;

namespace Insectum.Game
{
    internal sealed class BiomeTemplates
    {
        private static readonly BiomeTemplates Forest = new(ForestTemplates.DescentId, ForestTemplates.StampDescent,
            ForestTemplates.StampPocket, ForestTemplates.StampClosed, "Forest.Bracken.Pocket", "Forest.Loam.Closed", 2);
        private static readonly BiomeTemplates Swamp = new(SwampTemplates.DescentId, SwampTemplates.StampDescent,
            SwampTemplates.StampPocket, SwampTemplates.StampClosed, "Swamp.Reedbed.Plug", "Swamp.Silt.Closed", 4, SwampTemplates.Plug);
        private static readonly BiomeTemplates Caves = new(CavesTemplates.DescentId, CavesTemplates.StampDescent,
            CavesTemplates.StampPocket, CavesTemplates.StampClosed, "Caves.Geode.Plug", "Caves.Basalt.Closed", 5, CavesTemplates.Plug);

        private static readonly BiomeTemplates Desert = new(DesertTemplates.DescentId, DesertTemplates.StampDescent,
            DesertTemplates.StampPocket, DesertTemplates.StampClosed, "Desert.Oasis.Plug", "Desert.Talus.Closed", 5, DesertTemplates.Plug);

        private static readonly BiomeTemplates Alien = new(AlienTemplates.DescentId, AlienTemplates.StampDescent,
            AlienTemplates.StampPocket, AlienTemplates.StampClosed, "Alien.Nursery.Plug", "Alien.Clade.Closed", 5, AlienTemplates.Plug);

        internal readonly Func<MacroPort, bool, string> DescentId;
        internal readonly Action<Dictionary<Vector2Int, TerrainMaterial>, Vector2Int, bool, bool, bool> StampDescent;
        internal readonly Action<Dictionary<Vector2Int, TerrainMaterial>, Vector2Int, bool> StampPocket;
        internal readonly Action<Dictionary<Vector2Int, TerrainMaterial>, Vector2Int, bool> StampClosed;
        internal readonly string PocketId;
        internal readonly string ClosedId;
        internal readonly int TrapOffset;
        internal readonly Vector2Int? Shortcut;

        private BiomeTemplates(Func<MacroPort, bool, string> descentId,
            Action<Dictionary<Vector2Int, TerrainMaterial>, Vector2Int, bool, bool, bool> stampDescent,
            Action<Dictionary<Vector2Int, TerrainMaterial>, Vector2Int, bool> stampPocket,
            Action<Dictionary<Vector2Int, TerrainMaterial>, Vector2Int, bool> stampClosed,
            string pocketId, string closedId, int trapOffset, Vector2Int? shortcut = null)
        {
            DescentId = descentId;
            StampDescent = stampDescent;
            StampPocket = stampPocket;
            StampClosed = stampClosed;
            PocketId = pocketId;
            ClosedId = closedId;
            TrapOffset = trapOffset;
            Shortcut = shortcut;
        }

        internal static BiomeTemplates For(WorldId world) => world switch
        {
            WorldId.Forest => Forest,
            WorldId.Swamp => Swamp,
            WorldId.Caves => Caves,
            WorldId.Desert => Desert,
            WorldId.Alien => Alien,
            _ => throw new ArgumentOutOfRangeException(nameof(world))
        };
    }
}
