using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using Insectum.Game;
using UnityEngine;

// Renders Insectum levels to PNG straight from the game's generator source, no Unity needed.
//   MapGen <seed|random> [level|all] [zoom] [outDir]
internal static class Program
{
    private static int Main(string[] args)
    {
        string repo = FindRepo();
        Resources.Root = Path.Combine(repo, "game", "Assets", "Resources");
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            Console.WriteLine(Usage);
            return args.Length == 0 ? 1 : 0;
        }
        if (args[0] == "export" && args.Length > 2)
        {
            if (!Enum.TryParse(args[1], true, out WorldId world))
            {
                Console.Error.WriteLine($"Unknown world {args[1]}");
                return 1;
            }
            RoomRecorder recorder = RoomRecorder.Capture(world, args[2], args.Length > 3 ? args[3] : null);
            if (recorder == null)
            {
                Console.Error.WriteLine($"{args[2]} was not carved in 400 seeds of {world}.");
                return 1;
            }
            Console.WriteLine(recorder.Data.ToJson());
            return 0;
        }
        if (args[0] == "roundtrip")
            return RoundTrip(args.Length > 1 ? args[1] : "all");
        if (args[0] == "chunks")
            return Chunks(args.Length > 1 ? args[1] : "all", args.Length > 2 ? args[2] : "",
                args.Length > 3 ? float.Parse(args[3], CultureInfo.InvariantCulture) : 16f,
                args.Length > 4 ? args[4] : Path.Combine(repo, "game", "Builds", "Chunks"));
        if (args.Length > 0 && args[0] == "stats")
            return Stats(args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 200,
                args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 1);

        int seed = args.Length > 0 && args[0] != "random"
            ? int.Parse(args[0], CultureInfo.InvariantCulture)
            : new System.Random().Next(int.MinValue, int.MaxValue);
        string levelArg = args.Length > 1 ? args[1] : "1";
        float zoom = args.Length > 2 ? float.Parse(args[2], CultureInfo.InvariantCulture) : 6f;
        string outDir = args.Length > 3 ? args[3] : Path.Combine(repo, "game", "Builds", "Maps");
        Directory.CreateDirectory(outDir);

        var levels = new List<int>();
        if (levelArg == "all")
            for (int i = 1; i <= RunWorlds.LevelCount; i++) levels.Add(i);
        else
            levels.Add(Math.Clamp(int.Parse(levelArg, CultureInfo.InvariantCulture), 1, RunWorlds.LevelCount));

