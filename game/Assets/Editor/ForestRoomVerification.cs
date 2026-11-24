using System;
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
        // Unlinked rooms keep this much rock between them, so templates can grow without merging.
        private const float MinimumRock = 1.5f;
        // The motor treats surfaces up to about 47 degrees as floor.
        private const float WalkableSlope = 47f;

        private static readonly Dictionary<RoomRole, int> RoleCounts = new()
        {
            [RoomRole.Start] = 1,
            [RoomRole.Main] = 23,
            [RoomRole.Junction] = 3,
            [RoomRole.SideTunnel] = 3,
            [RoomRole.Vault] = 3,
            [RoomRole.Finish] = 1
        };

        internal static void VerifyProfiles() => Debug.Log(CheckProfiles());

        internal static string CheckProfiles()
        {
            var counts = ForestRoomProfile.Shapes.ToDictionary(shape => shape, _ => 0);
            float closest = float.MaxValue;
            float steepest = 0f;
            for (int seed = FirstSeed; seed < FirstSeed + SeedCount; seed++)
                Check(ForestTunnelLayout.Generate(seed), counts, ref closest, ref steepest);
            Check(ForestTunnelLayout.Generate(7, forceFallback: true), new Dictionary<string, int>(counts), ref closest, ref steepest);

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

            // Every non-boss level of every world is a tunnel built from that world's templates.
            var templates = new Dictionary<WorldId, HashSet<string>>();
            int levels = 0;
            foreach (WorldId world in Enum.GetValues(typeof(WorldId)))
            {
                // Rooms and the suites that span several chunks, all of which should turn up.
                List<ForestRoomTemplate> catalog = ForestRoomCatalog.For(world).Concat(ForestSuites.For(world)).ToList();
                templates[world] = new HashSet<string>();
                for (int seed = -30; seed < 30; seed++)
                {
                    int level = RunWorlds.FirstLevel(world) + (seed + 30) % (RunWorlds.LevelsPerWorld - 1);
                    LevelLayout layout = RunWorlds.Generate(seed, level);
                    Require(layout.World == world && layout.LevelNumber == level && layout.Tunnel != null,
                        $"{world} seed {seed}, level {level} is not a {world} tunnel.");
                    Check(layout, new Dictionary<string, int>(counts), ref closest, ref steepest);
                    foreach (MacroChunk chunk in layout.Chunks.Where(chunk => chunk.Ports != MacroPort.None))
                    {
                        Require(catalog.Any(template => template.Id == chunk.TemplateId),
                            $"{world} seed {seed} used {chunk.TemplateId}, which is not one of its templates.");
                        templates[world].Add(chunk.TemplateId);
                    }
                    levels++;
                }
                Require(templates[world].Count == catalog.Count,
                    $"{world} never used {string.Join(", ", catalog.Select(t => t.Id).Except(templates[world]))}.");
            }

            return $"Insectum room profiles verified across {SeedCount} Forest 1 seeds, the fallback, and {levels} levels "
                + $"across all five worlds. Unlinked spaces keep {closest:F2} units of rock, the steepest walkable floor is "
                + $"{steepest:F1} degrees. Templates per world: "
                + string.Join(", ", templates.Select(world => $"{world.Key} {world.Value.Count}")) + ". Rooms per shape: "
                + string.Join(", ", counts.OrderByDescending(count => count.Value).Select(count => $"{count.Key} {count.Value}"))
                + ".";
        }

        private static void Check(LevelLayout layout, Dictionary<string, int> counts, ref float closest, ref float steepest)
        {
            string level = $"Forest tunnel seed {layout.RunSeed}{(layout.UsedFallback ? " fallback" : "")}";
            ForestTunnelSpace tunnel = layout.Tunnel;
            IReadOnlyDictionary<Vector2Int, ForestRoomProfile> rooms = tunnel.Rooms;
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
            foreach (var branch in tunnel.Branches)
                Require(tunnel.Jumps.ContainsKey((tunnel.Route[branch.source], branch.first)),
                    $"{level}: side tunnel {branch.source} has no marked ledge for its sign.");
            LevelMarker[] plugs = layout.Markers.Where(marker => marker.Kind == LevelMarkerKind.Shortcut).ToArray();
            Require(plugs.Length == 1 && rooms[plugs[0].Chunk].Role == RoomRole.Vault
                    && layout.Tiles.TryGetValue(HexGrid.CellAt(plugs[0].Position), out TerrainMaterial plug)
                    && plug == ForestTunnelLayout.PlugMaterial(layout.World)
                    && layout.Tiles.Values.Count(material => material is not (TerrainMaterial.Reinforced or TerrainMaterial.Boundary)) == 1,
                $"{level} does not have exactly one {ForestTunnelLayout.PlugMaterial(layout.World)} plug under a chest vault.");

            float[] walls = layout.Tiles.Where(tile => tile.Value == TerrainMaterial.Boundary)
                .Select(tile => HexGrid.Center(tile.Key).x).ToArray();
            float west = tunnel.Route.Min(landing => landing.x) - 5f, east = tunnel.Route.Max(landing => landing.x) + 5f;
            Require(walls.Any(x => x < west) && walls.Any(x => x > east) && walls.All(x => x < west || x > east),
                $"{level} is missing its indestructible side walls or has wall inside the map.");

            CheckSpacing(level, tunnel, ref closest, ref steepest);
        }

        // Each passage belongs to a room suite (its first chunk) or a link (its two chunks, read as the suites
        // they join). Spaces that are not joined by a link must keep MinimumRock between them, and every floor
        // must stay walkable.
        private static void CheckSpacing(string level, ForestTunnelSpace tunnel, ref float closest, ref float steepest)
        {
            object Suite(object owner) => owner is ValueTuple<Vector2Int, Vector2Int> link
                ? (tunnel.SuiteOf[link.Item1], tunnel.SuiteOf[link.Item2]) : owner;
            var passages = tunnel.Passages.Select(passage => (owner: Suite(passage.owner), passage.outline)).ToList();
            var links = new HashSet<(Vector2Int, Vector2Int)>();
            foreach (var (owner, _) in passages)
                if (owner is ValueTuple<Vector2Int, Vector2Int> link)
                {
                    links.Add(link);
                    links.Add((link.Item2, link.Item1));
                }

            for (int i = 0; i < passages.Count; i++)
            {
                var (owner, outline) = passages[i];
                Vector2 floor = outline[1] - outline[0];
                float angle = Mathf.Atan2(Mathf.Abs(floor.y), Mathf.Max(floor.x, 0.0001f)) * Mathf.Rad2Deg;
                // Steep bottom edges are cave walls, not route floors, so this is only reported. The physics
                // walker proves the route itself can be walked.
                if (angle <= WalkableSlope)
                    steepest = Mathf.Max(steepest, angle);

                for (int j = i + 1; j < passages.Count; j++)
                {
                    var (other, shape) = passages[j];
                    if (Joined(owner, other, links))
                        continue;
                    float gap = Gap(outline, shape);
                    closest = Mathf.Min(closest, gap);
                    Require(gap >= MinimumRock, $"{level}: {owner} and {other} have only {gap:F2} units of rock between them.");
                }
            }
        }

        private static bool Joined(object a, object b, HashSet<(Vector2Int, Vector2Int)> links) => (a, b) switch
        {
            (Vector2Int x, Vector2Int y) => x == y || links.Contains((x, y)),
            (Vector2Int room, ValueTuple<Vector2Int, Vector2Int> link) => link.Item1 == room || link.Item2 == room,
            (ValueTuple<Vector2Int, Vector2Int> link, Vector2Int room) => link.Item1 == room || link.Item2 == room,
            (ValueTuple<Vector2Int, Vector2Int> x, ValueTuple<Vector2Int, Vector2Int> y) =>
                x.Item1 == y.Item1 || x.Item1 == y.Item2 || x.Item2 == y.Item1 || x.Item2 == y.Item2,
            _ => true
        };

        private static float Gap(IReadOnlyList<Vector2> a, IReadOnlyList<Vector2> b)
        {
            Rect boundsA = Bounds(a);
            Rect boundsB = Bounds(b);
            float apart = Mathf.Max(boundsB.xMin - boundsA.xMax, boundsA.xMin - boundsB.xMax,
                boundsB.yMin - boundsA.yMax, boundsA.yMin - boundsB.yMax);
            // Boxes this far apart cannot hold the closest pair, so skip the exact measurement.
            if (apart >= MinimumRock * 2f)
                return apart;
            if (Inside(a, b[0]) || Inside(b, a[0]))
                return 0f;
            float best = float.MaxValue;
            for (int i = 0; i < a.Count; i++)
                for (int j = 0; j < b.Count; j++)
                    best = Mathf.Min(best, SegmentGap(a[i], a[(i + 1) % a.Count], b[j], b[(j + 1) % b.Count]));
            return best;
        }

        private static Rect Bounds(IReadOnlyList<Vector2> outline)
        {
            Vector2 min = outline[0];
            Vector2 max = outline[0];
            foreach (Vector2 point in outline)
            {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static bool Inside(IReadOnlyList<Vector2> outline, Vector2 point)
        {
            for (int i = 0; i < outline.Count; i++)
                if (Cross(outline[i], outline[(i + 1) % outline.Count], point) < 0f)
                    return false;
            return true;
        }

        private static float SegmentGap(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            if ((Cross(c, d, a) > 0f) != (Cross(c, d, b) > 0f) && (Cross(a, b, c) > 0f) != (Cross(a, b, d) > 0f))
                return 0f;
            return Mathf.Min(Mathf.Min(PointGap(a, c, d), PointGap(b, c, d)), Mathf.Min(PointGap(c, a, b), PointGap(d, a, b)));
        }

        private static float PointGap(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 span = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(point - a, span) / Mathf.Max(span.sqrMagnitude, 0.000001f));
            return Vector2.Distance(point, a + span * t);
        }

        private static float Cross(Vector2 origin, Vector2 a, Vector2 b) =>
            (a.x - origin.x) * (b.y - origin.y) - (a.y - origin.y) * (b.x - origin.x);

        private static RoomRole RoleAt(LevelLayout layout, Vector2 position)
        {
            Vector2Int cell = HexGrid.CellAt(position);
            MacroChunk chunk = layout.Chunks.Single(candidate => candidate.Contains(cell));
            return layout.Tunnel.Rooms[chunk.Coordinate].Role;
        }
    }
}
