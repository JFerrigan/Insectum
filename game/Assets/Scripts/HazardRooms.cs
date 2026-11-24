using UnityEngine;
using static Insectum.Game.ForestRoomCatalog;

namespace Insectum.Game
{
    // Hazard rooms for the Swamp, Caves, Desert and Alien worlds. They share the shapes the Forest bramble rooms
    // proved out (a flat hall, a tall switchback hollow, a bowl crossed by island leaps, and a low hall under a
    // dome) and put each world's own hazard in them:
    //
    // Desert cacti cost a heart and throw the player hard. Caves spikes kill a player who lands on them but let
    // one walk through. Swamp flytraps kill whatever enters their open mouths. Alien lasers sweep around their
    // emitters and kill what they touch, so their rooms mark where to wait for a gap.
    //
    // A full jump at running speed rises about 2.06 units and lands about 4.3 units ahead on level ground. Each
    // obstacle marks its take-off far enough before it that the jump clears its full height.
    internal static class HazardRooms
    {
        internal static readonly ForestRoomTemplate CactusGarden =
            new("Desert.Cactus.Garden", 1, Shape("Main.Crossing"), room => Hall(room, Cacti), hazardLevel: 1);
        internal static readonly ForestRoomTemplate CactusRavine =
            new("Desert.Cactus.Ravine", 1, Shape("Main.Switchback"), room => Hollow(room, Barrel), hazardLevel: 1);
        internal static readonly ForestRoomTemplate CactusPit =
            new("Desert.Cactus.Pit", 1, Shape("Main.Span"), CarveCactusPit, hazardLevel: 2);

        internal static readonly ForestRoomTemplate SpikeGallery =
            new("Caves.Spike.Gallery", 1, Shape("Main.Crossing"), room => Hall(room, SpikeBed), hazardLevel: 1);
        internal static readonly ForestRoomTemplate SpikeHollow =
            new("Caves.Spike.Hollow", 1, Shape("Main.Switchback"), room => Hollow(room, SpikeBed), hazardLevel: 1);
        internal static readonly ForestRoomTemplate SpikeChasm =
            new("Caves.Spike.Chasm", 1, Shape("Main.Span"), CarveSpikeChasm, hazardLevel: 2);

        internal static readonly ForestRoomTemplate FlytrapFen =
            new("Swamp.Flytrap.Fen", 1, Shape("Main.Crossing"), room => Hall(room, Trap), hazardLevel: 1);
        internal static readonly ForestRoomTemplate FlytrapHollow =
            new("Swamp.Flytrap.Hollow", 1, Shape("Main.Switchback"), room => Hollow(room, Trap), hazardLevel: 1);
        internal static readonly ForestRoomTemplate HangingGardens =
            new("Swamp.Flytrap.Gardens", 1, Shape("Main.Crossing"), CarveHangingGardens, hazardLevel: 2);

        internal static readonly ForestRoomTemplate Lighthouse =
            new("Alien.Laser.Lighthouse", 1, Shape("Main.Crossing"), CarveLighthouse, hazardLevel: 1);
        internal static readonly ForestRoomTemplate LaserHollow =
            new("Alien.Laser.Hollow", 1, Shape("Main.Switchback"), CarveLaserHollow, hazardLevel: 1);

        // Puts an obstacle centered at x on a flat floor and marks the leaps over it.
        private delegate void Obstacle(ForestRoomBuilder room, float x, float floor, int place);

        // Desert halls start with a barrel and add a tall cactus deeper in the world.
        private static void Cacti(ForestRoomBuilder room, float x, float floor, int place)
        {
            if (place == 1)
                Tall(room, x, floor, place);
            else
                Barrel(room, x, floor, place);
        }

        private static void Barrel(ForestRoomBuilder room, float x, float floor, int place)
        {
            room.Cactus(x, floor, false);
            Leaps(room, x, 0.4f, 1.2f, floor);
        }

        private static void Tall(ForestRoomBuilder room, float x, float floor, int place)
        {
            room.Cactus(x, floor, true);
            Leaps(room, x, 0.3f, 1.5f, floor);
        }

        private static void SpikeBed(ForestRoomBuilder room, float x, float floor, int place)
        {
            room.Spikes(x - 0.75f, x + 0.75f, floor);
            Leaps(room, x, 0.75f, 1.2f, floor);
        }

