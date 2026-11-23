using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static Insectum.Game.Editor.ProductionVerification;
using Object = UnityEngine.Object;

namespace Insectum.Game.Editor
{
    internal static class GenerationVerification
    {
        private const string BakeArgument = "-insectum-bake-forest";
        private const string CertificatePath = "Assets/Resources/ForestCertificates.txt";

        internal static void Verify()
        {
            using var fixture = new PhysicsFixture("Forest route verification", enableAbilities: false);
            VerifyPair(fixture);

            if (Environment.GetCommandLineArgs().Contains(BakeArgument))
                Bake(fixture);

            ForestGenerator.ForestCertificates certificates = ForestGenerator.ForestCertificates.Load();
            VerifyCertificateCoverage(certificates);
            VerifySeeds(fixture);
            VerifyPockets(fixture);
            VerifyFallback(fixture, certificates);
        }

        private static void VerifyPair(PhysicsFixture fixture)
        {
            foreach (bool mirrored in new[] { false, true })
            {
                var tiles = new Dictionary<Vector2Int, TerrainMaterial>();
                ForestTemplates.StampDescent(tiles, Vector2Int.zero, mirrored);
                ForestTemplates.StampDescent(tiles, Vector2Int.right, mirrored);
                var terrain = new HexTerrain(fixture.World.transform, tiles, TerrainMaterials.Load());
                foreach (Character character in Character.Roster)
                    ReplayPair(fixture, character, mirrored);
                terrain.Root.SetActive(false);
                Object.Destroy(terrain.Root);
            }

            Debug.Log("Insectum authored crossing verified: both directions, all five insects, damage-free.");
        }

        private static void ReplayPair(PhysicsFixture fixture, Character character, bool mirrored)
        {
            var actor = new GameObject("Forest crossing motor");
            actor.transform.SetParent(fixture.World.transform, false);
            actor.transform.localPosition = ForestTemplates.Landing(Vector2Int.zero, ForestTemplates.Entry, mirrored)
                + Vector2.up * (character.Radius + 0.06f);
            var motor = new PlayerMotor(actor, character.Radius, fixture.Surface);
            Vector2 checkpoint = ForestTemplates.Landing(Vector2Int.right, ForestTemplates.Entry, mirrored);
            Vector2 finish = ForestTemplates.Landing(Vector2Int.right, ForestTemplates.Exit, mirrored);
            var command = new PlayerCommand(move: mirrored ? -1 : 1);
            float peakImpact = 0f;
            bool crossed = false;
            motor.Landed += speed => peakImpact = Mathf.Max(peakImpact, speed);
            Physics2D.SyncTransforms();

            for (int tick = 0; tick < 500; tick++)
            {
                motor.Step(command);
                fixture.Simulate();
                Vector2 feet = Feet(motor, character);
                crossed |= motor.Grounded && Vector2.Distance(feet, checkpoint) < 0.9f;
                if (crossed && motor.Grounded && Vector2.Distance(feet, finish) < 0.7f)
                {
                    Require(peakImpact < PlayerMotor.FallDamageSpeed,
                        $"Authored crossing {character.Name} mirrored {mirrored}: impact {peakImpact:F2}.");
                    actor.SetActive(false);
                    Object.Destroy(actor);
                    return;
                }
            }

            Require(false,
                $"Authored crossing {character.Name} mirrored {mirrored}: checkpoint {crossed}, at {motor.Body.position}, checkpoint {checkpoint}, finish {finish}, impact {peakImpact:F2}.");
        }

        private static IEnumerable<LevelLayout> CandidateLayouts()
        {
            for (int band = -1; band <= 1; band++)
            {
                foreach (bool mirrored in new[] { false, true })
                {
                    int count = 1 << ForestGenerator.RouteLength(band, hasPocket: true);
                    for (int bits = 0; bits < count; bits++)
                        yield return ForestGenerator.Build(99173, 1, band, mirrored, bits, hasPocket: true);
                }
            }
        }

        private static void Bake(PhysicsFixture fixture)
        {
            var signatures = new SortedSet<string>(StringComparer.Ordinal);
            foreach (LevelLayout layout in CandidateLayouts())
            {
                foreach (Character character in Character.Roster)
                    ReplayLayout(fixture, character, layout);
                signatures.Add(ForestGenerator.Signature(layout));
            }

            LevelLayout fallback = ForestGenerator.Build(99173, 1, 0, false, 0, hasPocket: false);
            foreach (Character character in Character.Roster)
                ReplayLayout(fixture, character, fallback);
            File.WriteAllText(CertificatePath, $"fallback {ForestGenerator.Signature(fallback)}\n{string.Join("\n", signatures)}\n");
            AssetDatabase.ImportAsset(CertificatePath, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"Insectum baked {signatures.Count} Forest layout certificates plus fallback.");
        }

