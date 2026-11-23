using UnityEngine;

namespace Insectum.Game
{
    internal static class ForestScreen
    {
        private static readonly Color Ink = new Color32(18, 13, 29, 255);
        private static readonly Color Panel = new Color32(34, 24, 46, 255);
        private static readonly Color Edge = new Color32(87, 61, 83, 255);
        private static readonly Color Light = new Color32(250, 224, 180, 255);
        private static readonly Color Muted = new Color32(192, 169, 194, 255);
        private static readonly Color Gold = new Color32(253, 180, 96, 255);
        private static readonly Color Mint = new Color32(110, 213, 184, 255);
        private static readonly Color Pink = new Color32(244, 129, 153, 255);
        private static readonly string[] ShortNames = { "SPIDER", "BEETLE", "DRAGONFLY", "FIRE ANT", "WORM" };
        private static readonly string[] AbilityNames = { "High jump and auto wall climb", "Momentum shell", "Directional flight", "Heavy mandible strike", "Excavate one hex" };

        internal static Rect SeedField => new(23, 141, 124, 18);
        internal static Rect StartButton => new(168, 141, 129, 18);
        internal static Rect RetryButton => new(57, 132, 97, 19);
        internal static Rect RosterButton => new(166, 132, 97, 19);
        internal static Rect ResumeButton => new(100, 145, 120, 19);

        internal static Rect RosterCard(int index) => index == (int)Insect.Dragonfly
            ? new Rect(132, 59, 56, 49) : new Rect(23 + index * 56, 59, 50, 49);

        internal static void Draw(RenderTexture world, Camera camera, GameSession session, int selected, string seed,
            bool editingSeed, bool paused)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            Rect viewport = PixelViewport.ScreenRect;
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            GUI.color = Ink;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(viewport, world);

            float scale = viewport.width / PixelViewport.Width;
            GUI.matrix = Matrix4x4.TRS(new Vector3(viewport.x, viewport.y, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));
            if (session.Phase == RunPhase.Playing)
            {
                if (paused)
                    Fill(new Rect(0, 0, PixelViewport.Width, PixelViewport.Height), new Color32(15, 9, 27, 172));
                else if (session.Content?.NearbyShop() is LevelFeature shop)
                    DrawShopOffer(camera, session, shop);
                else if (session.Content?.ClosestInteractable() is LevelFeature feature)
                    DrawWorldPrompt(camera, feature.Position + Vector2.up * 1.4f, feature.InteractPrompt, Gold);
                else if (session.Abilities.DigFeedback is string feedback)
                    DrawWorldPrompt(camera, session.Motor.Body.position + Vector2.up * 1.2f, feedback, Gold);
                DrawHud(session.Run);
                if (paused)
                    DrawPause(session);
            }
            else if (session.Phase == RunPhase.Selection)
                DrawSelection(selected, seed, editingSeed);
            else
                DrawResults(session);

            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }

        private static void DrawSelection(int selected, string seed, bool editingSeed)
        {
            Shade();
            Center("INSECTUM", 160, 18, Ink, 3);
            Center("INSECTUM", 160, 16, Light, 3);
            Fill(new Rect(48, 47, 69, 1), Edge);
            Fill(new Rect(203, 47, 69, 1), Edge);
            Center("FOREST", 160, 43, Gold);

            for (int index = 0; index < ShortNames.Length; index++)
            {
                Rect rect = RosterCard(index);
                bool active = index == selected;
                Frame(rect, active ? Gold : Edge, active ? new Color32(62, 43, 60, 255) : Panel);
                int pose = active ? Mathf.FloorToInt(Time.unscaledTime * 4) % 3 : 0;
                GUI.color = Color.white;
                GUI.DrawTextureWithTexCoords(new Rect(rect.center.x - 16, rect.y + 6, 32, 24), ForestArt.Actors,
                    ForestArt.TexCoords(ForestArt.Actors, pose * 32, index * 24, 32, 24));
                Center(ShortNames[index], (int)rect.center.x, (int)rect.y + 38, active ? Light : Muted);
            }

            Character character = Character.Roster[selected];
            Frame(new Rect(23, 113, 274, 22), Edge, Panel);
            Write(character.Name, 30, 118, Light);
            Write(AbilityNames[selected], 30, 126, Mint);
            for (int i = 0; i < character.MaxHealth && i < 8; i++)
                Heart(287 - i * 8, 120, Pink);

            Frame(SeedField, editingSeed ? Gold : Edge, Panel);
            Write("SEED", 29, 147, Gold);
            Write(string.IsNullOrEmpty(seed) ? "RANDOM" : seed, 58, 147, editingSeed ? Light : Muted);
            Button(StartButton, "ENTER FOREST");
            Center("A/D OR ARROW KEYS TO SELECT", 160, 166, Muted);
        }