        private static void Trap(ForestRoomBuilder room, float x, float floor, int place)
        {
            room.Flytrap(x, floor);
            Leaps(room, x, LevelHazards.MouthSize / 2f, 1.3f, floor);
        }

        // Take-offs lead units before each side of an obstacle of the given half width, one for each direction.
        private static void Leaps(ForestRoomBuilder room, float x, float half, float lead, float floor)
        {
            room.Leap(new Vector2(x - half - lead, floor), 1);
            room.Leap(new Vector2(x + half + lead, floor), -1);
        }

        // Crossing. A flat hall under an arched roof, like the Bramble Hop. On a world's first level one obstacle
        // stands in the middle. Deeper levels have two, with the landing between them.
        private static void Hall(ForestRoomBuilder room, Obstacle obstacle)
        {
            const float floor = -DoorY;
            room.Cave(new[]
            {
                (-DoorX, floor, floor + Clearance), (-3.8f, floor, floor + 4.4f), (-1.6f, floor, floor + 5.2f),
                (0f, floor, floor + 5.4f), (1.6f, floor, floor + 5.2f), (3.8f, floor, floor + 4.4f),
                (DoorX, floor, floor + Clearance)
            });
            if (room.Level <= 1)
            {
                obstacle(room, 0f, floor, 0);
                room.SetLanding(new Vector2(-3.0f, floor));
            }
            else
            {
                obstacle(room, -2.75f, floor, 0);
                obstacle(room, 2.75f, floor, 1);
                room.SetLanding(new Vector2(0f, floor));
            }
            room.Door(RoomDoor.LowLeft, Clearance);
            room.Door(RoomDoor.LowRight, Clearance);
        }

        // Switchback. The tall hollow of the Bramble Hollow: an obstacle on the upper shelf, a drop onto the island
        // and the floor, and an obstacle in the low gallery on the way back to the lower door.
        private static void Hollow(ForestRoomBuilder room, Obstacle obstacle)
        {
            CarveHollowSpace(room);
            obstacle(room, -2.6f, DoorY, 0);
            obstacle(room, -2.25f, -DoorY, 1);
            room.SetLanding(new Vector2(5.0f, -DoorY));
            room.Door(RoomDoor.HighLeft, Clearance);
            room.Door(RoomDoor.LowLeft, Clearance);
        }

        private static void CarveHollowSpace(ForestRoomBuilder room)
        {
            room.Cave(new[]
            {
                (-DoorX, -DoorY, 0f), (-3.2f, -DoorY, 1.4f), (-1.2f, -DoorY, 2.5f), (0.6f, -DoorY, 3.1f),
                (0.6f, -DoorY, 7.0f), (1.8f, -DoorY, 7.15f), (3.2f, -DoorY, 7.05f), (4.6f, -DoorY, 6.4f),
                (5.4f, -4.1f, 5.3f), (6.0f, -3.85f, 3.9f), (6.4f, -3.6f, 1.6f), (6.6f, -3.3f, -0.2f)
            }, Islet(-0.6f, 4.6f, 0.6f, 1.3f));
            room.Cave(new[] { (-DoorX, DoorY, 7.2f), (-1.2f, DoorY, 7.2f), (0.6f, DoorY, 7.0f) });
        }

        // Span. The bowl of the Bramble Bowl: shelves at door height, an island in the middle, and a sunken floor
        // with a ramp out toward the far door. Leap to the island and on past the top of the ramp.
        private static void CarveBowlSpace(ForestRoomBuilder room, float bowl)
        {
            room.Cave(new[]
            {
                (-DoorX, DoorY, 7.2f), (-3.5f, DoorY, 7.2f), (-3.5f, bowl, 7.2f), (0.5f, bowl, 7.2f),
                (4.2f, DoorY, 7.2f), (DoorX, DoorY, 7.2f)
            }, Islet(-1.4f, 1.4f, DoorY, 1.0f));
            room.SetLanding(new Vector2(0.2f, DoorY));
            room.Leap(new Vector2(-4.1f, DoorY), 1);
            room.Leap(new Vector2(0.2f, DoorY), 1);
            room.Leap(new Vector2(4.6f, DoorY), -1);
            room.Leap(new Vector2(0.2f, DoorY), -1);
            room.Door(RoomDoor.HighLeft, Clearance);
            room.Door(RoomDoor.HighRight, Clearance);
        }

