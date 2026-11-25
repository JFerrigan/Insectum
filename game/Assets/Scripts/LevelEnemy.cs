using System;
using System.Collections.Generic;
using UnityEngine;

namespace Insectum.Game
{
    internal enum EnemyStyle
    {
        Crawler,
        Flyer,
        ChargeBoss,
        ReedRunner,
        Skimmer,
        VolleyBoss,
        CaveHopper,
        GlowFly,
        QuakeBoss,
        DuneBeetle,
        SandWasp,
        BurrowBoss,
        LatticeTick,
        ArcMote,
        BeamBoss,
        // A reworked boss: its BossKind picks the logic and view that run it.
        ArenaBoss
    }

    internal sealed class EnemySpec
    {
        public readonly string Name;
        public readonly EnemyStyle Style;
        public readonly int Hearts;
        public readonly int Nektar;
        public readonly float Radius;
        public readonly Color32 Tint;
        internal readonly BossKind Kind;

        public EnemySpec(string name, EnemyStyle style, int hearts, int nektar, float radius, Color32 tint)
            : this(name, style, hearts, nektar, radius, tint, BossKind.None)
        {
        }

        internal EnemySpec(string name, EnemyStyle style, int hearts, int nektar, float radius, Color32 tint, BossKind kind)
        {
            Name = name;
            Style = style;
            Hearts = hearts;
            Nektar = nektar;
            Radius = radius;
            Tint = tint;
            Kind = kind;
        }

        // Every world's regular enemies, which rooms may place by name.
        internal static IReadOnlyList<EnemySpec> Regulars => new[]
        {
            ForestContent.BarkMite, ForestContent.SporeMoth, SwampContent.ReedRunner, SwampContent.MarshSkimmer,
            CavesContent.Hopper, CavesContent.GlowFly, DesertContent.DuneBeetle, DesertContent.SandWasp,
            AlienContent.LatticeTick, AlienContent.ArcMote
        };

        // The regular enemy with this name, or null when the name is empty or unknown.
        internal static EnemySpec Find(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            foreach (EnemySpec spec in Regulars)
                if (spec.Name == name)
                    return spec;
            return null;
        }
    }

    internal sealed class LevelEnemy
    {
        internal enum Phase
        {
            Waiting,
            Warning,
            Attacking,
            Recovery
        }

        private const int EruptionSteps = 14;
        private const int BeamSteps = 45;
        private const float BeamLength = 8.6f;
        // Bosses keep to the arena's flat floor between its terraces.
        private const float Lane = BossArenaLayout.LaneHalfWidth;
        private readonly EnemySpec spec;
        private readonly Vector2 anchor;
        private readonly Transform root;
        private readonly SpriteRenderer body;
        private readonly SpriteRenderer warning;
        private readonly SpriteRenderer[] hearts;
        private readonly SpriteRenderer leftWave;
        private readonly SpriteRenderer rightWave;
        private readonly SpriteRenderer beam;
        private readonly Action<Vector2, Vector2> fire;
        private readonly int patrolOffset;
        private readonly bool echo;
        // A reworked boss's own logic and art, or null for every other enemy.
        private readonly Boss brain;
        private readonly StagBeetleView view;
        private Vector2 position;
        private int tick;
        private int remaining;
        private int direction = 1;
        private bool struck;
        private float chargeX;
        private float quakeRadius;
        private float burrowTargetX;
        private Vector2 displacement;
        private int burnStacks;
        private int burnTimer;
        private int burnPulses;

