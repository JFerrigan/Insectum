using UnityEngine;

namespace Insectum.Game
{
    internal enum ForestProp
    {
        Nektar, HeartSap, EnergySap, Shop, Chest, BossReward,
        Exit, LockedExit, Thorn, ThornWarning, ThornStrike, Dart,
        Warning, Blast, Slash, SidePath
    }

    internal static class ForestArt
    {
        private static Texture2D actors;
        private static Texture2D props;
        private static Texture2D terrain;
        private static Texture2D cavern;
        private static Texture2D glyphs;
        private static readonly Sprite[,] actorFrames = new Sprite[8, 6];
        private static readonly Sprite[] propSprites = new Sprite[16];
        private static Sprite backdrop;

        internal static Texture2D Actors => actors ??= Load("Actors");
        internal static Texture2D Terrain => terrain ??= Load("Terrain");
        internal static Texture2D Cavern => cavern ??= Load("Cavern");
        internal static Texture2D Glyphs => glyphs ??= Load("Glyphs");

        internal static Sprite Actor(int row, int frame) =>
            actorFrames[row, frame] ??= Frame(Actors, frame, row, 32, 24, new Vector2(0.5f, 0.18f));

        internal static Sprite Prop(ForestProp prop)
        {
            int index = (int)prop;
            props ??= Load("Props");
            return propSprites[index] ??= Frame(props, index % 8, index / 8, 32, 32, Vector2.one * 0.5f);
        }

        internal static Sprite Backdrop =>
            backdrop ??= Frame(Cavern, 0, 0, Cavern.width, Cavern.height, Vector2.one * 0.5f);

        internal static Rect TexCoords(Texture2D atlas, int x, int y, int width, int height) =>
            new((float)x / atlas.width, 1f - (float)(y + height) / atlas.height,
                (float)width / atlas.width, (float)height / atlas.height);

        internal static SpriteRenderer Place(Transform parent, string name, Vector2 position, Sprite sprite, int order)
        {
            var art = new GameObject(name);
            art.transform.SetParent(parent, false);
            art.transform.localPosition = PixelViewport.Snap(position);
            var renderer = art.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        internal static Sprite Frame(Texture2D atlas, int x, int y, int width, int height, Vector2 pivot) =>
            Sprite.Create(atlas, new Rect(x * width, atlas.height - (y + 1) * height, width, height),
                pivot, PixelViewport.PixelsPerUnit, 0, SpriteMeshType.FullRect);

        internal static Texture2D Load(string name) => Resources.Load<Texture2D>($"PixelArt/{name}");
    }

    internal static class PixelViewport
    {
        internal const int Width = 320;
        internal const int Height = 180;
        internal const float PixelsPerUnit = 16f;

        internal static Rect ScreenRect
        {
            get
            {
                int scale = Mathf.Max(1, Mathf.Min(Screen.width / Width, Screen.height / Height));
                int width = Width * scale;
                int height = Height * scale;
                return new Rect(Mathf.Floor((Screen.width - width) * 0.5f), Mathf.Floor((Screen.height - height) * 0.5f),
                    width, height);
            }
        }

        internal static Vector2 Pointer
        {
            get
            {
                Rect viewport = ScreenRect;
                return (new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y) - viewport.position)
                    / (viewport.width / Width);
            }
        }

        internal static Vector2 Snap(Vector2 position) =>
            new(Mathf.Round(position.x * PixelsPerUnit) / PixelsPerUnit,
                Mathf.Round(position.y * PixelsPerUnit) / PixelsPerUnit);
    }

    internal sealed class InsectActorView : MonoBehaviour
    {
        private GameSession session;
        private SpriteRenderer body;
        private SpriteRenderer slash;

        internal static void Attach(Transform actor, GameSession session)
        {
            var view = actor.gameObject.AddComponent<InsectActorView>();
            view.session = session;
            view.body = ForestArt.Place(actor, "Insect", Vector2.zero, ForestArt.Actor((int)session.Run.Character.Insect, 0), 5);
            view.slash = ForestArt.Place(actor, "Melee swing", Vector2.zero, ForestArt.Prop(ForestProp.Slash), 6);
            view.slash.enabled = false;
        }

        private void LateUpdate()
        {
            PlayerMotor motor = session.Motor;
            Insect insect = session.Run.Character.Insect;
            Vector2 position = motor.Body.position;
            body.transform.position = PixelViewport.Snap(position - Vector2.up * session.Run.Character.Radius);
            body.flipX = motor.Facing < 0;
            bool attack = session.MeleePoseSteps > 0;
            body.sprite = ForestArt.Actor((int)insect, Pose(motor, insect, attack));
            body.color = session.PlayerDamage.InvulnerabilitySteps > 0 && Time.frameCount % 6 < 3
                ? new Color32(255, 162, 173, 255) : Color.white;
            slash.enabled = attack;
            if (!attack)
                return;

            Vector2 direction = session.MeleeAim;
            float reachBonus = session.Run.Inventory.MeleeReachBonus;
            float offset = (session.HeavyPose ? 0.93f : 0.56f) + 0.45f * reachBonus;
            float size = (session.HeavyPose ? 1.35f : 1f) + 0.30f * reachBonus;
            slash.transform.position = PixelViewport.Snap(position + direction * offset);
            slash.transform.localScale = Vector3.one * size;
            slash.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        private int Pose(PlayerMotor motor, Insect insect, bool attack)
        {
            if (session.Abilities.Shelled)
                return 5;
            if (attack)
                return session.HeavyPose ? 5 : 4;
            if (session.Abilities.ArtCueSteps > 0)
                return 5;
            int step = 1 + (Mathf.FloorToInt(Time.time * 12f) & 1);
            if (session.Abilities.Climbing || session.Abilities.Flying)
                return step;
            if (!motor.Grounded)
                return 3;
            return Mathf.Abs(motor.Body.linearVelocity.x) > 0.45f ? step : insect == Insect.Dragonfly ? (step - 1) : 0;
        }
    }
}
