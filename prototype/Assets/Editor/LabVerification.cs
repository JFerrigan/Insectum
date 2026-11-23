using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Insectum
{
    public static class LabVerification
    {
        private const float StepTime = 1f / 60f;

        [MenuItem("Tools/Insectum/Verify Movement Lab", true)]
        private static bool CanRun() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem("Tools/Insectum/Verify Movement Lab")]
        public static void Run()
        {
            Scene previousScene = SceneManager.GetActiveScene();
            Scene testScene = SceneManager.CreateScene("Insectum verification", new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            SceneManager.SetActiveScene(testScene);
            HexTerrain terrain = null;
            LabMotor motor = null;
            try
            {
                terrain = new HexTerrain();
                motor = new LabMotor();
                PhysicsScene2D physics = testScene.GetPhysicsScene2D();

                // Walk across several joins, stopping against the intact plug.
                Advance(motor, physics, 120, 1f);
                Vector2 blocked = motor.Body.position;
                Require(blocked.x > 2.5f && blocked.x < 3.5f, $"Expected the intact plug to stop the player, got {blocked}.");
                Require(Mathf.Abs(blocked.y - (1f + motor.Radius)) < 0.08f, $"Unexpected shelf height at {blocked}.");
                Require(motor.Hearts == 4, "Walking across a shelf caused damage.");

                Vector2Int? target = terrain.FindDigTarget(blocked, Vector2.right, 1.18f);
                Require(target == HexTerrain.TunnelCell, $"Front excavation selected {target}.");
                Require(terrain.FindDigTarget(blocked, Vector2.down, 1.18f).HasValue, "Downward aiming missed the floor.");
                int energy = 6;
                int initialRevision = terrain.Revision;
                terrain.TryDig(HexTerrain.TunnelCell, ref energy);
                Require(energy == 6 && terrain.Revision == initialRevision, "An unaffordable excavation changed the room.");
                terrain.TryDig(Vector2Int.zero, ref energy);
                Require(energy == 6 && terrain.Revision == initialRevision, "Excavating reinforced terrain changed the room.");

                energy = 100;
                int originalTileCount = terrain.Tiles.Count;
                terrain.TryDig(HexTerrain.TunnelCell, ref energy);
                Require(energy == 93 && terrain.Revision == initialRevision + 1, "Packed dirt did not cost 7 energy and rebuild once.");
                Require(!terrain.Tiles.ContainsKey(HexTerrain.TunnelCell) && terrain.Tiles.Count == originalTileCount - 1,
                    "Excavation did not remove exactly one cell.");
                Advance(motor, physics, 90, 1f);
                Require(motor.Body.position.x > 8f, $"Worm could not pass through the excavated cell, got {motor.Body.position}.");

                // A normal landing is safe. A drop from the high shelf costs one heart.
                motor.Reset(false);
                Advance(motor, physics, 12, 0f);
                float startY = motor.Body.position.y;
                motor.QueueJump();
                Advance(motor, physics, 18, 0f, true);
                Require(motor.Body.position.y > startY + 1.5f, "A held jump did not reach the expected height.");
                Advance(motor, physics, 45, 0f);
                Require(motor.Hearts == 4, "A normal jump caused damage.");
                motor.Body.position = new Vector2(0f, 8f);
                Advance(motor, physics, 65, 0f);
                Require(motor.Hearts == 3, "A fast landing did not remove exactly one heart.");

                Debug.Log("Insectum lab verified: shelf movement, excavation cost, single-cell clearance, jump and fall damage.");
            }
            finally
            {
                if (motor != null)
                    Object.DestroyImmediate(motor.Body.gameObject);
                if (terrain != null)
                    Object.DestroyImmediate(terrain.Root);
                SceneManager.SetActiveScene(previousScene);
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(testScene, true);
            }
        }

        private static void Advance(LabMotor motor, PhysicsScene2D physics, int steps, float horizontal, bool holdJump = false)
        {
            for (int i = 0; i < steps; i++)
            {
                motor.Step(horizontal, holdJump, StepTime);
                Require(physics.Simulate(StepTime), "Unity skipped the physics step.");
            }
        }

        private static void Require(bool condition, string detail)
        {
            if (!condition)
                throw new InvalidOperationException(detail);
        }
    }
}
