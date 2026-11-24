using UnityEngine;
using static Insectum.Game.ForestRoomCatalog;

namespace Insectum.Game
{
    // Alien rooms are grown rather than carved: round bubble chambers, ribbed membrane tubes, spore pods hanging
    // in open space, and static columns crackling in round pits beside pads of Nektar.
    internal static class AlienRooms
    {
        internal static readonly ForestRoomTemplate[] Templates =
        {
            Generic,
            new("Alien.BubbleChamber", 2, Shape("Main.Crossing", "SideTunnel.Crossing"), CarveBubbleChamber),
            new("Alien.SporeCascade", 2, Shape("Main.Descent"), CarveSporeCascade),
            new("Alien.RibbedGut", 2, Shape("Main.Span"), CarveRibbedGut),
            new("Alien.HiveSpiral", 2, Shape("Main.Switchback"), CarveHiveSpiral),
            new("Alien.PodRise", 2, Shape("Main.Ascent", "SideTunnel.Ascent"), CarvePodRise),
            new("Alien.NerveWell", 2, Shape("Main.Switchback", "Main.ClimbBack", "SideTunnel.ClimbBack"), CarveNerveWell),
            HazardRooms.Lighthouse, HazardRooms.LaserHollow
        };

        // Crossing. One great bubble with a round roof, a shallow round floor, floating spore pods, and two small
        // bubbles above the doors where static columns guard Nektar.
        private static void CarveBubbleChamber(ForestRoomBuilder room)
        {
            room.Cave(new[]
            {
                (-6.8f, 0.9f, 1.5f), (-6.4f, 0.5f, 2.6f), (-5.8f, 0.3f, 3.2f), (-DoorX, 0.3f, 3.3f), (-DoorX, -DoorY, 5.34f),
                (-4.0f, -4.4f, 6.1f), (-2.6f, -4.65f, 6.7f), (-1.2f, -4.78f, 7.05f), (0f, -4.8f, 7.2f), (1.2f, -4.78f, 7.05f),
                (2.6f, -4.65f, 6.7f), (4.0f, -4.4f, 6.1f), (DoorX, -DoorY, 5.34f), (DoorX, 0.3f, 3.3f), (5.8f, 0.3f, 3.2f),
                (6.4f, 0.5f, 2.6f), (6.8f, 0.9f, 1.5f)
            }, Pod(-2.6f, 1.2f, 1.0f), Pod(2.2f, 3.0f, 1.2f), Pod(-0.4f, 5.0f, 0.6f));
            room.SetLanding(new Vector2(0f, -4.8f));
            room.Trap(new Vector2(-5.8f, 0.3f));
            room.Trap(new Vector2(5.8f, 0.3f));
            room.Treasure(new Vector2(-5.5f, 0.3f));
            room.Treasure(new Vector2(5.5f, 0.3f));
            room.Door(RoomDoor.LowLeft, Clearance);
            room.Door(RoomDoor.LowRight, Clearance);
        }

        // Descent. A cascade of round spore bowls, each spilling into the next, with pods floating overhead.
        private static void CarveSporeCascade(ForestRoomBuilder room)
        {
            room.Cave(new[]
            {
                (-DoorX, DoorY, 7.2f), (-4.4f, 3.9f, 7.0f), (-3.6f, 4.0f, 6.4f), (-3.4f, 4.1f, 6.5f), (-3.4f, 2.2f, 6.5f),
                (-2.6f, 1.95f, 7.1f), (-1.6f, 2.1f, 6.1f), (-1.4f, 2.2f, 5.8f), (-1.4f, 0.4f, 5.8f), (-0.4f, 0.15f, 6.8f),
                (0.8f, 0.3f, 5.4f), (1.0f, 0.4f, 5.0f), (1.0f, -1.6f, 5.0f), (2.0f, -1.6f, 5.6f), (3.8f, -2.99f, 4.0f),
                (DoorX, -DoorY, 1.2f)
            }, Pod(-2.4f, 5.6f, 0.5f), Pod(2.6f, 2.6f, 0.7f));
            room.SetLanding(new Vector2(1.5f, -1.6f));
            room.Door(RoomDoor.HighLeft, Clearance);
            room.Door(RoomDoor.LowRight, Clearance);
        }

