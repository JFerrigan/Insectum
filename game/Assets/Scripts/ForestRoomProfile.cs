using System;
using System.Collections.Generic;
using UnityEngine;

namespace Insectum.Game
{
    internal enum RoomRole
    {
        Start,
        Main,
        Junction,
        SideTunnel,
        Vault,
        Finish
    }

    // Doors are named after the room is mirrored so that it is entered from the left.
    // A start room has no entrance, so it is mirrored so that it leaves to the right.
    internal enum RoomDoor
    {
        None,
        HighLeft,
        LowLeft,
        HighRight,
        LowRight
    }

    internal readonly struct ForestRoomProfile
    {
        // Every shape the tunnel route and its side branches can produce.
        internal static readonly string[] Shapes =
        {
            "Start.Low", "Start.High",
            "Main.Switchback", "Main.Descent", "Main.Span", "Main.ClimbBack", "Main.Crossing", "Main.Ascent",
            "Junction.Descent.HighRight", "Junction.Crossing.HighRight", "Junction.Crossing.HighLeft",
            "Junction.Ascent.HighLeft",
            "SideTunnel.Crossing", "SideTunnel.ClimbBack", "SideTunnel.Ascent",
            "Vault.Low", "Vault.High",
            "Finish.Low", "Finish.High"
        };

        internal RoomRole Role { get; }
        internal RoomDoor Entry { get; }
        internal RoomDoor Exit { get; }
        internal RoomDoor Branch { get; }
        internal bool Mirrored { get; }
        internal string Shape { get; }

        // The chunk's ports in world orientation, with the mirror undone.
        internal MacroPort Ports => Port(Entry) | Port(Exit) | Port(Branch);

        private ForestRoomProfile(RoomRole role, int entry, int exit, int branch)
        {
            bool mirrored = entry >= 0 ? Rightward(entry) : !Rightward(exit);
            RoomDoor entryDoor = Door(entry, mirrored);
            RoomDoor exitDoor = Door(exit, mirrored);
            RoomDoor branchDoor = Door(branch, mirrored);
            Role = role;
            Mirrored = mirrored;
            Entry = entryDoor;
            Exit = exitDoor;
            Branch = branchDoor;
            Shape = role switch
            {
                RoomRole.Start => $"Start.{Height(exitDoor)}",
                RoomRole.Main => $"Main.{Passage(entryDoor, exitDoor)}",
                RoomRole.Junction => $"Junction.{Passage(entryDoor, exitDoor)}.{branchDoor}",
                RoomRole.SideTunnel => $"SideTunnel.{Passage(entryDoor, exitDoor)}",
                RoomRole.Vault => $"Vault.{Height(entryDoor)}",
                _ => $"Finish.{Height(entryDoor)}"
            };
        }

        internal static Dictionary<Vector2Int, ForestRoomProfile> Classify(IReadOnlyList<Vector2Int> route,
            IReadOnlyList<(int source, Vector2Int first, Vector2Int end)> branches)
        {
            var spurs = new Dictionary<int, Vector2Int>();
            foreach (var branch in branches)
                spurs.Add(branch.source, branch.first);

            var rooms = new Dictionary<Vector2Int, ForestRoomProfile>(route.Count + branches.Count * 2);
            for (int i = 0; i < route.Count; i++)
            {
                Vector2Int chunk = route[i];
                int entry = i > 0 ? Side(chunk, route[i - 1]) : -1;
                int exit = i + 1 < route.Count ? Side(chunk, route[i + 1]) : -1;
                int spur = spurs.TryGetValue(i, out Vector2Int first) ? Side(chunk, first) : -1;
                RoomRole role = i == 0 ? RoomRole.Start
                    : i == route.Count - 1 ? RoomRole.Finish
                    : spur >= 0 ? RoomRole.Junction : RoomRole.Main;
                rooms.Add(chunk, new ForestRoomProfile(role, entry, exit, spur));
            }

            foreach (var branch in branches)
            {
                rooms.Add(branch.first, new ForestRoomProfile(RoomRole.SideTunnel,
                    Side(branch.first, route[branch.source]), Side(branch.first, branch.end), -1));
                rooms.Add(branch.end, new ForestRoomProfile(RoomRole.Vault, Side(branch.end, branch.first), -1, -1));
            }
            return rooms;
        }

        // The canonical door for one of the chunk's world sides.
        internal RoomDoor DoorAt(int side) => Door(side, Mirrored);

        public override string ToString() => Mirrored ? $"{Shape} (mirrored)" : Shape;

        private static int Side(Vector2Int from, Vector2Int to)
        {
            for (int side = 0; side < HexGrid.SideCount; side++)
                if (HexGrid.Neighbor(from, side) == to)
                    return side;
            throw new ArgumentException($"Chunks {from} and {to} are not neighbors.");
        }

        // Sides 0, 1, 3 and 4 are the four diagonal ports. Mirroring swaps left and right at the same height.
        private static bool Rightward(int side) => side is 0 or 1;
        internal static int Mirror(int side) => side switch { 0 => 4, 1 => 3, 3 => 1, 4 => 0, _ => side };

        // The door a world side becomes in a room or suite with this mirroring.
        internal static RoomDoor Door(int side, bool mirrored)
        {
            if (side < 0)
                return RoomDoor.None;
            return (mirrored ? Mirror(side) : side) switch
            {
                0 => RoomDoor.HighRight,
                1 => RoomDoor.LowRight,
                3 => RoomDoor.LowLeft,
                4 => RoomDoor.HighLeft,
                _ => throw new ArgumentException($"Forest rooms only use diagonal ports, not side {side}.")
            };
        }

        private MacroPort Port(RoomDoor door)
        {
            int side = door switch
            {
                RoomDoor.HighRight => 0,
                RoomDoor.LowRight => 1,
                RoomDoor.LowLeft => 3,
                RoomDoor.HighLeft => 4,
                _ => -1
            };
            return side < 0 ? MacroPort.None : (MacroPort)(1 << (Mirrored ? Mirror(side) : side));
        }

        private static string Height(RoomDoor door) => door is RoomDoor.HighLeft or RoomDoor.HighRight ? "High" : "Low";

        private static string Passage(RoomDoor entry, RoomDoor exit) => (entry, exit) switch
        {
            (RoomDoor.HighLeft, RoomDoor.LowLeft) => "Switchback",
            (RoomDoor.HighLeft, RoomDoor.LowRight) => "Descent",
            (RoomDoor.HighLeft, RoomDoor.HighRight) => "Span",
            (RoomDoor.LowLeft, RoomDoor.HighLeft) => "ClimbBack",
            (RoomDoor.LowLeft, RoomDoor.LowRight) => "Crossing",
            (RoomDoor.LowLeft, RoomDoor.HighRight) => "Ascent",
            _ => $"{entry}To{exit}"
        };
    }
}
