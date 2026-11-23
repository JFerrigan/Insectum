using System;
using UnityEngine;

namespace Insectum.Game
{
    public enum Insect
    {
        Spider,
        Beetle,
        Dragonfly,
        FireAnt,
        Worm
    }

    [Serializable]
    public sealed class Character
    {
        [SerializeField] private Insect insect;
        [SerializeField] private string displayName;
        [SerializeField, Min(1)] private int maxHealth;
        [SerializeField, Min(0)] private float maxEnergy;
        [SerializeField, Min(0.01f)] private float radius;
        [SerializeField] private Color32 tint;

        public static Character[] Roster => CharacterTuning.Current.Roster;
        public Insect Insect => insect;
        public string Name => displayName;
        public int MaxHealth => maxHealth;
        public float MaxEnergy => maxEnergy;
        public float Radius => radius;
        public Color Tint => tint;
    }

    public sealed class CharacterTuning : ScriptableObject
    {
        [SerializeField] private Character[] roster;

        [Header("Jumping Spider")]
        [Min(0)] public float spiderLeapEnergy = 16f;
        [Min(0)] public float spiderClimbEnergyPerSecond = 3f;
        [Min(0)] public float spiderLeapSpeed = 16f;
        [Min(0)] public float spiderClimbSpeed = 3.5f;

        [Header("Beetle")]
        [Min(0)] public float beetleEnergyPerHeart = 25f;
        [Min(0)] public float beetleCrushSpeed = 8f;
        [Min(1)] public int beetleCrushDamage = 2;

        [Header("Dragonfly")]
        [Min(0)] public float dragonflyEnergyPerSecond = 14f;
        [Min(0)] public float dragonflyFlightSpeed = 8f;
        [Min(0)] public float dragonflyFlightAcceleration = 42f;

        [Header("Fire Ant")]
        [Min(0)] public float fireAntAttackEnergy = 15f;
        [Min(1)] public int fireAntAttackDamage = 2;

        [Header("Combat")]
        [Min(0)] public int playerInvulnerabilitySteps = 36;
        [Min(1)] public int meleeRecoverySteps = 16;
        [Min(1)] public int heavyRecoverySteps = 24;

        internal Character[] Roster => roster;

        private static CharacterTuning current;
        public static CharacterTuning Current
        {
            get
            {
                if (current == null)
                    current = Resources.Load<CharacterTuning>("CharacterTuning")
                        ?? throw new InvalidOperationException("The CharacterTuning asset is missing.");
                return current;
            }
        }
    }
}
