using System;
using System.Collections.Generic;
using System.Linq;
using Insectum.Game;
using Insectum.Game.Editor;
using UnityEngine;

internal static class MireLichCheck
{
    private const float Dt = 1f / 60f;
    private static readonly BossArena Arena = BossArena.Hall;
    private static readonly int[] WarningSteps = { 42, 36, 30, 30, 36, 36, 24, 42, 24 };

    internal static void Run(int seeds, Action<bool, string> require)
    {
        CheckTerrain(require);
        CheckDodges(require);
        CheckTriggersAndGrowth(require);
        CheckMinions(require);

        int wins = 0, total = 0, air = 0;
        var phaseAir = new int[3];
        var phaseTotal = new int[3];
        var damage = new Dictionary<string, int>();
        for (uint seed = 1; seed <= seeds; seed++)
        {
            LevelLayout hall = BossArenaLayout.Generate((int)seed, 8, WorldId.Swamp);
            var fight = new Fight(hall.Tiles, hall.Markers[0].Position, seed, hall.Spawn + Vector2.up * 0.43f);
            var driver = new MireLichDriver();
            MireLich queen = fight.Queen;
            fight.Body.Resolved += (_, outcome) =>
            {
                if (outcome != DamageOutcome.Applied)
                    return;
                string move = queen.CurrentAct == MireLich.Act.LarvaDart ? "Larva Dart" : queen.MoveNames[(int)queen.CurrentMove];
                damage[move] = damage.TryGetValue(move, out int hits) ? hits + 1 : 1;
            };
            int steps = 0, warning = 0, flashes = 0, stagger = 0;
            MireLich.Act previous = queen.CurrentAct;
            MireLich.Move previousMove = queen.CurrentMove;
            int[] warningTotals = new int[9];
            bool unsafeWarning = false, unsafeOpening = false, badPadding = false, outOfBounds = false, badDeath = false;
            int landedHits = 0;
            for (; steps < 240 * 60 && !queen.DeathFinished && fight.Body.Health.Alive; steps++)
            {
                MireLichDriver.Input input = driver.Step(queen, fight.Point, fight.Velocity.x, fight.Grounded, 0f, false);
                int health = queen.Health.Current;
                fight.Step(input);
                if (queen.Health.Current < health)
                    landedHits++;

                bool warningAfter = queen.InWarning;
                bool sameWarning = queen.CurrentAct == previous && queen.CurrentMove == previousMove;
                if (!sameWarning && warning > 0)
                {
                    int expected = previous == MireLich.Act.LarvaWarning ? 24 : WarningSteps[(int)previousMove];
                    bool interrupted = queen.State == Boss.Mode.Dying
                        || queen.CurrentAct == MireLich.Act.Warning && queen.CurrentMove == MireLich.Move.Takeoff;
                    require(interrupted || warning == expected && flashes == 3,
                        $"Mire Lich seed {seed}: {previous}/{previousMove} warned for {warning} steps with {flashes} flashes instead of {expected} and 3");
                    warning = flashes = 0;
                }
                if (warningAfter)
                {
                    warning++;
                    flashes += queen.Flash ? 1 : 0;
                    unsafeWarning |= queen.Hitboxes.Count != 0 || queen.ContactHurts;
                    if (!sameWarning && queen.CurrentAct == MireLich.Act.Warning)
                        warningTotals[(int)queen.CurrentMove]++;
                }

                if (queen.State is Boss.Mode.Dying or Boss.Mode.Dead)
                    badDeath |= queen.Hazards.Any() || queen.Hitboxes.Count > 0 || fight.Combat.Targets.Count != 1;
                if (queen.State == Boss.Mode.Fighting)
                {
                    stagger += queen.CurrentAct == MireLich.Act.Stagger ? 1 : 0;
                    total++;
                    phaseTotal[queen.Phase - 1]++;
                    if (queen.Airborne)
                    {
                        air++;
                        phaseAir[queen.Phase - 1]++;
                    }
                }
                unsafeOpening |= queen.Opening && queen.ContactHurts;
                for (int i = 0; i < queen.Parts.Count; i++)
                {
                    float fit = (queen.Parts[i].Radius - Boss.HurtPadding) * Boss.ContactShrink;
                    badPadding |= Mathf.Abs(fit - queen.Body[i].Radius) > 0.0001f;
                }
                outOfBounds |= Mathf.Abs(queen.Position.x) > 17.2f || queen.Position.y < -1.1f
                    || queen.Position.y > Arena.CeilingY - 4.3f;
                previous = queen.CurrentAct;
                previousMove = queen.CurrentMove;
                queen.ClearCues();
            }

            bool won = queen.DeathFinished;
            wins += won ? 1 : 0;
            require(won && queen.Bounces == 1 && landedHits > 0 && stagger >= 45,
                $"Mire Lich seed {seed}: boss {queen.Health.Current}, player {fight.Body.Health.Current}, bounce count {queen.Bounces}, landed {landedHits}, stagger steps {stagger}");
            require(!unsafeWarning && !unsafeOpening && !badPadding && !outOfBounds && !badDeath,
                $"Mire Lich seed {seed}: warning {unsafeWarning}, opening {unsafeOpening}, padding {badPadding}, bounds {outOfBounds}, death threats {badDeath}");
            require(warningTotals[(int)MireLich.Move.Feed] > 0 && warningTotals[(int)MireLich.Move.Dive] > 0,
                $"Mire Lich seed {seed}: the fighter did not see both the dive and landing");
            Console.WriteLine($"  Mire Lich seed {seed}: {(won ? "won" : "LOST")} in {steps / 60f:F0} s, {fight.Body.Health.Current} hearts left.");
        }

        float airborne = air / (float)Math.Max(1, total);
        string phases = string.Join(", ", Enumerable.Range(0, 3).Select(i => $"{phaseAir[i] / (float)Math.Max(1, phaseTotal[i]):P0}"));
        Console.WriteLine($"Mire Lich: {wins}/{seeds} wins, {airborne:P0} airborne (by phase {phases}).");
        Console.WriteLine("Mire Lich hits taken by move: " + string.Join(", ", damage.Select(hit => $"{hit.Key} {hit.Value}")));
        require(wins == seeds && airborne is >= 0.65f and <= 0.85f, "Mire Lich: wins or air time outside the approved range");
    }

