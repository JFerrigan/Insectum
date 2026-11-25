using UnityEngine;

namespace Insectum.Game
{
    internal enum MenuPage { Main, Settings, Roster, Encyclopedia }
    internal enum EncyclopediaSection { Items, Enemies }

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
        private static readonly Color BarRed = new Color32(214, 58, 52, 255);
        private static readonly Color BarShine = new Color32(255, 128, 100, 255);
        private static readonly Color BarChip = new Color32(255, 226, 190, 255);
        private static readonly Color BarBack = new Color32(44, 22, 36, 255);
        private static readonly string[] ShortNames = { "SPIDER", "BEETLE", "DRAGONFLY", "FIRE ANT", "WORM" };
        private static readonly string[] AbilityNames = { "High jump and auto wall climb", "Momentum shell", "Directional flight", "Heavy mandible strike", "Excavate one hex" };
        private const int EncyclopediaRowsPerPage = 6;
        private const int EncyclopediaRowHeight = 18;
        private const int EncyclopediaTextX = 58;
        private const int EncyclopediaRight = 298;

        internal static Rect SeedField => new(23, 141, 124, 18);
        internal static Rect StartButton => new(168, 141, 129, 18);
        internal static Rect DevCheckbox => new(250, 163, 62, 10);
        internal static Rect BossRushCheckbox => new(176, 163, 68, 10);
        internal static Rect RosterBackButton => new(8, 8, 56, 19);
        internal static Rect RetryButton => new(57, 132, 97, 19);
        internal static Rect RosterButton => new(166, 132, 97, 19);
        internal static Rect ResumeButton => new(38, 159, 118, 17);
        internal static Rect QuitToRosterButton => new(164, 159, 118, 17);
        internal static Rect PlayButton => new(110, 74, 100, 19);
        internal static Rect SettingsButton => new(110, 97, 100, 19);
        internal static Rect EncyclopediaButton => new(110, 120, 100, 19);
        internal static Rect QuitButton => new(110, 143, 100, 19);
        internal static Rect EncyclopediaBackButton => RosterBackButton;
        internal static Rect EncyclopediaTab(EncyclopediaSection section) => new(106 + (int)section * 56, 30, 52, 15);
        internal static Rect EncyclopediaPrev => new(114, 160, 20, 17);
        internal static Rect EncyclopediaNext => new(186, 160, 20, 17);
        internal static Rect BackButton => new(110, 140, 100, 19);
        internal static Rect LevelDown(int row) => new(150, 72 + row * 26, 15, 15);
        internal static Rect LevelUp(int row) => new(262, 72 + row * 26, 15, 15);

        internal static Rect RosterCard(int index) => index == (int)Insect.Dragonfly
            ? new Rect(132, 59, 56, 49) : new Rect(23 + index * 56, 59, 50, 49);

        internal static int EncyclopediaPageCount(EncyclopediaSection section) =>
            (EncyclopediaEntryCount(section) + EncyclopediaRowsPerPage - 1) / EncyclopediaRowsPerPage;

        private static int EncyclopediaEntryCount(EncyclopediaSection section) => section == EncyclopediaSection.Items
            ? (int)ItemId.FluxCore - (int)ItemId.ExcavatorClaw + 1 : EnemySpec.All.Count;

