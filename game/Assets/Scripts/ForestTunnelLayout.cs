using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Insectum.Game
{
    internal static class ForestTunnelLayout
    {
        private const int Radius = 3;
        private const int MainCount = 28;
        private const int SearchBudget = 24000;
        private const uint RoomSalt = 0x6C8E9CF5u;
        internal const float Height = 3.1f;

        // The four diagonal ports have enough horizontal span for baseline movement.
        private static readonly int[] Sides = { 0, 1, 3, 4 };
        private static readonly Vector2Int[] Board = BoardCells().ToArray();
        private static readonly HashSet<Vector2Int> OnBoard = new(Board);
        private static readonly int[] FallbackOrder =
        {
            1, 1, 3, 1, 3, 1, 3, 4, 3, 4, 0, 4, 0, 4, 4, 3, 1, 3, 1, 3, 1, 1, 3, 1, 0, 1, 3
        };

        private readonly struct Arm
        {
            internal readonly Vector2 Foot;
            internal readonly float Clearance;
            internal readonly Vector2? Lip;

            internal Arm(Vector2 foot, float clearance, Vector2? lip)
            {
                Foot = foot;
                Clearance = clearance;
                Lip = lip;
            }
        }

        internal static LevelLayout Generate(int seed, bool forceFallback = false)
        {
            var seeds = new LevelSeeds(seed, 1);
            var random = new SeededRandom(seeds.Layout);
            var route = new List<Vector2Int>(MainCount);
            var branches = new List<(int source, Vector2Int first, Vector2Int end)>();
            bool fallback = true;
            for (int attempt = 0; attempt < 6 && fallback && !forceFallback; attempt++)
            {
                int entrance = random.Next(2);
                Vector2Int start = new(-3 + entrance, 3);
                Vector2Int exit = new(2 + entrance, -3);
                route.Clear();
                route.Add(start);
                int budget = SearchBudget;
                fallback = !Search(route, new HashSet<Vector2Int>(route), exit, branches, -1, 0, 0, 0, 0,
                    ref budget, ref random);
            }

            if (fallback)
            {
                route.Clear();
                route.Add(new Vector2Int(-2, 3));
                foreach (int side in FallbackOrder)
                    route.Add(HexGrid.Neighbor(route[^1], side));
                if (!AddBranches(route, branches, ref random))
                    throw new InvalidOperationException("The Forest tunnel fallback could not fit its branches.");
            }

            return Build(seed, seeds, route, branches, fallback);
        }

        private static IEnumerable<Vector2Int> BoardCells()
        {
            for (int r = -Radius; r <= Radius; r++)
                for (int q = Mathf.Max(-Radius, -Radius - r); q <= Mathf.Min(Radius, Radius - r); q++)
                    yield return new Vector2Int(q, r);
        }

        private static bool Search(List<Vector2Int> route, HashSet<Vector2Int> used, Vector2Int exit,
            List<(int source, Vector2Int first, Vector2Int end)> branches,
            int previous, int straight, int ascents, int turns, int uphillTurns, ref int budget, ref SeededRandom random)
        {
            if (--budget <= 0)
                return false;

            Vector2Int cell = route[^1];
            int remaining = MainCount - route.Count;
            if (cell == exit)
                return remaining == 0 && ascents >= 6 && turns >= 5 && uphillTurns >= 2
                    && AddBranches(route, branches, ref random);
            if (remaining <= 0 || HexGrid.Distance(cell, exit) > remaining
                || !CanReach(cell, exit, used, remaining))
                return false;

            int rotation = random.Next(Sides.Length);
            for (int i = 0; i < Sides.Length && budget > 0; i++)
            {
                int side = Sides[(i + rotation) % Sides.Length];
                if (side == previous && straight >= 3)
                    continue;
                Vector2Int next = HexGrid.Neighbor(cell, side);
                if (!OnBoard.Contains(next) || (next == exit && remaining != 1) || !used.Add(next))
                    continue;

                bool reversal = previous >= 0 && Rightward(previous) != Rightward(side);
                bool uphill = side is 0 or 4;
                route.Add(next);
                if (Search(route, used, exit, branches, side, side == previous ? straight + 1 : 1,
                    ascents + (uphill ? 1 : 0), turns + (reversal ? 1 : 0),
                    uphillTurns + (reversal && uphill ? 1 : 0), ref budget, ref random))
                    return true;
                route.RemoveAt(route.Count - 1);
                used.Remove(next);
            }
            return false;
        }

        private static bool CanReach(Vector2Int from, Vector2Int exit, HashSet<Vector2Int> used, int remaining)
        {
            var open = new Queue<Vector2Int>();
            var seen = new HashSet<Vector2Int> { from };
            open.Enqueue(from);
            while (open.TryDequeue(out Vector2Int cell))
            {
                foreach (int side in Sides)
                {
                    Vector2Int next = HexGrid.Neighbor(cell, side);
                    if (OnBoard.Contains(next) && !used.Contains(next) && seen.Add(next))
                        open.Enqueue(next);
                }
            }

            return seen.Contains(exit) && seen.Count > remaining;
        }

        private static bool AddBranches(List<Vector2Int> route,
            List<(int source, Vector2Int first, Vector2Int end)> branches, ref SeededRandom random)
        {
            branches.Clear();
            var used = new HashSet<Vector2Int>(route);
            var candidates = new List<(int source, Vector2Int first, Vector2Int end)>();
            int offset = random.Next(route.Count - 2);
            for (int i = 0; i < route.Count - 2; i++)
            {
                int index = 1 + (i + offset) % (route.Count - 2);
                Vector2Int root = route[index];
                int incoming = Side(root, route[index - 1]);
                int outgoing = Side(root, route[index + 1]);
                if (Rightward(incoming) == Rightward(outgoing))
                    continue;

                foreach (int upper in new[] { 0, 4 })
                {
                    int lower = Rightward(upper) ? 1 : 3;
                    Vector2Int first = HexGrid.Neighbor(root, upper);
                    if ((incoming != lower && outgoing != lower) || !OnBoard.Contains(first) || used.Contains(first))
                        continue;

                    foreach (int side in Sides)
                    {
                        Vector2Int end = HexGrid.Neighbor(first, side);
                        if (OnBoard.Contains(end) && !used.Contains(end))
                            candidates.Add((index, first, end));
                    }
                }
            }
            return Place(0);

            bool Place(int start)
            {
                if (branches.Count == 3)
                    return true;
                for (int i = start; i < candidates.Count; i++)
                {
                    var candidate = candidates[i];
                    if (used.Contains(candidate.first) || used.Contains(candidate.end) ||
                        branches.Exists(branch => branch.source == candidate.source))
                        continue;
                    branches.Add(candidate);
                    used.Add(candidate.first);
                    used.Add(candidate.end);
                    if (Place(i + 1))
                        return true;
                    branches.RemoveAt(branches.Count - 1);
                    used.Remove(candidate.first);
                    used.Remove(candidate.end);
                }
                return false;
            }
        }

        private static int Side(Vector2Int from, Vector2Int to)
        {
            foreach (int side in Sides)
                if (HexGrid.Neighbor(from, side) == to)
                    return side;
            throw new ArgumentException($"Chunks {from} and {to} do not share a diagonal port.");
        }

        private static bool Rightward(int side) => side is 0 or 1;
        private static Vector2 Center(Vector2Int chunk) => HexGrid.Center(ForestTemplates.MacroCenter(chunk));

        private static LevelLayout Build(int seed, LevelSeeds seeds, List<Vector2Int> route,
            List<(int source, Vector2Int first, Vector2Int end)> branches, bool fallback)
        {
            var space = new ForestTunnelSpace();
            var ports = new Dictionary<Vector2Int, MacroPort>();
            var arms = new Dictionary<(Vector2Int chunk, int side), Arm>();
            var landings = new Dictionary<Vector2Int, Vector2>();
            var links = new List<(Vector2Int from, Vector2Int to)>();
            var checkpoints = new List<RouteCheckpoint>();
            var markers = new List<LevelMarker>();
            var shop = branches.OrderBy(branch => branch.source).First();
            foreach (var room in ForestRoomProfile.Classify(route, branches))
                space.Rooms.Add(room.Key, room.Value);

            for (int i = 1; i < route.Count; i++)
                Connect(route[i - 1], route[i]);
            foreach (var branch in branches)
            {
                Connect(route[branch.source], branch.first);
                Connect(branch.first, branch.end);
            }

            // Templates draw from their own stream so adding one never changes a seed's route.
            var roomRandom = new SeededRandom(seeds.Layout ^ RoomSalt);
            var templateIds = new Dictionary<Vector2Int, string>();
            foreach (var entry in ports)
                CarveRoom(entry.Key);
            foreach (var link in links)
            {
                CarveLink(link.from, link.to);
                MarkJump(link.from, link.to);
                MarkJump(link.to, link.from);
            }

            var chunks = new List<MacroChunk>(Board.Length);
            var tiles = new Dictionary<Vector2Int, TerrainMaterial>();
            var boardTiles = new HashSet<Vector2Int>();
            foreach (Vector2Int coordinate in Board)
            {
                Vector2Int center = ForestTemplates.MacroCenter(coordinate);
                MacroPort mask = ports.GetValueOrDefault(coordinate);
                chunks.Add(new MacroChunk(coordinate, center, mask == MacroPort.None ? "Forest.Tunnel.Solid" : templateIds[coordinate],
                    mask, route.Contains(coordinate)));
                for (int r = -HexGrid.MacroRadius; r <= HexGrid.MacroRadius; r++)
                {
                    int min = Mathf.Max(-HexGrid.MacroRadius, -HexGrid.MacroRadius - r);
                    int max = Mathf.Min(HexGrid.MacroRadius, HexGrid.MacroRadius - r);
                    for (int q = min; q <= max; q++)
                    {
                        Vector2Int fine = center + new Vector2Int(q, r);
                        boardTiles.Add(fine);
                        if (space.AddRock(fine))
                            tiles.Add(fine, TerrainMaterial.Reinforced);
                    }
                }
            }
            Rect bounds = space.Bounds;
            // A camera centered near the edge of the board still needs solid rock around the tunnel.
            float viewHalfWidth = PixelViewport.WorldHalfWidth;
            float viewHalfHeight = PixelViewport.WorldHalfHeight;
            int firstRow = Mathf.FloorToInt((bounds.yMin - viewHalfHeight) / HexGrid.RowSpacing) - 1;
            int lastRow = Mathf.CeilToInt((bounds.yMax + viewHalfHeight) / HexGrid.RowSpacing) + 1;
            for (int r = firstRow; r <= lastRow; r++)
            {
                int firstColumn = Mathf.FloorToInt((bounds.xMin - viewHalfWidth) / HexGrid.ColumnSpacing - r / 2f) - 1;
                int lastColumn = Mathf.CeilToInt((bounds.xMax + viewHalfWidth) / HexGrid.ColumnSpacing - r / 2f) + 1;
                for (int q = firstColumn; q <= lastColumn; q++)
                {
                    var cell = new Vector2Int(q, r);
                    if (!boardTiles.Contains(cell) && space.AddRock(cell))
                        tiles.Add(cell, TerrainMaterial.Reinforced);
                }
            }

            for (int index = 1; index < route.Count; index++)
            {
                Vector2Int chunk = route[index];
                int entry = Side(chunk, route[index - 1]);
                checkpoints.Add(new RouteCheckpoint(landings[chunk], chunk, (MacroPort)(1 << entry), templateIds[chunk]));
            }

            var encounters = new SeededRandom(seeds.Encounters);
            for (int slot = 0; slot < 5; slot++)
            {
                int index = 3 + slot * 5 + encounters.Next(3);
                Vector2Int chunk = route[index];
                markers.Add(new LevelMarker(LevelMarkerKind.Encounter, landings[chunk], chunk));
            }

            foreach (var branch in branches)
            {
                LevelMarkerKind kind = branch.Equals(shop) ? LevelMarkerKind.Shop : LevelMarkerKind.Branch;
                markers.Add(new LevelMarker(kind, landings[branch.end], branch.end));
                space.Branches.Add((branch.source, landings[branch.first], landings[branch.end]));
            }

            foreach (Vector2Int chunk in route)
                space.Route.Add(landings[chunk]);

            Vector2 spawn = landings[route[0]];
            Vector2 exit = landings[route[^1]] + Vector2.up * 0.55f;
            int facing = Center(route[1]).x > spawn.x ? 1 : -1;
            return new LevelLayout(seed, 1, seeds, tiles, chunks, checkpoints, markers, spawn, exit,
                bounds, facing, fallback, tunnel: space);

            void MarkJump(Vector2Int from, Vector2Int to)
            {
                if (arms[(from, Side(from, to))].Lip is Vector2 lip)
                    space.Jumps.Add((landings[from], landings[to]), lip);
            }

            void Connect(Vector2Int from, Vector2Int to)
            {
                links.Add((from, to));
                ports[from] = ports.GetValueOrDefault(from) | (MacroPort)(1 << Side(from, to));
                ports[to] = ports.GetValueOrDefault(to) | (MacroPort)(1 << Side(to, from));
            }

            void CarveRoom(Vector2Int chunk)
            {
                ForestRoomProfile profile = space.Rooms[chunk];
                ForestRoomTemplate template = ForestRoomCatalog.Choose(profile, ref roomRandom);
                templateIds.Add(chunk, template.Id);
                var room = new ForestRoomBuilder(space, chunk, Center(chunk), profile);
                template.Carve(room);
                landings.Add(chunk, room.Landing);
                foreach (int side in Sides)
                {
                    if ((profile.Ports & (MacroPort)(1 << side)) == 0)
                        continue;
                    RoomDoor door = profile.DoorAt(side);
                    if (!room.Doors.TryGetValue(door, out var carved))
                        throw new InvalidOperationException($"{template.Id} did not carve the {door} door of {profile}.");
                    arms.Add((chunk, side), new Arm(room.WorldAnchor(door), carved.clearance, carved.lip));
                }
            }

            // Door anchors are fixed, so every link is a short, nearly level passage between two rooms.
            void CarveLink(Vector2Int from, Vector2Int to)
            {
                Arm a = arms[(from, Side(from, to))];
                Arm b = arms[(to, Side(to, from))];
                if (a.Foot.x > b.Foot.x)
                    (a, b) = (b, a);
                space.AddPassage(a.Foot, b.Foot, a.Clearance, b.Clearance, (from, to));
            }
        }
    }
}
