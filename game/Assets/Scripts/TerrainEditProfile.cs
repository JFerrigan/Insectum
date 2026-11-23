#if DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Insectum.Game
{
    internal sealed class TerrainEditProfile
    {
        private readonly GameSession session;
        private readonly int samples;
        private readonly Queue<Vector2Int> digs = new();
        private readonly Queue<Vector2Int> blasts = new();
        private readonly List<double> digCpu = new();
        private readonly List<double> blastCpu = new();
        private readonly List<double> editFrames = new();
        private readonly List<double> idleFrames = new();
        private int scene;
        private int warmup;
        private int cellsRemoved;
        private int multiCellBlasts;
        private bool measuredLastFrame;
        private bool finished;

        internal Vector2 Focus { get; private set; }

        private TerrainEditProfile(GameSession session, int samples)
        {
            this.session = session;
            this.samples = samples;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            LoadScene();
            warmup = 90;
            Debug.Log($"Insectum terrain profile started. {samples} edits, 60 FPS target, native {PixelViewport.Width}x{PixelViewport.Height} scene.");
        }

        internal static TerrainEditProfile FromArguments(GameSession session)
        {
            return int.TryParse(ForestReplayFile.Argument("-insectum-profile-terrain"), out int samples) && samples >= 4
                ? new TerrainEditProfile(session, Mathf.Min(samples, 1000)) : null;
        }

        internal void Frame(float seconds)
        {
            if (finished)
                return;
            if (warmup > 0)
            {
                warmup--;
                return;
            }

            if (measuredLastFrame)
            {
                editFrames.Add(seconds * 1000.0);
                measuredLastFrame = false;
                if (editFrames.Count == samples)
                    Finish();
                return;
            }

            bool dig = digCpu.Count < samples / 2;
            Queue<Vector2Int> deck = dig ? digs : blasts;
            while (deck.Count > 0)
            {
                Vector2Int cell = deck.Dequeue();
                if (!session.Terrain.Cells.ContainsKey(cell) || dig && !BordersAnotherRegion(cell))
                    continue;

                Focus = HexGrid.Center(cell) + Vector2.up * 1.2f;
                long start = Stopwatch.GetTimestamp();
                int removed = dig
                    ? session.Terrain.Excavate(cell) == ExcavationResult.Removed ? 1 : 0
                    : session.Terrain.Explode(HexGrid.Center(cell), 2.15f, 99);
                double elapsed = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                if (removed == 0)
                    continue;

                (dig ? digCpu : blastCpu).Add(elapsed);
                cellsRemoved += removed;
                if (!dig && removed > 1)
                    multiCellBlasts++;
                idleFrames.Add(seconds * 1000.0);
                measuredLastFrame = true;
                return;
            }

            LoadScene();
        }

        private void LoadScene()
        {
            int level = RunWorlds.FirstLevel((WorldId)(scene % 5));
            int seed = 2300 + scene++ * 37;
            while (RunWorlds.ChooseVariant(new LevelSeeds(seed, level), 1) != LevelVariant.Standard)
                seed++;

            session.StartGenerated(Character.Roster[(int)Insect.Worm], seed, level);
            Focus = session.Motor.Body.position;
            digs.Clear();
            blasts.Clear();
            HexTerrain terrain = session.Terrain;
            foreach (var tile in terrain.Cells.OrderBy(pair => pair.Key.y).ThenBy(pair => pair.Key.x))
            {
                if (tile.Value == TerrainMaterial.Reinforced)
                    continue;

                Vector2Int cell = tile.Key;
                if (BordersAnotherRegion(cell))
                    digs.Enqueue(cell);
                blasts.Enqueue(cell);
            }

            warmup = 12;
        }

        private bool BordersAnotherRegion(Vector2Int cell)
        {
            Vector2Int region = HexGrid.RegionAt(cell);
            for (int side = 0; side < HexGrid.SideCount; side++)
            {
                Vector2Int neighbor = HexGrid.Neighbor(cell, side);
                if (HexGrid.RegionAt(neighbor) != region && session.Terrain.Cells.ContainsKey(neighbor))
                    return true;
            }
            return false;
        }

        private void Finish()
        {
            finished = true;
            Debug.Log($"Insectum terrain profile complete. {samples} edits across {scene} rendered layouts, {cellsRemoved} cells removed, {multiCellBlasts} multi-cell blasts.");
            Report("cross-region dig CPU", digCpu);
            Report("blast CPU", blastCpu);
            Report("edit frame interval", editFrames);
            Report("idle frame interval", idleFrames);
            Application.targetFrameRate = 30;
        }

        private static void Report(string label, List<double> values)
        {
            values.Sort();
            int p95 = Mathf.CeilToInt(values.Count * 0.95f) - 1;
            int overBudget = values.Count(value => value > 1000.0 / 60.0);
            Debug.Log($"Insectum terrain profile {label}: n={values.Count}, median={values[values.Count / 2]:F3} ms, p95={values[p95]:F3} ms, worst={values[^1]:F3} ms, over 16.67 ms={overBudget}.");
        }
    }
}
#endif