        // Span. A ribbed gut: the floor sags in soft waves between the doors and ribs press down from the roof.
        private static void CarveRibbedGut(ForestRoomBuilder room)
        {
            room.Cave(new[]
            {
                (-DoorX, DoorY, 7.2f), (-4.4f, 3.62f, 5.8f), (-3.9f, 3.3f, 6.4f), (-3.4f, 3.03f, 6.9f), (-2.6f, 2.6f, 6.1f),
                (-2.4f, 2.57f, 5.4f), (-1.3f, 2.4f, 6.4f), (-1.2f, 2.37f, 6.8f), (0f, 2.0f, 5.6f), (1.2f, 2.37f, 6.8f),
                (1.3f, 2.4f, 6.4f), (2.4f, 2.57f, 5.4f), (2.6f, 2.6f, 6.1f), (3.4f, 3.03f, 6.9f), (3.9f, 3.3f, 6.4f),
                (4.4f, 3.62f, 5.8f), (DoorX, DoorY, 7.2f)
            }, Pod(-2.0f, 4.5f, 0.45f), Pod(2.0f, 4.5f, 0.45f));
            room.SetLanding(new Vector2(0f, 2.0f));
            room.Door(RoomDoor.HighLeft, Clearance);
            room.Door(RoomDoor.HighRight, Clearance);
        }

        // Switchback. A round hive: step off the upper lip onto a rounded ledge, drop to the floor, and leave
        // beneath the ledge.
        private static void CarveHiveSpiral(ForestRoomBuilder room)
        {
            room.Cave(new[]
            {
                (-DoorX, -DoorY, 0.4f), (-2.6f, -DoorY, 2.0f), (0.4f, -DoorY, 3.1f), (0.4f, -DoorY, 6.9f), (2.0f, -DoorY, 7.1f),
                (3.6f, -DoorY, 6.7f), (4.8f, -DoorY, 5.6f), (5.0f, -DoorY, 5.3f), (5.6f, -3.7f, 4.0f), (6.2f, -3.0f, 2.2f)
            }, new[] { (-0.6f, 0.2f, 0.6f), (0.6f, -0.6f, 0.6f), (2.0f, -0.9f, 0.6f), (3.4f, -0.6f, 0.6f), (4.4f, 0.2f, 0.6f) },
                Pod(2.4f, 6.0f, 0.5f), Pod(4.4f, 3.4f, 0.5f));
            room.Cave(new[] { (-DoorX, DoorY, 7.2f), (-2.8f, DoorY, 7.15f), (0.4f, DoorY, 7.0f) });
            room.SetLanding(new Vector2(4.6f, -DoorY));
            room.Door(RoomDoor.HighLeft, Clearance);
            room.Door(RoomDoor.LowLeft, Clearance);
        }

        // Ascent. Bulging slopes up to a landing and a step, under a round dome with a pod above the lower slope.
        private static void CarvePodRise(ForestRoomBuilder room)
        {
            room.Cave(new[]
            {
                (-DoorX, -DoorY, 1.8f), (-3.8f, -3.16f, 4.0f), (-3.2f, -2.75f, 4.7f), (-2.2f, -1.93f, 5.8f), (-0.7f, HubFloor, 6.6f),
                (0.7f, HubFloor, 7.0f), (0.7f, HubFloor + LedgeStep, 7.0f), (1.4f, 1.23f, 7.1f), (2.8f, 2.3f, 6.9f),
                (3.2f, 2.6f, 6.8f), (DoorX, DoorY, 7.2f)
            }, Pod(-3.2f, 1.4f, 0.7f));
            room.SetLanding(new Vector2(0f, HubFloor));
            room.Door(RoomDoor.LowLeft, Clearance);
            room.Door(RoomDoor.HighRight, Clearance, new Vector2(0.7f, HubFloor + LedgeStep));
        }

        // Switchback or climb. A landing with both ramps on the left that opens into a round nerve chamber. A
        // static column crackles in its pit beside a pad holding Nektar.
        private static void CarveNerveWell(ForestRoomBuilder room)
        {
            room.Cave(new[]
            {
                (-HubHalfWidth, HubFloor, HubTop), (0.2f, HubFloor, 5.4f), (1.6f, HubFloor, 6.6f), (2.4f, HubFloor, 6.9f),
                (3.2f, -1.8f, 6.6f), (4.0f, -2.2f, 6.0f), (4.6f, -2.2f, 5.6f), (5.6f, -1.4f, 4.2f), (6.2f, -0.9f, 2.4f)
            }, Pad(3.4f, 4.8f, 1.0f), Pod(1.2f, 4.6f, 0.5f));
            room.SetLanding(new Vector2(0f, HubFloor));
            room.Trap(new Vector2(4.3f, -2.2f));
            room.Treasure(new Vector2(4.1f, 1.0f));
            CarveHubRamps(room);
        }
    }
}
