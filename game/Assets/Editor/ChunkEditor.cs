using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Insectum.Game.Editor
{
    // Insectum > Chunk Editor. Makes a new room or suite, or opens a saved one or any built-in one, and saves it
    // as JSON in Assets/Resources/Rooms/<World>/<id>.json, where the generator picks it up. The canvas shows what
    // the game would carve, by replaying the room through the real ForestRoomBuilder.
    internal sealed class ChunkEditor : EditorWindow
    {
        private enum Tool { Select, Cave, Platform, Hall, Landing, Treasure, Trap, Enemy, Hazard, Laser, Door, Tile, Erase }

        private static readonly string[] PlatformKinds = { "Islet", "Pad", "Slab", "Crystal", "Pod" };
        private static readonly string[] HazardKinds = { "Bramble", "Spikes", "Flytrap", "Cactus" };
        private static readonly string[] DoorNames = { "HighLeft", "LowLeft", "HighRight", "LowRight" };
        private const float HandleRadius = 7f;
        private const float Snap = 0.05f;

        // Start panel.
        private bool started;
        private WorldId newWorld = WorldId.Forest;
        private bool newSuite;
        private int newMembers = 2;
        private string newName = "My Room";
        private readonly HashSet<string> newShapes = new() { "Main.Switchback" };
        private WorldId importWorld = WorldId.Forest;
        private int importTemplate;
        private int importShape;
        private string status = "";

        // The room being edited.
        private RoomData data;
        private string savedPath;
        private bool dirty;
        private readonly List<string> undo = new();
        private readonly List<string> redo = new();

        // Canvas.
        private Tool tool = Tool.Select;
        private string platformKind = "Islet";
        private string hazardKind = "Bramble";
        private string enemyName = "";
        private TerrainMaterial tileMaterial = TerrainMaterial.SoftDirt;
        private float scale = 22f;
        private Vector2 pan;
        private int selected = -1;
        private int selectedHandle = -1;
        private bool dragging;
        private Vector2? hallStart;
        private Vector2 inspectorScroll;

        // Preview of what the game carves, rebuilt after every change.
        private Preview preview;

        [MenuItem("Insectum/Chunk Editor")]
        private static void Open() => GetWindow<ChunkEditor>("Chunk Editor");

        private void OnGUI()
        {
            if (!started || data == null)
            {
                DrawStart();
                return;
            }
            HandleShortcuts();
            DrawToolbar();
            using (new EditorGUILayout.HorizontalScope())
            {
                Rect canvas = GUILayoutUtility.GetRect(200, 10000, 200, 10000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                DrawCanvas(canvas);
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(330)))
                {
                    inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll);
                    DrawRoomSettings();
                    DrawOpList();
                    DrawInspector();
                    DrawProblems();
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        // ---------------- start panel ----------------

        private void DrawStart()
        {
            EditorGUILayout.LabelField("New chunk", EditorStyles.boldLabel);
            newWorld = (WorldId)EditorGUILayout.EnumPopup("World", newWorld);
            newName = EditorGUILayout.TextField("Name", newName);
            newSuite = EditorGUILayout.Toggle("Multi-chunk suite", newSuite);
            if (newSuite)
            {
                newMembers = EditorGUILayout.IntSlider("Chunks", newMembers, 2, 4);
                EditorGUILayout.HelpBox("Starts as a chain of switchbacks stacked down and back (the commonest route run). " +
                                        "Change the footprint in Room settings after creating it.", MessageType.None);
            }
            else
            {
                EditorGUILayout.LabelField("Fits profiles (room shapes)");
                DrawShapeToggles(newShapes);
            }
            if (GUILayout.Button("Create", GUILayout.Height(26)))
                CreateNew();

            EditorGUILayout.Space(14);
            EditorGUILayout.LabelField("Edit a saved chunk", EditorStyles.boldLabel);
            foreach (string path in SavedRooms())
                if (GUILayout.Button(path.Replace(RoomsRoot + "/", ""), EditorStyles.miniButton))
                    LoadSaved(path);
            if (!SavedRooms().Any())
                EditorGUILayout.LabelField("No saved chunks yet.", EditorStyles.miniLabel);

            EditorGUILayout.Space(14);
            EditorGUILayout.LabelField("Edit a built-in chunk", EditorStyles.boldLabel);
            importWorld = (WorldId)EditorGUILayout.EnumPopup("World", importWorld);
            List<ForestRoomTemplate> templates = ForestSuites.Code(importWorld).Concat(ForestRoomCatalog.Code(importWorld)).ToList();
            string[] ids = templates.Select(template => template.Id).ToArray();
            importTemplate = Mathf.Clamp(EditorGUILayout.Popup("Template", importTemplate, ids), 0, ids.Length - 1);
            bool isSuite = templates[importTemplate].Footprint != null;
            string[] shapes = isSuite ? Array.Empty<string>() : FittingShapes(importWorld, templates[importTemplate]);
            if (!isSuite)
                importShape = Mathf.Clamp(EditorGUILayout.Popup("Profile", importShape, shapes), 0, Math.Max(0, shapes.Length - 1));
            if (GUILayout.Button("Open", GUILayout.Height(22)) && (isSuite || shapes.Length > 0))
                Import(importWorld, ids[importTemplate], isSuite ? null : shapes[importShape]);
            EditorGUILayout.HelpBox("A built-in room opens as data. Saving it with the same id replaces the built-in room " +
                                    "in the game; change the id to make a new room from it instead.", MessageType.None);
            if (status.Length > 0)
                EditorGUILayout.HelpBox(status, MessageType.Info);
        }

        private static void DrawShapeToggles(HashSet<string> shapes)
        {
            foreach (string shape in ForestRoomProfile.Shapes)
            {
                bool on = EditorGUILayout.ToggleLeft(shape, shapes.Contains(shape));
                if (on) shapes.Add(shape);
                else shapes.Remove(shape);
            }
        }

        private void CreateNew()
        {
            var room = new RoomData
            {
                world = newWorld.ToString(),
                name = newName,
                id = $"{newWorld}.Data.{new string(newName.Where(char.IsLetterOrDigit).ToArray())}",
                suite = newSuite
            };
            if (newSuite)
            {
                room.entries = new[] { "HighLeft" };
                room.steps = Enumerable.Range(0, newMembers - 1).Select(i => i % 2 == 0 ? 3 : 1).ToArray();
                room.exits = new[] { newMembers % 2 == 0 ? "LowRight" : "LowLeft" };
                room.memberOffsets = Offsets(room.steps);
                for (int member = 0; member < room.Members; member++)
                {
                    room.ops.Add(new RoomOp { op = "cave", member = member, points = new[] { -5.2f, -1f, 3f, 5.2f, -1f, 3f } });
                    room.ops.Add(new RoomOp { op = "landing", member = member, x = 0f, y = -1f });
                }
                room.ops.Add(new RoomOp { op = "door", member = 0, door = "HighLeft", h1 = ForestTunnelLayout.Height });
                room.ops.Add(new RoomOp { op = "door", member = room.Members - 1, door = room.exits[0], h1 = ForestTunnelLayout.Height });
            }
            else
            {
                room.shapes = newShapes.ToArray();
                room.ops.Add(new RoomOp { op = "cave", points = new[] { -5.2f, -4.125f, 7f, 0f, -4.125f, 7.2f, 5.2f, -4.125f, 7f } });
                room.ops.Add(new RoomOp { op = "landing", x = 0f, y = -4.125f });
                foreach (string door in RequiredDoors(newWorld, room).SelectMany(set => set).Distinct())
                    room.ops.Add(new RoomOp { op = "door", door = door, h1 = ForestTunnelLayout.Height });
            }
            Begin(room, null);
        }

        private void Import(WorldId world, string id, string shape)
        {
            RoomRecorder recorder = RoomRecorder.Capture(world, id, shape);
            if (recorder == null)
            {
                status = $"{id} {shape} never appeared in 400 seeds, so it could not be opened.";
                return;
            }
            Begin(recorder.Data, null);
        }

        private void LoadSaved(string path)
        {
            try
            {
                Begin(RoomData.FromJson(File.ReadAllText(path)), path);
            }
            catch (Exception exception)
            {
                status = $"Could not read {path}: {exception.Message}";
            }
        }

        private void Begin(RoomData room, string path)
        {
            if (room.suite && room.memberOffsets.Length < room.Members * 2)
                room.memberOffsets = Offsets(room.steps);
            data = room;
            savedPath = path;
            dirty = path == null;
            undo.Clear();
            redo.Clear();
            selected = -1;
            started = true;
            pan = Vector2.zero;
            Rebuild();
        }

        // Member centers relative to the first for a run of route steps in canonical orientation.
        private static float[] Offsets(int[] steps)
        {
            var chunk = Vector2Int.zero;
            Vector2 origin = HexGrid.Center(ForestTemplates.MacroCenter(chunk));
            var offsets = new List<float> { 0f, 0f };
            foreach (int step in steps)
            {
                chunk = HexGrid.Neighbor(chunk, step);
                Vector2 center = HexGrid.Center(ForestTemplates.MacroCenter(chunk)) - origin;
                offsets.Add(center.x);
                offsets.Add(center.y);
            }
            return offsets.ToArray();
        }

        private const string RoomsRoot = "Assets/Resources/Rooms";

        private static IEnumerable<string> SavedRooms() =>
            Directory.Exists(RoomsRoot)
                ? Directory.GetFiles(RoomsRoot, "*.json", SearchOption.AllDirectories).Select(path => path.Replace('\\', '/')).OrderBy(path => path)
                : Enumerable.Empty<string>();

        // ---------------- toolbar ----------------

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("◀ Chunks", EditorStyles.toolbarButton, GUILayout.Width(70)) && ConfirmLeave())
                    started = false;
                GUILayout.Label($"{data.name}{(dirty ? " *" : "")}", EditorStyles.toolbarButton);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(undo.Count == 0))
                    if (GUILayout.Button("Undo", EditorStyles.toolbarButton)) Undo();
                using (new EditorGUI.DisabledScope(redo.Count == 0))
                    if (GUILayout.Button("Redo", EditorStyles.toolbarButton)) Redo();
                if (GUILayout.Button("Save", EditorStyles.toolbarButton)) Save();
                if (GUILayout.Button("Preview in level", EditorStyles.toolbarButton)) PreviewInLevel();
                if (GUILayout.Button("Play here", EditorStyles.toolbarButton)) PlayHere();
            }
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                foreach (Tool each in Enum.GetValues(typeof(Tool)))
                    if (GUILayout.Toggle(tool == each, each.ToString(), EditorStyles.toolbarButton) && tool != each)
                    {
                        tool = each;
                        hallStart = null;
                    }
            }
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                switch (tool)
                {
                    case Tool.Platform:
                        platformKind = PlatformKinds[EditorGUILayout.Popup(Array.IndexOf(PlatformKinds, platformKind), PlatformKinds, EditorStyles.toolbarPopup, GUILayout.Width(90))];
                        GUILayout.Label("Click inside a cave to float a platform there.");
                        break;
                    case Tool.Hazard:
                        hazardKind = HazardKinds[EditorGUILayout.Popup(Array.IndexOf(HazardKinds, hazardKind), HazardKinds, EditorStyles.toolbarPopup, GUILayout.Width(90))];
                        GUILayout.Label("Click a flat floor. Shift-click hangs it from the roof above.");
                        break;
                    case Tool.Enemy:
                        string[] names = new[] { "(world's usual)" }.Concat(EnemySpec.Regulars.Select(spec => spec.Name)).ToArray();
                        int index = Math.Max(0, Array.IndexOf(names, enemyName));
                        index = EditorGUILayout.Popup(index, names, EditorStyles.toolbarPopup, GUILayout.Width(140));
                        enemyName = index == 0 ? "" : names[index];
                        GUILayout.Label("Click a floor point.");
                        break;
                    case Tool.Tile:
                        tileMaterial = (TerrainMaterial)EditorGUILayout.EnumPopup(tileMaterial, EditorStyles.toolbarPopup, GUILayout.Width(120));
                        GUILayout.Label("Click or drag over rock to paint its hex cells.");
                        break;
                    case Tool.Cave:
                        GUILayout.Label("Click to add a cave. In Select, ctrl-click a cave to add a point; Delete removes one.");
                        break;
                    case Tool.Hall:
                        GUILayout.Label(hallStart == null ? "Click the floor where the hall starts." : "Click where it ends.");
                        break;
                    case Tool.Door:
                        GUILayout.Label("Click near a door anchor (the circles) to add or remove a door.");
                        break;
                    default:
                        GUILayout.Label("Drag handles to move. Scroll to zoom, middle- or right-drag to pan. Delete removes the selection.");
                        break;
                }
                GUILayout.FlexibleSpace();
            }
        }

        // ---------------- canvas ----------------

        private Vector2 origin;

        private Vector2 ToScreen(Vector2 local) => origin + new Vector2(local.x, -local.y) * scale;

        private Vector2 ToLocal(Vector2 screen) => new((screen.x - origin.x) / scale, -(screen.y - origin.y) / scale);

        private static float Snapped(float value) => Mathf.Round(value / Snap) * Snap;

        private Vector2 Offset(int member) => data.MemberOffset(member);

        private void DrawCanvas(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.42f, 0.47f, 0.33f));
            Vector2 middle = Vector2.zero;
            for (int i = 0; i < data.Members; i++)
                middle += Offset(i);
            middle /= data.Members;
            origin = rect.center + pan - new Vector2(middle.x, -middle.y) * scale;

            GUI.BeginClip(rect);
            origin -= rect.position;
            Handles.BeginGUI();
            DrawGrid();
            DrawAir();
            DrawItems();
            Handles.EndGUI();
            GUI.EndClip();
            origin += rect.position;

            HandleInput(rect);
        }

        private void DrawGrid()
        {
            Handles.color = new Color(1f, 1f, 1f, 0.08f);
            for (int member = 0; member < data.Members; member++)
            {
                Vector2Int center = HexGrid.CellAt(Offset(member));
                foreach (Vector2Int cell in ChunkCells(center))
                    DrawCell(cell, false);
                Handles.color = new Color(1f, 1f, 1f, 0.5f);
                foreach (string door in DoorNames)
                {
                    Vector2 anchor = ToScreen(Offset(member) + ForestRoomBuilder.Anchor(RoomDataTemplates.ParseDoor(door)));
                    Handles.DrawWireDisc(anchor, Vector3.forward, 6f);
                }
                Handles.Label(ToScreen(Offset(member) + new Vector2(-7.6f, 6.8f)), $"chunk {member}", EditorStyles.whiteMiniLabel);
                Handles.color = new Color(1f, 1f, 1f, 0.08f);
            }
            Handles.color = Color.white;
        }

        private static IEnumerable<Vector2Int> ChunkCells(Vector2Int center)
        {
            for (int r = -HexGrid.MacroRadius; r <= HexGrid.MacroRadius; r++)
                for (int q = Math.Max(-HexGrid.MacroRadius, -HexGrid.MacroRadius - r); q <= Math.Min(HexGrid.MacroRadius, HexGrid.MacroRadius - r); q++)
                    yield return center + new Vector2Int(q, r);
        }

        private void DrawCell(Vector2Int cell, bool fill)
        {
            var corners = new Vector3[7];
            for (int side = 0; side < 6; side++)
                corners[side] = ToScreen(HexGrid.Vertex(HexGrid.Corner(cell, side)));
            corners[6] = corners[0];
            if (fill)
                Handles.DrawAAConvexPolygon(corners.Take(6).ToArray());
            else
                Handles.DrawPolyLine(corners);
        }

        private void DrawAir()
        {
            if (preview == null)
                return;
            Handles.color = new Color(0.08f, 0.06f, 0.12f, 1f);
            foreach (IReadOnlyList<Vector2> outline in preview.Passages)
                Handles.DrawAAConvexPolygon(outline.Select(point => (Vector3)ToScreen(point)).ToArray());
            foreach (var (cell, material) in preview.Tiles)
            {
                Handles.color = MaterialColor(material);
                DrawCell(cell, true);
            }
            foreach (TunnelHazard hazard in preview.Hazards)
            {
                Rect area = hazard.Area;
                Vector2 a = ToScreen(new Vector2(area.xMin, area.yMax));
                Vector2 b = ToScreen(new Vector2(area.xMax, area.yMin));
                EditorGUI.DrawRect(Rect.MinMaxRect(a.x, a.y, b.x, b.y), hazard.Kind == HazardKind.Laser
                    ? new Color(1f, 0.2f, 0.2f, 0.9f) : new Color(0.85f, 0.35f, 0.15f, 0.85f));
            }
            Handles.color = Color.white;
        }

        private static Color MaterialColor(TerrainMaterial material) =>
            Color.HSVToRGB((int)material * 0.137f % 1f, 0.5f, 0.75f) * new Color(1f, 1f, 1f, 0.85f);

        private void DrawItems()
        {
            for (int index = 0; index < data.ops.Count; index++)
            {
                RoomOp op = data.ops[index];
                bool chosen = index == selected;
                foreach (Handle handle in HandlesOf(index))
                {
                    Color color = op.op switch
                    {
                        "landing" => new Color(0.3f, 0.95f, 0.45f),
                        "treasure" => new Color(1f, 0.85f, 0.1f),
                        "trap" => new Color(1f, 0.55f, 0.1f),
                        "enemy" => new Color(1f, 0.3f, 0.3f),
                        "door" => new Color(0.5f, 0.8f, 1f),
                        "hazard" or "laser" => new Color(1f, 0.4f, 0.2f),
                        "tile" => new Color(0.8f, 0.8f, 0.8f),
                        _ => new Color(0.75f, 0.7f, 0.95f)
                    };
                    if (chosen)
                        color = handle.Id == selectedHandle ? Color.white : Color.Lerp(color, Color.white, 0.4f);
                    Handles.color = color;
                    Vector2 screen = ToScreen(handle.Position);
                    if (op.op is "cave" or "hall")
                        Handles.DrawSolidDisc(screen, Vector3.forward, chosen ? 4f : 2.5f);
                    else
                        Handles.DrawSolidDisc(screen, Vector3.forward, 5f);
                }
                if (op.op == "cave" && chosen)
                {
                    Handles.color = new Color(1f, 1f, 1f, 0.6f);
                    var triples = RoomDataTemplates.Triples(op.points);
                    Vector2 shift = Offset(op.member);
                    Handles.DrawPolyLine(triples.Select(t => (Vector3)ToScreen(shift + new Vector2(t.x, t.a))).ToArray());
                    Handles.DrawPolyLine(triples.Select(t => (Vector3)ToScreen(shift + new Vector2(t.x, t.b))).ToArray());
                }
                if (op.op == "enemy")
                    Handles.Label(ToScreen(Offset(op.member) + new Vector2(op.x, op.y)) + new Vector2(6, -16),
                        string.IsNullOrEmpty(op.enemy) ? "enemy" : op.enemy, EditorStyles.whiteMiniLabel);
            }
            if (hallStart is Vector2 start)
            {
                Handles.color = Color.white;
                Handles.DrawDottedLine(ToScreen(start), Event.current.mousePosition, 3f);
            }
            Handles.color = Color.white;
        }

        private readonly struct Handle
        {
            public readonly int Id;
            public readonly Vector2 Position;

            public Handle(int id, Vector2 position)
            {
                Id = id;
                Position = position;
            }
        }

        // Draggable points of an op, in suite-local units. Ids encode what each moves.
        private IEnumerable<Handle> HandlesOf(int index)
        {
            RoomOp op = data.ops[index];
            Vector2 shift = Offset(op.member);
            switch (op.op)
            {
                case "cave":
                    for (int i = 0; i < op.points.Length / 3; i++)
                    {
                        yield return new Handle(i * 2, shift + new Vector2(op.points[i * 3], op.points[i * 3 + 1]));
                        yield return new Handle(i * 2 + 1, shift + new Vector2(op.points[i * 3], op.points[i * 3 + 2]));
                    }
                    for (int p = 0; p < op.platforms.Count; p++)
                    {
                        RoomPlatform platform = op.platforms[p];
                        if (platform.kind == "Custom")
                            continue;
                        yield return new Handle(1000 + p * 2, shift + new Vector2(platform.left, platform.top));
                        yield return new Handle(1000 + p * 2 + 1, shift + new Vector2(platform.right, platform.top));
                    }
                    break;
                case "hall":
                    yield return new Handle(0, shift + new Vector2(op.x, op.y));
                    yield return new Handle(1, shift + new Vector2(op.x2, op.y2));
                    break;
                case "hazard":
                    float edge = op.flag ? op.h1 : op.c;
                    yield return new Handle(0, shift + new Vector2(op.a, edge));
                    yield return new Handle(1, shift + new Vector2(op.b, edge));
                    break;
                case "door":
                    if (op.flag)
                        yield return new Handle(0, shift + new Vector2(op.x2, op.y2));
                    break;
                default:
                    yield return new Handle(0, shift + new Vector2(op.x, op.y));
                    break;
            }
        }

        private void MoveHandle(RoomOp op, int id, Vector2 local)
        {
            Vector2 p = local - Offset(op.member);
            float x = Snapped(p.x), y = Snapped(p.y);
            switch (op.op)
            {
                case "cave" when id >= 1000:
                    RoomPlatform platform = op.platforms[(id - 1000) / 2];
                    if ((id - 1000) % 2 == 0) platform.left = x;
                    else platform.right = x;
                    platform.top = y;
                    break;
                case "cave":
                    int point = id / 2;
                    op.points[point * 3] = x;
                    op.points[point * 3 + (id % 2 == 0 ? 1 : 2)] = y;
                    break;
                case "hall":
                    if (id == 0) { op.x = x; op.y = y; }
                    else { op.x2 = x; op.y2 = y; }
                    break;
                case "hazard":
                    if (id == 0) op.a = x;
                    else op.b = x;
                    float height = op.h1 - op.c;
                    if (op.flag) { op.h1 = y; op.c = y - height; }
                    else { op.c = y; op.h1 = y + height; }
                    break;
                case "door":
                    op.x2 = x;
                    op.y2 = y;
                    break;
                default:
                    op.x = x;
                    op.y = y;
                    break;
            }
        }

        private void HandleInput(Rect rect)
        {
            Event e = Event.current;
            if (!rect.Contains(e.mousePosition) && !dragging)
                return;
            Vector2 local = ToLocal(e.mousePosition);
            switch (e.type)
            {
                case EventType.ScrollWheel:
                    float before = scale;
                    scale = Mathf.Clamp(scale * (e.delta.y > 0 ? 0.9f : 1.1f), 6f, 90f);
                    pan += (e.mousePosition - origin) * (1f - scale / before);
                    e.Use();
                    Repaint();
                    break;
                case EventType.MouseDrag when e.button is 1 or 2:
                    pan += e.delta;
                    e.Use();
                    Repaint();
                    break;
                case EventType.MouseDown when e.button == 0:
                    OnClick(local, e);
                    e.Use();
                    Repaint();
                    break;
                case EventType.MouseDrag when e.button == 0 && dragging && selected >= 0:
                    MoveHandle(data.ops[selected], selectedHandle, local);
                    dirty = true;
                    Rebuild();
                    e.Use();
                    Repaint();
                    break;
                case EventType.MouseDrag when e.button == 0 && tool == Tool.Tile:
                    PaintTile(local, false);
                    e.Use();
                    Repaint();
                    break;
                case EventType.MouseUp when e.button == 0:
                    dragging = false;
                    break;
            }
        }

        private (int op, int handle) HandleAt(Vector2 screen)
        {
            (int, int) best = (-1, -1);
            float bestDistance = HandleRadius;
            for (int index = data.ops.Count - 1; index >= 0; index--)
                foreach (Handle handle in HandlesOf(index))
                {
                    float distance = Vector2.Distance(ToScreen(handle.Position), screen);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = (index, handle.Id);
                    }
                }
            return best;
        }

        private int NearestMember(Vector2 local)
        {
            int best = 0;
            for (int member = 1; member < data.Members; member++)
                if (Vector2.Distance(local, Offset(member)) < Vector2.Distance(local, Offset(best)))
                    best = member;
            return best;
        }

        private RoomOp AddOp(string kind, Vector2 local)
        {
            int member = NearestMember(local);
            Vector2 p = local - Offset(member);
            var op = new RoomOp { op = kind, member = member, x = Snapped(p.x), y = Snapped(p.y) };
            data.ops.Add(op);
            selected = data.ops.Count - 1;
            selectedHandle = 0;
            return op;
        }

        private void OnClick(Vector2 local, Event e)
        {
            Vector2 screen = e.mousePosition;
            switch (tool)
            {
                case Tool.Select:
                {
                    var (index, handle) = HandleAt(screen);
                    if (index >= 0 && e.control && data.ops[index].op == "cave")
                    {
                        Record();
                        InsertCavePoint(data.ops[index], local);
                        selected = index;
                        Rebuild();
                        return;
                    }
                    selected = index;
                    selectedHandle = handle;
                    if (index >= 0)
                    {
                        Record();
                        dragging = true;
                    }
                    else if (e.control)
                    {
                        int cave = CaveAt(local);
                        if (cave >= 0)
                        {
                            Record();
                            InsertCavePoint(data.ops[cave], local);
                            selected = cave;
                            Rebuild();
                        }
                    }
                    return;
                }
                case Tool.Erase:
                {
                    var (index, _) = HandleAt(screen);
                    if (index < 0)
                        index = TileOpAt(local);
                    if (index >= 0)
                    {
                        Record();
                        data.ops.RemoveAt(index);
                        selected = -1;
                        Rebuild();
                    }
                    return;
                }
            }

            Record();
            switch (tool)
            {
                case Tool.Cave:
                {
                    RoomOp op = AddOp("cave", local);
                    op.points = new[] { op.x - 2f, op.y, op.y + 3.1f, op.x + 2f, op.y, op.y + 3.1f };
                    break;
                }
                case Tool.Platform:
                {
                    int cave = CaveAt(local);
                    if (cave < 0)
                    {
                        status = "Click inside a cave to place a platform.";
                        undo.RemoveAt(undo.Count - 1);
                        return;
                    }
                    RoomOp op = data.ops[cave];
                    Vector2 p = local - Offset(op.member);
                    op.platforms.Add(new RoomPlatform { kind = platformKind, left = Snapped(p.x - 1f), right = Snapped(p.x + 1f), top = Snapped(p.y), depth = 0.8f });
                    selected = cave;
                    break;
                }
                case Tool.Hall:
                    if (hallStart == null)
                    {
                        hallStart = local;
                        undo.RemoveAt(undo.Count - 1);
                        return;
                    }
                    else
                    {
                        Vector2 start = hallStart.Value;
                        hallStart = null;
                        RoomOp op = AddOp("hall", start);
                        Vector2 end = local - Offset(op.member);
                        op.x2 = Snapped(end.x);
                        op.y2 = Snapped(end.y);
                        op.h1 = op.h2 = ForestTunnelLayout.Height;
                    }
                    break;
                case Tool.Landing:
                {
                    int member = NearestMember(local);
                    data.ops.RemoveAll(other => other.op == "landing" && other.member == member);
                    AddOp("landing", local);
                    break;
                }
                case Tool.Treasure:
                    AddOp("treasure", local);
                    break;
                case Tool.Trap:
                    AddOp("trap", local);
                    break;
                case Tool.Enemy:
                    AddOp("enemy", local).enemy = enemyName;
                    break;
                case Tool.Hazard:
                {
                    RoomOp op = AddOp("hazard", local);
                    op.kind = hazardKind;
                    float height = hazardKind switch { "Spikes" => LevelHazards.SpikeHeight, "Flytrap" => LevelHazards.MouthSize, "Cactus" => 1.3f, _ => LevelHazards.BrambleHeight };
                    float half = hazardKind is "Flytrap" or "Cactus" ? 0.4f : 0.75f;
                    op.a = Snapped(op.x - half);
                    op.b = Snapped(op.x + half);
                    op.flag = e.shift;
                    op.c = e.shift ? op.y - height : op.y;
                    op.h1 = e.shift ? op.y : op.y + height;
                    break;
                }
                case Tool.Laser:
                {
                    RoomOp op = AddOp("laser", local);
                    op.a = 2.5f;
                    op.b = 45f;
                    op.count = 2;
                    break;
                }
                case Tool.Door:
                    ToggleDoor(local);
                    break;
                case Tool.Tile:
                    PaintTile(local, true);
                    break;
            }
            dirty = true;
            Rebuild();
        }

        private void ToggleDoor(Vector2 local)
        {
            int member = NearestMember(local);
            string best = null;
            float bestDistance = 1.5f;
            foreach (string door in DoorNames)
            {
                float distance = Vector2.Distance(local, Offset(member) + ForestRoomBuilder.Anchor(RoomDataTemplates.ParseDoor(door)));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = door;
                }
            }
            if (best == null)
            {
                status = "Click nearer one of the door circles.";
                return;
            }
            int existing = data.ops.FindIndex(op => op.op == "door" && op.member == member && op.door == best);
            if (existing >= 0)
                data.ops.RemoveAt(existing);
            else
                data.ops.Add(new RoomOp { op = "door", member = member, door = best, h1 = ForestTunnelLayout.Height });
        }

        private void PaintTile(Vector2 local, bool first)
        {
            Vector2Int cell = HexGrid.CellAt(local);
            int member = NearestMember(local);
            int existing = TileOpAt(local);
            if (existing >= 0 && data.ops[existing].material == tileMaterial.ToString())
                return;
            if (!first)
                Record();
            if (existing >= 0)
                data.ops.RemoveAt(existing);
            Vector2 center = HexGrid.Center(cell) - Offset(member);
            data.ops.Add(new RoomOp { op = "tile", member = member, x = center.x, y = center.y, material = tileMaterial.ToString() });
            dirty = true;
            Rebuild();
        }

        private int TileOpAt(Vector2 local)
        {
            Vector2Int cell = HexGrid.CellAt(local);
            return data.ops.FindIndex(op => op.op == "tile" && HexGrid.CellAt(Offset(op.member) + new Vector2(op.x, op.y)) == cell);
        }

        private int CaveAt(Vector2 local)
        {
            if (selected >= 0 && data.ops[selected].op == "cave" && InsideCaveSpan(data.ops[selected], local))
                return selected;
            for (int index = data.ops.Count - 1; index >= 0; index--)
                if (data.ops[index].op == "cave" && InsideCaveSpan(data.ops[index], local))
                    return index;
            return -1;
        }

        private bool InsideCaveSpan(RoomOp cave, Vector2 local)
        {
            var points = RoomDataTemplates.Triples(cave.points);
            Vector2 p = local - Offset(cave.member);
            for (int i = 0; i + 1 < points.Length; i++)
                if (points[i].x <= p.x && p.x <= points[i + 1].x && points[i + 1].x > points[i].x)
                {
                    float t = (p.x - points[i].x) / (points[i + 1].x - points[i].x);
                    return p.y >= Mathf.Lerp(points[i].a, points[i + 1].a, t) - 0.2f && p.y <= Mathf.Lerp(points[i].b, points[i + 1].b, t) + 0.2f;
                }
            return false;
        }

        private void InsertCavePoint(RoomOp cave, Vector2 local)
        {
            var points = RoomDataTemplates.Triples(cave.points).ToList();
            float x = Snapped((local - Offset(cave.member)).x);
            int at = points.FindIndex(point => point.x > x);
            if (at <= 0)
            {
                float floor = at == 0 ? points[0].a : points[^1].a, ceiling = at == 0 ? points[0].b : points[^1].b;
                points.Insert(at == 0 ? 0 : points.Count, (x, floor, ceiling));
            }
            else
            {
                var a = points[at - 1];
                var b = points[at];
                float t = (x - a.x) / Mathf.Max(0.001f, b.x - a.x);
                points.Insert(at, (x, Mathf.Lerp(a.a, b.a, t), Mathf.Lerp(a.b, b.b, t)));
            }
            cave.points = RoomDataTemplates.Flatten(points.ToArray());
            dirty = true;
        }

        private void HandleShortcuts()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown || GUIUtility.keyboardControl != 0)
                return;
            if ((e.command || e.control) && e.keyCode == KeyCode.Z)
            {
                if (e.shift) Redo();
                else Undo();
                e.Use();
            }
            else if ((e.command || e.control) && e.keyCode == KeyCode.S)
            {
                Save();
                e.Use();
            }
            else if (e.keyCode is KeyCode.Delete or KeyCode.Backspace && selected >= 0)
            {
                Record();
                RoomOp op = data.ops[selected];
                if (op.op == "cave" && selectedHandle >= 1000)
                    op.platforms.RemoveAt((selectedHandle - 1000) / 2);
                else if (op.op == "cave" && selectedHandle >= 0 && op.points.Length > 6)
                {
                    var points = RoomDataTemplates.Triples(op.points).ToList();
                    points.RemoveAt(selectedHandle / 2);
                    op.points = RoomDataTemplates.Flatten(points.ToArray());
                }
                else
                {
                    data.ops.RemoveAt(selected);
                    selected = -1;
                }
                selectedHandle = -1;
                dirty = true;
                Rebuild();
                e.Use();
            }
        }

        // ---------------- side panel ----------------

        private bool showSettings = true;

        private void DrawRoomSettings()
        {
            showSettings = EditorGUILayout.Foldout(showSettings, "Room settings", true, EditorStyles.foldoutHeader);
            if (!showSettings)
                return;
            EditorGUI.BeginChangeCheck();
            string before = data.ToJson();
            data.name = EditorGUILayout.TextField("Name", data.name);
            data.id = EditorGUILayout.TextField("Id", data.id);
            if (IsBuiltIn(data.id))
                EditorGUILayout.HelpBox("This id belongs to a built-in room: saving replaces it in the game.", MessageType.Warning);
            data.world = ((WorldId)EditorGUILayout.EnumPopup("World", data.World)).ToString();
            data.weight = Mathf.Max(1, EditorGUILayout.IntField("Weight", data.weight));
            if (!data.suite)
            {
                data.hazardLevel = EditorGUILayout.IntSlider(new GUIContent("Hazard level", "0 = ordinary room. Otherwise a hazard room, from this world level on."), data.hazardLevel, 0, 3);
                EditorGUILayout.LabelField("Fits profiles");
                var shapes = new HashSet<string>(data.shapes);
                DrawShapeToggles(shapes);
                data.shapes = ForestRoomProfile.Shapes.Where(shapes.Contains).ToArray();
            }
            else
            {
                EditorGUILayout.LabelField($"Suite of {data.Members} chunks", EditorStyles.miniBoldLabel);
                data.entries = CommaList("Entry doors", data.entries);
                data.exits = CommaList("Exit doors", data.exits);
                string steps = EditorGUILayout.TextField(new GUIContent("Steps", "Route sides between members, canonical orientation: 0 up-right, 1 down-right, 3 down-left, 4 up-left"), string.Join(",", data.steps));
                int[] parsed = steps.Split(',').Select(part => int.TryParse(part.Trim(), out int step) ? step : -1).Where(step => step is 0 or 1 or 3 or 4).ToArray();
                if (parsed.Length >= 1 && parsed.Length <= 3 && !parsed.SequenceEqual(data.steps))
                {
                    data.steps = parsed;
                    data.memberOffsets = Offsets(parsed);
                }
            }
            if (EditorGUI.EndChangeCheck())
            {
                PushUndo(before);
                dirty = true;
                Rebuild();
            }
        }

        private static string[] CommaList(string label, string[] values)
        {
            string text = EditorGUILayout.TextField(new GUIContent(label, "Comma list of HighLeft, LowLeft, HighRight, LowRight"), string.Join(",", values));
            return text.Split(',').Select(part => part.Trim()).Where(part => DoorNames.Contains(part)).ToArray();
        }

        private void DrawOpList()
        {
            EditorGUILayout.LabelField("Contents", EditorStyles.boldLabel);
            for (int index = 0; index < data.ops.Count; index++)
            {
                RoomOp op = data.ops[index];
                string label = op.op switch
                {
                    "cave" => $"cave ({op.points.Length / 3} points, {op.platforms.Count} platforms)",
                    "door" => $"door {op.door}",
                    "hazard" => $"hazard {op.kind}{(op.flag ? " hanging" : "")}",
                    "enemy" => $"enemy {(string.IsNullOrEmpty(op.enemy) ? "(usual)" : op.enemy)}",
                    "tile" => $"tile {op.material}",
                    _ => op.op
                };
                if (data.Members > 1)
                    label = $"[{op.member}] {label}";
                if (GUILayout.Toggle(index == selected, label, EditorStyles.miniButtonLeft) && index != selected)
                {
                    selected = index;
                    selectedHandle = -1;
                }
            }
        }

        private void DrawInspector()
        {
            if (selected < 0 || selected >= data.ops.Count)
                return;
            RoomOp op = data.ops[selected];
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField($"Selected: {op.op}", EditorStyles.boldLabel);
            string before = data.ToJson();
            EditorGUI.BeginChangeCheck();
            if (data.Members > 1)
                op.member = EditorGUILayout.IntSlider("Chunk", op.member, 0, data.Members - 1);
            switch (op.op)
            {
                case "cave":
                    EditorGUILayout.LabelField("x / floor / ceiling", EditorStyles.miniLabel);
                    for (int i = 0; i < op.points.Length / 3; i++)
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            op.points[i * 3] = EditorGUILayout.FloatField(op.points[i * 3]);
                            op.points[i * 3 + 1] = EditorGUILayout.FloatField(op.points[i * 3 + 1]);
                            op.points[i * 3 + 2] = EditorGUILayout.FloatField(op.points[i * 3 + 2]);
                        }
                    for (int p = 0; p < op.platforms.Count; p++)
                    {
                        RoomPlatform platform = op.platforms[p];
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField($"Platform {p}", GUILayout.Width(70));
                            if (platform.kind == "Custom")
                            {
                                EditorGUILayout.LabelField($"Custom outline ({platform.points.Length / 3} points)");
                                continue;
                            }
                            platform.kind = PlatformKinds[EditorGUILayout.Popup(Math.Max(0, Array.IndexOf(PlatformKinds, platform.kind)), PlatformKinds)];
                        }
                        if (platform.kind == "Custom")
                            continue;
                        platform.left = EditorGUILayout.FloatField("  Left", platform.left);
                        platform.right = EditorGUILayout.FloatField("  Right", platform.right);
                        platform.top = EditorGUILayout.FloatField("  Top", platform.top);
                        platform.depth = EditorGUILayout.FloatField("  Depth", platform.depth);
                    }
                    break;
                case "hall":
                    op.x = EditorGUILayout.FloatField("From x", op.x);
                    op.y = EditorGUILayout.FloatField("From floor", op.y);
                    op.x2 = EditorGUILayout.FloatField("To x", op.x2);
                    op.y2 = EditorGUILayout.FloatField("To floor", op.y2);
                    op.h1 = EditorGUILayout.FloatField("Headroom at start", op.h1);
                    op.h2 = EditorGUILayout.FloatField("Headroom at end", op.h2);
                    break;
                case "door":
                    op.door = DoorNames[EditorGUILayout.Popup("Door", Math.Max(0, Array.IndexOf(DoorNames, op.door)), DoorNames)];
                    op.h1 = EditorGUILayout.FloatField("Clearance", op.h1);
                    op.flag = EditorGUILayout.Toggle(new GUIContent("Jump ledge (lip)", "The ledge a player jumps onto from the landing to leave by this door"), op.flag);
                    if (op.flag)
                    {
                        op.x2 = EditorGUILayout.FloatField("Lip x", op.x2);
                        op.y2 = EditorGUILayout.FloatField("Lip y", op.y2);
                    }
                    break;
                case "hazard":
                    op.kind = HazardKinds[EditorGUILayout.Popup("Kind", Math.Max(0, Array.IndexOf(HazardKinds, op.kind)), HazardKinds)];
                    op.a = EditorGUILayout.FloatField("From x", op.a);
                    op.b = EditorGUILayout.FloatField("To x", op.b);
                    op.c = EditorGUILayout.FloatField("Bottom", op.c);
                    op.h1 = EditorGUILayout.FloatField("Top", op.h1);
                    op.flag = EditorGUILayout.Toggle("Hanging", op.flag);
                    break;
                case "laser":
                    PointFields(op);
                    op.a = EditorGUILayout.FloatField("Reach", op.a);
                    op.b = EditorGUILayout.FloatField("Spin (deg/s)", op.b);
                    op.count = EditorGUILayout.IntSlider("Arms", op.count, 1, 6);
                    op.c = EditorGUILayout.FloatField("Phase (deg)", op.c);
                    break;
                case "enemy":
                    PointFields(op);
                    string[] names = new[] { "(world's usual)" }.Concat(EnemySpec.Regulars.Select(spec => spec.Name)).ToArray();
                    int index = EditorGUILayout.Popup("Enemy", Math.Max(0, Array.IndexOf(names, op.enemy)), names);
                    op.enemy = index == 0 ? "" : names[index];
                    break;
                case "wait":
                    PointFields(op);
                    op.count = EditorGUILayout.IntSlider("Direction", op.count, -1, 1);
                    op.a = EditorGUILayout.FloatField("Run distance", op.a);
                    break;
                case "leap":
                    PointFields(op);
                    op.count = EditorGUILayout.IntSlider("Direction", op.count, -1, 1);
                    break;
                case "tile":
                    PointFields(op);
                    op.material = ((TerrainMaterial)EditorGUILayout.EnumPopup("Material",
                        Enum.TryParse(op.material, out TerrainMaterial material) ? material : TerrainMaterial.SoftDirt)).ToString();
                    break;
                default:
                    PointFields(op);
                    break;
            }
            if (EditorGUI.EndChangeCheck())
            {
                PushUndo(before);
                dirty = true;
                Rebuild();
            }
            if (GUILayout.Button("Delete"))
            {
                Record();
                data.ops.RemoveAt(selected);
                selected = -1;
                dirty = true;
                Rebuild();
            }
        }

        private static void PointFields(RoomOp op)
        {
            op.x = EditorGUILayout.FloatField("x", op.x);
            op.y = EditorGUILayout.FloatField("y", op.y);
        }

        private void DrawProblems()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Checks", EditorStyles.boldLabel);
            if (preview == null || preview.Problems.Count == 0)
            {
                EditorGUILayout.HelpBox("No problems found. Walkability still needs a playtest or the production verification run.", MessageType.Info);
                return;
            }
            foreach (string problem in preview.Problems)
                EditorGUILayout.HelpBox(problem, MessageType.Warning);
        }

        // ---------------- preview and checks ----------------

        private sealed class Preview
        {
            public readonly List<IReadOnlyList<Vector2>> Passages = new();
            public readonly List<TunnelHazard> Hazards = new();
            public readonly List<(Vector2Int cell, TerrainMaterial material)> Tiles = new();
            public readonly List<string> Problems = new();
        }

        private void Rebuild()
        {
            preview = new Preview();
            int members = data.Members;
            var centers = Enumerable.Range(0, members).Select(Offset).ToArray();
            var chunks = Enumerable.Range(0, members).Select(i => new Vector2Int(i, 0)).ToArray();
            var openings = Enumerable.Range(0, members).Select(_ => (IReadOnlyCollection<RoomDoor>)Array.Empty<RoomDoor>()).ToArray();
            var space = new ForestTunnelSpace();
            ForestRoomBuilder room = null;
            try
            {
                ForestRoomTemplate template = RoomDataTemplates.Create(data);
                var suite = new RoomSuite(template, chunks, false);
                room = new ForestRoomBuilder(space, suite, centers, new ForestRoomProfile[members], openings);
                template.Carve(room);
            }
            catch (Exception exception)
            {
                preview.Problems.Add($"Carving failed: {exception.Message}");
            }
            foreach (var (_, outline) in space.Passages)
                preview.Passages.Add(outline);
            preview.Hazards.AddRange(space.Hazards);
            foreach (var pair in space.TileOverrides)
                preview.Tiles.Add((pair.Key, pair.Value));
            if (room != null)
                Check(room, space);
            Repaint();
        }

        private void Check(ForestRoomBuilder room, ForestTunnelSpace space)
        {
            List<string> problems = preview.Problems;
            bool Open(Vector2 point) => preview.Passages.Any(outline => Inside(outline, point + Vector2.up * 0.15f));

            if (string.IsNullOrWhiteSpace(data.id))
                problems.Add("The room needs an id.");
            if (!data.suite && data.shapes.Length == 0)
                problems.Add("Pick at least one profile the room fits.");
            for (int member = 0; member < data.Members; member++)
            {
                if (!data.ops.Any(op => op.op == "landing" && op.member == member))
                    problems.Add($"Chunk {member} has no landing.");
                else if (!Open(room.LandingOf(member)))
                    problems.Add($"Chunk {member}'s landing is not in open air.");
                foreach (var door in room.DoorsOf(member))
                    if (!Open(room.WorldAnchor(member, door.Key) + Vector2.up * 0.05f))
                        problems.Add($"Chunk {member}'s {door.Key} door anchor is not open floor.");
            }
            if (!data.suite)
                foreach (string shape in data.shapes)
                    foreach (string door in RequiredDoors(data.World, data, shape))
                        if (!room.DoorsOf(0).ContainsKey(RoomDataTemplates.ParseDoor(door)))
                            problems.Add($"Profile {shape} needs a {door} door.");
            if (data.suite)
            {
                if (!data.entries.Any(door => room.DoorsOf(0).ContainsKey(RoomDataTemplates.ParseDoor(door))))
                    problems.Add("Chunk 0 needs a door matching one of the entry doors.");
                if (!data.exits.Any(door => room.DoorsOf(data.Members - 1).ContainsKey(RoomDataTemplates.ParseDoor(door))))
                    problems.Add($"Chunk {data.Members - 1} needs a door matching one of the exit doors.");
            }
            foreach (RoomOp op in data.ops.Where(op => op.op == "cave"))
            {
                var points = RoomDataTemplates.Triples(op.points);
                for (int i = 0; i + 1 < points.Length; i++)
                {
                    float run = points[i + 1].x - points[i].x;
                    if (run > 0.01f && Mathf.Atan2(Mathf.Abs(points[i + 1].a - points[i].a), run) * Mathf.Rad2Deg > 42f)
                        problems.Add($"Chunk {op.member}: floor from x {points[i].x:0.0} to {points[i + 1].x:0.0} is steeper than 42°.");
                    if (run <= 0.01f && Mathf.Abs(points[i + 1].a - points[i].a) >= 5f)
                        problems.Add($"Chunk {op.member}: drop at x {points[i].x:0.0} is 5 units or more.");
                }
                if (data.Members == 1)
                    foreach (var (x, floor, ceiling) in points)
                        if (Mathf.Abs(x) > 8.3f || floor < -7.2f || ceiling > 7.2f)
                        {
                            problems.Add($"Cave point at x {x:0.0} leaves the chunk's hexagon (x ±8.3, y ±7.2).");
                            break;
                        }
            }
        }

        private static bool Inside(IReadOnlyList<Vector2> polygon, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                Vector2 a = polygon[i], b = polygon[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        // ---------------- profiles ----------------

        private static readonly Dictionary<WorldId, Dictionary<string, ForestRoomProfile>> ProfileCache = new();

        // One real profile per room shape, gathered from generated levels.
        private static Dictionary<string, ForestRoomProfile> Profiles(WorldId world)
        {
            if (ProfileCache.TryGetValue(world, out var profiles))
                return profiles;
            profiles = new Dictionary<string, ForestRoomProfile>();
            int first = RunWorlds.FirstLevel(world);
            for (int seed = 1; seed <= 20 && profiles.Count < ForestRoomProfile.Shapes.Length; seed++)
                for (int level = first; level < first + RunWorlds.LevelsPerWorld - 1; level++)
                    foreach (ForestRoomProfile profile in RunWorlds.Generate(seed, level).Tunnel.Rooms.Values)
                        profiles.TryAdd(profile.Shape, profile);
            return ProfileCache[world] = profiles;
        }

        private static IEnumerable<string> RequiredDoors(WorldId world, RoomData room, string shape)
        {
            if (!Profiles(world).TryGetValue(shape, out ForestRoomProfile profile))
                yield break;
            foreach (RoomDoor door in new[] { profile.Entry, profile.Exit, profile.Branch })
                if (door != RoomDoor.None)
                    yield return door.ToString();
        }

        private static IEnumerable<IEnumerable<string>> RequiredDoors(WorldId world, RoomData room) =>
            room.shapes.Select(shape => RequiredDoors(world, room, shape));

        private static string[] FittingShapes(WorldId world, ForestRoomTemplate template) =>
            ForestRoomProfile.Shapes.Where(shape => Profiles(world).TryGetValue(shape, out var profile) && template.Fits(profile)).ToArray();

        private static bool IsBuiltIn(string id) =>
            Enum.GetValues(typeof(WorldId)).Cast<WorldId>().Where(world => RunWorlds.FirstLevel(world) > 0)
                .Any(world => ForestRoomCatalog.Code(world).Concat(ForestSuites.Code(world)).Any(template => template.Id == id));

        // ---------------- undo, save, try ----------------

        private void Record() => PushUndo(data.ToJson());

        private void PushUndo(string snapshot)
        {
            undo.Add(snapshot);
            if (undo.Count > 200)
                undo.RemoveAt(0);
            redo.Clear();
        }

        private void Undo()
        {
            if (undo.Count == 0)
                return;
            redo.Add(data.ToJson());
            data = RoomData.FromJson(undo[^1]);
            undo.RemoveAt(undo.Count - 1);
            selected = -1;
            dirty = true;
            Rebuild();
        }

        private void Redo()
        {
            if (redo.Count == 0)
                return;
            undo.Add(data.ToJson());
            data = RoomData.FromJson(redo[^1]);
            redo.RemoveAt(redo.Count - 1);
            selected = -1;
            dirty = true;
            Rebuild();
        }

        private bool ConfirmLeave() =>
            !dirty || EditorUtility.DisplayDialog("Unsaved chunk", $"Leave {data.name} without saving?", "Leave", "Stay");

        private bool Save()
        {
            if (string.IsNullOrWhiteSpace(data.id))
            {
                EditorUtility.DisplayDialog("Chunk Editor", "Give the room an id before saving.", "OK");
                return false;
            }
            if (preview != null && preview.Problems.Count > 0 &&
                !EditorUtility.DisplayDialog("Chunk Editor", $"{preview.Problems.Count} problem(s) found:\n\n" +
                    string.Join("\n", preview.Problems.Take(6)) + "\n\nSave anyway?", "Save anyway", "Cancel"))
                return false;
            string directory = $"{RoomsRoot}/{data.world}";
            Directory.CreateDirectory(directory);
            string path = $"{directory}/{data.id}.json";
            if (savedPath != null && savedPath != path && File.Exists(savedPath) &&
                EditorUtility.DisplayDialog("Chunk Editor", $"The id changed. Remove the old file {savedPath}?", "Remove", "Keep both"))
                AssetDatabase.DeleteAsset(savedPath);
            File.WriteAllText(path, data.ToJson());
            AssetDatabase.ImportAsset(path);
            savedPath = path;
            dirty = false;
            RoomLibrary.Reload();
            ShowNotification(new GUIContent($"Saved {path}"));
            return true;
        }

        // Finds the first seed and level whose generation places this room.
        private (int seed, int level)? FindInLevels()
        {
            RoomLibrary.Reload();
            RoomLibrary.Inject(data.Clone());
            try
            {
                int first = RunWorlds.FirstLevel(data.World);
                for (int seed = 1; seed <= 400; seed++)
                    for (int level = first; level < first + RunWorlds.LevelsPerWorld - 1; level++)
                    {
                        if (seed % 20 == 0 && EditorUtility.DisplayCancelableProgressBar("Chunk Editor", $"Looking for {data.id} in seed {seed}", seed / 400f))
                            return null;
                        if (RunWorlds.Generate(seed, level).Chunks.Any(chunk => chunk.TemplateId == data.id))
                            return (seed, level);
                    }
                return null;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                RoomLibrary.Reload();
            }
        }

        private void PreviewInLevel()
        {
            if ((dirty && !Save()) || FindInLevels() is not (int seed, int level))
            {
                if (!dirty)
                    EditorUtility.DisplayDialog("Chunk Editor", $"{data.id} did not appear in 400 seeds. Check its profiles or footprint.", "OK");
                return;
            }
            string repo = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var process = new System.Diagnostics.ProcessStartInfo("/bin/bash", $"\"{repo}/tools/map.sh\" {seed} {level} 8")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = repo
            };
            System.Diagnostics.Process.Start(process);
            ShowNotification(new GUIContent($"Opening seed {seed} level {level}"));
        }

        private void PlayHere()
        {
            if ((dirty && !Save()) || FindInLevels() is not (int seed, int level))
            {
                if (!dirty)
                    EditorUtility.DisplayDialog("Chunk Editor", $"{data.id} did not appear in 400 seeds. Check its profiles or footprint.", "OK");
                return;
            }
            DevMode.RequestPlayHere(seed, level);
            EditorApplication.isPlaying = true;
        }
    }
}