        int failures = 0;
        foreach (int level in levels)
        {
            try
            {
                LevelLayout layout = RunWorlds.Generate(seed, level);
                string path = Path.Combine(outDir, $"seed{seed}_level{level:00}.png");
                Render(layout, zoom, path, out string info);
                string report = Report(layout, info);
                string textPath = Path.ChangeExtension(path, ".txt");
                File.WriteAllText(textPath, report);
                Console.WriteLine(report);
                Console.WriteLine($"  {path}\n  {textPath}\n");
            }
            catch (Exception exception)
            {
                failures++;
                Console.Error.WriteLine($"Level {level}: generation failed: {exception}");
            }
        }
        return failures == 0 ? 0 : 1;
    }

    // Multi-chunk rooms (suites) grouped from the tunnel's chunk -> suite anchor map. Anchor first.
    private static List<List<Vector2Int>> Suites(LevelLayout layout)
    {
        var groups = new Dictionary<Vector2Int, List<Vector2Int>>();
        if (layout.Tunnel == null)
            return new List<List<Vector2Int>>();
        foreach (var pair in layout.Tunnel.SuiteOf)
        {
            if (!groups.TryGetValue(pair.Value, out var list)) groups[pair.Value] = list = new List<Vector2Int>();
            list.Add(pair.Key);
        }
        var suites = new List<List<Vector2Int>>();
        foreach (var pair in groups)
            if (pair.Value.Count > 1)
            {
                pair.Value.Remove(pair.Key);
                pair.Value.Insert(0, pair.Key);
                suites.Add(pair.Value);
            }
        return suites;
    }

    // The macro chunk whose hexagon holds a cell: chunk centers sit on the lattice cell = (11q + 5r, -5q + 6r).
    private static Vector2Int ChunkAt(Vector2Int cell)
    {
        float q = (6f * cell.x - 5f * cell.y) / 91f;
        float r = (5f * cell.x + 11f * cell.y) / 91f;
        var guess = new Vector2Int((int)MathF.Round(q), (int)MathF.Round(r));
        Vector2Int best = guess;
        int bestDistance = int.MaxValue;
        for (int dq = -1; dq <= 1; dq++)
            for (int dr = -1; dr <= 1; dr++)
            {
                var macro = new Vector2Int(guess.x + dq, guess.y + dr);
                int distance = HexGrid.Distance(cell, ForestTemplates.MacroCenter(macro));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = macro;
                }
            }
        return best;
    }

    private static string TemplateAt(LevelLayout layout, Vector2Int chunk)
    {
        foreach (MacroChunk c in layout.Chunks)
            if (c.Coordinate == chunk) return c.TemplateId;
        return "?";
    }

    //   MapGen stats [seedCount] [firstSeed]: how often multi-chunk rooms appear, per world.
    private static int Stats(int seedCount, int firstSeed)
    {
        var worlds = new SortedDictionary<int, (string name, int levels, int withSuite, int suites, int failures)>();
        var sizes = new SortedDictionary<int, int>();
        var templates = new Dictionary<string, int>();
        var perLevel = new SortedDictionary<int, int>();
        int totalLevels = 0, totalSuites = 0;
        for (int seed = firstSeed; seed < firstSeed + seedCount; seed++)
            for (int level = 1; level <= RunWorlds.LevelCount; level++)
            {
                if (RunWorlds.LocalLevel(level) == RunWorlds.LevelsPerWorld)
                    continue; // boss arenas have no rooms
                int worldIndex = (level - 1) / RunWorlds.LevelsPerWorld;
                var w = worlds.TryGetValue(worldIndex, out var existing) ? existing : (name: RunWorlds.AtLevel(level).ToString(), levels: 0, withSuite: 0, suites: 0, failures: 0);
                LevelLayout layout;
                try { layout = RunWorlds.Generate(seed, level); }
                catch { w.failures++; worlds[worldIndex] = w; continue; }
                List<List<Vector2Int>> suites = Suites(layout);
                w.levels++;
                if (suites.Count > 0) w.withSuite++;
                w.suites += suites.Count;
                worlds[worldIndex] = w;
                totalLevels++;
                totalSuites += suites.Count;
                perLevel[suites.Count] = perLevel.TryGetValue(suites.Count, out int n) ? n + 1 : 1;
                foreach (var suite in suites)
                {
                    sizes[suite.Count] = sizes.TryGetValue(suite.Count, out int s) ? s + 1 : 1;
                    string id = TemplateAt(layout, suite[0]);
                    templates[id] = templates.TryGetValue(id, out int t) ? t + 1 : 1;
                }
            }

        Console.WriteLine($"Multi-chunk rooms over seeds {firstSeed}..{firstSeed + seedCount - 1} (boss levels skipped)\n");
        Console.WriteLine($"{"World",-8} {"levels",7} {"with any",9} {"%",6} {"per level",10} {"failed",7}");
        foreach (var w in worlds.Values)
            Console.WriteLine($"{w.name,-8} {w.levels,7} {w.withSuite,9} {100.0 * w.withSuite / Math.Max(1, w.levels),5:0.0}% " +
                              $"{(double)w.suites / Math.Max(1, w.levels),10:0.00} {w.failures,7}");
        Console.WriteLine($"{"All",-8} {totalLevels,7} {"",9} {"",6} {(double)totalSuites / Math.Max(1, totalLevels),10:0.00}");
        Console.WriteLine("\nSuites per level:");
        foreach (var pair in perLevel)
            Console.WriteLine($"  {pair.Key,2}: {pair.Value,6} levels ({100.0 * pair.Value / totalLevels:0.0}%)");
        Console.WriteLine("\nSuite sizes (chunks):");
        foreach (var pair in sizes)
            Console.WriteLine($"  {pair.Key,2}: {pair.Value,6}");
        Console.WriteLine("\nTemplates:");
        var ordered = new List<KeyValuePair<string, int>>(templates);
        ordered.Sort((a, b) => b.Value.CompareTo(a.Value));
        foreach (var pair in ordered)
            Console.WriteLine($"  {pair.Value,6}  {pair.Key}");
        return 0;
    }

    private const string Usage = @"MapGen: render Insectum levels without Unity.

  MapGen <seed|random> [level|all] [zoom] [outDir]
      Map of one level (1-20) or all 20: PNG plus a text listing per level.
      zoom is pixels per world unit (default 6). outDir defaults to game/Builds/Maps.
  MapGen chunks [world|all] [filter] [zoom] [outDir]
      One cropped PNG per room template and multi-chunk suite, found in real generated levels.
      world is forest, swamp, caves, desert, alien or all. filter keeps names containing it
      (for example Suite or Cascade). zoom defaults to 16. outDir defaults to game/Builds/Chunks.
  MapGen stats [seedCount] [firstSeed]
      How often multi-chunk suites spawn, per world (default 200 seeds from 1).
  MapGen export <world> <templateId> [shape]
      Prints a code room or suite as Chunk Editor JSON (one shape of a room; suites need none).
  MapGen roundtrip [world|all]
      Converts every code room and suite to Chunk Editor data, through JSON, and checks the data
      carves exactly the same passages, doors, landings, spots and hazards.
  MapGen -h
      This help.";

    // Finds a real example of every room template (one per room shape it fits) and every suite, then renders
    // each cropped to its own chunks.
    private static int Chunks(string worldArg, string filter, float zoom, string outDir)
    {
        var worlds = new List<WorldId>();
        foreach (WorldId world in Enum.GetValues(typeof(WorldId)))
            if (worldArg == "all" || string.Equals(worldArg, world.ToString(), StringComparison.OrdinalIgnoreCase))
                worlds.Add(world);
        worlds.RemoveAll(world => !TryFirstLevel(world, out _));
        if (worlds.Count == 0)
        {
            Console.Error.WriteLine($"Unknown world '{worldArg}'.\n\n{Usage}");
            return 1;
        }

        int missingTotal = 0;
        foreach (WorldId world in worlds)
        {
            TryFirstLevel(world, out int firstLevel);
            var wanted = new HashSet<string>();
            foreach (ForestRoomTemplate template in ForestRoomCatalog.For(world)) wanted.Add(template.Id);
            foreach (ForestRoomTemplate template in ForestSuites.For(world)) wanted.Add(template.Id);
            wanted.RemoveWhere(id => !id.Contains(filter, StringComparison.OrdinalIgnoreCase));

            // key -> (seed, level, chunks, description)
            var found = new SortedDictionary<string, (int seed, int level, List<Vector2Int> chunks, string text)>();
            var seenIds = new HashSet<string>();
            for (int seed = 1; seed <= 400 && (seed <= 40 || !wanted.IsSubsetOf(seenIds)); seed++)
                for (int level = firstLevel; level < firstLevel + RunWorlds.LevelsPerWorld - 1; level++)
                {
                    LevelLayout layout = RunWorlds.Generate(seed, level);
                    if (layout.Tunnel == null) continue;
                    var inSuite = new HashSet<Vector2Int>();
                    foreach (List<Vector2Int> suite in Suites(layout))
                    {
                        inSuite.UnionWith(suite);
                        string id = TemplateAt(layout, suite[0]);
                        if (!wanted.Contains(id)) continue;
                        seenIds.Add(id);
                        if (!found.ContainsKey(id))
                            found[id] = (seed, level, suite, $"{suite.Count} chunks");
                    }
                    foreach (MacroChunk chunk in layout.Chunks)
                    {
                        if (inSuite.Contains(chunk.Coordinate) || !wanted.Contains(chunk.TemplateId)) continue;
                        if (!layout.Tunnel.Rooms.TryGetValue(chunk.Coordinate, out ForestRoomProfile room)) continue;
                        seenIds.Add(chunk.TemplateId);
                        string key = $"{chunk.TemplateId} {room.Shape}";
                        if (!found.ContainsKey(key))
                            found[key] = (seed, level, new List<Vector2Int> { chunk.Coordinate },
                                $"{room.Role}/{room.Shape}{(room.Mirrored ? " mirrored" : "")}");
                    }
                }

            string dir = Path.Combine(outDir, world.ToString());
            Directory.CreateDirectory(dir);
            var index = new System.Text.StringBuilder();
            foreach (var pair in found)
            {
                var (seed, level, chunks, text) = pair.Value;
                LevelLayout layout = RunWorlds.Generate(seed, level);
                Vector2 lo = new(float.MaxValue, float.MaxValue), hi = new(float.MinValue, float.MinValue);
                foreach (Vector2Int chunk in chunks)
                {
                    Vector2 center = HexGrid.Center(ForestTemplates.MacroCenter(chunk));
                    lo = Vector2.Min(lo, center);
                    hi = Vector2.Max(hi, center);
                }
                var view = Rect.MinMaxRect(lo.x - 10f, lo.y - 9f, hi.x + 10f, hi.y + 9f);
                string file = Path.Combine(dir, pair.Key.Replace(' ', '_').Replace('/', '-') + ".png");
                Render(layout, zoom, file, out _, view);
                string line = $"{pair.Key,-50} {text,-34} seed {seed} level {level} chunks {string.Join(" ", chunks)}";
                index.AppendLine(line);
                Console.WriteLine(line);
            }
            var missing = new List<string>(wanted);
            missing.RemoveAll(seenIds.Contains);
            foreach (string id in missing)
            {
                index.AppendLine($"{id,-50} NOT FOUND in 400 seeds");
                Console.WriteLine($"{id,-50} NOT FOUND in 400 seeds");
            }
            missingTotal += missing.Count;
            File.WriteAllText(Path.Combine(dir, "index.txt"), index.ToString());
            Console.WriteLine($"  {found.Count} images in {dir}\n");
        }
        return missingTotal == 0 ? 0 : 2;
    }

    private static int RoundTrip(string worldArg)
    {
        int failures = 0, checkedCount = 0;
        foreach (WorldId world in Enum.GetValues(typeof(WorldId)))
        {
            if (worldArg != "all" && !string.Equals(worldArg, world.ToString(), StringComparison.OrdinalIgnoreCase))
                continue;
            if (!TryFirstLevel(world, out _))
                continue;
            var keys = new List<(string id, string shape)>();
            foreach (ForestRoomTemplate template in ForestSuites.Code(world))
                keys.Add((template.Id, null));
            foreach (ForestRoomTemplate template in ForestRoomCatalog.Code(world))
                foreach (string shape in ForestRoomProfile.Shapes)
                    keys.Add((template.Id, shape));
            foreach (var (id, shape) in keys)
            {
                if (shape != null && !FitsShape(world, id, shape))
                    continue;
                RoomRecorder recorder = RoomRecorder.Capture(world, id, shape, 60);
                if (recorder == null)
                    continue;
                checkedCount++;
                RoomData data = RoomData.FromJson(recorder.Data.ToJson());
                string original = Carve(recorder, recorder.Suite.Template);
                string replayed = Carve(recorder, RoomDataTemplates.Create(data));
                if (original != replayed)
                {
                    failures++;
                    Console.WriteLine($"MISMATCH {id} {shape}");
                    string[] a = original.Split('\n'), b = replayed.Split('\n');
                    for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
                        if (i >= a.Length || i >= b.Length || a[i] != b[i])
                        {
                            Console.WriteLine($"  code: {(i < a.Length ? a[i] : "-")}\n  data: {(i < b.Length ? b[i] : "-")}");
                            break;
                        }
                }
            }
        }
        Console.WriteLine($"{checkedCount} rooms checked, {failures} mismatches");
        return failures == 0 ? 0 : 1;
    }

    private static bool FitsShape(WorldId world, string id, string shape)
    {
        foreach (ForestRoomTemplate template in ForestRoomCatalog.Code(world))
            if (template.Id == id)
                foreach (var pair in AnyProfiles())
                    if (pair.Shape == shape && template.Fits(pair))
                        return true;
        return false;
    }

    private static List<ForestRoomProfile> profileCache;

    // One profile per shape, gathered from generated levels, to ask which shapes a template fits.
    private static List<ForestRoomProfile> AnyProfiles()
    {
        if (profileCache != null)
            return profileCache;
        var byShape = new Dictionary<string, ForestRoomProfile>();
        for (int seed = 1; seed <= 20; seed++)
            for (int level = 1; level <= 3; level++)
            {
                LevelLayout layout = RunWorlds.Generate(seed, level);
                foreach (ForestRoomProfile profile in layout.Tunnel.Rooms.Values)
                    byShape.TryAdd(profile.Shape, profile);
            }
        return profileCache = new List<ForestRoomProfile>(byShape.Values);
    }

    // Carves a template into a fresh space with the inputs the recorder saw and describes everything it made.
    private static string Carve(RoomRecorder recorder, ForestRoomTemplate template)
    {
        var space = new ForestTunnelSpace();
        var suite = new RoomSuite(template, recorder.Suite.Chunks, recorder.Suite.Mirrored);
        var room = new ForestRoomBuilder(space, suite, recorder.Centers, recorder.Profiles, recorder.Openings, recorder.Level);
        template.Carve(room);
        var text = new System.Text.StringBuilder();
        string P(Vector2 v) => $"{v.x:0.0000},{v.y:0.0000}";
        foreach (var (_, outline) in space.Passages)
            text.AppendLine("passage " + string.Join(" ", System.Linq.Enumerable.Select(outline, P)));
        foreach (TunnelHazard hazard in space.Hazards)
            text.AppendLine($"hazard {hazard.Kind} {P(hazard.Area.min)} {P(hazard.Area.max)} {hazard.Hanging}");
        foreach (var leap in space.Leaps) text.AppendLine($"leap {P(leap.takeoff)} {leap.direction}");
        foreach (var wait in space.Waits) text.AppendLine($"wait {P(wait.spot)} {wait.direction} {wait.distance:0.000}");
        foreach (var tile in space.TileOverrides) text.AppendLine($"tile {tile.Key} {tile.Value}");
        for (int i = 0; i < recorder.Centers.Length; i++)
        {
            text.AppendLine($"landing {i} {P(room.LandingOf(i))}");
            foreach (var door in room.DoorsOf(i))
                text.AppendLine($"door {i} {door.Key} {door.Value.clearance:0.000} {(door.Value.lip is Vector2 lip ? P(lip) : "-")}");
        }
        foreach (Vector2 spot in room.TrapSpots) text.AppendLine($"trap {P(spot)}");
        foreach (Vector2 spot in room.TreasureSpots) text.AppendLine($"treasure {P(spot)}");
        foreach (var (spot, enemy) in room.EnemySpots) text.AppendLine($"enemy {P(spot)} {enemy}");
        return text.ToString();
    }

    private static bool TryFirstLevel(WorldId world, out int level)
    {
        for (level = 1; level <= RunWorlds.LevelCount; level += RunWorlds.LevelsPerWorld)
            if (RunWorlds.AtLevel(level) == world)
                return true;
        return false;
    }

    private static string FindRepo()
    {
        string dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "game", "Assets", "Resources")))
            dir = Path.GetDirectoryName(dir);
        return dir ?? throw new DirectoryNotFoundException("Could not find the Insectum repo above " + AppContext.BaseDirectory);
    }

    private static string LevelName(int level) =>
        $"{RunWorlds.AtLevel(level)} {(Math.Max(1, level) - 1) / RunWorlds.LevelsPerWorld + 1}-{RunWorlds.LocalLevel(level)}";

    private static void Render(LevelLayout layout, float pixelsPerUnit, string path, out string info, Rect? view = null)
    {
        Rect bounds = view ?? layout.Bounds;
        if (view == null)
        {
            bounds.xMin -= 2f; bounds.yMin -= 2f; bounds.xMax += 2f; bounds.yMax += 2f;
        }
        float ppu = MathF.Min(pixelsPerUnit, 8192f / MathF.Max(bounds.width, bounds.height));
        int width = Math.Max(1, (int)MathF.Ceiling(bounds.width * ppu));
        int height = Math.Max(1, (int)MathF.Ceiling(bounds.height * ppu));
        var image = new Image(width, height);

        var air = new Color32(20, 16, 30, 255);
        var passages = new List<(Rect box, IReadOnlyList<Vector2> outline)>();
        if (layout.Tunnel != null)
            foreach (var passage in layout.Tunnel.Passages)
            {
                Vector2 lo = passage.outline[0], hi = lo;
                foreach (Vector2 p in passage.outline) { lo = Vector2.Min(lo, p); hi = Vector2.Max(hi, p); }
                passages.Add((Rect.MinMaxRect(lo.x, lo.y, hi.x, hi.y), passage.outline));
            }
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var world = new Vector2(bounds.xMin + (x + 0.5f) / ppu, bounds.yMin + (y + 0.5f) / ppu);
                bool open = !layout.Tiles.TryGetValue(HexGrid.CellAt(world), out TerrainMaterial material);
                if (!open)
                    foreach (var (box, outline) in passages)
                        if (box.Contains(world) && Inside(outline, world)) { open = true; break; }
                image.Set(x, y, open ? air : MaterialColor(material));
            }

        // Chunk outlines: a faint line between ordinary chunks, a bright one around each multi-chunk room, and
        // none between members of the same room so it reads as one space.
        var chunkOf = new Vector2Int[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                chunkOf[y * width + x] = ChunkAt(HexGrid.CellAt(
                    new Vector2(bounds.xMin + (x + 0.5f) / ppu, bounds.yMin + (y + 0.5f) / ppu)));
        var suiteOf = new Dictionary<Vector2Int, int>();
        List<List<Vector2Int>> suiteList = Suites(layout);
        for (int i = 0; i < suiteList.Count; i++)
            foreach (Vector2Int chunk in suiteList[i])
                suiteOf[chunk] = i;
        int SuiteAt(Vector2Int chunk) => suiteOf.TryGetValue(chunk, out int i) ? i : -1;
        var chunkLine = new Color32(150, 170, 120, 255);
        var suiteLine = new Color32(255, 80, 200, 255);
        var suiteEdges = new List<(int x, int y)>();
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Vector2Int here = chunkOf[y * width + x];
                Vector2Int right = x + 1 < width ? chunkOf[y * width + x + 1] : here;
                Vector2Int up = y + 1 < height ? chunkOf[(y + 1) * width + x] : here;
                foreach (Vector2Int other in new[] { right, up })
                {
                    if (other == here)
                        continue;
                    int a = SuiteAt(here), b = SuiteAt(other);
                    if (a >= 0 && a == b)
                        continue;
                    if (a >= 0 || b >= 0)
                        suiteEdges.Add((x, y));
                    else
                        image.Set(x, y, chunkLine);
                }
            }
        // Suite outlines go on top, two pixels thick.
        foreach (var (x, y) in suiteEdges)
        {
            image.Set(x, y, suiteLine);
            image.Set(x + 1, y, suiteLine);
            image.Set(x, y + 1, suiteLine);
        }

        (int, int) P(Vector2 p) => ((int)MathF.Floor((p.x - bounds.xMin) * ppu), (int)MathF.Floor((p.y - bounds.yMin) * ppu));
        void Line(Vector2 a, Vector2 b, Color32 c) => image.Line(P(a), P(b), c);
        void Dot(Vector2 p, int r, Color32 c) => image.Dot(P(p), r, c);

        if (layout.Tunnel != null)
        {
            foreach ((Vector2 from, Vector2 to) in layout.Tunnel.Lining)
                Line(from, to, new Color32(150, 140, 170, 255));
            IReadOnlyList<Vector2> route = layout.Tunnel.Route;
            for (int i = 1; i < route.Count; i++)
                Line(route[i - 1], route[i], new Color32(240, 210, 80, 255));
            foreach (var branch in layout.Tunnel.Branches)
                Line(branch.first, branch.end, new Color32(200, 200, 200, 255));
        }
        foreach (LevelMarker marker in layout.Markers)
            Dot(marker.Position, 2, MarkerColor(marker.Kind));
        foreach (RouteCheckpoint checkpoint in layout.Checkpoints)
            Dot(checkpoint.Landing, 1, new Color32(120, 220, 255, 255));
        Dot(layout.Spawn, 3, new Color32(80, 240, 120, 255));
        Dot(layout.Exit, 3, new Color32(255, 70, 70, 255));
        image.WritePng(path);

        var counts = new SortedDictionary<string, int>();
        foreach (LevelMarker marker in layout.Markers)
        {
            string key = marker.Kind.ToString().ToLowerInvariant();
            counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
        }
        var parts = new List<string>();
        foreach (var pair in counts) parts.Add($"{pair.Value} {pair.Key}");
        info = $"Seed {layout.RunSeed} · {LevelName(layout.LevelNumber)} · variant {layout.Variant}" +
               (layout.UsedFallback ? " · FALLBACK" : "") +
               $" · {layout.Bounds.width:0}x{layout.Bounds.height:0} units · " + string.Join(", ", parts);
    }

    private static string F(Vector2 v) => $"({v.x,6:0.0},{v.y,6:0.0})";

    // Text listing of everything the generator placed, grouped by chunk.
    private static string Report(LevelLayout layout, string info)
    {
        var text = new System.Text.StringBuilder();
        text.AppendLine(info);
        text.AppendLine($"Spawn {F(layout.Spawn)}   Exit {F(layout.Exit)}   Bounds {F(layout.Bounds.min)}-{F(layout.Bounds.max)}" +
                        $"   Route direction {layout.RouteDirection}   Tiles {layout.Tiles.Count}");

        var markers = new Dictionary<Vector2Int, List<LevelMarker>>();
        foreach (LevelMarker marker in layout.Markers)
        {
            if (!markers.TryGetValue(marker.Chunk, out var list)) markers[marker.Chunk] = list = new List<LevelMarker>();
            list.Add(marker);
        }
        var hazards = new Dictionary<Vector2Int, List<TunnelHazard>>();
        if (layout.Tunnel != null)
            foreach (TunnelHazard hazard in layout.Tunnel.Hazards)
            {
                if (!hazards.TryGetValue(hazard.Chunk, out var list)) hazards[hazard.Chunk] = list = new List<TunnelHazard>();
                list.Add(hazard);
            }
        var checkpoints = new Dictionary<Vector2Int, RouteCheckpoint>();
        foreach (RouteCheckpoint checkpoint in layout.Checkpoints)
            checkpoints[checkpoint.Chunk] = checkpoint;

        text.AppendLine();
        text.AppendLine($"CHUNKS ({layout.Chunks.Count})");
        var seen = new HashSet<Vector2Int>();
        foreach (MacroChunk chunk in layout.Chunks)
        {
            seen.Add(chunk.Coordinate);
            string kind = chunk.MainRoute ? "main  " : chunk.IsBranch ? "branch" : "filler";
            text.Append($"  {chunk.Coordinate,-9} {kind}  {chunk.TemplateId,-28} ports {chunk.Ports}");
            if (layout.Tunnel != null && layout.Tunnel.Rooms.TryGetValue(chunk.Coordinate, out ForestRoomProfile room))
                text.Append($"  room {room.Role}/{room.Shape}{(room.Mirrored ? " mirrored" : "")}");
            text.AppendLine();
            AppendContents(text, chunk.Coordinate, checkpoints, markers, hazards);
        }

        // Rooms and things whose chunk isn't in the chunk list (tunnel levels key rooms separately).
        if (layout.Tunnel != null)
            foreach (var pair in layout.Tunnel.Rooms)
                if (seen.Add(pair.Key))
                {
                    ForestRoomProfile room = pair.Value;
                    text.AppendLine($"  {pair.Key,-9} room    {room.Role}/{room.Shape}{(room.Mirrored ? " mirrored" : "")}" +
                                    $"  entry {room.Entry} exit {room.Exit} branch {room.Branch}");
                    AppendContents(text, pair.Key, checkpoints, markers, hazards);
                }
        var leftovers = new HashSet<Vector2Int>(markers.Keys);
        leftovers.UnionWith(hazards.Keys);
        leftovers.UnionWith(checkpoints.Keys);
        leftovers.ExceptWith(seen);
        foreach (Vector2Int key in leftovers)
        {
            text.AppendLine($"  {key,-9} (no chunk)");
            AppendContents(text, key, checkpoints, markers, hazards);
        }

        List<List<Vector2Int>> suites = Suites(layout);
        text.AppendLine();
        text.AppendLine($"MULTI-CHUNK ROOMS ({suites.Count})");
        foreach (var suite in suites)
            text.AppendLine($"  {TemplateAt(layout, suite[0]),-28} {suite.Count} chunks: {string.Join(" ", suite)}");

        if (layout.Tunnel != null)
        {
            text.AppendLine();
            text.AppendLine($"ROUTE ({layout.Tunnel.Route.Count} points)  " + string.Join(" ", layout.Tunnel.Route.ConvertAll(F)));
            foreach (var branch in layout.Tunnel.Branches)
                text.AppendLine($"BRANCH from route point {branch.source}: {F(branch.first)} -> {F(branch.end)}");
            foreach (var leap in layout.Tunnel.Leaps)
                text.AppendLine($"LEAP at {F(leap.takeoff)} direction {leap.direction}");
        }
        return text.ToString();
    }

    private static void AppendContents(System.Text.StringBuilder text, Vector2Int chunk,
        Dictionary<Vector2Int, RouteCheckpoint> checkpoints, Dictionary<Vector2Int, List<LevelMarker>> markers,
        Dictionary<Vector2Int, List<TunnelHazard>> hazards)
    {
        if (checkpoints.TryGetValue(chunk, out RouteCheckpoint checkpoint))
            text.AppendLine($"      checkpoint {F(checkpoint.Landing)} entry {checkpoint.EntryPort} template {checkpoint.TemplateId}");
        if (markers.TryGetValue(chunk, out var list))
            foreach (LevelMarker marker in list)
                text.AppendLine($"      {marker.Kind.ToString().ToLowerInvariant(),-10} {F(marker.Position)}");
        if (hazards.TryGetValue(chunk, out var hazardList))
            foreach (TunnelHazard hazard in hazardList)
                text.AppendLine($"      hazard {hazard.Kind,-8} {F(hazard.Area.min)}-{F(hazard.Area.max)}{(hazard.Hanging ? " hanging" : "")}");
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

    private static Color32 MaterialColor(TerrainMaterial material)
    {
        // Muted hue per material, same idea as the editor Map Viewer.
        float h = (int)material * 0.137f % 1f, s = 0.35f, v = 0.55f;
        float c = v * s, x = c * (1f - MathF.Abs(h * 6f % 2f - 1f)), m = v - c;
        (float r, float g, float b) = (int)(h * 6f) switch
        {
            0 => (c, x, 0f), 1 => (x, c, 0f), 2 => (0f, c, x), 3 => (0f, x, c), 4 => (x, 0f, c), _ => (c, 0f, x)
        };
        return new Color32((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255), 255);
    }

    private static Color32 MarkerColor(LevelMarkerKind kind) => kind switch
    {
        LevelMarkerKind.Encounter => new Color32(255, 90, 90, 255),
        LevelMarkerKind.Trap => new Color32(255, 150, 40, 255),
        LevelMarkerKind.Shop => new Color32(60, 230, 230, 255),
        LevelMarkerKind.Treasure => new Color32(255, 215, 0, 255),
        LevelMarkerKind.Boss => new Color32(240, 60, 240, 255),
        _ => new Color32(255, 255, 255, 255)
    };

    // Bottom-up RGBA canvas with a minimal PNG encoder.
    private sealed class Image
    {
        private readonly int width, height;
        private readonly byte[] rgba;

        public Image(int width, int height)
        {
            this.width = width;
            this.height = height;
            rgba = new byte[width * height * 4];
        }

        public void Set(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return;
            int i = ((height - 1 - y) * width + x) * 4;
            rgba[i] = c.r; rgba[i + 1] = c.g; rgba[i + 2] = c.b; rgba[i + 3] = c.a;
        }

        public void Dot((int x, int y) c, int r, Color32 color)
        {
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                    if (x * x + y * y <= r * r + r)
                        Set(c.x + x, c.y + y, color);
        }

        public void Line((int x, int y) a, (int x, int y) b, Color32 color)
        {
            int dx = Math.Abs(b.x - a.x), dy = -Math.Abs(b.y - a.y);
            int sx = a.x < b.x ? 1 : -1, sy = a.y < b.y ? 1 : -1, error = dx + dy;
            while (true)
            {
                Set(a.x, a.y, color);
                if (a == b) return;
                int e2 = 2 * error;
                if (e2 >= dy) { error += dy; a.x += sx; }
                if (e2 <= dx) { error += dx; a.y += sy; }
            }
        }

        public void WritePng(string path)
        {
            using var file = File.Create(path);
            file.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            var header = new byte[13];
            WriteInt(header, 0, width); WriteInt(header, 4, height);
            header[8] = 8; header[9] = 6;
            Chunk(file, "IHDR", header);

            using var raw = new MemoryStream();
            using (var z = new ZLibStream(raw, CompressionLevel.Optimal, leaveOpen: true))
                for (int y = 0; y < height; y++)
                {
                    z.WriteByte(0);
                    z.Write(rgba, y * width * 4, width * 4);
                }
            Chunk(file, "IDAT", raw.ToArray());
            Chunk(file, "IEND", Array.Empty<byte>());
        }

        private static void Chunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4]; WriteInt(len, 0, data.Length); s.Write(len);
            var body = new byte[4 + data.Length];
            for (int i = 0; i < 4; i++) body[i] = (byte)type[i];
            Array.Copy(data, 0, body, 4, data.Length);
            s.Write(body);
            var crc = new byte[4]; WriteInt(crc, 0, (int)Crc(body)); s.Write(crc);
        }

        private static void WriteInt(byte[] b, int o, int v)
        {
            b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v;
        }

        private static uint[] crcTable;

        private static uint Crc(byte[] data)
        {
            if (crcTable == null)
            {
                crcTable = new uint[256];
                for (uint n = 0; n < 256; n++)
                {
                    uint c = n;
                    for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                    crcTable[n] = c;
                }
            }
            uint crc = 0xFFFFFFFFu;
            foreach (byte b in data) crc = crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFFu;
        }
    }
}