        public DamageReceiver Damage { get; }
        public string Name => spec.Name;
        public int Nektar => spec.Nektar;
        public bool Boss => brain != null || spec.Style is EnemyStyle.ChargeBoss or EnemyStyle.VolleyBoss or EnemyStyle.QuakeBoss
            or EnemyStyle.BurrowBoss or EnemyStyle.BeamBoss;
        internal Boss Brain => brain;
        // True once a defeated enemy has finished falling: at once for most, after the death animation for a boss.
        internal bool DeathFinished => brain?.DeathFinished ?? !Damage.Health.Alive;
        internal EnemyStyle Style => spec.Style;
        internal EnemySpec Spec => spec;
        private bool Flies => spec.Style is EnemyStyle.Flyer or EnemyStyle.Skimmer or EnemyStyle.GlowFly or EnemyStyle.SandWasp or EnemyStyle.ArcMote;
        private bool Shielded => spec.Style == EnemyStyle.BeamBoss && AttackPhase != Phase.Recovery;
        private bool Burrowing => spec.Style == EnemyStyle.BurrowBoss && AttackPhase == Phase.Attacking
            && remaining > EruptionSteps;
        public Vector2 Position
        {
            get => brain?.Position ?? position;
            private set => position = value;
        }
        public Phase AttackPhase { get; private set; }

        internal LevelEnemy(Transform parent, Sprite pixel, EnemySpec spec, Vector2 landing, int patrolOffset,
            Action<Vector2, Vector2> fire, bool echo = false, uint seed = 0)
        {
            this.spec = spec;
            this.patrolOffset = patrolOffset;
            this.echo = echo;
            this.fire = fire;
            if (spec.Kind != BossKind.None)
            {
                var beetle = new StagBeetle(BossArena.Hall, landing, seed);
                brain = beetle;
                Damage = brain.Damage;
                if (pixel != null)
                    view = new StagBeetleView(parent, beetle);
                return;
            }

            anchor = landing + Vector2.up * (Flies ? 1.65f : spec.Radius);
            chargeX = anchor.x;
            Position = anchor;
            Damage = new DamageReceiver(new HealthPool(spec.Hearts), spec.Radius, () => Position, 9,
                impulse => displacement = Vector2.ClampMagnitude(displacement + impulse * (Boss ? 0.045f : 0.09f), 0.8f),
                absorb: hit => Shielded);
            if (pixel == null)
                return;

            root = new GameObject(echo ? $"{spec.Name} echo" : spec.Name).transform;
            root.SetParent(parent, false);
            root.position = Position;
            Vector2 basePosition = Vector2.down * (Flies ? 0.44f : spec.Radius);
            body = ForestArt.Place(root, "Carapace", basePosition, ActorArt(0), 4);
            if (Boss)
                body.transform.localScale = Vector3.one * 1.25f;
            warning = ForestArt.Place(root, "Attack warning", Vector2.up * (spec.Radius + 0.55f),
                spec.Style == EnemyStyle.BurrowBoss ? DesertArt.BurrowWarning : ForestArt.Prop(ForestProp.Warning), 6);
            warning.transform.localScale = Vector3.one * (spec.Style == EnemyStyle.BurrowBoss ? 1.15f : 0.7f);
            warning.enabled = false;
            if (spec.Style == EnemyStyle.QuakeBoss)
            {
                leftWave = ForestArt.Place(parent, "Left shockwave", Vector2.zero, CavesArt.Pulse, 5);
                rightWave = ForestArt.Place(parent, "Right shockwave", Vector2.zero, CavesArt.Pulse, 5);
                leftWave.flipX = true;
                leftWave.enabled = rightWave.enabled = false;
            }
            if (spec.Style == EnemyStyle.BeamBoss)
            {
                beam = ForestArt.Place(parent, "Sweeping beam", Vector2.zero, AlienArt.Beam, 5);
                beam.enabled = false;
            }
            hearts = new SpriteRenderer[spec.Hearts];
            for (int i = 0; i < hearts.Length; i++)
            {
                Vector2 point = new((i - (hearts.Length - 1) / 2f) * 0.14f, spec.Radius + 0.27f);
                hearts[i] = ForestRoute.CreateMarker(root, "Heart", point,
                    new Vector2(0.10f, 0.07f), pixel, new Color32(252, 193, 91, 255), 3);
            }
        }

        internal void Ignite(int stacks)
        {
            if (stacks <= 0 || !Damage.Health.Alive)
                return;

            burnStacks = Mathf.Max(burnStacks, stacks);
            if (burnPulses == 0)
                burnTimer = 27;
            burnPulses = 2;
        }

