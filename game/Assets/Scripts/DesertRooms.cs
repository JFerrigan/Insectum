using UnityEngine;
using static Insectum.Game.ForestRoomCatalog;

namespace Insectum.Game
{
    // Desert rooms are dunes and ruins: long rippled sand slopes, squared temple halls with coffered roofs and
    // niches, fallen lintels floating as ruin slabs, and cinder jets hidden in niches and sand pits.
    internal static class DesertRooms
    {
        internal static readonly ForestRoomTemplate[] Templates =
        {
            Generic,
            new("Desert.BuriedTemple", 2, Shape("Main.Crossing", "SideTunnel.Crossing"), CarveBuriedTemple),
            new("Desert.Sandfall", 2, Shape("Main.Descent"), CarveSandfall),
            new("Desert.DuneSea", 2, Shape("Main.Span"), CarveDuneSea),
            new("Desert.CinderTerraces", 2, Shape("Main.Switchback"), CarveCinderTerraces),
            new("Desert.WindRise", 2, Shape("Main.Ascent", "SideTunnel.Ascent"), CarveWindRise),
            new("Desert.OasisWell", 2, Shape("Main.Switchback", "Main.ClimbBack", "SideTunnel.ClimbBack"), CarveOasisWell)
        };

        // Crossing. A buried temple: ramps up to a central dais under a stepped, coffered roof, fallen lintels
        // above the ramps, and cinder-jet niches above both doors.
        private static void CarveBuriedTemple(ForestRoomBuilder room)
        {
            room.Cave(new[]
            {
                (-7.0f, 0.4f, 2.4f), (-DoorX, 0.4f, 2.4f), (-DoorX, -DoorY, 3.6f), (-3.6f, -DoorY, 3.6f), (-3.6f, -DoorY, 4.8f),
                (-2.6f, -DoorY, 4.8f), (-1.6f, -3.4f, 4.8f), (-1.6f, -3.4f, 5.8f), (1.6f, -3.4f, 5.8f), (1.6f, -3.4f, 4.8f),
                (2.6f, -DoorY, 4.8f), (3.6f, -DoorY, 4.8f), (3.6f, -DoorY, 3.6f), (DoorX, -DoorY, 3.6f), (DoorX, 0.4f, 2.4f),
                (7.0f, 0.4f, 2.4f)
            }, Slab(-3.0f, -2.2f, 2.0f, 2.6f), Slab(2.2f, 3.0f, 2.0f, 2.6f), Slab(-0.8f, 0.8f, 3.4f, 3.9f));
            room.SetLanding(new Vector2(0f, -3.4f));
            room.Trap(new Vector2(-6.1f, 0.4f));
            room.Trap(new Vector2(6.1f, 0.4f));
            room.Treasure(new Vector2(0f, 3.9f));
            room.Door(RoomDoor.LowLeft, Clearance);
            room.Door(RoomDoor.LowRight, Clearance);
        }

        // Descent. A long rippled sand chute, a ledge, and a final drop into a sheltered hollow by the door.
        private static void CarveSandfall(ForestRoomBuilder room)
        {
            room.Cave(new[]
            {
                (-DoorX, DoorY, 7.2f), (-4.4f, DoorY, 7.2f), (-2.6f, 2.72f, 7.0f), (-2.2f, 2.5f, 6.9f), (-0.4f, 1.0f, 6.0f),
                (0.4f, 0.95f, 5.6f), (0.4f, -1.6f, 5.6f), (3.0f, -1.6f, 4.0f), (3.0f, -DoorY, 4.0f), (3.4f, -DoorY, 3.6f),
                (4.4f, -DoorY, 2.6f), (DoorX, -DoorY, 1.0f)
            }, Slab(1.2f, 2.6f, 2.4f, 3.0f));
            room.SetLanding(new Vector2(4.0f, -DoorY));
            room.Treasure(new Vector2(1.9f, 3.0f));
            room.Door(RoomDoor.HighLeft, Clearance);
            room.Door(RoomDoor.LowRight, Clearance);
        }