        private static void DrawResults(GameSession session)
        {
            bool cleared = session.Phase == RunPhase.Cleared;
            Shade();
            string heading = !cleared ? "RUN ENDED" : session.Run.LevelNumber == RunWorlds.LevelCount
                ? "RUN COMPLETE" : $"{session.Run.World} CLEARED";
            Center(heading, 160, 30, cleared ? Gold : Pink, 2);
            Frame(new Rect(57, 54, 206, 70), Edge, Panel);
            int row = (int)session.Run.Character.Insect;
            GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(new Rect(62, 64, 64, 48), ForestArt.Actors,
                ForestArt.TexCoords(ForestArt.Actors, 0, row * 24, 32, 24));
            Write(session.Run.Character.Name, 134, 66, Light);
            Write($"{session.Run.World} {session.Run.WorldLevel}/{RunWorlds.LevelsPerWorld}", 134, 78, Mint);
            Write($"NEKTAR {session.Run.Inventory.Nektar}", 134, 90, Gold);
            Write($"SEED {session.Run.Seed}", 134, 102, Muted);
            Button(RetryButton, "ENTER RETRY");
            Button(RosterButton, "ESC ROSTER");
        }

        private static void DrawHud(RunState run)
        {
            const int heartsPerRow = 18;
            for (int i = 0; i < run.MaxHealth; i++)
            {
                int x = 8 + i % heartsPerRow * 9;
                int y = 9 + i / heartsPerRow * 8;
                Heart(x - 1, y, Ink);
                Heart(x + 1, y, Ink);
                Heart(x, y - 1, Ink);
                Heart(x, y + 1, Ink);
                Heart(x, y, i < run.Health ? Pink : Edge);
            }

            int barY = 17 + (run.MaxHealth - 1) / heartsPerRow * 8;
            Frame(new Rect(7, barY, 53, 7), Ink, Edge);
            Fill(new Rect(8, barY + 1, Mathf.Round(51f * run.Energy / run.Character.MaxEnergy), 5), Mint);

            string nektar = run.Inventory.Nektar.ToString();
            int nektarX = 312 - TextWidth(nektar);
            int levelX = nektarX - 20;
            string level = $"{run.World} {run.WorldLevel}/{RunWorlds.LevelsPerWorld}";
            Right(level, levelX + 1, 10, Ink);
            Right(level, levelX, 9, Light);
            Write(nektar, nektarX + 1, 10, Ink);
            Write(nektar, nektarX, 9, Gold);

            Sprite pickup = ForestArt.Prop(ForestProp.Nektar);
            GUI.color = Color.white;
            // Crop the transparent margins of the pickup's atlas cell.
            GUI.DrawTextureWithTexCoords(new Rect(nektarX - 12, 5, 9, 16), pickup.texture,
                ForestArt.TexCoords(pickup.texture, 12, 9, 9, 16));
        }

        private static void DrawShopOffer(Camera camera, GameSession session, LevelFeature shop)
        {
            string label = !shop.Available ? "SOLD OUT"
                : session.Content.ClosestInteractable() == shop ? shop.InteractPrompt : shop.ShopOffer;
            DrawWorldPrompt(camera, shop.Position + Vector2.up * 1.4f, label, shop.Available ? Gold : Muted);
        }

        private static void DrawWorldPrompt(Camera camera, Vector2 point, string prompt, Color color)
        {
            Vector3 anchor = camera.WorldToViewportPoint(point);
            int halfWidth = (TextWidth(prompt) + 1) / 2;
            int x = Mathf.Clamp(Mathf.RoundToInt(anchor.x * PixelViewport.Width), halfWidth + 2,
                PixelViewport.Width - halfWidth - 2);
            int y = Mathf.RoundToInt((1f - anchor.y) * PixelViewport.Height) - 7;
            Center(prompt, x - 1, y, Ink);
            Center(prompt, x + 1, y, Ink);
            Center(prompt, x, y - 1, Ink);
            Center(prompt, x, y + 1, Ink);
            Center(prompt, x, y, color);
        }

