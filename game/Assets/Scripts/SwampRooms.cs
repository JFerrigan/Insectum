using UnityEngine;
using static Insectum.Game.ForestRoomCatalog;

namespace Insectum.Game
{
    // Swamp rooms are low and wide: mud hummocks underfoot, root curtains hanging from the roof, and lily pads
    // floating just high enough to walk beneath, where gas vents and Nektar wait for a careful hop.
    internal static class SwampRooms
    {
        internal static readonly ForestRoomTemplate[] Templates =
        {
            Generic,
            new("Swamp.SunkenFen", 2, Shape("Main.Crossing", "SideTunnel.Crossing"), CarveSunkenFen),
            new("Swamp.MudSlide", 2, Shape("Main.Switchback"), CarveMudSlide),
            new("Swamp.ReedTerraces", 2, Shape("Main.Descent"), CarveReedTerraces),
            new("Swamp.BogBasin", 2, Shape("Main.Span"), CarveBogBasin),
            new("Swamp.RootTangle", 2, Shape("Main.Switchback", "Main.ClimbBack", "SideTunnel.ClimbBack"), CarveRootTangle),
            new("Swamp.HummockRise", 2, Shape("Main.Ascent", "SideTunnel.Ascent"), CarveHummockRise)
        };

        // Crossing. A wide fen of hummocks and shallow dips under root curtains, with two lily pads to hop onto.
        private static void CarveSunkenFen(ForestRoomBuilder room)
        {
            room.Cave(new[]
            {
                (-DoorX, -DoorY, 2.4f), (-4.0f, -DoorY, 2.6f), (-3.6f, -DoorY, 2.4f), (-3.0f, -3.78f, 1.6f), (-2.6f, -3.55f, 2.0f),
                (-1.6f, -DoorY, 2.8f), (-0.4f, -4.45f, 3.0f), (0.5f, -4.45f, 2.2f), (1.0f, -4.45f, 2.6f), (2.2f, -DoorY, 3.0f),
                (3.0f, -3.71f, 1.8f), (3.2f, -3.6f, 2.0f), (4.2f, -DoorY, 2.4f), (DoorX, -DoorY, 2.4f)
            }, Pad(-4.8f, -3.7f, -2.25f), Pad(2.6f, 4.0f, -1.7f));
            room.SetLanding(new Vector2(0.3f, -4.45f));
            room.Trap(new Vector2(-4.25f, -2.25f));
            room.Trap(new Vector2(3.3f, -1.7f));
            room.Treasure(new Vector2(-4.25f, -2.25f));
            room.Treasure(new Vector2(3.3f, -1.7f));
            room.Door(RoomDoor.LowLeft, Clearance);
            room.Door(RoomDoor.LowRight, Clearance);
        }

        // Switchback. Slide down a long mud bank into a bowl, then leave along the gallery beneath the bank.
        private static void CarveMudSlide(ForestRoomBuilder room)
        {
            room.Cave(new[]
            {
                (-DoorX, DoorY, 7.2f), (-4.4f, DoorY, 7.2f), (-1.0f, 1.49f, 6.8f), (2.6f, -1.3f, 6.0f)
            });
            room.Cave(new[]
            {
                (-DoorX, -DoorY, 2.925f), (-4.4f, -DoorY, 2.925f), (-1.0f, -DoorY, 0.29f), (2.6f, -DoorY, -2.5f),
                (2.6f, -DoorY, 6.0f), (3.4f, -DoorY, 4.6f), (4.2f, -DoorY, 5.6f), (5.2f, -DoorY, 4.6f), (6.0f, -3.7f, 3.0f),
                (6.4f, -3.3f, 1.6f)
            });
            room.SetLanding(new Vector2(4.4f, -DoorY));
            room.Door(RoomDoor.HighLeft, Clearance);
            room.Door(RoomDoor.LowLeft, Clearance);
        }