    private static void CheckTerrain(Action<bool, string> require)
    {
        var tiles = FlatHall();
        var platform = new Vector2Int(0, 3);
        tiles.Add(platform, TerrainMaterial.Mud);
        var terrain = new BossTerrain(tiles);
        Vector2 launch = new(HexGrid.Center(platform).x, 9f);
        var glob = new BossProjectile(launch, Vector2.down * 600f);
        require(glob.Step(terrain, 19f) && Mathf.Abs(glob.Position.y - 5.5f) < 0.01f,
            "Mire Lich: a fast glob tunneled through a platform");
        tiles.Remove(platform);
        glob = new BossProjectile(launch, Vector2.down * 600f);
        require(glob.Step(terrain, 19f) && glob.Position.y < 1.01f,
            "Mire Lich: a glob still landed on an excavated platform");

        foreach (MireLich.Move move in new[] { MireLich.Move.Dive, MireLich.Move.Feed })
        {
            platform = new Vector2Int(0, 4);
            tiles = FlatHall();
            tiles.Add(platform, TerrainMaterial.Mud);
            float x = HexGrid.Center(platform).x;
            var fight = new Fight(tiles, new Vector2(x, 1f), 3, new Vector2(x, 7.45f));
            fight.Until(q => q.CurrentAct == MireLich.Act.Hover);
            fight.Queen.ForceMove((int)move);
            fight.Until(q => q.InWarning && q.CurrentMove == move);
            float mark = fight.Queen.Marker?.y ?? 0f;
            tiles.Remove(platform);
            fight.Step(default, movePlayer: false);
            require(mark > 6f && fight.Queen.Marker is Vector2 lower && lower.y < 1.01f,
                $"Mire Lich: {move} did not update its locked shadow after excavation");
            fight.Until(q => q.CurrentAct == (move == MireLich.Move.Dive ? MireLich.Act.DiveStuck : MireLich.Act.Feed));
            require(fight.Queen.Position.y < (move == MireLich.Move.Dive ? 2.9f : 1.01f),
                $"Mire Lich: {move} did not reach the excavated surface");
        }
    }

