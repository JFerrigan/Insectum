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
        internal static void VerifyGeneration(WorldId world, TerrainMaterial plugMaterial, TerrainMaterial otherMaterial)
        {
            int firstLevel = RunWorlds.FirstLevel(world);
            string bakeArgument = $"-insectum-bake-{world.ToString().ToLowerInvariant()}";
            string certificatePath = $"Assets/Resources/{world}Certificates.txt";
            using var fixture = new PhysicsFixture($"{world} routes", enableAbilities: false);
            var signatures = new SortedSet<string>(StringComparer.Ordinal);
            bool bake = Environment.GetCommandLineArgs().Contains(bakeArgument);
            foreach (LevelLayout layout in Candidates(world, firstLevel))
            {
                if (bake)
                {
                    foreach (Character character in Character.Roster)
                        GenerationVerification.ReplayLayout(fixture, character, layout);
                }
                signatures.Add(ForestGenerator.Signature(layout));
            }

            LevelLayout fallback = ForestGenerator.Build(77189, firstLevel, 0, false, 0, hasPocket: false, world);
            if (bake)
            {
                foreach (Character character in Character.Roster)
                    GenerationVerification.ReplayLayout(fixture, character, fallback);
                File.WriteAllText(certificatePath, $"fallback {ForestGenerator.Signature(fallback)}\n{string.Join("\n", signatures)}\n");
                AssetDatabase.ImportAsset(certificatePath, ImportAssetOptions.ForceSynchronousImport);
                Debug.Log($"Insectum baked {signatures.Count} {world} layout certificates plus fallback.");
            }

            ForestGenerator.ForestCertificates certificate = ForestGenerator.ForestCertificates.Load(world);
            Require(signatures.All(certificate.Contains), $"{world} certificates do not cover the authored layouts.");
            var rejected = new ForestGenerator.ForestCertificates($"fallback {certificate.Fallback}");
            LevelLayout knownGood = ForestGenerator.Generate(1503, firstLevel, rejected, world);
            Require(knownGood.UsedFallback && knownGood.World == world, $"The {world} fallback was not selected.");
            foreach (Character character in Character.Roster)
                GenerationVerification.ReplayLayout(fixture, character, knownGood);

            var observed = new HashSet<string>();
            for (int seed = -35; seed < 65; seed++)
            {
                int level = firstLevel + (seed + 35) % 3;
                LevelLayout layout = RunWorlds.Generate(seed, level);
                LevelLayout duplicate = RunWorlds.Generate(seed, level);
                string signature = ForestGenerator.Signature(layout);
                Require(layout.World == world && !layout.UsedFallback
                        && signature == ForestGenerator.Signature(duplicate)
                        && GenerationVerification.SameMarkers(layout.Markers, duplicate.Markers),
                    $"{world} seed {seed}/{level} did not reproduce a certified route.");
                LevelMarker[] shortcuts = layout.Markers.Where(marker => marker.Kind == LevelMarkerKind.Shortcut).ToArray();
                Require(shortcuts.Length == 1 && layout.Tiles.TryGetValue(HexGrid.CellAt(shortcuts[0].Position), out var plug)
                        && plug == plugMaterial && layout.Tiles.Values.Contains(otherMaterial),
                    $"{world} seed {seed}/{level} missed the {plugMaterial} shortcut or {otherMaterial} material.");
                GenerationVerification.VerifyGeometry(layout);
                observed.Add(signature);
                foreach (Character character in Character.Roster)
                    GenerationVerification.ReplayLayout(fixture, character, layout);
            }

            Require(observed.Count >= 20, $"Expected diverse {world} assemblies, got {observed.Count}.");
            Debug.Log($"Insectum {world} generation verified: 100 seeds, {observed.Count} layouts, five insects per seed, every checkpoint, no abilities or items, no damage.");
        }

        private static IEnumerable<LevelLayout> Candidates(WorldId world, int firstLevel)
        {
            for (int band = -1; band <= 1; band++)
            {
                foreach (bool mirrored in new[] { false, true })
                {
                    int count = 1 << ForestGenerator.RouteLength(band, hasPocket: true);
                    for (int bits = 0; bits < count; bits++)
                        yield return ForestGenerator.Build(77189, firstLevel, band, mirrored, bits, hasPocket: true, world);
                }
            }
        }

        internal static void VerifyPocket(PhysicsFixture fixture, WorldId world, ItemId prize, int direction, bool excavate)
        {
            int firstLevel = RunWorlds.FirstLevel(world);
            int seed = Enumerable.Range(660, 70).First(candidate =>
                RunWorlds.Generate(candidate, firstLevel).RouteDirection == direction);
            GameSession session = fixture.Session;
            Character character = Character.Roster[(int)(excavate ? Insect.Worm : Insect.Beetle)];
            session.StartGenerated(character, seed, firstLevel);
            Walk(fixture, () => session.AtExit && session.Motor.Grounded, 760);
            fixture.Advance(8);
            LevelLayout layout = session.Layout;
            Vector2 pocket = layout.Markers.Single(marker => marker.Kind == LevelMarkerKind.Branch).Position;
            for (int drop = 2; drop >= 0; drop--)
            {
                Vector2 landing = pocket - new Vector2(drop * HexGrid.HalfWidth * direction, drop * HexGrid.RowSpacing);
                GenerationVerification.JumpToPocketLedge(fixture, character, layout, landing);
            }

            LevelMarker shortcut = layout.Markers.Single(marker => marker.Kind == LevelMarkerKind.Shortcut);
            Vector2Int plug = HexGrid.CellAt(shortcut.Position);
            LevelFeature chest = session.Content.Features.Single(feature => feature.Kind == LevelFeatureKind.Chest);
            float before = session.Run.Energy;
            int revision = session.Terrain.Revision;
            int cost = session.Terrain.Materials.Get(session.Terrain.Cells[plug]).excavationEnergy;
            fixture.Advance(55, new PlayerCommand(move: direction));
            Require(Vector2.Distance(session.Motor.Body.position, chest.Position) > 1.35f
                    && session.Terrain.Cells.ContainsKey(plug),
                $"{world} seed {seed}: {character.Name} bypassed the intact one-cell plug by walking.");
            if (excavate)
            {
                fixture.Step(new PlayerCommand(move: direction, abilityPressed: true));
                Require(session.Terrain.Revision == revision + 1 && !session.Terrain.Cells.ContainsKey(plug)
                        && session.Run.Energy == before - cost,
                    $"{world} seed {seed}, shortcut {plug}: a real excavation did not remove one tile for {cost} energy.");
            }
            else
            {
                fixture.Step(new PlayerCommand(move: direction, jumpPressed: true, jumpHeld: true));
                fixture.Advance(30, new PlayerCommand(move: direction, jumpHeld: true));
                Require(session.Terrain.Revision == revision && session.Terrain.Cells.ContainsKey(plug)
                        && session.Run.Energy == before,
                    $"{world} seed {seed}, shortcut {plug}: an ordinary jump altered terrain or energy.");
            }

            Walk(fixture, () => Vector2.Distance(session.Motor.Body.position, chest.Position) < 1.18f, 120);
            fixture.Step(new PlayerCommand(interactPressed: true));
            Require(!chest.Available && session.Run.Inventory.Count(prize) == 1,
                $"{world} seed {seed}: {character.Name} could not pass the plug and claim its side chest.");
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

        internal static void Walk(PhysicsFixture fixture, Func<bool> reached, int limit)
        {
            GameSession session = fixture.Session;
            for (int i = 0; i < limit && !reached() && session.Phase == RunPhase.Playing; i++)
                fixture.Step(new PlayerCommand(move: session.Layout.RouteDirection));
            Require(reached(), $"{session.Run.World} seed {session.Run.Seed}/{session.Run.LevelNumber} stopped at {session.Motor.Body.position} with {session.Run.Health} hearts.");
        }
    }
}
