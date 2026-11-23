using System.Collections.Generic;
using UnityEngine;

namespace Insectum
{
    public sealed class MovementLab : MonoBehaviour
    {
        private const int Width = 368;
        private const int Height = 146;
        private const int PixelsPerUnit = 10;
        private static readonly Vector2 WorldOrigin = new(-18.4f, -3.2f);
        private readonly List<Vector2[]> outlines = new();
        private HexTerrain terrain;
        private LabMotor motor;
        private Texture2D view;
        private int drawnRevision = -1;
        private int energy = 100;
        private float digDelay;
        private bool showCollision;
        private bool digPressed;
        private bool jumpPressed;
        private bool smoothingPressed;
        private bool resetPressed;
        private bool switchPressed;
        private Vector2Int? target;
        private string feedback = "Dig through the packed-dirt plug or try the shelves on the left.";

        private void Awake()
        {
            Time.fixedDeltaTime = 1f / 60f;
            terrain = new HexTerrain();
            motor = new LabMotor();
            view = new Texture2D(Width, Height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };

            var camera = new GameObject("Lab camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(16, 13, 23, 255);
            camera.cullingMask = 0;
        }

        private void OnDestroy() => Destroy(view);

        private void Update()
        {
            jumpPressed |= Input.GetKeyDown(KeyCode.Space);
            digPressed |= Input.GetKeyDown(KeyCode.X);
            smoothingPressed |= Input.GetKeyDown(KeyCode.Tab);
            resetPressed |= Input.GetKeyDown(KeyCode.R);
            switchPressed |= Input.GetKeyDown(KeyCode.C);
            if (Input.GetKeyDown(KeyCode.F1))
                showCollision = !showCollision;
        }

        private void FixedUpdate()
        {
            if (resetPressed || switchPressed || motor.Body.position.y < -5f)
            {
                terrain.Reset();
                motor.Reset(switchPressed ? !motor.IsWorm : motor.IsWorm);
                energy = 100;
                digDelay = 0f;
                feedback = motor.IsWorm ? "Dig through the packed-dirt plug." : "Practice movement on the left shelves.";
                resetPressed = false;
                switchPressed = false;
            }
            if (smoothingPressed)
            {
                terrain.SetSmoothing(!terrain.Smooth);
                smoothingPressed = false;
            }

            float horizontal = (Held(KeyCode.D, KeyCode.RightArrow) ? 1f : 0f)
                - (Held(KeyCode.A, KeyCode.LeftArrow) ? 1f : 0f);
            if (jumpPressed)
                motor.QueueJump();
            jumpPressed = false;

            float dt = Time.fixedDeltaTime;
            motor.Step(horizontal, Input.GetKey(KeyCode.Space), dt);
            bool down = Held(KeyCode.S, KeyCode.DownArrow);
            Vector2 aim = down ? Vector2.down : new Vector2(motor.Facing, 0f);
            target = motor.IsWorm ? terrain.FindDigTarget(motor.Body.position, aim, 1.18f) : null;

            digDelay -= dt;
            if (motor.IsWorm && motor.Hearts > 0 && digDelay <= 0f && (digPressed || Input.GetKey(KeyCode.X)))
            {
                feedback = target.HasValue ? terrain.TryDig(target.Value, ref energy) : "No tile in reach";
                target = terrain.FindDigTarget(motor.Body.position, aim, 1.18f);
                digDelay = 0.15f;
            }
            digPressed = false;
        }

        private static bool Held(KeyCode first, KeyCode second) => Input.GetKey(first) || Input.GetKey(second);

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint)
                return;
            if (drawnRevision != terrain.Revision)
                RefreshTerrain();

            float fit = Mathf.Min(Screen.width / (float)Width, (Screen.height - 120f) / Height);
            float scale = fit >= 1f ? Mathf.Floor(fit) : Mathf.Max(0.1f, fit);
            var room = new Rect((Screen.width - Width * scale) / 2f, (Screen.height - Height * scale) / 2f,
                Width * scale, Height * scale);
            GUI.DrawTexture(room, view);

            Vector2 ScreenPoint(Vector2 world)
            {
                Vector2 point = (world - WorldOrigin) * PixelsPerUnit;
                return new Vector2(room.x + Mathf.Round(point.x) * scale, room.yMax - Mathf.Round(point.y) * scale);
            }

            if (showCollision)
            {
                foreach (var path in outlines)
                    DrawOutline(path, Vector2.zero, ScreenPoint, new Color32(98, 205, 209, 255));
            }
            if (target.HasValue)
                DrawOutline(HexTerrain.Corners, HexTerrain.Center(target.Value), ScreenPoint, new Color32(255, 205, 111, 255));

