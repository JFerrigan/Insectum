#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;

namespace Insectum.Game
{
    internal enum ReplayKind
    {
        Authored,
        Forest,
        Run
    }

    internal static class ForestReplayFile
    {
        internal static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index < args.Length - 1 ? args[index + 1] : null;
        }

        internal static void Write(string path, Insect insect, int seed, ReplayKind kind, IReadOnlyList<PlayerCommand> commands)
        {
            using var file = new StreamWriter(path);
            file.WriteLine(insect);
            file.WriteLine(seed);
            file.WriteLine(kind);
            foreach (PlayerCommand command in commands)
            {
                int bits = (command.JumpPressed ? 1 : 0) | (command.JumpHeld ? 2 : 0) | (command.AttackPressed ? 4 : 0)
                    | (command.AbilityPressed ? 8 : 0) | (command.AbilityHeld ? 16 : 0) | (command.InteractPressed ? 32 : 0)
                    | (command.ItemPressed ? 64 : 0);
                file.WriteLine($"{command.Move},{command.Aim},{bits}");
            }
        }

        internal static PlayerCommand ReadCommand(string line)
        {
            string[] values = line.Split(',');
            int bits = int.Parse(values[2]);
            return new PlayerCommand(int.Parse(values[0]), int.Parse(values[1]), (bits & 1) != 0, (bits & 2) != 0,
                (bits & 4) != 0, (bits & 8) != 0, (bits & 16) != 0, (bits & 32) != 0, (bits & 64) != 0);
        }
    }

#if DEVELOPMENT_BUILD
    internal sealed class ForestReplay
    {
        private readonly GameSession session;
        private readonly string[] lines;
        private int cursor = 3;
        private int level;
        private int terrainRevision;
        private int renderedFrames;
        private double renderSeconds;

        private ForestReplay(GameSession session, string[] lines)
        {
            this.session = session;
            this.lines = lines;
            var insect = Enum.Parse<Insect>(lines[0]);
            var character = Character.Roster[(int)insect];
            int seed = int.Parse(lines[1]);
            ReplayKind kind = bool.TryParse(lines[2], out bool authored)
                ? authored ? ReplayKind.Authored : ReplayKind.Forest
                : Enum.Parse<ReplayKind>(lines[2]);
            switch (kind)
            {
                case ReplayKind.Authored: session.Start(character, seed); break;
                case ReplayKind.Forest: session.StartForest(character, seed); break;
                case ReplayKind.Run: session.StartGenerated(character, seed); break;
            }
            level = session.Run.LevelNumber;
            UnityEngine.Application.runInBackground = true;
            if (int.TryParse(ForestReplayFile.Argument("-insectum-render-fps"), out int frameRate) && frameRate > 0)
            {
                UnityEngine.QualitySettings.vSyncCount = 0;
                UnityEngine.Application.targetFrameRate = frameRate;
            }
            UnityEngine.Debug.Log($"Insectum replay started. {kind}, {character.Name}, seed {seed}, {lines.Length - cursor} steps, level {level}, {session.Variant}, night radius {session.NightVisionRadius:F1}.");
        }

        internal static ForestReplay FromArguments(GameSession session)
        {
            string path = ForestReplayFile.Argument("-insectum-replay");
            return path == null ? null : new ForestReplay(session, File.ReadAllLines(path));
        }

        internal void ObserveFrame(float seconds)
        {
            if (cursor < lines.Length && renderedFrames > 60)
                renderSeconds += seconds;
            if (cursor < lines.Length)
                renderedFrames++;
        }

        internal void Step()
        {
            if (cursor == lines.Length)
                return;

            session.Step(ForestReplayFile.ReadCommand(lines[cursor++]));
            if (session.Run.LevelNumber != level)
            {
                level = session.Run.LevelNumber;
                terrainRevision = 0;
                UnityEngine.Debug.Log($"Insectum replay entered level {level}. {session.Run.World} {session.Run.WorldLevel}, {session.Variant}, health {session.Run.Health}, night radius {session.NightVisionRadius:F1}.");
            }
            if (session.Terrain.Revision != terrainRevision)
            {
                terrainRevision = session.Terrain.Revision;
                UnityEngine.Debug.Log($"Insectum replay edited terrain. Revision {terrainRevision}. Energy {session.Run.Energy}.");
            }
            if (cursor == lines.Length)
                UnityEngine.Debug.Log($"Insectum replay completed. Phase {session.Phase}, level {level}, health {session.Run.Health}, Nektar {session.Run.Inventory.Nektar}, Moth Eyes {session.Run.Inventory.Count(ItemId.MothEyes)}, position {session.Motor.Body.position}, rendered FPS {(renderSeconds > 0 ? (renderedFrames - 61) / renderSeconds : 0):F1}.");
        }
    }
#endif
}
#endif