        internal static void Draw(RenderTexture world, Camera camera, GameSession session, MenuPage menu, int selected, string seed,
            bool editingSeed, bool paused, bool bossRush, EncyclopediaSection encyclopediaTab, int encyclopediaPage)
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
                if (!paused)
                {
                    DrawTutorial(camera, session);
                    DrawPickupName(camera, session);
                    DrawVariantBanner(session);
                    if (DevMode.Enabled && DevMode.ShowHitboxes)
                        DrawHitboxes(camera, session.Content?.MainBoss?.Brain);
                    DrawBossBar(session);
                }
                DrawHud(session.Run);
                if (DevMode.Enabled && !paused)
                    DrawDevPanel(session);
                if (paused)
                    DrawPause(session);
            }
            else if (session.Phase == RunPhase.Selection && menu == MenuPage.Main)
                DrawMainMenu();
            else if (session.Phase == RunPhase.Selection && menu == MenuPage.Settings)
                DrawSettings();
            else if (session.Phase == RunPhase.Selection && menu == MenuPage.Encyclopedia)
                DrawEncyclopedia(encyclopediaTab, encyclopediaPage);
            else if (session.Phase == RunPhase.Selection)
                DrawSelection(selected, seed, editingSeed, bossRush);
            else
                DrawResults(session);

            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }

        private static void DrawMainMenu()
        {
            Shade();
            Center("INSECTUM", 160, 36, Ink, 4);
            Center("INSECTUM", 160, 34, Light, 4);
            Fill(new Rect(70, 66, 180, 1), Edge);
            Button(PlayButton, "PLAY");
            Button(SettingsButton, "SETTINGS");
            Button(EncyclopediaButton, "ENCYCLOPEDIA");
            Button(QuitButton, "QUIT");
        }

        private static void DrawEncyclopedia(EncyclopediaSection section, int page)
        {
            Shade();
            Button(EncyclopediaBackButton, "BACK");
            Center("ENCYCLOPEDIA", 160, 12, Light, 2);
            DrawEncyclopediaTab(EncyclopediaSection.Items, section, "ITEMS");
            DrawEncyclopediaTab(EncyclopediaSection.Enemies, section, "ENEMIES");
            Frame(new Rect(12, 48, 296, 2 + EncyclopediaRowsPerPage * EncyclopediaRowHeight), Edge, Panel);

            int first = page * EncyclopediaRowsPerPage;
            int rows = Mathf.Min(EncyclopediaRowsPerPage, EncyclopediaEntryCount(section) - first);
            for (int row = 0; row < rows; row++)
            {
                int y = 49 + row * EncyclopediaRowHeight;
                if (row > 0)
                    Fill(new Rect(20, y - 1, 280, 1), Edge);
                if (section == EncyclopediaSection.Items)
                    DrawEncyclopediaItem(ItemId.ExcavatorClaw + first + row, y);
                else
                    DrawEncyclopediaEnemy(EnemySpec.All[first + row], y);
            }

            int pages = EncyclopediaPageCount(section);
            if (pages > 1)
            {
                Button(EncyclopediaPrev, "<", page > 0);
                Button(EncyclopediaNext, ">", page < pages - 1);
                Center($"{page + 1}/{pages}", 160, 165, Muted);
            }
        }

        private static void DrawEncyclopediaTab(EncyclopediaSection tab, EncyclopediaSection section, string label)
        {
            Rect rect = EncyclopediaTab(tab);
            bool selected = tab == section;
            bool hover = rect.Contains(PixelViewport.Pointer);
            Frame(rect, selected ? Light : hover ? Gold : Edge, selected ? new Color32(64, 41, 55, 255) : Panel);
            Center(label, (int)rect.center.x, (int)rect.y + 4, selected || hover ? Light : Muted);
        }

        private static void DrawEncyclopediaItem(ItemId item, int y)
        {
            bool active = Items.IsActive(item);
            DrawItemIcon(item, new Rect(28, y + 1, 16, 16));
            Write(Items.Name(item), EncyclopediaTextX, y + 1, Light);
            Right(active ? "ACTIVE" : "PASSIVE", EncyclopediaRight, y + 1, active ? Gold : Mint);
            Write(Items.Description(item), EncyclopediaTextX, y + 9, Muted);
        }

        private static void DrawEncyclopediaEnemy((EnemySpec Spec, WorldId World) entry, int y)
        {
            var (spec, world) = entry;
            Sprite portrait = EnemySpec.Portrait(spec);
            Rect source = portrait.rect;
            float scale = Mathf.Min(30f / source.width, 16f / source.height);
            int width = Mathf.RoundToInt(source.width * scale);
            int height = Mathf.RoundToInt(source.height * scale);
            DrawSprite(portrait, new Rect(36 - width / 2, y + 9 - height / 2, width, height));
            Write(spec.Name, EncyclopediaTextX, y + 1, Light);
            Write(world.ToString(), EncyclopediaTextX, y + 9, Muted);
            if (spec.IsBoss)
                Write("BOSS", 148, y + 1, Gold);
            Heart(193, y + 6, Pink);
            Write($"X {spec.Hearts}", 203, y + 5, Light);
            Right($"{spec.Nektar} NEKTAR", EncyclopediaRight, y + 5, Gold);
        }

        private static void DrawSettings()
        {
            Shade();
            Center("SETTINGS", 160, 28, Light, 2);
            Frame(new Rect(34, 60, 252, 64), Edge, Panel);
            DrawLevel(0, "SFX", GameSettings.SfxLevel);
            DrawLevel(1, "MUSIC", GameSettings.MusicLevel);
            Button(BackButton, "BACK");
        }

        private static void DrawLevel(int row, string label, int level)
        {
            Rect down = LevelDown(row);
            Rect up = LevelUp(row);
            Write(label, 46, (int)down.y + 4, Gold);
            Button(down, "-");
            Button(up, "+");
            for (int i = 0; i < GameSettings.MaxLevel; i++)
                Fill(new Rect(170 + i * 9, down.y + 3, 7, 9), i < level ? Mint : Edge);
            Right(level.ToString(), 144, (int)down.y + 4, Light);
        }

        private static void DrawSelection(int selected, string seed, bool editingSeed, bool bossRush)
        {
            Shade();
            Button(RosterBackButton, "BACK");
            Center("INSECTUM", 160, 18, Ink, 3);
            Center("INSECTUM", 160, 16, Light, 3);
            Fill(new Rect(48, 47, 224, 1), Edge);

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
            Button(StartButton, "START");
            Checkbox(BossRushCheckbox, "BOSS RUSH", bossRush);
            Checkbox(DevCheckbox, "DEV MODE", DevMode.Enabled);
        }

        private static void Checkbox(Rect rect, string label, bool on)
        {
            Frame(new Rect(rect.x, rect.y, 10, 10), on ? Gold : Edge, Panel);
            if (on)
                Fill(new Rect(rect.x + 3, rect.y + 3, 4, 4), Gold);
            Write(label, (int)rect.x + 14, (int)rect.y + 2, on ? Gold : Muted);
        }

        private static void DrawResults(GameSession session)
        {
            bool cleared = session.Phase == RunPhase.Cleared;
            Shade();
            string heading = !cleared ? "RUN ENDED" : session.BossRush ? "BOSS RUSH COMPLETE"
                : session.Run.LevelNumber == RunWorlds.LevelCount ? "RUN COMPLETE" : $"{session.Run.World} CLEARED";
            Center(heading, 160, 30, cleared ? Gold : Pink, 2);
            Frame(new Rect(57, 54, 206, 70), Edge, Panel);
            int row = (int)session.Run.Character.Insect;
            GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(new Rect(62, 64, 64, 48), ForestArt.Actors,
                ForestArt.TexCoords(ForestArt.Actors, 0, row * 24, 32, 24));
            Write(session.Run.Character.Name, 134, 66, Light);
            Write(DevMode.LevelName(session.Run.LevelNumber), 134, 78, Mint);
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
            if (Items.IsActive(run.Inventory.Active))
            {
                float charge = run.Inventory.ActiveCharge;
                Frame(new Rect(7, barY + 8, 53, 5), Ink, Edge);
                Fill(new Rect(8, barY + 9, Mathf.Round(51f * charge), 3), charge >= 1f ? Gold : Muted);
            }

            string nektar = run.Inventory.Nektar.ToString();
            int nektarX = 312 - TextWidth(nektar);
            int levelX = nektarX - 20;
            string level = DevMode.LevelName(run.LevelNumber);
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
            DrawWorldPrompt(camera, shop.Position + Vector2.up * 2.75f, label, shop.Available ? Gold : Muted);
        }

        // Faint control hints painted into the opening room of the first level. They are placed
        // once per level inside the open air around the spawn and fade out as the player leaves.
        private static readonly string[] TutorialLines = { "SPACE JUMP   J HIT", "K ABILITY   L ITEM", "E INTERACT" };
        private const float TutorialScale = 0.75f;
        private static Vector2 tutorialSpawn = new(float.NaN, float.NaN);
        private static Vector2 tutorialAnchor;

        private static void DrawTutorial(Camera camera, GameSession session)
        {
            if (session.Run.LevelNumber != 1)
                return;
            if (session.LevelSpawn != tutorialSpawn)
            {
                tutorialSpawn = session.LevelSpawn;
                tutorialAnchor = PlaceTutorial(session.LevelSpawn, session.Motor.Body);
            }
            float distance = Vector2.Distance(session.Motor.Body.position, tutorialSpawn);
            float alpha = 0.4f * Mathf.Clamp01(1f - (distance - 4f) / 6f);
            if (alpha <= 0.01f)
                return;
            Color color = Muted;
            color.a = alpha;
            Vector3 view = camera.WorldToViewportPoint(tutorialAnchor);
            float x = view.x * PixelViewport.Width;
            float y = (1f - view.y) * PixelViewport.Height;
            float lineHeight = 9f * TutorialScale;
            for (int i = 0; i < TutorialLines.Length; i++)
                WriteScaled(TutorialLines[i], x - (TutorialLines[i].Length * 6 - 1) * TutorialScale / 2f,
                    y + i * lineHeight, color, TutorialScale);
        }

        // Finds open air above the spawn: below the ceiling, centred between the side walls.
        private static Vector2 PlaceTutorial(Vector2 spawn, Rigidbody2D player)
        {
            const float blockHeight = 1.4f;
            float ceiling = OpenDistance(spawn + Vector2.up * 0.5f, Vector2.up, 8f, player) + 0.5f;
            float top = Mathf.Min(3.4f, ceiling - 0.5f);
            float middle = Mathf.Max(top - blockHeight / 2f, 1.2f);
            Vector2 probe = spawn + Vector2.up * middle;
            float left = OpenDistance(probe, Vector2.left, 10f, player);
            float right = OpenDistance(probe, Vector2.right, 10f, player);
            return new Vector2(probe.x + (right - left) / 2f, spawn.y + Mathf.Max(top, 1.2f + blockHeight / 2f));
        }

        private static readonly RaycastHit2D[] tutorialHits = new RaycastHit2D[8];

        private static float OpenDistance(Vector2 from, Vector2 direction, float max, Rigidbody2D player)
        {
            var filter = new ContactFilter2D { useTriggers = false };
            int count = Physics2D.Raycast(from, direction, filter, tutorialHits, max);
            float best = max;
            for (int i = 0; i < count; i++)
                if (tutorialHits[i].rigidbody != player && tutorialHits[i].distance < best)
                    best = tutorialHits[i].distance;
            return best;
        }

        private static void WriteScaled(string text, float x, float y, Color color, float scale)
        {
            GUI.color = color;
            Texture2D font = ForestArt.Glyphs;
            foreach (char character in text)
            {
                int code = char.ToUpperInvariant(character) - 32;
                if (code > 0 && code <= 63 && x >= 0f && x + 5f * scale <= PixelViewport.Width)
                    GUI.DrawTextureWithTexCoords(new Rect(x, y, 5f * scale, 7f * scale), font,
                        ForestArt.TexCoords(font, (code % 16) * 6, (code / 16) * 8, 5, 7));
                x += 6f * scale;
            }
        }

        private const float PickupSeconds = 1.6f;

        private static void DrawPickupName(Camera camera, GameSession session)
        {
            if (session.Content?.PickupName is not string name)
                return;
            float t = (Time.time - session.Content.PickupTime) / PickupSeconds;
            if (t < 0f || t >= 1f)
                return;
            float alpha = t < 0.5f ? 1f : 1f - (t - 0.5f) * 2f;
            Vector2 point = session.Motor.Body.position + Vector2.up * (1.6f + t * 1.2f);
            Color color = Gold;
            color.a = alpha;
            DrawWorldPrompt(camera, point, name, color, alpha);
        }

        private const float BannerSeconds = 2.4f;

        // Announces a level modifier: rises from screen center and fades out.
        // ---------- boss bar ----------
        private const int BarX = 16, BarY = 166, BarWidth = 288, BarHeight = 6;
        private static LevelEnemy barBoss;
        private static int barHealth;
        private static float barChip;
        private static float barHitTime = -1f;
        private static float barChipAfter;
        private static float barGoneTime = -1f;

        // The full-width bar along the bottom, the boss's name over it, and its name card while it makes its
        // entrance. Lost health shows pale and drains after a moment; ticks mark the phase changes.
        private static void DrawBossBar(GameSession session)
        {
            LevelEnemy boss = session.Content?.MainBoss;
            if (boss == null)
            {
                barBoss = null;
                return;
            }

            HealthPool health = boss.Damage.Health;
            float now = Time.unscaledTime;
            if (boss != barBoss)
            {
                barBoss = boss;
                barHealth = health.Current;
                barChip = 1f;
                barGoneTime = -1f;
            }
            float fill = health.Current / (float)health.Maximum;
            if (health.Current < barHealth)
            {
                barHitTime = now;
                barChipAfter = now + 0.3f;
            }
            barHealth = health.Current;
            if (now > barChipAfter)
                barChip = Mathf.MoveTowards(barChip, fill, Time.unscaledDeltaTime * 0.9f);
            barChip = Mathf.Max(barChip, fill);

            Boss brain = boss.Brain;
            float intro = brain?.IntroProgress ?? Mathf.Clamp01((Time.time - session.LevelStartTime) / 2f);
            DrawNameCard(boss, intro);
            if (boss.DeathFinished && barGoneTime < 0f)
                barGoneTime = now;
            float leave = barGoneTime < 0f ? 0f : Mathf.Clamp01((now - barGoneTime - 1f) / 0.4f);
            if (leave >= 1f)
                return;
            float arrive = Mathf.Clamp01(intro * 3f);
            int y = BarY + Mathf.RoundToInt((1f - arrive + leave) * 18f);

            string name = boss.Name.ToUpperInvariant();
            Center(name, 161, y - 9, Ink);
            Center(name, 160, y - 10, Light);
            Frame(new Rect(BarX - 1, y - 1, BarWidth + 2, BarHeight + 2), Ink, BarBack);
            Fill(new Rect(BarX, y, Mathf.Round(BarWidth * barChip), BarHeight), BarChip);
            float width = Mathf.Round(BarWidth * fill);
            bool flash = now - barHitTime < 0.07f;
            Fill(new Rect(BarX, y, width, BarHeight), flash ? Color.white : BarRed);
            if (!flash && width > 0f)
                Fill(new Rect(BarX, y, width, 1), BarShine);
            foreach (float mark in PhaseMarks)
            {
                int x = BarX + Mathf.RoundToInt(BarWidth * mark);
                Fill(new Rect(x, y - 2, 1, BarHeight + 4), fill <= mark ? Edge : Light);
            }
        }

        private static readonly float[] PhaseMarks = { 2f / 3f, 1f / 3f };

        // The boss's name, large, while it makes its entrance. Just the name: no tag line.
        private static void DrawNameCard(LevelEnemy boss, float intro)
        {
            if (intro >= 1f)
                return;
            float alpha = intro < 0.2f ? intro / 0.2f : intro < 0.75f ? 1f : 1f - (intro - 0.75f) / 0.25f;
            string name = boss.Name.ToUpperInvariant();
            Color ink = Ink;
            ink.a = alpha;
            Color color = Light;
            color.a = alpha;
            int y = 52 - Mathf.RoundToInt((1f - Mathf.Clamp01(intro * 5f)) * 6f);
            Center(name, 161, y + 2, ink, 2);
            Center(name, 160, y, color, 2);
            Color gold = Gold;
            gold.a = alpha;
            int lineWidth = TextWidth(name, 2);
            Fill(new Rect(160 - lineWidth / 2, y + 18, lineWidth, 1), gold);
        }

        // Dev overlay: hurtboxes green (armor grey, weak points yellow), the contact outline pink while
        // touching hurts, and live hitboxes pink boxes.
        private static void DrawHitboxes(Camera camera, Boss brain)
        {
            if (brain == null)
                return;
            Vector2 ToScreen(Vector2 world)
            {
                Vector3 view = camera.WorldToViewportPoint(world);
                return new Vector2(view.x * PixelViewport.Width, (1f - view.y) * PixelViewport.Height);
            }
            float pixelsPerUnit = PixelViewport.Width / (2f * PixelViewport.WorldHalfWidth);
            void Circle(Hurtbox part, Color color)
            {
                Vector2 center = ToScreen(part.Center);
                float radius = part.Radius * pixelsPerUnit;
                for (int i = 0; i < 24; i++)
                {
                    float angle = i * Mathf.PI / 12f;
                    Fill(new Rect(Mathf.Round(center.x + Mathf.Cos(angle) * radius), Mathf.Round(center.y + Mathf.Sin(angle) * radius), 1, 1), color);
                }
            }
            foreach (Hurtbox part in brain.Parts)
                Circle(part, part.Armored ? Muted : part.Multiplier > 1 ? Gold : Mint);
            if (brain.ContactHurts)
                foreach (Hurtbox part in brain.Body)
                    Circle(part, Pink);
            foreach (BossHitbox hitbox in brain.Hitboxes)
            {
                Vector2 a = ToScreen(new Vector2(hitbox.Area.xMin, hitbox.Area.yMax));
                Vector2 b = ToScreen(new Vector2(hitbox.Area.xMax, hitbox.Area.yMin));
                Fill(new Rect(a.x, a.y, b.x - a.x, 1), Pink);
                Fill(new Rect(a.x, b.y, b.x - a.x, 1), Pink);
                Fill(new Rect(a.x, a.y, 1, b.y - a.y), Pink);
                Fill(new Rect(b.x, a.y, 1, b.y - a.y), Pink);
            }
            if (brain.ContactHurts)
                Write("CONTACT", 4, 4, Pink);
        }

        private static void DrawVariantBanner(GameSession session)
        {
            string banner = session.Variant switch
            {
                LevelVariant.Night => "NIGHTFALL",
                LevelVariant.HeavySpawn => "THE SWARM",
                LevelVariant.DoubleBoss => "TWIN TERROR",
                _ => null
            };
            if (banner == null)
                return;
            float t = (Time.time - session.LevelStartTime) / BannerSeconds;
            if (t < 0f || t >= 1f)
                return;
            float alpha = t < 0.15f ? t / 0.15f : t < 0.55f ? 1f : 1f - (t - 0.55f) / 0.45f;
            int y = Mathf.RoundToInt(PixelViewport.Height / 2f - 10 - t * 18f);
            Color ink = Ink;
            ink.a = alpha;
            Color color = session.Variant == LevelVariant.Night ? Mint : session.Variant == LevelVariant.DoubleBoss ? Pink : Gold;
            color.a = alpha;
            Center(banner, 160, y + 2, ink, 2);
            Center(banner, 160, y, color, 2);
        }

        private static void DevButton(Rect rect, string text, Color color)
        {
            if (text.Length == 0)
                return;
            bool hover = rect.Contains(PixelViewport.Pointer);
            Frame(rect, hover ? Light : color, hover ? new Color32(94, 60, 62, 255) : new Color32(64, 41, 55, 255));
            Center(text, (int)rect.center.x, (int)rect.y + 2, Light);
        }

        private static void DrawDevPanel(GameSession session)
        {
            DevButton(DevMode.Toggle, "DEV", Mint);
            if (!DevMode.PanelOpen)
                return;
            Rect panel = DevMode.Panel;
            Frame(panel, Mint, new Color32(18, 13, 29, 220));
            for (int row = 0; row < DevMode.RowLabels.Length; row++)
            {
                int y = DevMode.PanelY + row * DevMode.RowHeight;
                Write(DevMode.RowLabels[row], DevMode.PanelX + 4, y + 2, Mint);
                DevButton(DevMode.Prev(row), "<", Edge);
                DevButton(DevMode.Next(row), ">", Edge);
                DevButton(DevMode.Go(row), "GO", Gold);
                string value = DevMode.RowValue(session, row);
                if (value.Length > 17)
                    value = value[..17];
                Center(value, DevMode.PanelX + 98, y + 2, Light);
            }
            string[] actions = DevMode.Actions;
            for (int i = 0; i < actions.Length; i++)
                DevButton(DevMode.Action(i), actions[i], i == 0 && DevMode.Invincible ? Mint : Gold);
        }

        private static void DrawWorldPrompt(Camera camera, Vector2 point, string prompt, Color color, float alpha = 1f)
        {
            Vector3 anchor = camera.WorldToViewportPoint(point);
            int halfWidth = (TextWidth(prompt) + 1) / 2;
            int x = Mathf.Clamp(Mathf.RoundToInt(anchor.x * PixelViewport.Width), halfWidth + 2,
                PixelViewport.Width - halfWidth - 2);
            int y = Mathf.RoundToInt((1f - anchor.y) * PixelViewport.Height) - 7;
            Color ink = Ink;
            ink.a *= alpha;
            Center(prompt, x - 1, y, ink);
            Center(prompt, x + 1, y, ink);
            Center(prompt, x, y - 1, ink);
            Center(prompt, x, y + 1, ink);
            Center(prompt, x, y, color);
        }

        private static void DrawPause(GameSession session)
        {
            RunState run = session.Run;
            Frame(new Rect(14, 31, 292, 148), Edge, Panel);
            Center("PAUSED", 160, 34, Light, 2);

            Write("CONTROLS", 24, 50, Gold);
            Write("A/D  MOVE", 24, 58, Light);
            Write("SPACE  JUMP (HOLD HIGHER)", 24, 66, Light);
            Write("J HIT / W+J UP / AIR S+J DOWN", 24, 74, Light);
            Write("E  INTERACT  /  L  ITEM", 24, 82, Light);
            Write(run.Character.Insect switch
            {
                Insect.Spider => "K  HIGH JUMP",
                Insect.Beetle => "K  TOGGLE SHELL",
                Insect.Dragonfly => "K  FLY  /  WASD  STEER",
                Insect.FireAnt => "K  HEAVY HIT",
                _ => "K  DIG AHEAD  /  S+K  DIG DOWN"
            }, 24, 90, Mint);

            if (run.Character.Insect == Insect.Spider)
            {
                Write("WALK INTO WALL TO CLIMB", 24, 98, Muted);
                Write("RELEASE MOVE TO DROP", 176, 98, Muted);
            }

            Write(run.Character.Name, 198, 50, Gold);
            Write($"SEED {run.Seed}", 198, 58, Light);
            string variant = session.Variant switch
            {
                LevelVariant.Night => "NIGHT",
                LevelVariant.HeavySpawn => "HEAVY SPAWN",
                LevelVariant.DoubleBoss => $"BOSSES {session.Content.BossesRemaining}/2",
                _ => null
            };
            if (variant != null)
                Write(variant, 198, 66, Mint);
            DrawPauseInventory(run.Inventory);

            if (session.InteractionPrompt is string prompt)
                Center(prompt, 160, 142, Gold);
            if (session.Status is string status)
                Center(status, 160, 150, Muted);
            Button(ResumeButton, "ESC  RESUME");
            Button(QuitToRosterButton, "Q  CHARACTERS");
        }

        private static void DrawPauseInventory(RunInventory inventory)
        {
            const int iconSize = 22;
            const int iconY = 115;
            const int passiveX = 76;
            Write("ACTIVE", 24, 106, Gold);
            Write("PASSIVES", passiveX, 106, Gold);
            var activeSlot = new Rect(24, iconY, iconSize, iconSize);
            Frame(activeSlot, Edge, Ink);
            if (Items.IsActive(inventory.Active))
                DrawItemIcon(inventory.Active, activeSlot);
            else
                Center("-", (int)activeSlot.center.x, iconY + 8, Muted);

            int x = passiveX;
            foreach (var passive in inventory.Passives)
            {
                if (passive.Value <= 0)
                    continue;
                DrawItemIcon(passive.Key, new Rect(x, iconY, iconSize, iconSize));
                if (passive.Value > 1)
                {
                    string count = passive.Value.ToString();
                    int right = x + iconSize;
                    int width = TextWidth(count) + 2;
                    Fill(new Rect(right - width, 132, width, 9), Ink);
                    Right(count, right - 1, 133, Light);
                }
                x += iconSize;
            }
            if (x == passiveX)
                Center("-", passiveX + iconSize / 2, iconY + 8, Muted);
        }

        private static void DrawItemIcon(ItemId id, Rect target) => DrawSprite(ForestArt.Item(id), target);

        private static void DrawSprite(Sprite sprite, Rect target)
        {
            Texture2D atlas = sprite.texture;
            Rect source = sprite.rect;
            GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(target, atlas,
                new Rect(source.x / atlas.width, source.y / atlas.height, source.width / atlas.width, source.height / atlas.height));
        }

        private static void Shade() =>
            Fill(new Rect(0, 0, PixelViewport.Width, PixelViewport.Height), new Color32(15, 9, 27, 83));

        private static void Button(Rect rect, string text, bool enabled = true)
        {
            if (!enabled)
            {
                Frame(rect, Edge, Panel);
                Center(text, (int)rect.center.x, (int)rect.y + 6, Muted);
                return;
            }

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