        private static void VerifyCertificateCoverage(ForestGenerator.ForestCertificates certificates)
        {
            foreach (LevelLayout layout in CandidateLayouts())
            {
                string signature = ForestGenerator.Signature(layout);
                Require(certificates.Contains(signature),
                    $"Forest templates have changed. Missing certification {signature} for {TemplateNames(layout)}. Run verification with {BakeArgument}.");
            }
        }

        private static void VerifySeeds(PhysicsFixture fixture)
        {
            var observed = new HashSet<string>();
            for (int seed = -45; seed < 55; seed++)
            {
                const int number = 1;
                LevelLayout first = ForestGenerator.Generate(seed, number);
                LevelLayout duplicate = ForestGenerator.Generate(seed, number);
                string signature = ForestGenerator.Signature(first);
                Require(signature == ForestGenerator.Signature(duplicate)
                        && SameMarkers(first.Markers, duplicate.Markers) && !first.UsedFallback,
                    $"Forest seed {seed}/{number} did not repeat or used the fallback.");
                var nextLevel = new LevelSeeds(seed, number + 1);
                Require(first.Seeds.Layout != first.Seeds.Encounters && first.Seeds.Layout != first.Seeds.Loot
                        && first.Seeds.Encounters != first.Seeds.Loot && first.Seeds.Layout != nextLevel.Layout
                        && first.Seeds.Encounters != nextLevel.Encounters && first.Seeds.Loot != nextLevel.Loot,
                    $"Forest seed {seed}/{number} did not produce distinct random streams.");
                VerifyGeometry(first);
                observed.Add(signature);

                foreach (Character character in Character.Roster)
                    ReplayLayout(fixture, character, first);
            }

            Require(observed.Count >= 20, $"Expected diverse Forest assemblies, got {observed.Count}.");
            Debug.Log($"Insectum Forest generation verified: 100 seeds, {observed.Count} layouts, five insects per seed, every checkpoint, no abilities, no damage.");
        }

        private static void VerifyFallback(PhysicsFixture fixture, ForestGenerator.ForestCertificates certificates)
        {
            var noCandidates = new ForestGenerator.ForestCertificates($"fallback {certificates.Fallback}");
            LevelLayout fallback = ForestGenerator.Generate(1931, 1, noCandidates);
            Require(fallback.UsedFallback && !fallback.Chunks.Any(chunk => chunk.IsBranch),
                "Forest generator did not use the known-good fallback after rejecting candidates.");
            foreach (Character character in Character.Roster)
                ReplayLayout(fixture, character, fallback);
            Debug.Log("Insectum Forest fallback verified after bounded rejection of all candidates.");
        }

        internal static void VerifyGeometry(LevelLayout layout)
        {
            var cells = new HashSet<Vector2Int>();
            var byCoordinate = layout.Chunks.ToDictionary(chunk => chunk.Coordinate);
            foreach (MacroChunk chunk in layout.Chunks)
            {
                int count = 0;
                int radius = HexGrid.MacroRadius;
                for (int r = -radius; r <= radius; r++)
                {
                    for (int q = -radius; q <= radius; q++)
                    {
                        Vector2Int cell = chunk.CenterCell + new Vector2Int(q, r);
                        if (!chunk.Contains(cell))
                            continue;
                        Require(cells.Add(cell), $"{layout.World} seed {layout.RunSeed} has overlapping macro chunks at {cell}.");
                        count++;
                    }
                }
                Require(count == 91, $"{layout.World} seed {layout.RunSeed} chunk {chunk.Coordinate} does not own exactly 91 cells.");

                for (int side = 0; side < HexGrid.SideCount; side++)
                {
                    if ((chunk.Ports & (MacroPort)(1 << side)) == 0)
                        continue;
                    Require(byCoordinate.TryGetValue(HexGrid.Neighbor(chunk.Coordinate, side), out MacroChunk neighbor)
                            && (neighbor.Ports & (MacroPort)(1 << ((side + 3) % HexGrid.SideCount))) != 0,
                        $"{layout.World} seed {layout.RunSeed}, template {chunk.TemplateId} has a nonreciprocal port on side {side}.");
                }
            }

            Require(layout.Chunks.Count == 19 && layout.Tiles.Keys.All(cells.Contains)
                    && layout.Chunks.Count(chunk => chunk.IsBranch) == 1
                    && layout.Checkpoints.Count == layout.Chunks.Count(chunk => chunk.MainRoute) - 1,
                $"{layout.World} seed {layout.RunSeed} has invalid chunk coverage, branch, or entry landmarks.");
        }

        internal static bool SameMarkers(IReadOnlyList<LevelMarker> first, IReadOnlyList<LevelMarker> second) =>
            first.Count == second.Count && first.Zip(second, (a, b) =>
                a.Kind == b.Kind && a.Position == b.Position && a.Chunk == b.Chunk).All(same => same);

