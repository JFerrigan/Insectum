using UnityEngine;
using static Insectum.Game.ForestRoomCatalog;

namespace Insectum.Game
{
    // Caves rooms are crystalline and vertical: faceted walls, stalactites hanging from jagged roofs, pointed
    // crystals floating in open geodes, and falling-crystal traps guarding ledges and pit floors.
    internal static class CavesRooms
    {
        internal static readonly ForestRoomTemplate[] Templates =
        {
            Generic,
            new("Caves.GeodeHall", 2, Shape("Main.Crossing", "SideTunnel.Crossing"), CarveGeodeHall),
            new("Caves.FaultLine", 2, Shape("Main.Descent"), CarveFaultLine),
            new("Caves.StalactiteGallery", 2, Shape("Main.Span"), CarveStalactiteGallery),
            new("Caves.CrystalWell", 2, Shape("Main.Switchback"), CarveCrystalWell),
            new("Caves.ShardRise", 2, Shape("Main.Ascent", "SideTunnel.Ascent"), CarveShardRise),
            new("Caves.EchoChamber", 2, Shape("Main.Switchback", "Main.ClimbBack", "SideTunnel.ClimbBack"), CarveEchoChamber),
            HazardRooms.SpikeGallery, HazardRooms.SpikeHollow, HazardRooms.SpikeChasm
        };

        // Crossing. A cracked-open geode with a faceted dome, floating crystals, and crystal-guarded wings above
        // both doors that only a climber can reach.
        private static void CarveGeodeHall(ForestRoomBuilder room)
        {
            room.Cave(new[]
            {
                (-6.6f, 1.0f, 2.4f), (-5.9f, 0.3f, 3.6f), (-DoorX, 0.2f, 4.2f), (-DoorX, -DoorY, 4.2f), (-4.4f, -DoorY, 4.8f),
                (-3.4f, -DoorY, 6.0f), (-2.2f, -4.6f, 6.6f), (-1.0f, -4.6f, 7.0f), (1.0f, -4.6f, 7.1f), (2.2f, -4.6f, 6.6f),
                (3.4f, -DoorY, 6.0f), (4.4f, -DoorY, 4.8f), (DoorX, -DoorY, 4.2f), (DoorX, 0.2f, 4.2f), (5.9f, 0.3f, 3.6f),
                (6.6f, 1.0f, 2.4f)
            }, Crystal(-3.4f, -1.6f, 1.0f, 0.7f), Crystal(0.6f, 2.4f, 2.8f, 0.8f), Crystal(-0.6f, 0.4f, 5.0f, 0.6f));
            room.SetLanding(new Vector2(0f, -4.6f));
            room.Trap(new Vector2(-5.9f, 0.3f));
            room.Trap(new Vector2(5.9f, 0.3f));
            room.Treasure(new Vector2(-5.6f, 0.25f));
            room.Treasure(new Vector2(5.6f, 0.25f));
            room.Door(RoomDoor.LowLeft, Clearance);
            room.Door(RoomDoor.LowRight, Clearance);
        }

        // Descent. A tilted fault: broken shelves stepping down under a jagged roof that follows the slope.
        private static void CarveFaultLine(ForestRoomBuilder room)
        {
            room.Cave(new[]
            {
                (-DoorX, DoorY, 7.2f), (-4.0f, DoorY, 6.6f), (-3.6f, DoorY, 6.9f), (-3.6f, 2.4f, 6.9f), (-2.8f, 2.08f, 7.0f),
                (-1.6f, 1.6f, 5.6f), (-1.6f, 0f, 5.6f), (-0.4f, -0.34f, 6.2f), (1.2f, -0.8f, 4.6f), (1.2f, -2.4f, 4.6f),
                (2.2f, -2.4f, 5.4f), (3.0f, -2.4f, 4.4f), (4.4f, -3.5f, 3.0f), (DoorX, -DoorY, 1.0f)
            }, Crystal(-1.2f, 0.4f, 3.8f, 0.5f), Crystal(2.4f, 3.6f, 1.8f, 0.4f));
            room.SetLanding(new Vector2(2.0f, -2.4f));
            room.Door(RoomDoor.HighLeft, Clearance);
            room.Door(RoomDoor.LowRight, Clearance);
        }