        // Span. A pinball pit of cacti in the bowl. A fall bounces the player between them, and Nektar may wait
        // in a gap among them.
        private static void CarveCactusPit(ForestRoomBuilder room)
        {
            const float bowl = 1.0f;
            CarveBowlSpace(room, bowl);
            room.Cactus(-3.0f, bowl, false);
            room.Cactus(-1.6f, bowl, true);
            room.Cactus(-0.2f, bowl, false);
            room.Treasure(new Vector2(-0.95f, bowl));
        }

        // Span. A chasm floored with spikes. Falling in is fatal, so the leaps are the only way across.
        private static void CarveSpikeChasm(ForestRoomBuilder room)
        {
            const float bowl = 1.0f;
            CarveBowlSpace(room, bowl);
            room.Spikes(-3.5f, 0.5f, bowl);
        }

        // Crossing. A low hall with flytraps hanging from its roof on both sides of a dome, and one rooted in the
        // floor beneath the dome. Walk under the hanging ones and jump only under the dome.
        private static void CarveHangingGardens(ForestRoomBuilder room)
        {
            const float floor = -DoorY;
            const float roof = floor + Clearance;
            room.Cave(new[]
            {
                (-DoorX, floor, roof), (-2.2f, floor, roof), (-1.4f, floor, floor + 4.8f), (0f, floor, floor + 5.6f),
                (1.4f, floor, floor + 4.8f), (2.2f, floor, roof), (DoorX, floor, roof)
            });
            room.HangingFlytrap(-4.0f, roof);
            room.HangingFlytrap(4.0f, roof);
            Trap(room, 0f, floor, 0);
            room.SetLanding(new Vector2(-2.6f, floor));
            room.Door(RoomDoor.LowLeft, Clearance);
            room.Door(RoomDoor.LowRight, Clearance);
        }

        // Crossing. A flat hall under a tall dome with a laser hanging in it. Its beam sweeps the floor between
        // x -2.6 and 2.6, so the landing stands clear of it and a walker waits there for a gap, then runs across.
        // A world's first level has one arm turning a quarter turn a second. Deeper levels have two arms turning
        // a sixth of a turn a second.
        private static void CarveLighthouse(ForestRoomBuilder room)
        {
            const float floor = -DoorY;
            room.Cave(new[]
            {
                (-DoorX, floor, floor + Clearance), (-2.6f, floor, floor + 4.8f), (0f, floor, floor + 5.8f),
                (2.6f, floor, floor + 4.8f), (DoorX, floor, floor + Clearance)
            });
            bool first = room.Level <= 1;
            room.Laser(new Vector2(0f, floor + 3.4f), 3.6f, first ? 90f : 60f, first ? 1 : 2, 90f);
            room.SetLanding(new Vector2(-4.2f, floor));
            room.Wait(new Vector2(-4.2f, floor), 1, 7.6f);
            room.Wait(new Vector2(4.2f, floor), -1, 7.6f);
            room.Door(RoomDoor.LowLeft, Clearance);
            room.Door(RoomDoor.LowRight, Clearance);
        }

        // Switchback. The tall hollow with a laser hanging under the island. Its beam sweeps the low gallery
        // between x -1.7 and 3.3 and is blocked by the island above, so a walker waits at the landing before
        // running back under it to the lower door.
        private static void CarveLaserHollow(ForestRoomBuilder room)
        {
            CarveHollowSpace(room);
            bool first = room.Level <= 1;
            room.Laser(new Vector2(0.8f, -1.1f), 3.2f, first ? -90f : -60f, first ? 1 : 2, 90f);
            room.SetLanding(new Vector2(5.0f, -DoorY));
            room.Wait(new Vector2(5.0f, -DoorY), -1, 7.5f);
            room.Wait(new Vector2(-2.5f, -DoorY), 1, 7.5f);
            room.Door(RoomDoor.HighLeft, Clearance);
            room.Door(RoomDoor.LowLeft, Clearance);
        }
    }
}
