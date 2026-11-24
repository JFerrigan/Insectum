using System.Collections.Generic;
using UnityEngine;

namespace Insectum.Game
{
    internal enum HazardKind
    {
        Bramble,
        Cactus,
        Spikes,
        Flytrap,
        Laser
    }

    // A hazard a room template carves into its geometry. Hazards belong to the level layout rather than its
    // content, so every crossing meets them, with or without enemies and pickups.
    internal readonly struct TunnelHazard
    {
        // A patch of brambles, a cactus, a spike bed, or a flytrap's mouth, covering its area.
        internal TunnelHazard(HazardKind kind, Rect area, bool hanging, Vector2Int chunk)
            : this(kind, area, hanging, chunk, 0f, 0f, 0, 0f)
        {
        }

        // A laser emitter at the center of its area. Its arms reach the given distance, start at phase degrees
        // and turn spin degrees a second, counterclockwise when positive.
        internal TunnelHazard(HazardKind kind, Rect area, bool hanging, Vector2Int chunk, float reach, float spin,
            int arms, float phase)
        {
            Kind = kind;
            Area = area;
            Hanging = hanging;
            Chunk = chunk;
            Reach = reach;
            Spin = spin;
            Arms = arms;
            Phase = phase;
        }

        internal HazardKind Kind { get; }
        internal Rect Area { get; }
        // Hanging hazards grow down from a roof. The rest grow up from a floor, except lasers, which float.
        internal bool Hanging { get; }
        internal Vector2Int Chunk { get; }
        internal float Reach { get; }
        internal float Spin { get; }
        internal int Arms { get; }
        internal float Phase { get; }
        // Spikes, flytraps and lasers kill outright. Brambles and cacti cost a heart.
        internal bool Lethal => Kind is HazardKind.Spikes or HazardKind.Flytrap or HazardKind.Laser;
    }

    // Steps every hazard in a level against the player and, for flytraps and lasers, against enemies too.
    //
    // Brambles and cacti are always armed: touching one costs a heart and throws the player clear, followed by
    // the usual invulnerability, and a downward strike that reaches one bounces the player off it unhurt.
    // Spikes kill a player who comes down onto them but let one walk through. A flytrap snaps shut on whatever
    // enters its open mouth, killing it, then chews for three seconds before it opens again. Lasers spin at a
    // fixed rate from the moment the level loads, their beams stop at rock, and they kill what they touch.
    // Dune Mantle blocks any hazard hit, as it blocks traps.
    internal sealed partial class LevelHazards
    {
        internal const float BrambleHeight = 0.5f;
        internal const float SpikeHeight = 0.45f;
        internal const float MouthSize = 1.0f;
    }

#if !MAPGEN
    internal sealed partial class LevelHazards
    {
        internal const int LethalHearts = 99;
        // The hit test shrinks the player a little, so grazing the art's outermost thorns is free.
        private const float Forgiveness = 0.08f;
        // Spikes kill only a body coming down faster than this. Walking keeps it near -0.6 and a drop of a
        // sixth of a unit passes it.
        private const float SpikeFallSpeed = -3.5f;
        private const int ChewSteps = 180;
        private const int ReopenSteps = 30;
        private const float TwitchRange = 2f;
        private const float TileWidth = 0.5f;
        // A run waits until every laser stays this far beyond touching distance the whole way.
        private const float RunMargin = 0.25f;
        private static readonly Vector2 FloorThrow = new(3.2f, 8.5f);
        private static readonly Vector2 RoofThrow = new(3.2f, -2.5f);
        private static readonly Vector2 CactusThrow = new(6.5f, 6.0f);

        private readonly List<TunnelHazard> hazards;
        private readonly int[] chewing;
        private readonly SpriteRenderer[] heads;
        private readonly SpriteRenderer[][] beams;
        private readonly PhysicsScene2D physics;
        private readonly Collider2D ignored;
        private readonly RaycastHit2D[] hits = new RaycastHit2D[8];
        private int tick;

        // Beams are clipped against everything in the physics scene except the ignored collider, the player's.
        internal LevelHazards(Transform parent, Sprite pixel, IReadOnlyList<TunnelHazard> hazards, PhysicsScene2D physics,
            Collider2D ignored)
        {
            this.hazards = new List<TunnelHazard>(hazards);
            this.physics = physics;
            this.ignored = ignored;
            chewing = new int[this.hazards.Count];
            heads = new SpriteRenderer[this.hazards.Count];
            beams = new SpriteRenderer[this.hazards.Count][];
            if (pixel == null)
                return;

            var root = new GameObject("Hazards").transform;
            root.SetParent(parent, false);
            for (int i = 0; i < this.hazards.Count; i++)
                PlaceArt(root, i);
        }

        internal IReadOnlyList<TunnelHazard> Hazards => hazards;

        // Steps since the level loaded. Laser angles follow it.
        internal int Tick => tick;

