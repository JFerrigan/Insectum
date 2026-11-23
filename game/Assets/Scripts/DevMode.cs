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

        internal static readonly ItemId[] ItemList = BuildItems();

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
                    if (!specs.Contains(enemy.Spec))
                        specs.Add(enemy.Spec);
            return specs;
        }

        internal static string LevelName(int level) =>
            $"{RunWorlds.AtLevel(level)} {RunWorlds.LocalLevel(level)}".ToUpperInvariant();

        // ---------- layout (pixel viewport coordinates, 320x180) ----------
        internal const int PanelX = 2, PanelY = 104, RowHeight = 12;
        internal static Rect Toggle => new(PanelX, 168, 26, 10);
        internal static Rect Panel => new(PanelX, PanelY - 3, 186, RowHeight * 5 + 4);
        internal static Rect Prev(int row) => new(PanelX + 36, PanelY + row * RowHeight, 10, 10);
        internal static Rect Next(int row) => new(PanelX + 150, PanelY + row * RowHeight, 10, 10);
        internal static Rect Go(int row) => new(PanelX + 163, PanelY + row * RowHeight, 20, 10);
        internal static Rect Action(int index) => new(PanelX + 3 + index * 46, PanelY + 3 * RowHeight, 43, 10);
        internal static Rect Action2(int index) => new(PanelX + 3 + index * 46, PanelY + 4 * RowHeight, 43, 10);
        internal static readonly string[] RowLabels = { "LEVEL", "ENEMY", "ITEM" };

        internal static string[] Actions => new[] { Invincible ? "GOD ON" : "GOD OFF", "HEAL", "+50 NEK", "ALL ITEM" };
        internal static readonly string[] Actions2 = { "NEXT LVL", "BOSS LVL", "", "" };

        internal static string RowValue(GameSession session, int row) => row switch
        {
            0 => LevelName(Level),
            1 => Enemies(session) is { Count: > 0 } list ? list[Wrap(EnemyIndex, list.Count)].Name.ToUpperInvariant() : "NONE",
            _ => Items.Name(ItemList[Wrap(ItemIndex, ItemList.Length)]).ToUpperInvariant()
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

            for (int row = 0; row < 3; row++)
            {
                int step = Prev(row).Contains(pointer) ? -1 : Next(row).Contains(pointer) ? 1 : 0;
                if (step != 0)
                {
                    if (row == 0) Level = Wrap(Level - 1 + step, RunWorlds.LevelCount) + 1;
                    else if (row == 1) EnemyIndex += step;
                    else ItemIndex += step;
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
                else
                    session.DevGive(ItemList[Wrap(ItemIndex, ItemList.Length)]);
                return true;
            }

            if (Action(0).Contains(pointer)) Invincible = !Invincible;
            else if (Action(1).Contains(pointer)) session.DevHeal();
            else if (Action(2).Contains(pointer)) session.Run.Inventory.AddNektar(50);
            else if (Action(3).Contains(pointer))
                foreach (ItemId item in ItemList)
                    session.DevGive(item);
            else if (Action2(0).Contains(pointer))
                session.DevJump(Math.Min(session.Run.LevelNumber + 1, RunWorlds.LevelCount));
            else if (Action2(1).Contains(pointer))
            {
                int world = (session.Run.LevelNumber - 1) / RunWorlds.LevelsPerWorld;
                session.DevJump(world * RunWorlds.LevelsPerWorld + RunWorlds.LevelsPerWorld);
            }
            return true;
        }
    }
}
