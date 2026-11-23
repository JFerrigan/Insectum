using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Insectum.Game.Editor.ProductionVerification;

namespace Insectum.Game.Editor
{
    internal static class ForestRoomVerification
    {
        private const int FirstSeed = -100;
        private const int SeedCount = 200;

        private static readonly Dictionary<RoomRole, int> RoleCounts = new()
        {
            [RoomRole.Start] = 1,
            [RoomRole.Main] = 23,
            [RoomRole.Junction] = 3,
            [RoomRole.SideTunnel] = 3,
            [RoomRole.Vault] = 3,
            [RoomRole.Finish] = 1
        };

        internal static void VerifyProfiles()
        {
            var counts = ForestRoomProfile.Shapes.ToDictionary(shape => shape, _ => 0);
            for (int seed = FirstSeed; seed < FirstSeed + SeedCount; seed++)
                Classify(ForestTunnelLayout.Generate(seed), counts);
            Classify(ForestTunnelLayout.Generate(7, forceFallback: true), new Dictionary<string, int>(counts));

            foreach (int seed in new[] { 12, 1051 })
            {
                var rooms = ForestTunnelLayout.Generate(seed).Tunnel.Rooms;
                var repeat = ForestTunnelLayout.Generate(seed).Tunnel.Rooms;
                Require(rooms.Count == repeat.Count && rooms.All(room => repeat.TryGetValue(room.Key, out var other)
                    && other.ToString() == room.Value.ToString()),
                    $"Forest tunnel seed {seed} did not reproduce its room profiles.");
            }

            string[] missing = counts.Where(count => count.Value == 0).Select(count => count.Key).ToArray();
            Require(missing.Length == 0,
                $"Forest room shapes never generated across {SeedCount} seeds: {string.Join(", ", missing)}.");
            Debug.Log($"Insectum room profiles verified across {SeedCount} seeds and the fallback. Rooms per shape: "
                + string.Join(", ", counts.OrderByDescending(count => count.Value).Select(count => $"{count.Key} {count.Value}"))
                + ".");
        }

        private static void Classify(LevelLayout layout, Dictionary<string, int> counts)
        {
            string level = $"Forest tunnel seed {layout.RunSeed}{(layout.UsedFallback ? " fallback" : "")}";
            IReadOnlyDictionary<Vector2Int, ForestRoomProfile> rooms = layout.Tunnel.Rooms;
            foreach (MacroChunk chunk in layout.Chunks)
            {
                bool carved = chunk.Ports != MacroPort.None;
                Require(rooms.TryGetValue(chunk.Coordinate, out ForestRoomProfile room) == carved,
                    $"{level}: chunk {chunk.Coordinate} {(carved ? "has no room profile" : "is solid but has a profile")}.");
                if (!carved)
                    continue;

                Require(counts.ContainsKey(room.Shape), $"{level}: chunk {chunk.Coordinate} has unknown shape {room}.");
                counts[room.Shape]++;
                Require(room.Ports == chunk.Ports,
                    $"{level}: chunk {chunk.Coordinate} profile {room} implies ports {room.Ports}, not {chunk.Ports}.");
                bool main = room.Role is RoomRole.Start or RoomRole.Main or RoomRole.Junction or RoomRole.Finish;
                Require(main == chunk.MainRoute, $"{level}: chunk {chunk.Coordinate} profile {room} disagrees with its route flag.");
                bool leftEntry = room.Entry is RoomDoor.HighLeft or RoomDoor.LowLeft;
                bool rightExit = room.Exit is RoomDoor.HighRight or RoomDoor.LowRight;
                Require(room.Role == RoomRole.Start ? room.Entry == RoomDoor.None && rightExit : leftEntry,
                    $"{level}: chunk {chunk.Coordinate} profile {room} is not mirrored to enter from the left.");
            }

            foreach (var role in RoleCounts)
                Require(rooms.Values.Count(room => room.Role == role.Key) == role.Value,
                    $"{level} has {rooms.Values.Count(room => room.Role == role.Key)} {role.Key} rooms instead of {role.Value}.");
            Require(RoleAt(layout, layout.Spawn) == RoomRole.Start && RoleAt(layout, layout.Exit) == RoomRole.Finish,
                $"{level}: the spawn and exit are not in its start and finish rooms.");
            foreach (LevelMarker marker in layout.Markers.Where(marker => marker.Kind is LevelMarkerKind.Shop or LevelMarkerKind.Branch))
                Require(rooms[marker.Chunk].Role == RoomRole.Vault,
                    $"{level}: the {marker.Kind} at {marker.Chunk} is not in a vault room.");
        }

        private static RoomRole RoleAt(LevelLayout layout, Vector2 position)
        {
            Vector2Int cell = HexGrid.CellAt(position);
            MacroChunk chunk = layout.Chunks.Single(candidate => candidate.Contains(cell));
            return layout.Tunnel.Rooms[chunk.Coordinate].Role;
        }
    }
}