        // Span. An asymmetric dune: a long rippled descent to a trough, then a steeper climb out, with a ruin
        // slab floating over the windward slope.
        private static void CarveDuneSea(ForestRoomBuilder room)
        {
            room.Cave(new[]
            {
                (-DoorX, DoorY, 7.2f), (-4.6f, DoorY, 7.1f), (-2.6f, 3.0f, 6.8f), (-1.8f, 2.85f, 6.9f), (-1.2f, 2.95f, 7.1f),
                (0.6f, 1.6f, 6.8f), (1.4f, 1.6f, 6.6f), (3.0f, 2.86f, 6.8f), (4.6f, DoorY, 7.0f), (DoorX, DoorY, 7.2f)
            }, Slab(-2.2f, -0.4f, 4.15f, 4.6f));
            room.SetLanding(new Vector2(1.0f, 1.6f));
            room.Treasure(new Vector2(-1.3f, 4.6f));
            room.Door(RoomDoor.HighLeft, Clearance);
            room.Door(RoomDoor.HighRight, Clearance);
        }

        // Switchback. Step off the upper shelf onto a fallen temple block, drop into the hall, and leave along
        // the floor beneath the block.
        private static void CarveCinderTerraces(ForestRoomBuilder room)
        {
            room.Cave(new[]
            {
                (-DoorX, -DoorY, 0.6f), (-2.8f, -DoorY, 2.0f), (0.2f, -DoorY, 3.1f), (0.2f, -DoorY, 7.0f), (2.0f, -DoorY, 7.1f),
                (3.6f, -DoorY, 6.6f), (4.8f, -DoorY, 5.6f), (5.6f, -DoorY, 4.2f), (6.2f, -3.6f, 2.4f)
            }, Slab(-0.8f, 4.4f, -0.6f, 0.4f));
            room.Cave(new[] { (-DoorX, DoorY, 7.2f), (-2.4f, DoorY, 7.15f), (0.2f, DoorY, 7.0f) });
            room.SetLanding(new Vector2(5.0f, -DoorY));
            room.Door(RoomDoor.HighLeft, Clearance);
            room.Door(RoomDoor.LowLeft, Clearance);
        }

        // Ascent. Wind-carved dune slopes up to a landing and a sandstone step, under a wavy roof.
        private static void CarveWindRise(ForestRoomBuilder room)
        {
            room.Cave(new[]
            {
                (-DoorX, -DoorY, 1.6f), (-3.6f, -2.98f, 3.8f), (-2.8f, -2.4f, 4.4f), (-1.6f, -1.43f, 6.0f), (-0.7f, HubFloor, 6.6f),
                (0.7f, HubFloor, 7.0f), (0.7f, HubFloor + LedgeStep, 7.0f), (3.0f, 2.4f, 6.8f), (DoorX, DoorY, 7.2f)
            }, Slab(-3.6f, -2.0f, 2.0f, 2.6f));
            room.SetLanding(new Vector2(0f, HubFloor));
            room.Door(RoomDoor.LowLeft, Clearance);
            room.Door(RoomDoor.HighRight, Clearance, new Vector2(0.7f, HubFloor + LedgeStep));
        }

        // Switchback or climb. A landing with both ramps on the left that opens onto a dry oasis. A cinder jet
        // sits in the sand pit beneath a ruin block holding Nektar.
        private static void CarveOasisWell(ForestRoomBuilder room)
        {
            room.Cave(new[]
            {
                (-HubHalfWidth, HubFloor, HubTop), (0.4f, HubFloor, 5.6f), (2.0f, HubFloor, 6.4f), (2.6f, HubFloor, 6.4f),
                (3.4f, -2.0f, 6.0f), (4.8f, -2.0f, 5.0f), (5.6f, -1.2f, 3.8f), (6.2f, -0.8f, 2.0f)
            }, Slab(3.2f, 4.8f, -0.4f, 0.6f));
            room.SetLanding(new Vector2(0f, HubFloor));
            room.Trap(new Vector2(4.1f, -2.0f));
            room.Treasure(new Vector2(4.0f, 0.6f));
            CarveHubRamps(room);
        }
    }
}