        private static void DrawPause(GameSession session)
        {
            RunState run = session.Run;
            Frame(new Rect(14, 26, 292, 144), Edge, Panel);
            Center("PAUSED", 160, 33, Light, 2);

            Write("CONTROLS", 24, 55, Gold);
            Write("A/D  MOVE", 24, 66, Light);
            Write("SPACE  JUMP (HOLD HIGHER)", 24, 75, Light);
            Write("J  HIT  /  W+J  HIT UP", 24, 84, Light);
            Write("E  INTERACT  /  L  ITEM", 24, 93, Light);
            Write(run.Character.Insect switch
            {
                Insect.Spider => "K  HIGH JUMP",
                Insect.Beetle => "K  TOGGLE SHELL",
                Insect.Dragonfly => "K  FLY  /  WASD  STEER",
                Insect.FireAnt => "K  HEAVY HIT",
                _ => "K  DIG AHEAD  /  S+K  DIG DOWN"
            }, 24, 102, Mint);

            if (run.Character.Insect == Insect.Spider)
            {
                Write("WALK INTO WALL TO CLIMB", 24, 111, Muted);
                Write("RELEASE MOVE TO DROP", 176, 111, Muted);
            }

            Write(run.Character.Name, 176, 55, Gold);
            Write($"SEED {run.Seed}", 176, 66, Light);
            Write($"ITEM  {Items.Name(run.Inventory.Active)}", 176, 75, Light);
            int cooldown = run.Inventory.ActiveCooldownSteps;
            if (cooldown > 0)
                Write($"RECHARGE  {cooldown * PlayerMotor.StepSeconds:0.0}S", 176, 84, Muted);

            string variant = session.Variant switch
            {
                LevelVariant.Night => "NIGHT",
                LevelVariant.HeavySpawn => "HEAVY SPAWN",
                LevelVariant.DoubleBoss => $"BOSSES {session.Content.BossesRemaining}/2",
                _ => null
            };
            if (variant != null)
                Write(variant, 176, 93, Mint);

            if (session.InteractionPrompt is string prompt)
                Center(prompt, 160, 122, Gold);
            if (session.Status is string status)
                Center(status, 160, 133, Muted);
            Button(ResumeButton, "ESC  RESUME");
        }

        private static void Shade() =>
            Fill(new Rect(0, 0, PixelViewport.Width, PixelViewport.Height), new Color32(15, 9, 27, 83));

        private static void Button(Rect rect, string text)
        {
            bool hover = rect.Contains(PixelViewport.Pointer);
            Frame(rect, hover ? Light : Gold, hover ? new Color32(94, 60, 62, 255) : new Color32(64, 41, 55, 255));
            Center(text, (int)rect.center.x, (int)rect.y + 6, Light);
        }

        private static void Heart(int x, int y, Color color)
        {
            Fill(new Rect(x, y + 1, 7, 2), color);
            Fill(new Rect(x + 1, y, 2, 1), color);
            Fill(new Rect(x + 4, y, 2, 1), color);
            Fill(new Rect(x + 1, y + 3, 5, 1), color);
            Fill(new Rect(x + 2, y + 4, 3, 1), color);
            Fill(new Rect(x + 3, y + 5, 1, 1), color);
        }

        private static void Frame(Rect rect, Color border, Color fill)
        {
            Fill(rect, border);
            Fill(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f), fill);
        }

        private static void Fill(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
        }

        private static int TextWidth(string text, int scale = 1) => (text.Length * 6 - 1) * scale;

        private static void Right(string text, int x, int y, Color color) =>
            Write(text, x - TextWidth(text), y, color);

        private static void Center(string text, int x, int y, Color color, int scale = 1) =>
            Write(text, x - TextWidth(text, scale) / 2, y, color, scale);

        private static void Write(string text, int x, int y, Color color, int scale = 1)
        {
            GUI.color = color;
            Texture2D font = ForestArt.Glyphs;
            foreach (char character in text)
            {
                int code = char.ToUpperInvariant(character) - 32;
                if (code < 0 || code > 63)
                    code = '?' - 32;
                if (code != 0 && x >= 0 && x + 5 * scale <= PixelViewport.Width)
                    GUI.DrawTextureWithTexCoords(new Rect(x, y, 5 * scale, 7 * scale), font,
                        ForestArt.TexCoords(font, (code % 16) * 6, (code / 16) * 8, 5, 7));
                x += 6 * scale;
            }
        }
    }
}
