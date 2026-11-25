using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Insectum.Game
{
    public enum ItemId
    {
        None,
        ExcavatorClaw,
        SeedBomb,
        MothEyes,
        BurrowSense,
        Heartwood,
        FlameGlands,
        VolatileBlood,
        EmergencyReboot,
        ReedFlask,
        PrismFang,
        DuneMantle,
        FluxCore
    }

    public static class Items
    {
        public static string Name(ItemId item) => item switch
        {
            ItemId.ExcavatorClaw => "Excavator Claw",
            ItemId.SeedBomb => "Seed Bomb",
            ItemId.MothEyes => "Moth Eyes",
            ItemId.BurrowSense => "Burrow Sense",
            ItemId.Heartwood => "Heartwood",
            ItemId.FlameGlands => "Flame Glands",
            ItemId.VolatileBlood => "Volatile Blood",
            ItemId.EmergencyReboot => "Emergency Reboot",
            ItemId.ReedFlask => "Reed Flask",
            ItemId.PrismFang => "Prism Fang",
            ItemId.DuneMantle => "Dune Mantle",
            ItemId.FluxCore => "Flux Core",
            _ => "Empty"
        };

        public static bool IsActive(ItemId item) => item is ItemId.ExcavatorClaw or ItemId.SeedBomb;
        internal static bool IsKnown(ItemId item) => item >= ItemId.ExcavatorClaw && item <= ItemId.FluxCore;

        public static string Description(ItemId item) => item switch
        {
            ItemId.ExcavatorClaw => "Dig through terrain",
            ItemId.SeedBomb => "Blast nearby enemies and terrain",
            ItemId.MothEyes => "Wider night vision",
            ItemId.BurrowSense => "Reveal nearby buried Nektar",
            ItemId.Heartwood => "+1 max heart",
            ItemId.FlameGlands => "Melee hits set enemies on fire",
            ItemId.VolatileBlood => "Defeated enemies explode",
            ItemId.EmergencyReboot => "Revive once at half health",
            ItemId.ReedFlask => "Energy sap restores more energy",
            ItemId.PrismFang => "Longer melee reach",
            ItemId.DuneMantle => "Block one trap hit per level",
            ItemId.FluxCore => "Defeating enemies restores energy",
            _ => string.Empty
        };
    }

    public sealed class RunInventory
    {
        private readonly Dictionary<ItemId, int> passives = new();
        private readonly RunState run;
        private int blockedTraps;

        public IReadOnlyDictionary<ItemId, int> Passives { get; }
        public ItemId Active { get; private set; }
        public int ActiveCooldownSteps { get; private set; }
        private int activeCooldownTotal;
        // 0 right after use, 1 when the active item is ready.
        public float ActiveCharge => ActiveCooldownSteps <= 0 || activeCooldownTotal <= 0 ? 1f
            : 1f - ActiveCooldownSteps / (float)activeCooldownTotal;
        public int Nektar { get; private set; }
        public bool ReviveUsed { get; private set; }
        internal float MeleeReachBonus => Count(ItemId.PrismFang) * 0.35f;
        internal float EnergySapBonus => Count(ItemId.ReedFlask) * 10f;
        public float NightVisionRadius => 4.05f + Count(ItemId.MothEyes) * 1.5f;

        internal RunInventory(RunState run)
        {
            this.run = run;
            Passives = new ReadOnlyDictionary<ItemId, int>(passives);
        }

        public int Count(ItemId item) => passives.TryGetValue(item, out int stacks) ? stacks : 0;

        public void AddNektar(int amount)
        {
            if (amount > 0)
                Nektar = (int)Math.Min(int.MaxValue, (long)Nektar + amount);
        }

        public bool TryBuy(ItemId item, int price)
        {
            if (price < 0 || price > Nektar || (Items.IsActive(item) && Active == item) || !Acquire(item))
                return false;

            Nektar -= price;
            return true;
        }

        public bool Acquire(ItemId item)
        {
            if (!Items.IsKnown(item))
                return false;

            if (Items.IsActive(item))
            {
                if (Active != item)
                {
                    Active = item;
                    ActiveCooldownSteps = 0;
                }
                return true;
            }

            passives[item] = Count(item) + 1;
            if (item == ItemId.Heartwood)
                run.Vitality.IncreaseMaximum();
            return true;
        }

        internal bool TryRevive()
        {
            if (run.Alive || ReviveUsed || Count(ItemId.EmergencyReboot) == 0)
                return false;

            ReviveUsed = true;
            run.Vitality.Restore(Math.Max(1, run.MaxHealth / 2));
            return true;
        }

        internal void EnterLevel() => blockedTraps = 0;

        internal bool TryBlockTrap()
        {
            if (blockedTraps >= Count(ItemId.DuneMantle))
                return false;

            blockedTraps++;
            return true;
        }

        internal void Step()
        {
            if (ActiveCooldownSteps > 0)
                ActiveCooldownSteps--;
        }

        internal void BeginCooldown(int steps) => ActiveCooldownSteps = activeCooldownTotal = steps;
    }
}
