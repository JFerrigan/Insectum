using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Insectum.Game.Editor
{
    public static class ProductionVerification
    {
        private const string BootScene = "Assets/Scenes/Boot.unity";
        private const string PendingVerification = "Insectum.ProductionVerification.Pending";

        [InitializeOnLoadMethod]
        private static void Register()
        {
            EditorApplication.playModeStateChanged += VerifyInPlayMode;
        }

        public static void Run()
        {
            try
            {
                EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
                Require(scenes.Length > 0 && scenes[0].enabled && scenes[0].path == BootScene,
                    "Boot must be the first enabled build scene.");
                Scene boot = EditorSceneManager.OpenScene(BootScene);
                Require(Array.Exists(boot.GetRootGameObjects(), root => root.GetComponent<GameBoot>() != null),
                    "Boot is missing its GameBoot component.");
                SessionState.SetBool(PendingVerification, true);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception error)
            {
                Finish(1, error);
            }
        }

        private static void VerifyInPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingVerification, false))
                return;

            SessionState.EraseBool(PendingVerification);
            try
            {
                Require(UnityEngine.Object.FindFirstObjectByType<GameBoot>() is GameBoot boot && boot.isActiveAndEnabled,
                    "GameBoot did not initialize in play mode.");
                BootstrapVerification.Verify();
                TerrainVerification.Verify();
                CombatVerification.Verify();
                GenerationVerification.Verify();
                SwampVerification.VerifyGeneration();
                CavesVerification.VerifyGeneration();
                DesertVerification.VerifyGeneration();
                AlienVerification.VerifyGeneration();
                ForestRunVerification.Verify();
                SwampVerification.VerifyPlaythrough();
                CavesVerification.VerifyPlaythrough();
                DesertVerification.VerifyPlaythrough();
                AlienVerification.VerifyPlaythrough();
                FullRunVerification.Verify();
            }
            catch (Exception error)
            {
                Finish(1, error);
                return;
            }

            Finish(0);
        }

        private static void Finish(int exitCode, Exception error = null)
        {
            if (error != null)
                Debug.LogException(error);
            EditorApplication.Exit(exitCode);
        }

        internal static void Require(bool condition, string description)
        {
            if (!condition)
                throw new InvalidOperationException(description);
        }
    }
}
