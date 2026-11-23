using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static Insectum.Game.Editor.ProductionVerification;

namespace Insectum.Game.Editor
{
    internal static class BiomeVerification
    {
        // Levels 1 to 3 of every world are room-template tunnels. Each sampled level must reproduce from its
        // seed, carry one plug of the world's soft material under a chest vault, and let all five insects cross
        // it damage-free with ordinary movement; one insect also visits every side tunnel.
        internal static void VerifyGeneration(WorldId world, TerrainMaterial plugMaterial)
        {
            int firstLevel = RunWorlds.FirstLevel(world);
            using var fixture = new PhysicsFixture($"{world} tunnels", enableAbilities: false);
            var observed = new HashSet<string>();
            var templates = new HashSet<string>();
            int crossings = 0;
            for (int seed = -6; seed < 6; seed++)
            {
                int level = firstLevel + (seed + 6) % (RunWorlds.LevelsPerWorld - 1);
                LevelLayout layout = RunWorlds.Generate(seed, level);
                LevelLayout duplicate = RunWorlds.Generate(seed, level);
                string signature = ForestGenerator.Signature(layout);
                Require(layout.World == world && layout.Tunnel != null && !layout.UsedFallback
                        && signature == ForestGenerator.Signature(duplicate)
                        && GenerationVerification.SameMarkers(layout.Markers, duplicate.Markers),
                    $"{world} seed {seed}/{level} did not reproduce its tunnel.");
                LevelMarker plug = layout.Markers.Single(marker => marker.Kind == LevelMarkerKind.Shortcut);
                Require(layout.Tiles.TryGetValue(HexGrid.CellAt(plug.Position), out TerrainMaterial material)
                        && material == plugMaterial,
                    $"{world} seed {seed}/{level} has no {plugMaterial} plug beneath its chest vault.");
                observed.Add(signature);
                foreach (MacroChunk chunk in layout.Chunks.Where(chunk => chunk.Ports != MacroPort.None))
                    templates.Add(chunk.TemplateId);
                Cross(fixture, layout);
                crossings += Character.Roster.Length;
            }

            LevelLayout fallback = ForestTunnelLayout.Generate(77189, firstLevel, world, forceFallback: true);
            Require(fallback.UsedFallback && fallback.World == world, $"The {world} tunnel fallback was not used.");
            Cross(fixture, fallback);
            Require(observed.Count == 12, $"Expected twelve distinct {world} tunnels, got {observed.Count}.");
            Debug.Log($"Insectum {world} tunnels verified: 12 seeds and the fallback, {crossings + 5} damage-free crossings, "
                + $"side tunnels explored, {templates.Count} room templates in use.");
        }

        private static void Cross(PhysicsFixture fixture, LevelLayout layout)
        {
            foreach (Character character in Character.Roster)
            {
                fixture.Session.Start(character, layout.RunSeed, layout);
                TunnelWalker.CrossRoute(fixture, explore: character.Insect == Insect.FireAnt);
                Require(fixture.Session.Run.Health == character.MaxHealth,
                    $"{layout.World} seed {layout.RunSeed}/{layout.LevelNumber}: {character.Name} took damage crossing the tunnel.");
                fixture.Step(new PlayerCommand(interactPressed: true));
                Require(fixture.Session.Phase == RunPhase.Cleared,
                    $"{layout.World} seed {layout.RunSeed}/{layout.LevelNumber} could not interact with its exit.");
            }
        }