        // Descent. Rice-paddy terraces with low mud rims, stepping down to the low door.
        private static void CarveReedTerraces(ForestRoomBuilder room)
        {
            room.Cave(new[]
            {
                (-DoorX, DoorY, 7.2f), (-3.8f, DoorY, 7.2f), (-3.6f, 4.275f, 7.2f), (-3.6f, 2.6f, 7.2f), (-1.8f, 2.6f, 7.1f),
                (-1.6f, 2.75f, 7.0f), (-1.6f, 1.1f, 7.0f), (0.2f, 1.1f, 6.8f), (0.4f, 1.25f, 6.7f), (0.4f, -0.4f, 6.7f),
                (2.2f, -0.4f, 6.2f), (2.4f, -0.25f, 6.0f), (2.4f, -DoorY, 6.0f), (3.4f, -DoorY, 5.4f), (4.4f, -DoorY, 3.6f),
                (DoorX, -DoorY, 1.6f)
            }, Islet(-3.0f, -1.4f, 5.6f, 0.8f));
            room.SetLanding(new Vector2(3.6f, -DoorY));
            room.Treasure(new Vector2(-2.2f, 5.6f));
            room.Door(RoomDoor.HighLeft, Clearance);
            room.Door(RoomDoor.LowRight, Clearance);
        }

        // Span. Drop into a sunken bog basin under a high roof, then wade up the long far bank.
        private static void CarveBogBasin(ForestRoomBuilder room)
        {
            room.Cave(new[]
            {
                (-DoorX, DoorY, 7.2f), (-3.8f, DoorY, 7.2f), (-3.8f, 1.0f, 7.2f), (-0.4f, 1.0f, 7.1f), (1.6f, 2.3f, 6.6f),
                (3.0f, 3.21f, 6.0f), (4.4f, DoorY, 6.8f), (DoorX, DoorY, 7.2f)
            }, Islet(-3.2f, -1.0f, 5.4f, 0.8f), Islet(0.6f, 2.6f, 5.8f, 0.6f));
            room.SetLanding(new Vector2(-2.0f, 1.0f));
            room.Treasure(new Vector2(-2.1f, 5.4f));
            room.Door(RoomDoor.HighLeft, Clearance);
            room.Door(RoomDoor.HighRight, Clearance);
        }

        // Switchback or climb. A landing with both ramps on the left that opens onto a flooded pool. A gas vent
        // fumes in the pool beneath a lily pad holding Nektar.
        private static void CarveRootTangle(ForestRoomBuilder room)
        {
            room.Cave(new[]
            {
                (-HubHalfWidth, HubFloor, HubTop), (0f, HubFloor, 4.8f), (1.6f, HubFloor, 3.9f), (2.0f, HubFloor, 4.3f),
                (3.2f, -1.6f, 4.9f), (4.6f, -1.6f, 4.4f), (5.0f, -1.6f, 3.6f), (5.6f, -0.9f, 1.8f)
            }, Pad(3.4f, 4.6f, 0.3f));
            room.SetLanding(new Vector2(0f, HubFloor));
            room.Trap(new Vector2(3.9f, -1.6f));
            room.Treasure(new Vector2(4.0f, 0.3f));
            CarveHubRamps(room);
        }

        // Ascent. A hummocky bank climbing under root curtains, with a lily pad above the lower slope.
        private static void CarveHummockRise(ForestRoomBuilder room)
        {
            room.Cave(new[]
            {
                (-DoorX, -DoorY, 1.6f), (-3.6f, -2.94f, 3.0f), (-3.0f, -2.5f, 2.6f), (-2.4f, -2.03f, 2.4f), (-1.0f, -0.93f, 4.6f),
                (-0.7f, HubFloor, 4.8f), (0.7f, HubFloor, 5.0f), (0.7f, HubFloor + LedgeStep, 5.0f), (1.6f, 1.37f, 4.0f),
                (3.0f, 2.4f, 5.6f), (4.2f, 3.34f, 4.9f), (DoorX, DoorY, 7.2f)
            }, Pad(-4.6f, -3.6f, -0.9f));
            room.SetLanding(new Vector2(0f, HubFloor));
            room.Treasure(new Vector2(-4.1f, -0.9f));
            room.Door(RoomDoor.LowLeft, Clearance);
            room.Door(RoomDoor.HighRight, Clearance, new Vector2(0.7f, HubFloor + LedgeStep));
        }
    }
}
