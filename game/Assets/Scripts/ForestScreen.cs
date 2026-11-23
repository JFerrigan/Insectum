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
        private static readonly string[] AbilityNames = { "Leap and wall climb", "Momentum shell", "Directional flight", "Heavy mandible strike", "Excavate one hex" };

        internal static Rect SeedField => new(23, 141, 124, 18);
        internal static Rect StartButton => new(168, 141, 129, 18);
        internal static Rect RetryButton => new(57, 132, 97, 19);
        internal static Rect RosterButton => new(166, 132, 97, 19);

        internal static Rect RosterCard(int index) => index == (int)Insect.Dragonfly
            ? new Rect(132, 59, 56, 49) : new Rect(23 + index * 56, 59, 50, 49);

        internal static void Draw(RenderTexture world, GameSession session, int selected, string seed, bool editingSeed)
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
                DrawHud(session);
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

        private static void DrawHud(GameSession session)
        {
            RunState run = session.Run;
            Frame(new Rect(5, 5, 310, 26), Edge, new Color32(29, 21, 41, 242));
            Write($"{run.World} {run.WorldLevel}/{RunWorlds.LevelsPerWorld}", 11, 9, Gold);
            Write(run.Character.Name, 83, 9, Light);
            string variant = session.Variant switch
            {
                LevelVariant.Night => "NIGHT",
                LevelVariant.HeavySpawn => "HEAVY SPAWN",
                LevelVariant.DoubleBoss => $"BOSSES {session.Content.BossesRemaining}/2",
                _ => null
            };
            if (variant != null)
                Write(variant, 174, 9, Mint);
            Right($"NEKTAR {run.Inventory.Nektar}", 308, 9, Gold);
            for (int i = 0; i < Mathf.Min(run.MaxHealth, 8); i++)
                Heart(11 + i * 8, 20, i < run.Health ? Pink : Edge);
            if (run.MaxHealth > 8)
                Write($"{run.Health}/{run.MaxHealth}", 77, 20, Light);

            Write("K", 118, 20, Mint);
            Fill(new Rect(127, 20, 51, 7), Edge);
            Fill(new Rect(128, 21, 49, 5), Ink);
            Fill(new Rect(128, 21, Mathf.Round(49f * run.Energy / run.Character.MaxEnergy), 5), Mint);
            if (run.Inventory.Active != ItemId.None)
            {
                int cooldown = run.Inventory.ActiveCooldownSteps;
                string recharge = cooldown > 0 ? $" {cooldown * PlayerMotor.StepSeconds:0.0}S" : string.Empty;
                Right($"L {Items.Name(run.Inventory.Active)}{recharge}", 308, 20, cooldown == 0 ? Light : Muted);
            }
            else
                Right($"SEED {run.Seed}", 308, 20, Muted);

            string prompt = session.InteractionPrompt;
            if (session.Status is string status)
                Toast(status, prompt == null ? 140 : 128, Mint);
            if (prompt != null)
                Toast(prompt, 144, Gold);

            Fill(new Rect(5, 164, 310, 11), new Color32(27, 17, 38, 226));
            Center(Controls(session), 160, 166, Muted);
        }

        private static string Controls(GameSession session) => session.Run.Character.Insect switch
        {
            Insect.Spider when session.Abilities.Climbing => "W/S CLIMB   RELEASE K TO DROP",
            Insect.Spider => "A/D MOVE   SPACE JUMP   J HIT   K LEAP   W+K CLIMB",
            Insect.Beetle when session.Abilities.Shelled => "K OPEN SHELL",
            Insect.Beetle => "A/D MOVE   SPACE JUMP   J HIT   K SHELL",
            Insect.Dragonfly when session.Abilities.Flying => "A/D/W/S STEER   J HIT   K LAND",
            Insect.Dragonfly => "A/D MOVE   SPACE JUMP   J HIT   K FLY",
            Insect.FireAnt => "A/D MOVE   SPACE JUMP   J HIT   K HEAVY HIT",
            _ => "A/D MOVE   SPACE JUMP   J HIT   K DIG   S+K BELOW"
        };

        private static void Shade() =>
            Fill(new Rect(0, 0, PixelViewport.Width, PixelViewport.Height), new Color32(15, 9, 27, 83));

        private static void Button(Rect rect, string text)
        {
            bool hover = rect.Contains(PixelViewport.Pointer);
            Frame(rect, hover ? Light : Gold, hover ? new Color32(94, 60, 62, 255) : new Color32(64, 41, 55, 255));
            Center(text, (int)rect.center.x, (int)rect.y + 6, Light);
        }

        private static void Toast(string text, int y, Color color)
        {
            int width = Mathf.Min(306, TextWidth(text) + 12);
            Frame(new Rect((320 - width) / 2, y, width, 13), Edge, Panel);
            Center(text, 160, y + 3, color);
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
