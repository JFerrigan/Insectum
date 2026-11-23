using UnityEngine;

namespace Insectum.Game
{
    internal enum TrapStyle
    {
        Thorn,
        Gas,
        Crystal,
        Heat,
        Arc
    }

    internal sealed class LevelTrap
    {
        internal const int Cycle = 150;
        private const int WarningStart = 70;
        private const int StrikeStart = 103;
        private const int StrikeEnd = 134;
        private readonly SpriteRenderer art;
        private readonly TrapStyle style;
        private readonly Vector2 landing;
        private bool struck;

        public Vector2 Position { get; }
        internal TrapStyle Style => style;
        public bool Warning { get; private set; }
        public bool Extended { get; private set; }

        public LevelTrap(Transform parent, Sprite pixel, Vector2 landing, TrapStyle style = TrapStyle.Thorn)
        {
            this.style = style;
            this.landing = landing;
            Position = landing + Vector2.up * (style == TrapStyle.Thorn ? 0.25f : 0.50f);
            if (pixel == null)
                return;

            string name = style switch
            {
                TrapStyle.Gas => "Gas vent",
                TrapStyle.Crystal => "Falling crystal",
                TrapStyle.Heat => "Cinder jet",
                TrapStyle.Arc => "Static column",
                _ => "Thorn nest"
            };
            art = ForestArt.Place(parent, name, ArtPosition(0), TrapArt(0), 3);
            if (style == TrapStyle.Arc)
                art.transform.localScale = new Vector3(1f, 1.5f, 1f);
        }

        internal void Step(int tick, DamageReceiver player)
        {
            (int cycle, int warn, int strike, int end, float width, float height, float uplift) = style switch
            {
                TrapStyle.Gas => (180, 58, 102, 148, 0.88f, 0.95f, 3.8f),
                TrapStyle.Crystal => (172, 56, 100, 122, 0.62f, 1.1f, 4.6f),
                TrapStyle.Heat => (164, 61, 96, 116, 1.00f, 0.67f, 2.6f),
                TrapStyle.Arc => (176, 56, 102, 140, 0.60f, 1.82f, 5.2f),
                _ => (Cycle, WarningStart, StrikeStart, StrikeEnd, 0.62f, 0.75f, 6f)
            };
            int phase = tick % cycle;
            Warning = phase >= warn && phase < strike;
            Extended = phase >= strike && phase < end;
            if (!Extended)
                struck = false;
            else if (!struck)
            {
                Vector2 offset = player.Position - Position;
                if (Mathf.Abs(offset.x) < width && Mathf.Abs(offset.y) < height)
                {
                    struck = true;
                    float push = style == TrapStyle.Heat ? Mathf.Sign(offset.x) * 5.4f : 0f;
                    player.Receive(new DamageHit(DamageSource.Trap, 1, new Vector2(push, uplift)));
                }
            }

            if (art != null)
            {
                int frame = Extended ? 2 : Warning ? 1 : 0;
                art.sprite = TrapArt(frame);
                art.transform.localPosition = PixelViewport.Snap(ArtPosition(frame));
            }
        }

        private Vector2 ArtPosition(int phase) => landing + Vector2.up * (style switch
        {
            TrapStyle.Crystal when phase < 2 => 2.20f,
            TrapStyle.Arc => 1.27f,
            _ => 0.56f
        });

        private Sprite TrapArt(int phase) => style switch
        {
            TrapStyle.Gas => SwampArt.Vent(phase),
            TrapStyle.Crystal => CavesArt.Crystal(phase),
            TrapStyle.Heat => DesertArt.Jet(phase),
            TrapStyle.Arc => AlienArt.Arc(phase),
            _ => ForestArt.Prop((ForestProp)((int)ForestProp.Thorn + phase))
        };
    }

    internal sealed class LevelDart
    {
        private readonly SpriteRenderer art;
        private readonly Vector2 velocity;
        private Vector2 position;
        private int remaining = 115;

        public LevelDart(Transform parent, Sprite pixel, Vector2 origin, Vector2 direction)
        {
            position = origin;
            velocity = direction * 5.2f;
            art = pixel == null ? null : ForestArt.Place(parent, "Spore dart", origin, ForestArt.Prop(ForestProp.Dart), 4);
            if (art != null)
            {
                art.transform.localScale = Vector3.one * 0.65f;
                art.transform.Rotate(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            }
        }

        internal bool Step(PhysicsScene2D physics, DamageReceiver player)
        {
            Vector2 delta = velocity * PlayerMotor.StepSeconds;
            RaycastHit2D impact = physics.Raycast(position, delta.normalized, delta.magnitude);
            Vector2 destination = position + delta;
            float t = Mathf.Clamp01(Vector2.Dot(player.Position - position, delta) / delta.sqrMagnitude);
            bool hitsPlayer = (player.Position - (position + delta * t)).sqrMagnitude <= player.Radius * player.Radius
                && (impact.collider == null || impact.distance >= t * delta.magnitude - player.Radius);
            if (hitsPlayer)
                player.Receive(new DamageHit(DamageSource.Projectile, 1,
                    new Vector2(Mathf.Sign(velocity.x) * 3f, 2f)));
            if (hitsPlayer || impact.collider != null || --remaining <= 0)
            {
                if (art != null)
                    Object.Destroy(art.gameObject);
                return false;
            }

            position = destination;
            if (art != null)
                art.transform.position = PixelViewport.Snap(position);
            return true;
        }
    }
}