        internal void Step(DamageReceiver target)
        {
            if (brain != null)
            {
                TickBurn();
                brain.Step(target);
                if (view != null)
                    view.Update();
                else
                    brain.ClearCues();
                return;
            }

            if (!Damage.Health.Alive)
            {
                HideDefeated();
                return;
            }

            TickBurn();
            if (!Damage.Health.Alive)
            {
                HideDefeated();
                return;
            }

            Vector2 toPlayer = target.Position - Position;
            tick++;
            if (remaining > 0)
                remaining--;
            displacement = Vector2.MoveTowards(displacement, Vector2.zero, 3f * PlayerMotor.StepSeconds);

            switch (AttackPhase)
            {
                case Phase.Waiting:
                    float reach = Boss ? (echo ? 5.0f : 8.5f) : Flies ? 3.4f : 1.42f;
                    if (toPlayer.sqrMagnitude < reach * reach)
                    {
                        direction = toPlayer.x < 0f ? -1 : 1;
                        if (spec.Style == EnemyStyle.BurrowBoss)
                            burrowTargetX = Mathf.Clamp(target.Position.x, -Lane, Lane);
                        SetPhase(Phase.Warning, spec.Style switch
                        {
                            EnemyStyle.VolleyBoss => 51,
                            EnemyStyle.QuakeBoss => 44,
                            EnemyStyle.BurrowBoss => 42,
                            EnemyStyle.BeamBoss => 50,
                            EnemyStyle.ChargeBoss => 38,
                            _ => 40
                        });
                    }
                    break;
                case Phase.Warning:
                    if (remaining == 0)
                        StartAttack(target);
                    break;
                case Phase.Attacking:
                    if (spec.Style == EnemyStyle.ChargeBoss)
                        chargeX = Mathf.Clamp(chargeX + direction * 8.1f * PlayerMotor.StepSeconds, -Lane, Lane);
                    if (spec.Style == EnemyStyle.QuakeBoss)
                        StepQuake(target);
                    else if (spec.Style == EnemyStyle.BurrowBoss)
                        StepBurrow(target);
                    else if (spec.Style == EnemyStyle.BeamBoss)
                        StepBeam(target);
                    else if (!Flies && spec.Style != EnemyStyle.VolleyBoss && !struck)
                        TryStrike(target);
                    if (remaining == 0)
                        SetPhase(Phase.Recovery, spec.Style switch
                        {
                            EnemyStyle.VolleyBoss => 69,
                            EnemyStyle.QuakeBoss => 75,
                            EnemyStyle.BurrowBoss => 62,
                            EnemyStyle.BeamBoss => 87,
                            EnemyStyle.ChargeBoss => 52,
                            _ => 55
                        });
                    break;
                case Phase.Recovery:
                    if (remaining == 0)
                        SetPhase(Phase.Waiting);
                    break;
            }

            UpdatePosition();
            if (!Burrowing)
                TryContact(target);
            UpdateArt();
        }

        private void StartAttack(DamageReceiver target)
        {
            struck = false;
            quakeRadius = 0f;
            SetPhase(Phase.Attacking, spec.Style switch
            {
                EnemyStyle.VolleyBoss => 27,
                EnemyStyle.QuakeBoss => 37,
                EnemyStyle.BurrowBoss => 44,
                EnemyStyle.BeamBoss => BeamSteps,
                EnemyStyle.ChargeBoss => 51,
                _ => 9
            });
            Vector2 aim = (target.Position - Position).normalized;
            if (spec.Style == EnemyStyle.VolleyBoss)
            {
                if (aim == Vector2.zero)
                    aim = Vector2.right * direction;
                fire(Position, Quaternion.Euler(0f, 0f, 24f) * aim);
                fire(Position, aim);
                fire(Position, Quaternion.Euler(0f, 0f, -24f) * aim);
            }
            else if (Flies && aim != Vector2.zero)
                fire(Position, aim);
        }

