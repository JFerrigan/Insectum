using UnityEngine;

namespace Insectum.Game
{
    public sealed class RunState
    {
        public Character Character { get; }
        public int Seed { get; }
        public int Health => Vitality.Current;
        public int MaxHealth => Vitality.Maximum;
        public RunInventory Inventory { get; }
        internal HealthPool Vitality { get; }
        public float Energy { get; private set; }
        public int LevelNumber { get; private set; }
        public WorldId World => RunWorlds.AtLevel(LevelNumber);
        public int WorldLevel => RunWorlds.LocalLevel(LevelNumber);
        public bool Alive => Health > 0;

        public RunState(Character character, int seed)
        {
            Character = character;
            Seed = seed;
            Vitality = new HealthPool(character.MaxHealth);
            Inventory = new RunInventory(this);
        }

        public void EnterNextLevel()
        {
            LevelNumber++;
            Energy = Character.MaxEnergy;
            Inventory.EnterLevel();
        }

        public bool TrySpendEnergy(float cost)
        {
            if (cost < 0f || !float.IsFinite(cost) || cost > Energy)
                return false;
            Energy -= cost;
            return true;
        }

        public void RestoreEnergy(float amount)
        {
            if (float.IsFinite(amount) && amount > 0f)
                Energy = Mathf.Min(Character.MaxEnergy, Energy + amount);
        }

        public void RestoreHealth(int hearts) => Vitality.Restore(hearts);

        public void TakeDamage(int hearts)
        {
            Vitality.TakeDamage(hearts);
        }
    }
}
