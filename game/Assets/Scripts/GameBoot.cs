using UnityEngine;

namespace Insectum.Game
{
    public sealed class GameBoot : MonoBehaviour
    {
        private readonly KeyboardInput keyboard = new();
        private GameSession session;
        private Camera routeCamera;
        private SpriteRenderer backdrop;
        private WorldId backdropWorld;
        private RenderTexture view;
        private RenderTexture nightView;
        private Material nightMaterial;
        private static readonly int NightLight = Shader.PropertyToID("_Light");
        private Texture2D markerTexture;
        private Sprite marker;
        private PhysicsMaterial2D surface;
        private int selected;
        private string seedText = string.Empty;
        private bool editingSeed;

#if DEVELOPMENT_BUILD
        private ForestReplay replay;
        private TerrainEditProfile terrainProfile;
#endif

        private void Awake()
        {
            Time.fixedDeltaTime = PlayerMotor.StepSeconds;
            markerTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            markerTexture.SetPixel(0, 0, Color.white);
            markerTexture.Apply();
            marker = Sprite.Create(markerTexture, new Rect(0f, 0f, 1f, 1f), Vector2.one / 2f, 1f);
            surface = new PhysicsMaterial2D("Player movement") { friction = 0f };

            view = CreateNativeTarget("Insectum 320 x 180");
            nightView = CreateNativeTarget("Insectum night vision");
            nightMaterial = new Material(Resources.Load<Shader>("NightVision"));
            var cameraObject = new GameObject("Forest Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(transform, false);
            routeCamera = cameraObject.GetComponent<Camera>();
            routeCamera.orthographic = true;
            routeCamera.orthographicSize = PixelViewport.Height / (2f * PixelViewport.PixelsPerUnit);
            routeCamera.aspect = (float)PixelViewport.Width / PixelViewport.Height;
            routeCamera.allowHDR = false;
            routeCamera.allowMSAA = false;
            routeCamera.targetTexture = view;
            routeCamera.clearFlags = CameraClearFlags.SolidColor;
            routeCamera.backgroundColor = new Color32(23, 18, 37, 255);
            routeCamera.transform.position = new Vector3(0f, 0f, -10f);
            backdrop = ForestArt.Place(transform, "Cavern background", Vector2.zero, ForestArt.Backdrop, -20);

            session = new GameSession(transform, marker, surface);

#if DEVELOPMENT_BUILD
            replay = ForestReplay.FromArguments(session);
            if (replay == null)
                terrainProfile = TerrainEditProfile.FromArguments(session);
#endif
        }

        private void Update()
        {
#if DEVELOPMENT_BUILD
            if (terrainProfile != null)
            {
                terrainProfile.Frame(Time.unscaledDeltaTime);
                return;
            }
            if (replay != null)
            {
                replay.ObserveFrame(Time.unscaledDeltaTime);
                return;
            }
#endif
            if (session.Phase == RunPhase.Playing)
            {
                keyboard.Sample();
                return;
            }

            if (session.Phase == RunPhase.Selection)
                UpdateSelection();
            else if (Input.GetKeyDown(KeyCode.Return) || Click(ForestScreen.RetryButton))
                BeginRun();
            else if (Input.GetKeyDown(KeyCode.Escape) || Click(ForestScreen.RosterButton))
                session.ReturnToSelection();
        }

        private void UpdateSelection()
        {
            if (Input.GetKeyDown(KeyCode.Return) || Click(ForestScreen.StartButton))
            {
                BeginRun();
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                editingSeed = ForestScreen.SeedField.Contains(PixelViewport.Pointer);
                for (int i = 0; i < Character.Roster.Length; i++)
                    if (ForestScreen.RosterCard(i).Contains(PixelViewport.Pointer))
                        selected = i;
            }

            if (Input.GetKeyDown(KeyCode.Tab))
                editingSeed = !editingSeed;
            if (Input.GetKeyDown(KeyCode.Escape))
                editingSeed = false;

            if (editingSeed)
            {
                foreach (char key in Input.inputString)
                {
                    if (key == '\b' && seedText.Length > 0)
                        seedText = seedText[..^1];
                    else if (seedText.Length < 11 && (key is >= '0' and <= '9' || key == '-' && seedText.Length == 0))
                        seedText += key;
                }
                return;
            }

            int change = (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow) ? 1 : 0)
                - (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow) ? 1 : 0);
            int count = Character.Roster.Length;
            selected = (selected + change + count) % count;
        }