    private static void CheckDodges(Action<bool, string> require)
    {
        foreach ((string insect, float radius) in new[]
        {
            ("Jumping Spider", 0.43f), ("Stag Beetle", 0.43f), ("Dragonfly", 0.43f),
            ("Fire Ant", 0.43f), ("Worm", 0.34f)
        })
        {
            foreach (MireLich.Move move in new[] { MireLich.Move.Dive, MireLich.Move.Jab, MireLich.Move.Probe })
                foreach (float initial in new[] { 0f, -6.5f })
                    CheckDodge(move, insect, radius, initial, require);

            var jumper = new Fight(FlatHall(), new Vector2(0f, 1f), 7, new Vector2(-1.38f, 1f + radius), radius);
            jumper.Velocity = Vector2.right * 6.5f;
            bool touched = false;
            for (int i = 0; i < 42; i++)
            {
                jumper.Step(new MireLichDriver.Input(1, i == 0, true, false, false), stepQueen: false);
                Vector2 at = jumper.Point;
                float x = Mathf.Clamp(at.x, -MireLich.PuddleWidth * 0.5f, MireLich.PuddleWidth * 0.5f);
                float y = Mathf.Clamp(at.y, 1f, 1f + MireLich.PuddleHeight);
                touched |= (at - new Vector2(x, y)).sqrMagnitude <= radius * radius;
            }
            require(!touched, $"Mire Lich: the {insect} running jump touched a capped puddle");
        }
    }

    private static void CheckDodge(MireLich.Move move, string insect, float radius, float initial, Action<bool, string> require)
    {
        foreach (bool dodge in new[] { false, true })
        {
            var fight = new Fight(FlatHall(), new Vector2(0f, 1f), 7, new Vector2(0f, 1f + radius), radius);
            if (move == MireLich.Move.Probe)
                fight.Point = new Vector2(0f, -0.35f);
            else
            {
                fight.Until(q => q.CurrentAct == MireLich.Act.Hover);
                fight.Queen.ForceMove((int)move);
            }
            fight.Until(q => q.CurrentAct == MireLich.Act.Warning && q.CurrentMove == move);
            if (move == MireLich.Move.Jab)
                fight.Point = fight.Queen.Position + new Vector2(fight.Queen.Facing * 1.45f, radius + 0.06f);
            if (move == MireLich.Move.Dive)
                fight.Point = new Vector2(fight.Queen.Marker?.x ?? 0f, 1f + radius);
            int direction = move == MireLich.Move.Probe ? 1 : fight.Queen.Facing;
            fight.Velocity = new Vector2(initial * direction, 0f);
            int prior = fight.Body.Health.Current;
            int steps = move == MireLich.Move.Dive ? 125 : 65;
            for (int i = 0; i < steps; i++)
                fight.Step(new MireLichDriver.Input(dodge ? direction : 0, false, false, false, false),
                    movePlayer: dodge, underfloor: move == MireLich.Move.Probe);
            require((prior == fight.Body.Health.Current) == dodge,
                $"Mire Lich: {move}/{insect}, initial speed {initial}, dodge {dodge}, lost {prior - fight.Body.Health.Current}");
        }
    }

