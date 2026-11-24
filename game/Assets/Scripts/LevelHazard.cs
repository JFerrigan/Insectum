using System.Collections.Generic;
using UnityEngine;

namespace Insectum.Game
{
    internal enum HazardKind
    {
        Bramble
    }

    // A hazard a room template carves into its geometry. Hazards belong to the level layout rather than its
    // content, so every crossing meets them, with or without enemies and pickups.
    internal readonly struct TunnelHazard
    {
        internal TunnelHazard(HazardKind kind, Rect area, bool hanging, Vector2Int chunk)
        {
            Kind = kind;
            Area = area;
            Hanging = hanging;
            Chunk = chunk;
        }

        internal HazardKind Kind { get; }
        internal Rect Area { get; }
        // Hanging hazards grow down from a roof. The rest grow up from a floor.
        internal bool Hanging { get; }
        internal Vector2Int Chunk { get; }
    }

    // Brambles are always armed. Touching one costs a heart and throws the player clear, followed by the usual
    // invulnerability. A downward strike that reaches a floor bramble bounces the player off it unhurt.
    internal sealed class LevelHazards
    {
        internal const float BrambleHeight = 0.5f;
        // The hit test shrinks the player a little, so grazing the art's outermost thorns is free.
        private const float Forgiveness = 0.08f;
        private const float TileWidth = 0.5f;
        private static readonly Vector2 FloorThrow = new(3.2f, 8.5f);
        private static readonly Vector2 RoofThrow = new(3.2f, -2.5f);
        private readonly List<TunnelHazard> hazards;

        internal LevelHazards(Transform parent, Sprite pixel, IReadOnlyList<TunnelHazard> hazards)
        {
            this.hazards = new List<TunnelHazard>(hazards);
            if (pixel == null)
                return;

            var root = new GameObject("Hazards").transform;
            root.SetParent(parent, false);
            foreach (TunnelHazard hazard in hazards)
                PlaceArt(root, hazard);
        }

        internal IReadOnlyList<TunnelHazard> Hazards => hazards;

        internal void Step(DamageReceiver player, Vector2 velocity)
        {
            float reach = player.Radius - Forgiveness;
            foreach (TunnelHazard hazard in hazards)
            {
                Rect area = hazard.Area;
                Vector2 position = player.Position;
                Vector2 closest = new(Mathf.Clamp(position.x, area.xMin, area.xMax), Mathf.Clamp(position.y, area.yMin, area.yMax));
                if ((position - closest).sqrMagnitude >= reach * reach)
                    continue;

                // Throw the player toward the nearer end of the bramble, replacing its current speed.
                float away = position.x < area.center.x ? -1f : 1f;
                Vector2 thrown = hazard.Hanging ? RoofThrow : FloorThrow;
                player.Receive(new DamageHit(DamageSource.Trap, 1, new Vector2(away * thrown.x, thrown.y) - velocity));
                return;
            }
        }

        // True when a downward strike reaches the top of a floor bramble.
        internal bool Pogo(MeleeStrike strike)
        {
            Vector2 origin = strike.Origin;
            foreach (TunnelHazard hazard in hazards)
            {
                Rect area = hazard.Area;
                if (!hazard.Hanging && origin.x + strike.HalfWidth >= area.xMin && origin.x - strike.HalfWidth <= area.xMax
                    && origin.y >= area.yMax && origin.y - strike.Reach <= area.yMax)
                    return true;
            }
            return false;
        }

        // Half-unit tiles with capped ends. Hanging brambles use the floor art flipped onto the roof.
        private static void PlaceArt(Transform root, TunnelHazard hazard)
        {
            Rect area = hazard.Area;
            int tiles = Mathf.Max(1, Mathf.RoundToInt(area.width / TileWidth));
            float width = area.width / tiles;
            float y = hazard.Hanging ? area.yMax : area.yMin;
            for (int i = 0; i < tiles; i++)
            {
                float x = area.xMin + width * (i + 0.5f);
                int cel = tiles > 1 && i == 0 ? ForestArt.BrambleLeft
                    : tiles > 1 && i == tiles - 1 ? ForestArt.BrambleRight
                    : Mathf.Abs(Mathf.RoundToInt(x * 7f + y * 13f)) % ForestArt.BrambleVariants;
                SpriteRenderer art = ForestArt.Place(root, "Bramble", new Vector2(x, y), ForestArt.Bramble(cel), 2);
                art.flipY = hazard.Hanging;
            }
        }
    }
}