        private void StepQuake(DamageReceiver target)
        {
            float previous = quakeRadius;
            quakeRadius += 0.13f;
            Vector2 delta = target.Position - Position;
            float distance = Mathf.Abs(delta.x);
            if (struck || distance > quakeRadius + target.Radius || distance < previous - target.Radius
                || delta.y > 0.38f || delta.y < -1.2f)
                return;

            struck = true;
            target.Receive(new DamageHit(DamageSource.Melee, 1,
                new Vector2(Mathf.Sign(delta.x) * 4.6f, 4.1f)));
        }

        private void StepBurrow(DamageReceiver target)
        {
            if (remaining != EruptionSteps)
                return;

            chargeX = burrowTargetX;
            Vector2 offset = target.Position - new Vector2(chargeX, anchor.y);
            if (Mathf.Abs(offset.x) <= 0.9f + target.Radius && Mathf.Abs(offset.y) < 0.96f)
                target.Receive(new DamageHit(DamageSource.Melee, 1,
                    new Vector2(Mathf.Sign(offset.x) * 3.9f, 5.6f)));
        }

        private Vector2 BeamDirection()
        {
            float sweep = AttackPhase == Phase.Attacking ? 1f - (float)remaining / BeamSteps : 0f;
            float angle = Mathf.Lerp(40f, -42f, sweep) * Mathf.Deg2Rad;
            return new Vector2(direction * Mathf.Cos(angle), Mathf.Sin(angle));
        }

        private void StepBeam(DamageReceiver target)
        {
            if (struck)
                return;

            Vector2 ray = BeamDirection();
            Vector2 offset = target.Position - Position;
            float distance = Mathf.Clamp(Vector2.Dot(offset, ray), 0f, BeamLength);
            float radius = target.Radius + 0.09f;
            if ((offset - ray * distance).sqrMagnitude > radius * radius)
                return;

            struck = true;
            target.Receive(new DamageHit(DamageSource.Projectile, 1, new Vector2(direction * 3.2f, 2.4f)));
        }

        private void HideDefeated()
        {
            if (root != null)
                root.gameObject.SetActive(false);
            if (leftWave != null)
                leftWave.enabled = rightWave.enabled = false;
            if (beam != null)
                beam.enabled = false;
        }

        private void TryStrike(DamageReceiver target)
        {
            Vector2 offset = target.Position - Position;
            float horizontalReach = Boss ? spec.Radius + target.Radius + 0.05f : 1.1f;
            if (Mathf.Abs(offset.x) > horizontalReach || Mathf.Abs(offset.y) > (Boss ? 0.95f : 0.7f))
                return;
            if (!Boss && offset.x * direction < -0.22f)
                return;

            struck = true;
            var impulse = new Vector2((offset.x < 0f ? -1f : 1f) * (Boss ? 5.2f : 3.4f), 2.8f);
            target.Receive(new DamageHit(DamageSource.Melee, 1, impulse));
        }

        private void TryContact(DamageReceiver target)
        {
            Vector2 offset = target.Position - Position;
            float reach = spec.Radius + target.Radius;
            if (offset.sqrMagnitude > reach * reach)
                return;

            float push = offset.x < 0f ? -8f : 8f;
            target.Receive(new DamageHit(DamageSource.Contact, 1, new Vector2(push, 2.8f)));
        }

        private void TickBurn()
        {
            if (burnPulses == 0 || --burnTimer > 0)
                return;

            if (Damage.Receive(new DamageHit(DamageSource.Burn, burnStacks)) != DamageOutcome.Ignored)
                burnPulses--;
            burnTimer = burnPulses > 0 ? 27 : 0;
            if (burnPulses == 0)
                burnStacks = 0;
        }

        private void SetPhase(Phase phase, int duration = 0)
        {
            AttackPhase = phase;
            remaining = duration;
        }

