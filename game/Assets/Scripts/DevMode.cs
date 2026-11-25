using System;
using System.Collections.Generic;
using UnityEngine;

namespace Insectum.Game
{
    // Developer cheats, enabled from the character select screen. State lives here so the
    // session only needs a couple of hooks (invincibility, jump, spawn, give).
    internal static class DevMode
    {
        internal static bool Enabled;
        internal static bool Invincible;
        internal static bool PanelOpen = true;
        internal static int Level = 1;
        internal static int EnemyIndex;
        internal static int ItemIndex;
        internal static int BossIndex;
        internal static bool ShowHitboxes;

        internal static readonly ItemId[] ItemList = BuildItems();

        private const string PlayHereKey = "Insectum.PlayHere";

        // Asks the next Play mode to start at this seed and level (the Chunk Editor's Play here).
        internal static void RequestPlayHere(int seed, int level) => PlayerPrefs.SetString(PlayHereKey, $"{seed},{level}");

        internal static bool TakePlayHere(out int seed, out int level)
        {
            seed = level = 0;
            string value = PlayerPrefs.GetString(PlayHereKey, "");
            if (value.Length == 0)
                return false;
            PlayerPrefs.DeleteKey(PlayHereKey);
            string[] parts = value.Split(',');
            return parts.Length == 2 && int.TryParse(parts[0], out seed) && int.TryParse(parts[1], out level);
        }

        private static ItemId[] BuildItems()
        {
            var items = new List<ItemId>();
            foreach (ItemId item in Enum.GetValues(typeof(ItemId)))
                if (Items.IsKnown(item))
                    items.Add(item);
            return items.ToArray();
        }

        // Spawnable enemies: every world's common reinforcement plus whatever already lives in this level
        // (which includes the boss on boss levels).
        internal static List<EnemySpec> Enemies(GameSession session)
        {
            var specs = new List<EnemySpec>
            {
                ForestContent.BarkMite, SwampContent.ReedRunner, CavesContent.Hopper,
                DesertContent.DuneBeetle, AlienContent.LatticeTick
            };
            if (session.Content != null)
                foreach (LevelEnemy enemy in session.Content.Enemies)
                    if (!specs.Contains(enemy.Spec) && enemy.Brain == null)
                        specs.Add(enemy.Spec);
            return specs;
        }

        internal static string LevelName(int level) =>
            $"{RunWorlds.AtLevel(level)} {(Math.Max(1, level) - 1) / RunWorlds.LevelsPerWorld + 1}-{RunWorlds.LocalLevel(level)}".ToUpperInvariant();

        // ---------- layout (pixel viewport coordinates, 320x180) ----------
        internal const int PanelX = 2, PanelY = 80, RowHeight = 12;
        internal static Rect Toggle => new(PanelX, 168, 26, 10);
        internal static Rect Panel => new(PanelX, PanelY - 3, 186, RowHeight * 6 + 4);
        internal static Rect Prev(int row) => new(PanelX + 36, PanelY + row * RowHeight, 10, 10);
        internal static Rect Next(int row) => new(PanelX + 150, PanelY + row * RowHeight, 10, 10);
        internal static Rect Go(int row) => new(PanelX + 163, PanelY + row * RowHeight, 20, 10);
        // Two rows of three, wide enough for 8-character labels (47px).
        internal static Rect Action(int index) => new(PanelX + 3 + index % 3 * 61, PanelY + (4 + index / 3) * RowHeight, 58, 10);
        internal static readonly string[] RowLabels = { "LEVEL", "ENEMY", "ITEM", "BOSS" };

        // The reworked boss's moves to force, then phase jumps, freeze, the hitbox overlay and a kill.
        internal static List<string> BossCommands(GameSession session)
        {
            var commands = new List<string>();
            Boss brain = session.Content?.MainBoss?.Brain;
            if (brain == null)
                return commands;
            commands.AddRange(brain.MoveNames);
            commands.Add("PHASE 2");
            commands.Add("PHASE 3");
            commands.Add(brain.Frozen ? "THAW AI" : "FREEZE AI");
            commands.Add(ShowHitboxes ? "HIDE BOXES" : "SHOW BOXES");
            commands.Add("KILL");
            return commands;
        }