        // True while the flytrap at this index is shut.
        internal bool Shut(int index) => chewing[index] > 0;

        // Velocity is the player's current velocity. Fall speed is its vertical speed going into the last physics
        // step, before landing could zero it.
        internal void Step(DamageReceiver player, Vector2 velocity, float fallSpeed, IReadOnlyList<LevelEnemy> enemies)
        {
            tick++;
            for (int i = 0; i < hazards.Count; i++)
            {
                TunnelHazard hazard = hazards[i];
                switch (hazard.Kind)
                {
                    case HazardKind.Bramble:
                    case HazardKind.Cactus:
                        if (Touches(hazard.Area, player.Position, player.Radius - Forgiveness))
                        {
                            Vector2 thrown = hazard.Kind == HazardKind.Cactus ? CactusThrow
                                : hazard.Hanging ? RoofThrow : FloorThrow;
                            // Toward the nearer end, replacing the player's current speed.
                            float away = player.Position.x < hazard.Area.center.x ? -1f : 1f;
                            player.Receive(new DamageHit(DamageSource.Trap, 1, new Vector2(away * thrown.x, thrown.y) - velocity));
                        }
                        break;
                    case HazardKind.Spikes:
                        if (fallSpeed < SpikeFallSpeed && Touches(hazard.Area, player.Position, player.Radius - Forgiveness))
                            player.Receive(Lethal);
                        break;
                    case HazardKind.Flytrap:
                        StepFlytrap(i, player, enemies);
                        break;
                    case HazardKind.Laser:
                        StepLaser(i, player, enemies);
                        break;
                }
            }
        }

        // True when a downward strike reaches the top of a floor bramble or cactus.
        internal bool Pogo(MeleeStrike strike)
        {
            Vector2 origin = strike.Origin;
            foreach (TunnelHazard hazard in hazards)
            {
                if (hazard.Hanging || hazard.Kind is not (HazardKind.Bramble or HazardKind.Cactus))
                    continue;
                Rect area = hazard.Area;
                if (origin.x + strike.HalfWidth >= area.xMin && origin.x - strike.HalfWidth <= area.xMax
                    && origin.y >= area.yMax && origin.y - strike.Reach <= area.yMax)
                    return true;
            }
            return false;
        }

        // True when a body starting from rest at center and running in the direction would cover the distance
        // without any laser arm coming near it, ignoring the rock that may block a beam. Walkers use it to wait
        // for a gap in a sweep.
        internal bool ClearRun(Vector2 center, int direction, float distance, float radius)
        {
            float x = center.x;
            float speed = 0f;
            for (int step = 1; Mathf.Abs(x - center.x) < distance; step++)
            {
                speed = Mathf.Min(PlayerMotor.RunSpeed, speed + PlayerMotor.RunAcceleration * PlayerMotor.StepSeconds);
                x += direction * speed * PlayerMotor.StepSeconds;
                var body = new Vector2(x, center.y);
                foreach (TunnelHazard hazard in hazards)
                {
                    if (hazard.Kind != HazardKind.Laser)
                        continue;
                    for (int arm = 0; arm < hazard.Arms; arm++)
                    {
                        Vector2 heading = ArmDirection(hazard, tick + step, arm);
                        Vector2 origin = hazard.Area.center;
                        if (SegmentDistance(body, origin, origin + heading * hazard.Reach) < radius - Forgiveness + RunMargin)
                            return false;
                    }
                }
            }
            return true;
        }

        private static DamageHit Lethal => new(DamageSource.Trap, LethalHearts);

        private void StepFlytrap(int index, DamageReceiver player, IReadOnlyList<LevelEnemy> enemies)
        {
            TunnelHazard hazard = hazards[index];
            if (chewing[index] > 0)
            {
                chewing[index]--;
                SetHead(index, chewing[index] > ReopenSteps ? 2 : 1);
                return;
            }

            if (Touches(hazard.Area, player.Position, player.Radius - Forgiveness))
            {
                player.Receive(Lethal);
                chewing[index] = ChewSteps;
                SetHead(index, 2);
                return;
            }

            if (enemies != null)
            {
                foreach (LevelEnemy enemy in enemies)
                {
                    if (enemy.Boss || !enemy.Damage.Health.Alive || !Touches(hazard.Area, enemy.Position, enemy.Damage.Radius))
                        continue;
                    enemy.Damage.Receive(Lethal);
                    chewing[index] = ChewSteps;
                    SetHead(index, 2);
                    return;
                }
            }

            bool near = (player.Position - hazard.Area.center).sqrMagnitude < TwitchRange * TwitchRange;
            SetHead(index, near ? 1 : 0);
        }