    private static void CheckTriggersAndGrowth(Action<bool, string> require)
    {
        var fight = new Fight(FlatHall(), new Vector2(0f, 1f), 3, new Vector2(0f, 1.43f));
        MireLich queen = fight.Queen;
        fight.Until(q => q.CurrentAct == MireLich.Act.Hover);
        queen.ForceMove((int)MireLich.Move.Feed);
        fight.Until(q => q.CurrentAct == MireLich.Act.Feed);
        // A real upward strike while standing at floor level exercises the second probe trigger.
        fight.Point = new Vector2(queen.Position.x, 1.25f);
        bool struck = fight.Combat.Melee(new MeleeStrike(fight.Point, Vector2.up, 1.3f, 0.38f, 1, 4f));
        fight.Until(q => q.CurrentMove == MireLich.Move.Probe && q.InWarning);
        require(struck && queen.CurrentMove == MireLich.Move.Probe && queen.InWarning,
            "Mire Lich: an upward strike did not queue a Subsurface Probe");

        fight = new Fight(FlatHall(), new Vector2(0f, 1f), 3, new Vector2(0f, 1.43f));
        queen = fight.Queen;
        queen.DevSetPhase(3);
        fight.Until(q => q.Phase == 3 && q.CurrentAct == MireLich.Act.Stagger);
        int guarded = queen.Health.Current;
        require(queen.Damage.Receive(new DamageHit(DamageSource.Melee, 1)) == DamageOutcome.Absorbed
            && queen.Health.Current == guarded, "Mire Lich: her phase stagger did not guard the first step");
        for (int stage = 2; stage <= 3; stage++)
        {
            queen.ForceMove((int)MireLich.Move.Feed);
            fight.Until(q => q.CurrentAct == MireLich.Act.Feed);
            float oldRadius = queen.Parts.Where(part => part.Multiplier == 2).Max(part => part.Radius);
            fight.Until(q => q.Stage >= stage);
            require(queen.Stage == stage && queen.Parts.Where(part => part.Multiplier == 2).Max(part => part.Radius) > oldRadius,
                $"Mire Lich: completed feeding did not enlarge stage {stage}");
        }
        Hurtbox weak = queen.Parts.First(part => part.Multiplier == 2);
        int hp = queen.Health.Current;
        queen.Damage.Receive(new DamageHit(DamageSource.Melee, 1), weak);
        require(queen.Health.Current == hp - 2, "Mire Lich: the abdomen did not take double damage");

        fight = new Fight(FlatHall(), new Vector2(0f, 1f), 9, new Vector2(0f, 1.43f));
        queen = fight.Queen;
        fight.Until(q => q.CurrentAct == MireLich.Act.Hover);
        queen.ForceMove((int)MireLich.Move.Feed);
        fight.Until(q => q.CurrentAct == MireLich.Act.Feed);
        for (int i = 0; i < 6; i++)
        {
            queen.Damage.Receive(new DamageHit(DamageSource.Melee, 1));
            for (int wait = 0; wait < 9; wait++)
                fight.Step(default, movePlayer: false);
        }
        require(queen.InWarning && queen.CurrentMove == MireLich.Move.Takeoff,
            "Mire Lich: six hits did not cut feeding short");
    }

    private static void CheckMinions(Action<bool, string> require)
    {
        var fight = new Fight(FlatHall(), new Vector2(0f, 1f), 2, new Vector2(0f, 1.43f));
        MireLich queen = fight.Queen;
        queen.DevSetPhase(3);
        fight.Until(q => q.Phase == 3);
        queen.ForceMove((int)MireLich.Move.Bubbles);
        fight.Until(q => q.CurrentAct == MireLich.Act.AirRecovery && q.CurrentMove == MireLich.Move.Bubbles);
        require(fight.Combat.Targets.Count == 4, "Mire Lich: phase 3 did not register three bubbles");
        DamageReceiver bubble = fight.Combat.Targets[1];
        fight.Combat.Melee(new MeleeStrike(bubble.Position - Vector2.right, Vector2.right, 1.3f, 0.38f, 1, 4f));
        fight.Step(default, movePlayer: false);
        require(!fight.Combat.Targets.Contains(bubble) && !queen.Hazards.Any(h => h.Kind == MireLich.HazardKind.Cloud),
            "Mire Lich: a struck bubble stayed registered or made a cloud");
        queen.ForceMove((int)MireLich.Move.Larvae);
        fight.Until(q => q.CurrentAct == MireLich.Act.AirRecovery && q.CurrentMove == MireLich.Move.Larvae);
        require(fight.Combat.Targets.Count == 3 && fight.Combat.Targets.Skip(1).All(t => t.Health.Maximum == 2),
            "Mire Lich: larva call did not register two 2-HP targets");
        queen.ForceMove((int)MireLich.Move.Larvae);
        fight.Until(q => q.CurrentAct == MireLich.Act.Warning && q.CurrentMove == MireLich.Move.Larvae);
        fight.Until(q => q.CurrentAct == MireLich.Act.AirRecovery && q.CurrentMove == MireLich.Move.Larvae);
        require(fight.Combat.Targets.Count == 4, "Mire Lich: larva cap is not three");
        queen.Health.TakeDamage(queen.Health.Current);
        fight.Step(default, movePlayer: false);
        require(!queen.Hazards.Any() && fight.Combat.Targets.Count == 1,
            "Mire Lich: death left live minions behind");
    }