        private static void VerifyPockets(PhysicsFixture fixture)
        {
            foreach (bool mirrored in new[] { false, true })
            {
                LevelLayout layout = ForestGenerator.Build(8109, 1, 0, mirrored, 0, hasPocket: true);
                Vector2 pocket = layout.Markers.Single(marker => marker.Kind == LevelMarkerKind.Branch).Position;
                foreach (Character character in Character.Roster)
                {
                    ReachExit(fixture, character, layout);
                    fixture.Advance(8);
                    for (int rise = 2; rise >= 0; rise--)
                    {
                        Vector2 landing = pocket - new Vector2(rise * HexGrid.HalfWidth * layout.RouteDirection,
                            rise * HexGrid.RowSpacing);
                        JumpToPocketLedge(fixture, character, layout, landing);
                    }

                    GameSession session = fixture.Session;
                    for (int tick = 0; tick < 160 && !(session.AtExit && session.Motor.Grounded); tick++)
                    {
                        float deltaX = layout.Exit.x - session.Motor.Body.position.x - 0.32f * layout.RouteDirection;
                        fixture.Step(new PlayerCommand(move: Mathf.Abs(deltaX) > 0.16f ? Math.Sign(deltaX) : 0));
                    }

                    Require(session.AtExit && session.Motor.Grounded && session.Run.Health == character.MaxHealth,
                        $"Forest pocket seed {layout.RunSeed}, {character.Name}, direction {layout.RouteDirection} could not return safely to the exit from {session.Motor.Body.position}; health {session.Run.Health}.");
                    fixture.Step(new PlayerCommand(interactPressed: true));
                    Require(session.Phase == RunPhase.Cleared, "The Forest pocket return could not interact with the exit.");
                }
            }

            Debug.Log("Insectum optional Forest pocket verified with ordinary jumps and safe exit returns in both orientations for all five insects.");
        }

        internal static void JumpToPocketLedge(PhysicsFixture fixture, Character character, LevelLayout layout, Vector2 landing)
        {
            for (int tick = 0; tick < 80; tick++)
            {
                PlayerMotor motor = fixture.Session.Motor;
                float deltaX = landing.x - motor.Body.position.x;
                int move = Mathf.Abs(deltaX) > 0.18f ? Math.Sign(deltaX) : 0;
                fixture.Step(new PlayerCommand(move: move, jumpPressed: tick == 0, jumpHeld: true));
                Vector2 feet = Feet(motor, character);
                if (motor.Grounded && Vector2.Distance(feet, landing) < 0.58f)
                    return;
            }

            Require(false, $"Forest pocket seed {layout.RunSeed}, {character.Name}, direction {layout.RouteDirection} could not jump to {landing}; body at {fixture.Session.Motor.Body.position}.");
        }

        internal static void ReplayLayout(PhysicsFixture fixture, Character character, LevelLayout layout)
        {
            ReachExit(fixture, character, layout);
            fixture.Step(new PlayerCommand(interactPressed: true));
            Require(fixture.Session.Phase == RunPhase.Cleared, $"{layout.World} seed {layout.RunSeed} could not interact with its exit.");
        }

        private static void ReachExit(PhysicsFixture fixture, Character character, LevelLayout layout)
        {
            GameSession session = fixture.Session;
            session.Start(character, layout.RunSeed, layout);
            var command = new PlayerCommand(move: layout.RouteDirection);
            int port = 0;
            float peakImpact = 0f;
            session.Motor.Landed += speed => peakImpact = Mathf.Max(peakImpact, speed);
            fixture.Advance(8);
            for (int tick = 0; tick < 1300 && session.Phase == RunPhase.Playing; tick++)
            {
                fixture.Step(command);
                PlayerMotor motor = session.Motor;
                Vector2 feet = Feet(motor, character);
                if (port < layout.Checkpoints.Count && motor.Grounded
                    && Vector2.Distance(feet, layout.Checkpoints[port].Landing) < 0.9f)
                    port++;

                if (session.AtExit && motor.Grounded)
                {
                    Require(port == layout.Checkpoints.Count
                            && session.Run.Health == character.MaxHealth && peakImpact < PlayerMotor.FallDamageSpeed,
                        Failure(layout, character, session, port, peakImpact));
                    return;
                }
            }

            Require(false, Failure(layout, character, session, port, peakImpact));
        }

        private static Vector2 Feet(PlayerMotor motor, Character character) =>
            motor.Body.position - Vector2.up * character.Radius;

        private static string TemplateNames(LevelLayout layout) =>
            string.Join(", ", layout.Chunks.Where(chunk => chunk.MainRoute).Select(chunk => chunk.TemplateId));

        private static string Failure(LevelLayout layout, Character character, GameSession session, int port, float peakImpact)
        {
            string pending = port == layout.Checkpoints.Count
                ? $"exit {layout.Exit}"
                : $"{layout.Checkpoints[port].TemplateId} {layout.Checkpoints[port].EntryPort} port at {layout.Checkpoints[port].Landing}";
            return $"{layout.World} seed {layout.RunSeed}/{layout.LevelNumber}, {character.Name}, direction {layout.RouteDirection}, pending {pending}, templates {TemplateNames(layout)}, body at {session.Motor.Body.position}, peak impact {peakImpact:F2}, health {session.Run.Health}.";
        }
    }
}
