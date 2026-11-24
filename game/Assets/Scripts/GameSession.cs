using System.Collections.Generic;
using UnityEngine;

namespace Insectum.Game
{
    public enum RunPhase
    {
        Selection,
        Playing,
        Cleared,
        Defeated
    }

    public sealed class GameSession
    {
        private readonly Transform world;
        private readonly Sprite pixel;
        private readonly PhysicsMaterial2D surface;
        private readonly bool enableAbilities;
        private readonly bool enableVariants;
        private readonly CharacterTuning tuning;
        private GameObject level;
        private SpriteRenderer exitArt;
        private int attackCooldown;
        private bool fullRun;
        private int lastLevel;
        private int nextCheckpoint;
        private Vector2 respawnPosition;

        internal int MeleePoseSteps { get; private set; }
        internal Vector2 MeleeAim { get; private set; }
        internal bool HeavyPose { get; private set; }

        public RunPhase Phase { get; private set; } = RunPhase.Selection;
        public RunState Run { get; private set; }
        public PlayerMotor Motor { get; private set; }
        public HexTerrain Terrain { get; private set; }
        public CombatArena Combat { get; private set; }
        public DamageReceiver PlayerDamage { get; private set; }
        public PlayerAbilities Abilities { get; private set; }
        internal LevelContent Content { get; private set; }
        public string Status => Content?.Status;
        public string InteractionPrompt => Content?.ClosestInteractable()?.InteractPrompt ?? (AtExit
            ? Content?.ExitUnlocked == false ? Content.BossGatePrompt : $"E  Enter {Run.World} exit"
            : null);
        public LevelLayout Layout { get; private set; }
        public LevelVariant Variant { get; private set; }
        internal float LevelStartTime { get; private set; }
        internal float NightVisionRadius => Variant == LevelVariant.Night ? Run.Inventory.NightVisionRadius : 0f;
        public Rect LevelBounds => Layout?.Bounds ?? ForestRoute.Bounds;
        private Vector2 Exit => Layout?.Exit ?? ForestRoute.Exit;
        public bool AtExit => Motor != null && Vector2.Distance(Motor.Body.position, Exit) < 1.15f;

        public GameSession(Transform world, Sprite pixel, PhysicsMaterial2D surface, bool enableAbilities = true,
            bool enableVariants = true)
        {
            this.world = world;
            this.pixel = pixel;
            this.surface = surface;
            this.enableAbilities = enableAbilities;
            this.enableVariants = enableVariants;
            tuning = CharacterTuning.Current;
        }

        public void StartGenerated(Character character, int seed) =>
            StartRun(character, seed, RunWorlds.LevelCount, 1);

        internal void StartTunnelTrial(Character character, int seed)
        {
            BeginRun(character, seed, true, 1);
            lastLevel = RunWorlds.LevelCount;
            LoadLevel(ForestTunnelLayout.Generate(seed));
        }

        internal void StartForest(Character character, int seed) =>
            StartRun(character, seed, RunWorlds.LevelsPerWorld, 1);

        internal void StartGenerated(Character character, int seed, int firstLevel) =>
            StartRun(character, seed, RunWorlds.LevelCount, firstLevel);

        private void StartRun(Character character, int seed, int lastLevel, int firstLevel)
        {
            BeginRun(character, seed, true, firstLevel);
            this.lastLevel = lastLevel;
            LoadLevel(RunWorlds.Generate(seed, Run.LevelNumber));
        }

        public void Start(Character character, int seed) => Start(character, seed, null);

        internal void Start(Character character, int seed, LevelLayout layout)
        {
            BeginRun(character, seed, false, layout?.LevelNumber ?? 1);
            LoadLevel(layout);
        }

        private void BeginRun(Character character, int seed, bool playFullRun, int firstLevel)
        {
            ReturnToSelection();
            Run = new RunState(character, seed);
            for (int levelNumber = 0; levelNumber < firstLevel; levelNumber++)
                Run.EnterNextLevel();
            fullRun = playFullRun;
        }