        private static void RunBossCommand(GameSession session, int index)
        {
            Boss brain = session.Content?.MainBoss?.Brain;
            List<string> commands = BossCommands(session);
            if (brain == null || commands.Count == 0)
            {
                session.Content?.ShowStatus("No reworked boss here");
                return;
            }
            index = Wrap(index, commands.Count);
            int moves = brain.MoveNames.Count;
            if (index < moves)
                brain.ForceMove(index);
            else if (index == moves)
                brain.DevSetPhase(2);
            else if (index == moves + 1)
                brain.DevSetPhase(3);
            else if (index == moves + 2)
                brain.Frozen = !brain.Frozen;
            else if (index == moves + 3)
                ShowHitboxes = !ShowHitboxes;
            else if (brain.Health.Alive)
                brain.Damage.Receive(new DamageHit(DamageSource.Melee, brain.Health.Current));
            session.Content?.ShowStatus($"Boss: {commands[index]}");
        }

        internal static string[] Actions => new[] { Invincible ? "GOD ON" : "GOD OFF", "HEAL", "+50 NEK", "ALL ITEM", "NEXT LVL", "BOSS LVL" };

        internal static string RowValue(GameSession session, int row) => row switch
        {
            0 => LevelName(Level),
            1 => Enemies(session) is { Count: > 0 } list ? list[Wrap(EnemyIndex, list.Count)].Name.ToUpperInvariant() : "NONE",
            2 => Items.Name(ItemList[Wrap(ItemIndex, ItemList.Length)]).ToUpperInvariant(),
            _ => BossCommands(session) is { Count: > 0 } commands ? commands[Wrap(BossIndex, commands.Count)] : "NO BOSS"
        };

        private static int Wrap(int value, int count) => (value % count + count) % count;

        // Returns true when the click landed on the dev UI so gameplay ignores it.
        internal static bool HandleClick(GameSession session, Vector2 pointer)
        {
            if (Toggle.Contains(pointer))
            {
                PanelOpen = !PanelOpen;
                return true;
            }
            if (!PanelOpen || !Panel.Contains(pointer))
                return false;

            for (int row = 0; row < RowLabels.Length; row++)
            {
                int step = Prev(row).Contains(pointer) ? -1 : Next(row).Contains(pointer) ? 1 : 0;
                if (step != 0)
                {
                    if (row == 0) Level = Wrap(Level - 1 + step, RunWorlds.LevelCount) + 1;
                    else if (row == 1) EnemyIndex += step;
                    else if (row == 2) ItemIndex += step;
                    else BossIndex += step;
                    return true;
                }
                if (!Go(row).Contains(pointer))
                    continue;
                if (row == 0)
                    session.DevJump(Level);
                else if (row == 1)
                {
                    List<EnemySpec> list = Enemies(session);
                    session.DevSpawn(list[Wrap(EnemyIndex, list.Count)]);
                }
                else if (row == 2)
                    session.DevGive(ItemList[Wrap(ItemIndex, ItemList.Length)]);
                else
                    RunBossCommand(session, BossIndex);
                return true;
            }

            if (Action(0).Contains(pointer)) Invincible = !Invincible;
            else if (Action(1).Contains(pointer)) session.DevHeal();
            else if (Action(2).Contains(pointer)) session.Run.Inventory.AddNektar(50);
            else if (Action(3).Contains(pointer))
                foreach (ItemId item in ItemList)
                    session.DevGive(item);
            else if (Action(4).Contains(pointer))
                session.DevJump(Math.Min(session.Run.LevelNumber + 1, RunWorlds.LevelCount));
            else if (Action(5).Contains(pointer))
            {
                int world = (session.Run.LevelNumber - 1) / RunWorlds.LevelsPerWorld;
                session.DevJump(world * RunWorlds.LevelsPerWorld + RunWorlds.LevelsPerWorld);
            }
            return true;
        }
    }
}
