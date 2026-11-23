using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Insectum.Game.Editor.ProductionVerification;

namespace Insectum.Game.Editor
{
    internal sealed class PhysicsFixture : IDisposable
    {
        private readonly Scene scene;
        public PhysicsScene2D Physics { get; }
        public PhysicsMaterial2D Surface { get; } = new("Verification") { friction = 0f };
        public GameObject World { get; }
        public GameSession Session { get; }
        public List<PlayerCommand> Replay { get; set; }

        public PhysicsFixture(string name, bool enableAbilities = true, bool enableVariants = false)
        {
            scene = SceneManager.CreateScene(name, new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            Physics = scene.GetPhysicsScene2D();
            World = new GameObject("Verification World");
            SceneManager.MoveGameObjectToScene(World, scene);
            Session = new GameSession(World.transform, null, Surface, enableAbilities, enableVariants);
        }

        public void Step(PlayerCommand command = default)
        {
            Replay?.Add(command);
            Physics2D.SyncTransforms();
            Session.Step(command);
            Simulate();
        }

        public void Simulate()
        {
            Require(Physics.Simulate(PlayerMotor.StepSeconds), "Unity could not advance the local 2D physics scene.");
        }

        public void Advance(int steps, PlayerCommand command = default)
        {
            for (int frame = 0; frame < steps; frame++)
                Step(command);
        }

        public void Dispose()
        {
            Session.ReturnToSelection();
            World.SetActive(false);
            SceneManager.UnloadSceneAsync(scene);
            UnityEngine.Object.Destroy(Surface);
        }
    }
}