        private void StepLaser(int index, DamageReceiver player, IReadOnlyList<LevelEnemy> enemies)
        {
            TunnelHazard hazard = hazards[index];
            Vector2 origin = hazard.Area.center;
            for (int arm = 0; arm < hazard.Arms; arm++)
            {
                Vector2 direction = ArmDirection(hazard, tick, arm);
                float reach = Clip(origin, direction, hazard.Reach);
                Vector2 end = origin + direction * reach;
                if (beams[index] != null)
                {
                    Transform beam = beams[index][arm].transform;
                    beam.localScale = new Vector3(reach, 1f, 1f);
                    beam.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                }

                if (SegmentDistance(player.Position, origin, end) < player.Radius - Forgiveness)
                    player.Receive(Lethal);
                if (enemies == null)
                    continue;
                foreach (LevelEnemy enemy in enemies)
                    if (!enemy.Boss && enemy.Damage.Health.Alive
                        && SegmentDistance(enemy.Position, origin, end) < enemy.Damage.Radius)
                        enemy.Damage.Receive(Lethal);
            }
        }

        private static Vector2 ArmDirection(TunnelHazard hazard, int step, int arm)
        {
            float degrees = hazard.Phase + hazard.Spin * step * PlayerMotor.StepSeconds + 360f * arm / hazard.Arms;
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        // How far a beam travels before it meets rock or anything solid other than the player.
        private float Clip(Vector2 origin, Vector2 direction, float reach)
        {
            int count = physics.Raycast(origin, direction, reach, hits);
            for (int i = 0; i < count; i++)
                if (hits[i].collider != ignored && !hits[i].collider.isTrigger)
                    reach = Mathf.Min(reach, hits[i].distance);
            return reach;
        }

        private static bool Touches(Rect area, Vector2 position, float radius)
        {
            Vector2 closest = new(Mathf.Clamp(position.x, area.xMin, area.xMax), Mathf.Clamp(position.y, area.yMin, area.yMax));
            return (position - closest).sqrMagnitude < radius * radius;
        }

        private static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 span = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(point - a, span) / Mathf.Max(span.sqrMagnitude, 0.000001f));
            return Vector2.Distance(point, a + span * t);
        }

        // Shows a flytrap's frame: 0 open, 1 twitching or reopening, 2 shut.
        private void SetHead(int index, int frame)
        {
            if (heads[index] != null)
                heads[index].sprite = SwampArt.Flytrap(frame);
        }

        private void PlaceArt(Transform root, int index)
        {
            TunnelHazard hazard = hazards[index];
            Rect area = hazard.Area;
            float rootY = hazard.Hanging ? area.yMax : area.yMin;
            switch (hazard.Kind)
            {
                case HazardKind.Bramble:
                case HazardKind.Spikes:
                    PlaceTiles(root, hazard);
                    break;
                case HazardKind.Cactus:
                    ForestArt.Place(root, "Cactus", new Vector2(area.center.x, rootY),
                        DesertArt.Cactus(area.height > 1f ? 1 : 0), 2);
                    break;
                case HazardKind.Flytrap:
                    heads[index] = ForestArt.Place(root, "Flytrap", new Vector2(area.center.x, rootY), SwampArt.Flytrap(0), 3);
                    heads[index].flipY = hazard.Hanging;
                    break;
                case HazardKind.Laser:
                    beams[index] = new SpriteRenderer[hazard.Arms];
                    for (int arm = 0; arm < hazard.Arms; arm++)
                        beams[index][arm] = ForestArt.Place(root, "Laser beam", area.center, AlienArt.LaserBeam(), 4);
                    heads[index] = ForestArt.Place(root, "Laser emitter", area.center, AlienArt.LaserEmitter(), 5);
                    break;
            }
        }

        // Half-unit tiles with capped ends, laid out like the bramble sheet in every tiled atlas. Hanging patches
        // use the floor art flipped onto the roof.
        private static void PlaceTiles(Transform root, TunnelHazard hazard)
        {
            Rect area = hazard.Area;
            int tiles = Mathf.Max(1, Mathf.RoundToInt(area.width / TileWidth));
            float width = area.width / tiles;
            float y = hazard.Hanging ? area.yMax : area.yMin;
            bool spikes = hazard.Kind == HazardKind.Spikes;
            for (int i = 0; i < tiles; i++)
            {
                float x = area.xMin + width * (i + 0.5f);
                int cel = tiles > 1 && i == 0 ? ForestArt.BrambleLeft
                    : tiles > 1 && i == tiles - 1 ? ForestArt.BrambleRight
                    : Mathf.Abs(Mathf.RoundToInt(x * 7f + y * 13f)) % ForestArt.BrambleVariants;
                SpriteRenderer art = ForestArt.Place(root, spikes ? "Spikes" : "Bramble", new Vector2(x, y),
                    spikes ? CavesArt.Spike(cel) : ForestArt.Bramble(cel), 2);
                art.flipY = hazard.Hanging;
            }
        }
    }
#endif
}