        // Walks to the chest vault that stands on the level's plug and opens the chest. Worm then digs down
        // through the plug for the material's energy cost and uncovers the buried Nektar.
        internal static void VerifyVault(PhysicsFixture fixture, WorldId world, ItemId prize, bool excavate)
        {
            const int seed = 660;
            int firstLevel = RunWorlds.FirstLevel(world);
            GameSession session = fixture.Session;
            Character character = Character.Roster[(int)(excavate ? Insect.Worm : Insect.Beetle)];
            session.StartGenerated(character, seed, firstLevel);
            LevelMarker shortcut = session.Layout.Markers.Single(marker => marker.Kind == LevelMarkerKind.Shortcut);
            Vector2Int plug = HexGrid.CellAt(shortcut.Position);
            Vector2 center = HexGrid.Center(plug);
            LevelFeature chest = session.Content.Features.Where(feature => feature.Kind == LevelFeatureKind.Chest)
                .OrderBy(feature => Vector2.Distance(feature.Position, shortcut.Position)).First();
            Require(session.Content.BuriedNektar.ContainsKey(plug) && session.Terrain.Cells[plug] != TerrainMaterial.Reinforced,
                $"{world} seed {seed}: the chest vault has no buried plug.");

            TunnelWalker.WalkTo(fixture, chest.Position, 1.18f);
            fixture.Step(new PlayerCommand(interactPressed: true));
            Require(!chest.Available && session.Run.Inventory.Count(prize) == 1,
                $"{world} seed {seed}: {character.Name} could not claim the vault chest.");

            float before = session.Run.Energy;
            int cost = session.Terrain.Materials.Get(session.Terrain.Cells[plug]).excavationEnergy;
            if (!excavate)
            {
                fixture.Advance(30, new PlayerCommand(jumpPressed: true, jumpHeld: true));
                Require(session.Terrain.Cells.ContainsKey(plug) && session.Run.Energy == before,
                    $"{world} seed {seed}: ordinary movement disturbed the plug or spent energy.");
                return;
            }

            // Stand on the vault floor above the plug and hold the downward dig. Reinforced rock is free, so only
            // the plug costs energy.
            Vector2 landing = session.Layout.Markers.Single(marker => marker.Kind == LevelMarkerKind.Branch
                && marker.Chunk == shortcut.Chunk).Position;
            session.Motor.Warp(landing + Vector2.up * (character.Radius + 0.06f));
            fixture.Advance(20);
            for (int step = 0; step < 90 && session.Terrain.Cells.ContainsKey(plug); step++)
                fixture.Step(new PlayerCommand(aim: -1, abilityHeld: true));
            Require(!session.Terrain.Cells.ContainsKey(plug) && session.Run.Energy == before - cost
                    && session.Content.Features.Any(feature => feature.Kind == LevelFeatureKind.Nektar && feature.Position == center),
                $"{world} seed {seed}: Worm did not dig the {cost}-energy plug and uncover its Nektar. Energy {before} to {session.Run.Energy}.");
        }

        internal static void MoveTo(PhysicsFixture fixture, float x, Func<bool> reached, int limit)
        {
            GameSession session = fixture.Session;
            for (int i = 0; i < limit && !reached() && session.Phase == RunPhase.Playing; i++)
            {
                float dx = x - session.Motor.Body.position.x;
                fixture.Step(new PlayerCommand(move: Mathf.Abs(dx) > 0.11f ? Math.Sign(dx) : 0));
            }
            Require(reached(), $"{session.Run.World} movement stopped at {session.Motor.Body.position}, targeting {x:F2}.");
        }

        internal static void Walk(PhysicsFixture fixture, Func<bool> reached, int limit, bool useHeavy = false)
        {
            GameSession session = fixture.Session;
            if (session.Layout?.Tunnel != null)
            {
                TunnelWalker.Walk(fixture, reached, useHeavy);
                return;
            }
            for (int i = 0; i < limit && !reached() && session.Phase == RunPhase.Playing; i++)
                fixture.Step(TrailCommand(session, useHeavy));
            Require(reached(), $"{session.Run.World} seed {session.Run.Seed}/{session.Run.LevelNumber} stopped at {session.Motor.Body.position} with {session.Run.Health} hearts.");
        }

        private static PlayerCommand TrailCommand(GameSession session, bool useHeavy)
        {
            int move = session.Layout.RouteDirection;
            Vector2 position = session.Motor.Body.position;
            bool heavy = useHeavy && session.Run.Energy >= CharacterTuning.Current.fireAntAttackEnergy;
            float reach = heavy ? 2.1f : 1.3f;
            float halfWidth = heavy ? 0.83f : 0.38f;
            foreach (LevelEnemy enemy in session.Content.Enemies)
            {
                if (!enemy.Damage.Health.Alive)
                    continue;

                Vector2 offset = enemy.Position - position;
                float ahead = move * offset.x;
                float radius = enemy.Damage.Radius;
                if (enemy.Boss)
                {
                    if (ahead > 0f && ahead < radius + 1.35f && Mathf.Abs(offset.y) < 0.85f
                        && session.Motor.Grounded)
                        return new PlayerCommand(move: move, jumpPressed: true, jumpHeld: true);
                    continue;
                }

                if (ahead >= 0f && ahead < reach + radius && Mathf.Abs(offset.y) < halfWidth + radius)
                    return new PlayerCommand(move: session.Motor.Facing == move ? 0 : move,
                        attackPressed: true, abilityPressed: heavy);
                if (Mathf.Abs(offset.x) < halfWidth + radius && offset.y > 0f && offset.y < reach + radius)
                    return new PlayerCommand(aim: 1, attackPressed: true, abilityPressed: heavy);
                if (session.Run.Character.Insect == Insect.Dragonfly && ahead > 0f && ahead < 3f
                    && offset.y is < -0.65f and > -4.5f && session.Motor.Grounded)
                    return new PlayerCommand(move: move, jumpPressed: true, jumpHeld: true);
            }

            return new PlayerCommand(move: move, jumpHeld: true);
        }
    }
}
