using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Insectum.Game.Editor
{
    // Draws the generated level for any seed. Open from Insectum > Map Viewer.
    internal sealed class MapViewer : EditorWindow
    {
        private int seed = 1;
        private int level = 1;
        private float pixelsPerUnit = 6f;
        private Texture2D map;
        private string info = "";
        private Vector2 scroll;

        [MenuItem("Insectum/Map Viewer")]
        private static void Open() => GetWindow<MapViewer>("Insectum Map").Render();

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.HorizontalScope())
            {
                seed = EditorGUILayout.IntField("Seed", seed);
                if (GUILayout.Button("Randomize", GUILayout.Width(90)))
                {
                    seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                    GUI.FocusControl(null);
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                level = EditorGUILayout.IntSlider("Level", level, 1, RunWorlds.LevelCount);
                GUILayout.Label(DevMode.LevelName(level), GUILayout.Width(110));
            }
            pixelsPerUnit = EditorGUILayout.Slider("Zoom", pixelsPerUnit, 2f, 16f);
            if (EditorGUI.EndChangeCheck() || map == null)
                Render();

            EditorGUILayout.HelpBox(info, MessageType.None);
            GUILayout.Label("Green spawn · red exit · yellow route · markers: encounter red, trap orange, shop cyan, " +
                "treasure gold, boss magenta, branch/shortcut white", EditorStyles.miniLabel);
            if (map == null)
                return;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            Rect rect = GUILayoutUtility.GetRect(map.width, map.height, GUILayout.ExpandWidth(false));
            GUI.DrawTexture(rect, map);
            EditorGUILayout.EndScrollView();
        }

        private void Render()
        {
            try
            {
                map = Draw(seed, level, pixelsPerUnit, map, out info);
            }
            catch (Exception exception)
            {
                info = $"Generation failed: {exception.Message}";
                map = null;
            }
            Repaint();
        }

        // Command line: Unity -batchmode -quit -projectPath game -executeMethod Insectum.Game.Editor.MapViewer.Export
        //   [-mapSeed N] [-mapLevel N] [-mapZoom N] [-mapOut path.png]
        // Seed defaults to random.
        internal static void Export()
        {
            string[] args = Environment.GetCommandLineArgs();
            string Arg(string name, string fallback)
            {
                int i = Array.IndexOf(args, name);
                return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
            }
            int seed = int.TryParse(Arg("-mapSeed", ""), out int s) ? s : new System.Random().Next(int.MinValue, int.MaxValue);
            int level = Mathf.Clamp(int.Parse(Arg("-mapLevel", "1")), 1, RunWorlds.LevelCount);
            float zoom = float.Parse(Arg("-mapZoom", "6"), System.Globalization.CultureInfo.InvariantCulture);
            string output = System.IO.Path.GetFullPath(Arg("-mapOut", $"Builds/Maps/seed{seed}_level{level}.png"));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(output));
            Texture2D texture = Draw(seed, level, zoom, null, out string info);
            System.IO.File.WriteAllBytes(output, texture.EncodeToPNG());
            Debug.Log($"INSECTUM_MAP {output}\nINSECTUM_MAP_INFO {info}");
        }

        private static Texture2D Draw(int seed, int level, float pixelsPerUnit, Texture2D map, out string info)
        {
            LevelLayout layout = RunWorlds.Generate(seed, level);

            Rect bounds = layout.Bounds;
            bounds.xMin -= 2f; bounds.yMin -= 2f; bounds.xMax += 2f; bounds.yMax += 2f;
            float ppu = Mathf.Min(pixelsPerUnit, 4096f / Mathf.Max(bounds.width, bounds.height));
            int width = Mathf.Max(1, Mathf.CeilToInt(bounds.width * ppu));
            int height = Mathf.Max(1, Mathf.CeilToInt(bounds.height * ppu));
            if (map == null || map.width != width || map.height != height)
            {
                if (map != null)
                    UnityEngine.Object.DestroyImmediate(map);
                map = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            }

            var pixels = new Color32[width * height];
            var air = new Color32(20, 16, 30, 255);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    var world = new Vector2(bounds.xMin + (x + 0.5f) / ppu, bounds.yMin + (y + 0.5f) / ppu);
                    pixels[y * width + x] = layout.Tiles.TryGetValue(HexGrid.CellAt(world), out TerrainMaterial material)
                        ? MaterialColor(material) : air;
                }

            Vector2Int ToPixel(Vector2 p) => new(Mathf.FloorToInt((p.x - bounds.xMin) * ppu), Mathf.FloorToInt((p.y - bounds.yMin) * ppu));
            void Line(Vector2 a, Vector2 b, Color32 c) => DrawLine(pixels, width, height, ToPixel(a), ToPixel(b), c);
            void Dot(Vector2 p, int r, Color32 c) => DrawDot(pixels, width, height, ToPixel(p), r, c);

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

            // Texture rows go bottom-up, GUI draws top-down: flip so up is up.
            var flipped = new Color32[pixels.Length];
            for (int y = 0; y < height; y++)
                Array.Copy(pixels, y * width, flipped, (height - 1 - y) * width, width);
            map.SetPixels32(flipped);
            map.Apply();

            var counts = new Dictionary<LevelMarkerKind, int>();
            foreach (LevelMarker marker in layout.Markers)
                counts[marker.Kind] = counts.TryGetValue(marker.Kind, out int n) ? n + 1 : 1;
            info = $"Seed {seed} · {DevMode.LevelName(level)} · variant {layout.Variant}" +
                   (layout.UsedFallback ? " · FALLBACK" : "") +
                   $" · {bounds.width:0}x{bounds.height:0} units · " + string.Join(", ", Describe(counts));
            return map;
        }

        private static IEnumerable<string> Describe(Dictionary<LevelMarkerKind, int> counts)
        {
            foreach (var pair in counts)
                yield return $"{pair.Value} {pair.Key.ToString().ToLowerInvariant()}";
        }

        private static Color32 MaterialColor(TerrainMaterial material)
        {
            Color color = Color.HSVToRGB((int)material * 0.137f % 1f, 0.35f, 0.55f);
            return color;
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

        private static void DrawDot(Color32[] pixels, int width, int height, Vector2Int c, int r, Color32 color)
        {
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                    if (x * x + y * y <= r * r + r)
                        Plot(pixels, width, height, c.x + x, c.y + y, color);
        }

        private static void DrawLine(Color32[] pixels, int width, int height, Vector2Int a, Vector2Int b, Color32 color)
        {
            int dx = Mathf.Abs(b.x - a.x), dy = -Mathf.Abs(b.y - a.y);
            int sx = a.x < b.x ? 1 : -1, sy = a.y < b.y ? 1 : -1, error = dx + dy;
            while (true)
            {
                Plot(pixels, width, height, a.x, a.y, color);
                if (a == b)
                    return;
                int e2 = 2 * error;
                if (e2 >= dy) { error += dy; a.x += sx; }
                if (e2 <= dx) { error += dx; a.y += sy; }
            }
        }

        private static void Plot(Color32[] pixels, int width, int height, int x, int y, Color32 color)
        {
            if (x >= 0 && y >= 0 && x < width && y < height)
                pixels[y * width + x] = color;
        }
    }
}