        private void UpdatePosition()
        {
            if (Boss)
            {
                float depth = Burrowing ? 2.7f : 0f;
                Position = new Vector2(Mathf.Clamp(chargeX + displacement.x, -Lane - 0.3f, Lane + 0.3f), anchor.y - depth);
                return;
            }

            float sway = Mathf.Sin((tick + patrolOffset) * 0.035f);
            float bob = Mathf.Sin((tick + patrolOffset) * 0.072f);
            float height = Flies ? bob * 0.24f : spec.Style == EnemyStyle.CaveHopper ? Mathf.Max(0f, bob) * 0.26f : 0f;
            Position = anchor + displacement + new Vector2(sway * 0.56f, height);
        }

        private Sprite ActorArt(int frame) => spec.Style switch
        {
            EnemyStyle.ReedRunner => SwampArt.Actor(0, frame),
            EnemyStyle.Skimmer => SwampArt.Actor(1, frame),
            EnemyStyle.VolleyBoss => SwampArt.Actor(2, frame),
            EnemyStyle.CaveHopper => CavesArt.Actor(0, frame),
            EnemyStyle.GlowFly => CavesArt.Actor(1, frame),
            EnemyStyle.QuakeBoss => CavesArt.Actor(2, frame),
            EnemyStyle.DuneBeetle => DesertArt.Actor(0, frame),
            EnemyStyle.SandWasp => DesertArt.Actor(1, frame),
            EnemyStyle.BurrowBoss => DesertArt.Actor(2, frame),
            EnemyStyle.LatticeTick => AlienArt.Actor(0, frame),
            EnemyStyle.ArcMote => AlienArt.Actor(1, frame),
            EnemyStyle.BeamBoss => AlienArt.Actor(2, frame),
            _ => ForestArt.Actor(5 + (int)spec.Style, frame)
        };

        private void UpdateArt()
        {
            if (root == null)
                return;

            root.position = PixelViewport.Snap(Position);
            body.flipX = direction < 0;
            int frame = AttackPhase == Phase.Warning ? 5 : AttackPhase == Phase.Attacking ? 4
                : 1 + ((tick + patrolOffset) / 8) % 2;
            body.sprite = ActorArt(frame);
            body.enabled = !Burrowing;
            warning.enabled = AttackPhase == Phase.Warning || Burrowing;
            if (spec.Style == EnemyStyle.BurrowBoss)
                warning.transform.position = PixelViewport.Snap(new Vector2(burrowTargetX, anchor.y - spec.Radius + 0.18f));
            if (leftWave != null)
            {
                leftWave.enabled = rightWave.enabled = AttackPhase == Phase.Attacking;
                Vector2 center = new(anchor.x, anchor.y - spec.Radius + 0.19f);
                leftWave.transform.position = PixelViewport.Snap(center - Vector2.right * quakeRadius);
                rightWave.transform.position = PixelViewport.Snap(center + Vector2.right * quakeRadius);
            }
            if (beam != null)
            {
                beam.enabled = AttackPhase is Phase.Warning or Phase.Attacking;
                Vector2 ray = BeamDirection();
                beam.transform.position = PixelViewport.Snap(Position + ray * (BeamLength * 0.5f));
                beam.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(ray.y, ray.x) * Mathf.Rad2Deg);
                beam.transform.localScale = new Vector3(BeamLength / beam.sprite.bounds.size.x, 1f, 1f);
                beam.color = AttackPhase == Phase.Warning ? new Color32(170, 165, 239, 110) : Color.white;
            }
            body.color = Damage.InvulnerabilitySteps > 0 ? new Color32(255, 241, 212, 255) : AttackPhase switch
            {
                Phase.Warning => new Color32(244, 154, 58, 255),
                Phase.Attacking => new Color32(242, 90, 66, 255),
                Phase.Recovery when Boss => new Color32(237, 209, 139, 255),
                _ when Shielded => new Color32(137, 203, 223, 255),
                _ => echo ? new Color32(190, 185, 255, 255) : Color.white
            };
            for (int i = 0; i < hearts.Length; i++)
                hearts[i].enabled = !Burrowing && i < Damage.Health.Current;
        }
    }
}