        private void LoadLevel(LevelLayout layout)
        {
            ReleaseLevel();
            Layout = layout;
            LevelStartTime = Time.time;
            Variant = fullRun && enableVariants && lastLevel == RunWorlds.LevelCount
                ? layout.Variant : LevelVariant.Standard;
            level = new GameObject($"{layout?.World ?? WorldId.Forest} {layout?.WorldLevel ?? 1}");
            level.transform.SetParent(world, false);
            if (layout == null)
            {
                Terrain = ForestRoute.Build(level.transform, pixel);
            }
            else
            {
                Terrain = new HexTerrain(level.transform, layout.Tiles, TerrainMaterials.Load(), layout.Tunnel);
                if (pixel != null)
                {
                    HexTerrainView.Attach(Terrain, layout.World);
                    exitArt = ForestRoute.MarkExit(level.transform, pixel, layout.Exit);
                }
            }
            Combat = new CombatArena();
            attackCooldown = 0;
            MeleePoseSteps = 0;

            Character character = Run.Character;
            var actor = new GameObject(character.Name);
            actor.transform.SetParent(level.transform, false);
            respawnPosition = layout?.Spawn ?? ForestRoute.Spawn;
            actor.transform.localPosition = respawnPosition + Vector2.up * (character.Radius + 0.06f);
            nextCheckpoint = 0;
            Motor = new PlayerMotor(actor, character.Radius, surface, layout?.RouteDirection ?? 1,
                followSlopes: layout?.Tunnel != null);
            Motor.Landed += ApplyFallDamage;
            Abilities = new PlayerAbilities(Run, Motor, Terrain, Combat);
            PlayerMotor player = Motor;
            PlayerDamage = new DamageReceiver(Run.Vitality, character.Radius, () => player.Body.position,
                tuning.playerInvulnerabilitySteps, player.ApplyImpulse, AbsorbDamage);
            if (fullRun)
            {
                Content = new LevelContent(this, level.transform, pixel);
                RunWorlds.Populate(Content, layout);
                RunWorlds.ApplyVariant(Content, layout, Terrain, Variant);
                if (exitArt != null && !Content.ExitUnlocked)
                    exitArt.sprite = ForestArt.Prop(ForestProp.LockedExit);
            }
            else if (layout == null && pixel != null)
                CombatPractice.Attach(level.transform, pixel, Combat);
            if (pixel != null)
                InsectActorView.Attach(actor.transform, this);
            Phase = RunPhase.Playing;
        }

        public void Step(PlayerCommand command)
        {
            if (Phase != RunPhase.Playing)
                return;

            if (Run.Alive)
            {
                Combat.Step();
                PlayerDamage.Step();
                MotorControl control = enableAbilities ? Abilities.Step(command) : default;
                StepMelee(command);
                Content?.Step(command);
                if (Run.Alive)
                    Motor.Step(command, control);
                if (Motor.Body.position.y < (Layout?.KillHeight ?? ForestRoute.KillHeight))
                    Run.TakeDamage(Run.Health);
                Content?.FinishStep();
            }

            if (!Run.Alive)
            {
                if (!Run.Inventory.TryRevive())
                    End(RunPhase.Defeated);
                else
                {
                    Abilities.ResetModes();
                    Motor.Warp(respawnPosition + Vector2.up * (Run.Character.Radius + 0.06f));
                    PlayerDamage.ProtectFor(90);
                    Content?.ShowStatus("Emergency Reboot used");
                }
                return;
            }

            UpdateCheckpoint();
            if (!command.InteractPressed)
                return;
            if (Content?.TryInteract() == true)
            {
                if (exitArt != null && Content.ExitUnlocked)
                    exitArt.sprite = ForestArt.Prop(ForestProp.Exit);
                return;
            }
            if (!AtExit)
                return;
            if (Content?.ExitUnlocked == false)
            {
                Content.ShowStatus(Content.BossGatePrompt);
                return;
            }

            if (fullRun && Run.LevelNumber < lastLevel)
            {
                Run.EnterNextLevel();
                LoadLevel(RunWorlds.Generate(Run.Seed, Run.LevelNumber));
            }
            else
                End(RunPhase.Cleared);
        }

        private void UpdateCheckpoint()
        {
            if (!fullRun || Layout == null || !Motor.Grounded || nextCheckpoint >= Layout.Checkpoints.Count)
                return;
            Vector2 feet = Motor.Body.position - Vector2.up * Run.Character.Radius;
            Vector2 checkpoint = Layout.Checkpoints[nextCheckpoint].Landing;
            if ((feet - checkpoint).sqrMagnitude < 0.92f * 0.92f)
            {
                respawnPosition = checkpoint;
                nextCheckpoint++;
            }
        }