        // Span. Drop into a gallery under hanging stalactites, then climb faceted steps to the far door.
        private static void CarveStalactiteGallery(ForestRoomBuilder room)
        {
            room.Cave(new[]
            {
                (-DoorX, DoorY, 7.2f), (-4.0f, DoorY, 7.1f), (-4.0f, 1.2f, 7.1f), (-3.4f, 1.2f, 7.1f), (-2.6f, 1.2f, 4.6f),
                (-1.8f, 1.2f, 7.0f), (-0.6f, 1.2f, 6.9f), (-0.4f, 1.2f, 6.6f), (0.2f, 1.48f, 5.0f), (1.0f, 1.85f, 7.0f),
                (2.0f, 2.3f, 6.9f), (2.6f, 2.77f, 6.8f), (3.2f, 3.24f, 5.8f), (3.4f, 3.4f, 6.4f), (3.8f, 3.64f, 7.0f),
                (4.6f, DoorY, 7.1f), (DoorX, DoorY, 7.2f)
            }, Crystal(-1.6f, 0.0f, 4.2f, 0.4f));
            room.SetLanding(new Vector2(-2.0f, 1.2f));
            room.Door(RoomDoor.HighLeft, Clearance);
            room.Door(RoomDoor.HighRight, Clearance);
        }

        // Switchback. A deep crystal well: step off the upper shelf onto a crystal ledge, drop to the floor,
        // and leave through the gallery beneath.
        private static void CarveCrystalWell(ForestRoomBuilder room)
        {
            room.Cave(new[]
            {
                (-DoorX, -DoorY, 0f), (-3.0f, -DoorY, 1.6f), (-0.4f, -DoorY, 3.1f), (-0.4f, -DoorY, 7.0f), (1.6f, -DoorY, 7.15f),
                (3.4f, -DoorY, 6.6f), (4.8f, -DoorY, 5.6f), (5.6f, -3.8f, 4.0f), (6.2f, -3.2f, 1.8f)
            }, new[] { (-1.4f, 0f, 0.4f), (0f, -0.8f, 0.4f), (2.4f, -0.9f, 0.4f), (3.8f, 0.1f, 0.4f) },
                Crystal(1.0f, 2.4f, 5.6f, 0.5f), Crystal(3.6f, 4.6f, 3.2f, 0.5f));
            room.Cave(new[] { (-DoorX, DoorY, 7.2f), (-3.0f, DoorY, 7.15f), (-0.4f, DoorY, 7.0f) });
            room.SetLanding(new Vector2(4.6f, -DoorY));
            room.Door(RoomDoor.HighLeft, Clearance);
            room.Door(RoomDoor.LowLeft, Clearance);
        }

        // Ascent. Faceted slopes up to a landing and a crystal step, under a jagged roof with a floating shard.
        private static void CarveShardRise(ForestRoomBuilder room)
        {
            room.Cave(new[]
            {
                (-DoorX, -DoorY, 2.0f), (-3.8f, -3.15f, 4.2f), (-3.0f, -2.6f, 3.6f), (-1.2f, -1.11f, 6.2f), (-0.7f, HubFloor, 6.6f),
                (0.7f, HubFloor, 7.0f), (0.7f, HubFloor + LedgeStep, 7.0f), (1.8f, 1.57f, 6.0f), (2.6f, 2.2f, 6.9f),
                (4.2f, 3.38f, 6.6f), (DoorX, DoorY, 7.2f)
            }, Crystal(-4.4f, -2.8f, 1.4f, 0.6f));
            room.SetLanding(new Vector2(0f, HubFloor));
            room.Door(RoomDoor.LowLeft, Clearance);
            room.Door(RoomDoor.HighRight, Clearance, new Vector2(0.7f, HubFloor + LedgeStep));
        }

        // Switchback or climb. A landing with both ramps on the left that opens into a crystal chamber. A falling
        // crystal guards the pit floor beneath a ledge holding Nektar.
        private static void CarveEchoChamber(ForestRoomBuilder room)
        {
            room.Cave(new[]
            {
                (-HubHalfWidth, HubFloor, HubTop), (0f, HubFloor, 5.4f), (1.4f, HubFloor, 6.6f), (2.2f, HubFloor, 6.9f),
                (3.2f, -2.0f, 6.4f), (4.4f, -2.0f, 5.6f), (5.4f, -1.0f, 4.4f), (6.2f, -0.6f, 2.4f)
            }, Islet(3.0f, 4.6f, 0.6f, 0.8f), Crystal(1.0f, 2.2f, 4.6f, 0.4f));
            room.SetLanding(new Vector2(0f, HubFloor));
            room.Trap(new Vector2(3.8f, -2.0f));
            room.Treasure(new Vector2(3.8f, 0.6f));
            CarveHubRamps(room);
        }
    }
}