    private static Dictionary<Vector2Int, TerrainMaterial> FlatHall()
    {
        var tiles = new Dictionary<Vector2Int, TerrainMaterial>();
        for (int x = -14; x <= 14; x++)
        {
            tiles.Add(new Vector2Int(x, 0), TerrainMaterial.Reinforced);
            tiles.Add(new Vector2Int(x, -1), TerrainMaterial.Reinforced);
        }
        return tiles;
    }

    private sealed class Fight
    {
        internal readonly DamageReceiver Body;
        internal readonly MireLich Queen;
        internal readonly CombatArena Combat = new();
        internal Vector2 Point;
        internal Vector2 Velocity;
        private readonly float radius;
        private readonly BossTerrain terrain;
        private int cooldown;
        private int facing = 1;
        internal bool Grounded { get; private set; }

        internal Fight(IReadOnlyDictionary<Vector2Int, TerrainMaterial> tiles, Vector2 home, uint seed, Vector2 point,
            float radius = 0.43f)
        {
            Point = point;
            this.radius = radius;
            terrain = new BossTerrain(tiles);
            Queen = new MireLich(Arena, home, seed, terrain, Combat, (direction, distance) =>
                Point.x = Mathf.Clamp(Point.x + direction * distance, -16.5f, 16.5f));
            Combat.AddTarget(Queen.Damage);
            Body = new DamageReceiver(new HealthPool(6), radius, () => Point, 36, impulse => Velocity += impulse);
            Grounded = true;
        }

        internal void Until(Func<MireLich, bool> done)
        {
            for (int i = 0; i < 2000 && !done(Queen); i++)
                Step(default, movePlayer: false);
        }

        internal void Step(MireLichDriver.Input input, bool movePlayer = true, bool underfloor = false, bool stepQueen = true)
        {
            Combat.Step();
            Body.Step();
            if (input.Move != 0)
                facing = input.Move;
            cooldown--;
            if (input.Strike && cooldown <= 0)
            {
                Combat.Melee(new MeleeStrike(Point, Vector2.right * facing, input.Heavy ? 2.1f : 1.3f,
                    input.Heavy ? 0.83f : 0.38f, input.Heavy ? 2 : 1, input.Heavy ? 7f : 4f));
                cooldown = input.Heavy ? 24 : 16;
            }
            if (stepQueen)
                Queen.Step(Body);
            Queen.ClearCues();
            if (!movePlayer)
                return;

            float acceleration = Grounded ? input.Move == 0 ? 65f : 55f : 28f;
            Velocity.x = Mathf.MoveTowards(Velocity.x, input.Move * 6.5f, acceleration * Dt);
            float oldY = Point.y;
            if (!underfloor)
            {
                Velocity.y = input.Jump && Grounded ? 12.5f : Mathf.Max(-24f, Velocity.y - 38f * Dt);
                if (!input.HoldJump && Velocity.y > 4.2f)
                    Velocity.y = 4.2f;
                Point.y += Velocity.y * Dt;
            }
            Point.x = Mathf.Clamp(Point.x + Velocity.x * Dt, -16.5f, 16.5f);
            if (underfloor)
                return;

            Vector2 floor = default;
            Grounded = Velocity.y <= 0f && terrain.Sweep(new Vector2(Point.x, oldY - radius + 0.14f),
                new Vector2(Point.x, Point.y - radius - 0.025f), out floor);
            if (Grounded)
            {
                Point.y = floor.y + radius;
                Velocity.y = 0f;
            }
        }
    }
}