            Vector2 player = ScreenPoint(motor.Body.position);
            Color body = motor.IsWorm ? new Color32(248, 157, 124, 255) : new Color32(173, 199, 110, 255);
            DrawRect(new Rect(player.x - 4 * scale, player.y - 3 * scale, 8 * scale, 6 * scale), body);
            DrawRect(new Rect(player.x - 3 * scale, player.y - 4 * scale, 6 * scale, 8 * scale), body);
            DrawRect(new Rect(player.x + (motor.Facing > 0 ? 1 : -2) * scale, player.y - 2 * scale, scale, scale),
                new Color32(33, 22, 43, 255));

            string mode = terrain.Smooth ? "Smoothed shelves" : "Raw hexes";
            GUI.Label(new Rect(room.x, room.y - 48, room.width, 23), "Insectum / Hex terrain lab");
            GUI.Label(new Rect(room.x, room.y - 26, room.width, 23),
                $"{(motor.IsWorm ? "Worm" : "Baseline")}    Hearts {motor.Hearts}/4    Energy {energy}/100    {mode}");
            GUI.Label(new Rect(room.x, room.yMax + 6, Screen.width - room.x, 23), motor.Hearts > 0 ? feedback : "Press R to restart");
            GUI.Label(new Rect(room.x, room.yMax + 28, Screen.width - room.x, 23),
                "A/D move   Space jump   X dig   Down+X dig below   C switch   Tab smooth   F1 collision   R reset");
        }

        private void RefreshTerrain()
        {
            var pixels = new Color32[Width * Height];
            for (int y = 0; y < Height; y++)
            {
                var background = new Color32((byte)(22 + y / 13), 16, (byte)(34 + y / 8), 255);
                for (int x = 0; x < Width; x++)
                    pixels[y * Width + x] = background;
            }

            foreach (var tile in terrain.Tiles)
            {
                Vector2 center = (HexTerrain.Center(tile.Key) - WorldOrigin) * PixelsPerUnit;
                Color32 fill = tile.Value switch
                {
                    HexTerrain.Material.SoftDirt => new Color32(147, 90, 70, 255),
                    HexTerrain.Material.PackedDirt => new Color32(175, 113, 70, 255),
                    HexTerrain.Material.SoftStone => new Color32(113, 98, 140, 255),
                    HexTerrain.Material.Metal => new Color32(79, 133, 141, 255),
                    _ => new Color32(68, 54, 85, 255)
                };

                int left = Mathf.Max(0, Mathf.FloorToInt(center.x - PixelsPerUnit));
                int right = Mathf.Min(Width - 1, Mathf.CeilToInt(center.x + HexTerrain.ColumnSpacing * PixelsPerUnit));
                int bottom = Mathf.Max(0, Mathf.FloorToInt(center.y - PixelsPerUnit));
                int top = Mathf.Min(Height - 1, Mathf.CeilToInt(center.y + PixelsPerUnit));
                bool capped = terrain.HasShelfCap(tile.Key);
                for (int y = bottom; y <= top; y++)
                for (int x = left; x <= right; x++)
                {
                    float dx = (x + 0.5f - center.x) / PixelsPerUnit;
                    float dy = (y + 0.5f - center.y) / PixelsPerUnit;
                    float edge = 1f - Mathf.Abs(dx) / HexTerrain.ColumnSpacing;
                    if (Mathf.Abs(dx) <= HexTerrain.HalfWidth && Mathf.Abs(dy) <= edge)
                    {
                        bool border = Mathf.Abs(dx) > 0.76f || Mathf.Abs(dy) > edge - 0.12f;
                        pixels[y * Width + x] = border ? Shade(fill, 0.72f) : fill;
                    }
                    else if (capped && dx > 0f && dx < HexTerrain.ColumnSpacing && dy <= 1f
                        && dy > 1f - Mathf.Min(dx, HexTerrain.ColumnSpacing - dx) / HexTerrain.ColumnSpacing)
                    {
                        pixels[y * Width + x] = new Color32(104, 82, 107, 255);
                    }
                }
            }

            view.SetPixels32(pixels);
            view.Apply();
            outlines.Clear();
            for (int i = 0; i < terrain.Collider.pathCount; i++)
            {
                var path = new Vector2[terrain.Collider.GetPathPointCount(i)];
                terrain.Collider.GetPath(i, path);
                outlines.Add(path);
            }
            drawnRevision = terrain.Revision;
        }

        private static Color32 Shade(Color32 color, float value) =>
            new((byte)(color.r * value), (byte)(color.g * value), (byte)(color.b * value), color.a);

        private static void DrawOutline(Vector2[] path, Vector2 offset, System.Func<Vector2, Vector2> screen, Color color)
        {
            for (int i = 0; i < path.Length; i++)
            {
                Vector2 from = screen(path[i] + offset);
                Vector2 delta = screen(path[(i + 1) % path.Length] + offset) - from;
                Matrix4x4 previous = GUI.matrix;
                GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, from);
                DrawRect(new Rect(from.x, from.y, delta.magnitude, 1f), color);
                GUI.matrix = previous;
            }
        }

        private static void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
