using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Insectum.Game
{
    // A room or suite stored as data rather than code, made with the Chunk Editor (Insectum > Chunk Editor) and
    // saved as JSON in Resources/Rooms/<World>/<id>.json. Its ops replay the same ForestRoomBuilder calls a code
    // template makes, each in its member's local units (doors at ±5.2, ±4.125, entered from the left), so a data
    // room carves exactly as a code room would. A data room whose id matches a code template replaces it.
    [Serializable]
    public sealed class RoomData
    {
        public string id = "";
        public string name = "";
        public string world = "Forest";
        public int weight = 1;
        // Zero for an ordinary room. Otherwise a hazard room, offered from this world level on.
        public int hazardLevel;

        // A single room fits these profile shapes (ForestRoomProfile.Shapes).
        public string[] shapes = Array.Empty<string>();

        // A suite spans steps.Length + 1 chunks, entered by one of the entry doors and left by one of the exits.
        public bool suite;
        public string[] entries = Array.Empty<string>();
        public int[] steps = Array.Empty<int>();
        public string[] exits = Array.Empty<string>();
        // Each member's center relative to the first, as x, y pairs, from the layout the room was drawn against.
        public float[] memberOffsets = Array.Empty<float>();

        public List<RoomOp> ops = new();

        public int Members => suite ? steps.Length + 1 : 1;

        public WorldId World => Enum.TryParse(world, out WorldId parsed) ? parsed : WorldId.Forest;

        public Vector2 MemberOffset(int member) =>
            member > 0 && member * 2 + 1 < memberOffsets.Length
                ? new Vector2(memberOffsets[member * 2], memberOffsets[member * 2 + 1])
                : Vector2.zero;

        public string ToJson() => JsonUtility.ToJson(this, true);

        public static RoomData FromJson(string json) => JsonUtility.FromJson<RoomData>(json);

        public RoomData Clone() => FromJson(ToJson());
    }

    // One builder call. Which fields matter depends on op:
    //   cave      points = x, floor, ceiling triples left to right; platforms = islands inside it
    //   hall      (x, y) to (x2, y2), headroom h1 at the start and h2 at the end
    //   door      door, clearance h1; flag = has a lip at (x2, y2)
    //   landing, treasure, trap   (x, y)
    //   enemy     (x, y), enemy = name, or empty for the world's usual pick
    //   hazard    kind = Bramble | Spikes | Flytrap | Cactus, from a to b, bottom c to top h1, flag = hanging
    //   laser     emitter (x, y), reach a, spin b, count arms, phase c
    //   wait      (x, y), count = direction, a = distance;  leap (x, y), count = direction
    //   tile      (x, y) = a point in the hex cell, material
    [Serializable]
    public sealed class RoomOp
    {
        public string op = "";
        public int member;
        public float[] points = Array.Empty<float>();
        public List<RoomPlatform> platforms = new();
        public float x, y, x2, y2, h1, h2, a, b, c;
        public int count;
        public bool flag;
        public string door = "";
        public string kind = "";
        public string enemy = "";
        public string material = "";

        public RoomOp Clone() => JsonUtility.FromJson<RoomOp>(JsonUtility.ToJson(this));
    }

    // Solid rock floating inside a cave. Kind is Islet, Pad, Slab, Crystal, Pod or Custom (points are
    // x, bottom, top triples left to right).
    [Serializable]
    public sealed class RoomPlatform
    {
        public string kind = "Islet";
        public float left, right, top, depth = 0.8f;
        public float[] points = Array.Empty<float>();

        public (float x, float bottom, float top)[] Outline() => kind switch
        {
            "Pad" => ForestRoomCatalog.Pad(left, right, top),
            "Slab" => ForestRoomCatalog.Slab(left, right, top - depth, top),
            "Crystal" => ForestRoomCatalog.Crystal(left, right, top - depth / 2f, depth / 2f),
            "Pod" => ForestRoomCatalog.Pod((left + right) / 2f, top - (right - left) / 2f, (right - left) / 2f),
            "Custom" => RoomDataTemplates.Triples(points),
            _ => ForestRoomCatalog.Islet(left, right, top, depth)
        };
    }

    internal static class RoomDataTemplates
    {
        internal static ForestRoomTemplate Create(RoomData data)
        {
            if (!data.suite)
                return new ForestRoomTemplate(data.id, Math.Max(1, data.weight), ForestRoomCatalog.Shape(data.shapes),
                    room => Replay(data, room), data.hazardLevel);
            var footprint = new SuiteFootprint(data.entries.Select(ParseDoor).ToArray(), data.steps.ToArray(),
                data.exits.Select(ParseDoor).ToArray());
            return new ForestRoomTemplate(data.id, Math.Max(1, data.weight), footprint, room => Replay(data, room));
        }

        internal static void Replay(RoomData data, ForestRoomBuilder room)
        {
            foreach (RoomOp op in data.ops)
            {
                ForestRoomBuilder at = room.ForMember(Mathf.Clamp(op.member, 0, room.Members - 1));
                var point = new Vector2(op.x, op.y);
                switch (op.op)
                {
                    case "cave":
                        at.Cave(Triples(op.points), op.platforms.Select(platform => platform.Outline()).ToArray());
                        break;
                    case "hall":
                        at.Hall(point, new Vector2(op.x2, op.y2), op.h1, op.h2);
                        break;
                    case "door":
                        at.Door(ParseDoor(op.door), op.h1, op.flag ? new Vector2(op.x2, op.y2) : null);
                        break;
                    case "landing":
                        at.SetLanding(point);
                        break;
                    case "treasure":
                        at.Treasure(point);
                        break;
                    case "trap":
                        at.Trap(point);
                        break;
                    case "enemy":
                        at.Enemy(point, op.enemy);
                        break;
                    case "hazard":
                        at.Hazard(Enum.TryParse(op.kind, out HazardKind kind) ? kind : HazardKind.Bramble,
                            op.a, op.b, op.c, op.h1, op.flag);
                        break;
                    case "laser":
                        at.Laser(point, op.a, op.b, op.count, op.c);
                        break;
                    case "wait":
                        at.Wait(point, op.count, op.a);
                        break;
                    case "leap":
                        at.Leap(point, op.count);
                        break;
                    case "tile":
                        if (Enum.TryParse(op.material, out TerrainMaterial material))
                            at.Tile(point, material);
                        break;
                }
            }
        }

        internal static RoomDoor ParseDoor(string door) => Enum.TryParse(door, out RoomDoor parsed) ? parsed : RoomDoor.None;

        internal static (float x, float a, float b)[] Triples(float[] values)
        {
            var result = new (float x, float a, float b)[values.Length / 3];
            for (int i = 0; i < result.Length; i++)
                result[i] = (values[i * 3], values[i * 3 + 1], values[i * 3 + 2]);
            return result;
        }

        internal static float[] Flatten((float x, float a, float b)[] triples)
        {
            var values = new float[triples.Length * 3];
            for (int i = 0; i < triples.Length; i++)
            {
                values[i * 3] = triples[i].x;
                values[i * 3 + 1] = triples[i].a;
                values[i * 3 + 2] = triples[i].b;
            }
            return values;
        }
    }

    // Loads the data rooms in Resources/Rooms/<World> once and merges them with a world's code templates.
    internal static class RoomLibrary
    {
        private static readonly Dictionary<WorldId, List<RoomData>> Loaded = new();
        private static readonly Dictionary<(WorldId, bool), IReadOnlyList<ForestRoomTemplate>> Merged = new();
        // Extra rooms not on disk, for tools that try a room out before it is saved.
        private static readonly Dictionary<WorldId, List<RoomData>> Injected = new();

        internal static IReadOnlyList<RoomData> For(WorldId world)
        {
            if (Loaded.TryGetValue(world, out List<RoomData> rooms))
                return rooms;
            rooms = new List<RoomData>();
            foreach (TextAsset asset in Resources.LoadAll<TextAsset>($"Rooms/{world}"))
            {
                try
                {
                    RoomData data = RoomData.FromJson(asset.text);
                    if (data != null && !string.IsNullOrEmpty(data.id))
                        rooms.Add(data);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"Room data {asset.name} could not be read: {exception.Message}");
                }
            }
            Loaded[world] = rooms;
            return rooms;
        }

        // The code templates with data rooms of the same id swapped in place, then the new data rooms.
        internal static IReadOnlyList<ForestRoomTemplate> Merge(WorldId world, IReadOnlyList<ForestRoomTemplate> code, bool suites)
        {
            if (Merged.TryGetValue((world, suites), out IReadOnlyList<ForestRoomTemplate> merged))
                return merged;
            var data = new Dictionary<string, RoomData>();
            foreach (RoomData room in For(world).Concat(Injected.GetValueOrDefault(world) ?? new List<RoomData>()))
                if (room.suite == suites)
                    data[room.id] = room;
            if (data.Count == 0)
                return Merged[(world, suites)] = code;
            var list = new List<ForestRoomTemplate>();
            foreach (ForestRoomTemplate template in code)
                list.Add(data.Remove(template.Id, out RoomData replacement) ? RoomDataTemplates.Create(replacement) : template);
            foreach (RoomData room in data.Values)
                list.Add(RoomDataTemplates.Create(room));
            return Merged[(world, suites)] = list;
        }

        internal static void Inject(RoomData room)
        {
            if (!Injected.TryGetValue(room.World, out List<RoomData> list))
                Injected[room.World] = list = new List<RoomData>();
            list.RemoveAll(other => other.id == room.id);
            list.Add(room);
            Merged.Clear();
        }

        internal static void Reload()
        {
            Loaded.Clear();
            Merged.Clear();
            Injected.Clear();
        }
    }

    // Records the builder calls of one code template as it carves, to open it in the Chunk Editor.
    internal sealed class RoomRecorder
    {
        internal static string WantId;
        internal static string WantShape;
        internal static RoomRecorder Captured;

        internal readonly RoomData Data = new();
        internal int Depth;
        // What the builder was made with, so a tool can carve the same room again.
        internal RoomSuite Suite;
        internal Vector2[] Centers;
        internal ForestRoomProfile[] Profiles;
        internal IReadOnlyList<IReadOnlyCollection<RoomDoor>> Openings;
        internal int Level;

        // Called by every new builder: starts recording when it carves the wanted template.
        internal static RoomRecorder Begin(RoomSuite suite, IReadOnlyList<ForestRoomProfile> profiles)
        {
            if (WantId == null || Captured != null || suite.Template.Id != WantId)
                return null;
            if (WantShape != null && profiles[0].Shape != WantShape)
                return null;
            return Captured = new RoomRecorder();
        }

        internal void Add(int member, RoomOp op)
        {
            if (Depth > 0)
                return;
            op.member = member;
            Data.ops.Add(op);
        }

        // Generates levels in the template's world until one carves it (with the given shape for a single room),
        // and returns its recorded calls as an editable room. Null when no level among the seeds uses it.
        internal static RoomRecorder Capture(WorldId world, string templateId, string shape, int seeds = 400)
        {
            WantId = templateId;
            WantShape = shape;
            Captured = null;
            try
            {
                int first = 1;
                while (RunWorlds.AtLevel(first) != world)
                    first += RunWorlds.LevelsPerWorld;
                for (int seed = 1; seed <= seeds && Captured == null; seed++)
                    for (int level = first; level < first + RunWorlds.LevelsPerWorld - 1 && Captured == null; level++)
                        RunWorlds.Generate(seed, level);
                RoomRecorder recorder = Captured;
                if (recorder == null)
                    return null;

                ForestRoomTemplate template = recorder.Suite.Template;
                RoomData data = recorder.Data;
                data.id = template.Id;
                data.name = template.Id;
                data.world = world.ToString();
                data.weight = template.Weight;
                data.hazardLevel = template.HazardLevel;
                data.suite = template.Footprint != null;
                if (data.suite)
                {
                    data.entries = template.Footprint.Entries.Select(door => door.ToString()).ToArray();
                    data.steps = template.Footprint.Steps.ToArray();
                    data.exits = template.Footprint.Exits.Select(door => door.ToString()).ToArray();
                }
                else
                    data.shapes = new[] { recorder.Profiles[0].Shape };
                data.memberOffsets = new float[recorder.Centers.Length * 2];
                for (int i = 0; i < recorder.Centers.Length; i++)
                {
                    Vector2 offset = recorder.Centers[i] - recorder.Centers[0];
                    data.memberOffsets[i * 2] = recorder.Suite.Mirrored ? -offset.x : offset.x;
                    data.memberOffsets[i * 2 + 1] = offset.y;
                }
                return recorder;
            }
            finally
            {
                WantId = null;
                WantShape = null;
                Captured = null;
            }
        }
    }
}