        public void ReturnToSelection()
        {
            ReleaseLevel();
            Run = null;
            fullRun = false;
            Phase = RunPhase.Selection;
        }

        private void ReleaseLevel()
        {
            if (level != null)
            {
                level.SetActive(false);
                if (Application.isPlaying)
                    Object.Destroy(level);
                else
                    Object.DestroyImmediate(level);
            }

            level = null;
            exitArt = null;
            Layout = null;
            Variant = LevelVariant.Standard;
            Motor = null;
            Terrain = null;
            Combat = null;
            PlayerDamage = null;
            Abilities = null;
            Content = null;
        }

        private const float PogoSpeed = 10.5f;

        private void StepMelee(PlayerCommand command)
        {
            if (MeleePoseSteps > 0)
                MeleePoseSteps--;
            if (attackCooldown > 0)
                attackCooldown--;
            if (attackCooldown > 0 || Abilities.Shelled)
                return;

            bool heavy = enableAbilities && Run.Character.Insect == Insect.FireAnt && command.AbilityPressed
                && Run.TrySpendEnergy(tuning.fireAntAttackEnergy);
            if (!heavy && !command.AttackPressed)
                return;

            Vector2 direction = command.Aim > 0 ? Vector2.up
                : command.Aim < 0 && !Motor.Grounded ? Vector2.down
                : Vector2.right * Motor.FacingFor(command);
            Vector2 origin = Motor.Body.position;
            MeleePoseSteps = heavy ? 10 : 7;
            MeleeAim = direction;
            HeavyPose = heavy;
            attackCooldown = heavy ? tuning.heavyRecoverySteps : tuning.meleeRecoverySteps;
            float reachBonus = Run.Inventory.MeleeReachBonus;
            bool hit = Combat.Melee(heavy
                ? new MeleeStrike(origin, direction, 2.1f + reachBonus, 0.83f, tuning.fireAntAttackDamage, 7f)
                : new MeleeStrike(origin, direction, 1.3f + reachBonus, 0.38f, 1, 4f));
            if (hit && direction == Vector2.down)
                Motor.Bounce(PogoSpeed);
        }

        private bool AbsorbDamage(DamageHit hit)
        {
            if (DevMode.Enabled && DevMode.Invincible)
                return true;
            if (hit.Source != DamageSource.Trap || !Run.Inventory.TryBlockTrap())
                return Abilities.AbsorbDamage(hit);

            Content?.ShowStatus("Dune Mantle blocked a trap");
            return true;
        }

        // ---------- dev mode hooks ----------
        internal void DevJump(int levelNumber)
        {
            if (Run == null)
                return;
            RunInventory old = Run.Inventory;
            var passives = new List<KeyValuePair<ItemId, int>>(old.Passives);
            ItemId active = old.Active;
            int nektar = old.Nektar;
            StartGenerated(Run.Character, Run.Seed, Mathf.Clamp(levelNumber, 1, RunWorlds.LevelCount));
            foreach (var passive in passives)
                for (int i = 0; i < passive.Value; i++)
                    Run.Inventory.Acquire(passive.Key);
            if (active != ItemId.None)
                Run.Inventory.Acquire(active);
            Run.Inventory.AddNektar(nektar);
            Run.RestoreHealth(Run.MaxHealth);
        }

        internal void DevSpawn(EnemySpec spec)
        {
            if (Content == null || Motor == null)
                return;
            Content.AddEnemy(spec, Motor.Body.position + new Vector2(2.5f, 1f), 0);
            Content.ShowStatus($"Spawned {spec.Name}");
        }

        internal void DevGive(ItemId item)
        {
            if (Run?.Inventory.Acquire(item) == true)
                Content?.ShowStatus($"Gave {Items.Name(item)}");
        }

        internal void DevHeal()
        {
            Run.RestoreHealth(Run.MaxHealth);
            Run.RestoreEnergy(Run.Character.MaxEnergy);
        }

        private void ApplyFallDamage(float impactSpeed)
        {
            if (Motor.LastFallDistance >= PlayerMotor.FallDamageDistance && impactSpeed >= PlayerMotor.FallDamageSpeed)
                PlayerDamage.Receive(new DamageHit(DamageSource.Fall, 1));
        }

        private void End(RunPhase phase)
        {
            Motor.Body.linearVelocity = Vector2.zero;
            Motor.Body.simulated = false;
            Phase = phase;
        }
    }
}
