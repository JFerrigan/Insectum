using UnityEngine;
using static Insectum.Game.ForestRoomCatalog;

namespace Insectum.Game
{
    // Bramble rooms put the Forest's thorns on the walking route. A bramble costs a heart on touch and throws the
    // player clear. Each room marks its take-off points so the verification walker leaps where a player would.
    //
    // A full jump at running speed rises about 2.06 units and lands about 4.3 units ahead on level ground, so a
    // take-off 1.2 units before a bed up to 2 units wide clears it with room to spare.
    internal static class ForestBrambleRooms
    {
        private const float Lead = 1.2f;

        internal static readonly ForestRoomTemplate Hop =
            new("Forest.Bramble.Hop", 1, Shape("Main.Crossing"), CarveHop, hazardLevel: 1);
        internal static readonly ForestRoomTemplate Canopy =
            new("Forest.Bramble.Canopy", 1, Shape("Main.Crossing"), CarveCanopy, hazardLevel: 2);
        internal static readonly ForestRoomTemplate Bowl =
            new("Forest.Bramble.Bowl", 1, Shape("Main.Span"), CarveBowl, hazardLevel: 2);
        internal static readonly ForestRoomTemplate Drop =
            new("Forest.Bramble.Drop", 1, Shape("Main.Descent"), CarveDrop, hazardLevel: 2);

        internal static readonly ForestRoomTemplate[] Templates = { Hop, Canopy, Bowl, Drop };

        // Crossing. A flat hall under an arched roof with bramble beds to hop. On a world's first level one bed
        // lies in the middle. Deeper levels have two, with the landing between them.
        private static void CarveHop(ForestRoomBuilder room)
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
                Bed(room, -0.75f, 0.75f, floor);
                room.SetLanding(new Vector2(-3.0f, floor));
            }
            else
            {
                Bed(room, -3.5f, -2.0f, floor);
                Bed(room, 2.0f, 3.5f, floor);
                room.SetLanding(new Vector2(0f, floor));
            }
            room.Door(RoomDoor.LowLeft, Clearance);
            room.Door(RoomDoor.LowRight, Clearance);
        }

        // Crossing. A low hall hung with brambles on both sides of a dome, with one bed on the floor beneath the
        // dome. Walk under the hanging thorns and save the jump for the dome.
        private static void CarveCanopy(ForestRoomBuilder room)
        {
            const float floor = -DoorY;
            const float roof = floor + Clearance;
            room.Cave(new[]
            {
                (-DoorX, floor, roof), (-2.2f, floor, roof), (-1.4f, floor, floor + 4.8f), (0f, floor, floor + 5.6f),
                (1.4f, floor, floor + 4.8f), (2.2f, floor, roof), (DoorX, floor, roof)
            });
            room.HangingBrambles(-4.5f, -2.5f, roof);
            room.HangingBrambles(2.5f, 4.5f, roof);
            Bed(room, -0.75f, 0.75f, floor);
            room.SetLanding(new Vector2(-3.2f, floor));
            room.Door(RoomDoor.LowLeft, Clearance);
            room.Door(RoomDoor.LowRight, Clearance);
        }

        // Span. Shelves at door height on both sides of a bowl choked with brambles. Leap to the island in the
        // middle, then on past the top of the far ramp. A fall costs a heart or two before the ramp lets the
        // player out, and Nektar may wait in the thorns beneath the island.
        private static void CarveBowl(ForestRoomBuilder room)
        {
            const float bowl = 1.0f;
            room.Cave(new[]
            {
                (-DoorX, DoorY, 7.2f), (-3.5f, DoorY, 7.2f), (-3.5f, bowl, 7.2f), (0.5f, bowl, 7.2f),
                (4.2f, DoorY, 7.2f), (DoorX, DoorY, 7.2f)
            }, Islet(-1.4f, 1.4f, DoorY, 1.0f));
            room.Brambles(-3.5f, 0.5f, bowl);
            room.SetLanding(new Vector2(0.2f, DoorY));
            room.Treasure(new Vector2(-1.5f, bowl));
            room.Leap(new Vector2(-4.1f, DoorY), 1);
            room.Leap(new Vector2(0.2f, DoorY), 1);
            room.Leap(new Vector2(4.6f, DoorY), -1);
            room.Leap(new Vector2(0.2f, DoorY), -1);
            room.Door(RoomDoor.HighLeft, Clearance);
            room.Door(RoomDoor.HighRight, Clearance);
        }

        // Descent. A cliff above a bramble bed, with more brambles hanging from the roof over the drop. Run off
        // the edge: stepping off slowly lands in the bed, and jumping off meets the hanging thorns.
        private static void CarveDrop(ForestRoomBuilder room)
        {
            const float terrace = 1.4f;
            room.Cave(new[]
            {
                (-DoorX, DoorY, 7.2f), (-3.0f, DoorY, 7.2f), (-3.0f, terrace, 7.2f), (0.6f, terrace, 7.2f),
                (1.5f, terrace, 6.9f), (1.5f, -1.5f, 6.9f), (1.9f, -1.65f, 6.8f), (3.2f, -2.62f, 6.4f),
                (4.2f, -3.38f, 4.6f), (DoorX, -DoorY, 1.0f)
            });
            room.Brambles(-3.0f, -1.5f, terrace);
            room.HangingBrambles(-2.5f, 0.5f, 7.2f);
            room.SetLanding(new Vector2(0.5f, terrace));
            room.Door(RoomDoor.HighLeft, Clearance);
            room.Door(RoomDoor.LowRight, Clearance);
        }

        // A bed on level floor with a take-off Lead units before each end, so it can be jumped either way.
        private static void Bed(ForestRoomBuilder room, float from, float to, float floor)
        {
            room.Brambles(from, to, floor);
            room.Leap(new Vector2(from - Lead, floor), 1);
            room.Leap(new Vector2(to + Lead, floor), -1);
        }
    }
}