        private static bool Click(Rect region) => Input.GetMouseButtonDown(0) && region.Contains(PixelViewport.Pointer);

        private void FixedUpdate()
        {
#if DEVELOPMENT_BUILD
            if (terrainProfile != null)
                return;
            if (replay != null)
            {
                replay.Step();
                return;
            }
#endif
            if (session.Phase == RunPhase.Playing)
                session.Step(keyboard.Consume());
        }

        private void LateUpdate()
        {
            WorldId world = session.Run?.World ?? WorldId.Forest;
            if (world != backdropWorld)
            {
                backdrop.sprite = WorldArt.Backdrop(world);
                backdropWorld = world;
            }

            if (session.Motor == null)
            {
                routeCamera.transform.position = new Vector3(0f, 0f, -10f);
                backdrop.transform.position = Vector3.zero;
                return;
            }

            Vector2 position = session.Motor.Body.position;
#if DEVELOPMENT_BUILD
            position = terrainProfile?.Focus ?? position;
#endif
            float halfWidth = routeCamera.orthographicSize * routeCamera.aspect;
            Rect bounds = session.LevelBounds;
            float margin = Mathf.Min(halfWidth, bounds.width / 2f);
            float yMargin = Mathf.Min(routeCamera.orthographicSize, bounds.height / 2f);
            int direction = session.Layout?.RouteDirection ?? 1;
            float x = Mathf.Clamp(position.x + direction * 1.2f, bounds.xMin + margin, bounds.xMax - margin);
            float y = Mathf.Clamp(position.y - 1.2f, bounds.yMin + yMargin, bounds.yMax - yMargin);
            Vector2 cameraCenter = PixelViewport.Snap(new Vector2(x, y));
            routeCamera.transform.position = new Vector3(cameraCenter.x, cameraCenter.y, -10f);
            float driftX = Mathf.Sin(cameraCenter.x * 0.033f) * 1.65f;
            float driftY = Mathf.Sin(cameraCenter.y * 0.045f) * 0.32f;
            backdrop.transform.position = PixelViewport.Snap(cameraCenter + new Vector2(driftX, driftY));
        }

        private void OnGUI()
        {
            if (session == null || Event.current.type != EventType.Repaint)
                return;

            RenderTexture display = view;
            if (session.NightVisionRadius > 0f && session.Motor != null)
            {
                Vector3 center = routeCamera.WorldToViewportPoint(session.Motor.Body.position);
                nightMaterial.SetVector(NightLight, new Vector4(center.x * PixelViewport.Width, center.y * PixelViewport.Height,
                    session.NightVisionRadius * PixelViewport.PixelsPerUnit, 0f));
                RenderTexture previous = RenderTexture.active;
                Graphics.Blit(view, nightView, nightMaterial);
                RenderTexture.active = previous;
                display = nightView;
            }
            ForestScreen.Draw(display, session, selected, seedText, editingSeed);
        }

        private static RenderTexture CreateNativeTarget(string label) =>
            new(PixelViewport.Width, PixelViewport.Height, 0, RenderTextureFormat.ARGB32)
            {
                name = label,
                antiAliasing = 1,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

        private void BeginRun()
        {
            keyboard.Sample();
            keyboard.Consume();
            editingSeed = false;
            int seed = int.TryParse(seedText, out int requestedSeed) ? requestedSeed : Random.Range(0, int.MaxValue);
            session.StartGenerated(Character.Roster[selected], seed);
        }

        private void OnDestroy()
        {
            view.Release();
            nightView.Release();
            Destroy(nightView);
            Destroy(nightMaterial);
            Destroy(view);
            Destroy(marker);
            Destroy(markerTexture);
            Destroy(surface);
        }
    }
}
