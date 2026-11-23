using UnityEngine;
using static Insectum.Game.Editor.ProductionVerification;

namespace Insectum.Game.Editor
{
    internal static class BootstrapVerification
    {
        internal static void Verify()
        {
            using var fixture = new PhysicsFixture("Bootstrap verification");
            foreach (Character character in Character.Roster)
                VerifyRoute(fixture, character);
            VerifyJump(fixture);
            VerifyFallDamage(fixture);
            VerifyDefeatAndRestart(fixture);
            Debug.Log("Insectum bootstrap verified: Boot, five routes, variable and buffered jump, fall damage, defeat, restart.");
        }

        private static void VerifyRoute(PhysicsFixture fixture, Character character)
        {
            GameSession session = fixture.Session;
            session.Start(character, 472);
            Require(session.Run.Character == character && session.Run.Seed == 472 && session.Run.LevelNumber == 1,
                $"Starting {character.Name} did not select the correct run.");
            Require(session.Run.Health == character.MaxHealth && session.Run.Energy == character.MaxEnergy,
                $"Starting {character.Name} did not replenish health and energy.");
            fixture.Advance(8);

            for (int frame = 0; frame < 60 && session.Motor.Body.position.x < -2.4f; frame++)
                fixture.Step(new PlayerCommand(move: 1));

            Require(session.Motor.Body.position.x >= -2.4f, $"{character.Name} could not walk to the first gap.");
            fixture.Step(new PlayerCommand(move: 1, jumpPressed: true, jumpHeld: true));
            fixture.Advance(32, new PlayerCommand(move: 1, jumpHeld: true));
            Require(session.Motor.Body.position.x > 0.7f && session.Motor.Body.position.y > 1.15f,
                $"{character.Name} missed the first shelf at {session.Motor.Body.position}.");

            for (int frame = 0; frame < 320 && !session.AtExit && session.Phase == RunPhase.Playing; frame++)
                fixture.Step(new PlayerCommand(move: 1));

            Require(session.AtExit, $"{character.Name} did not reach the exit at {session.Motor.Body.position}. Phase {session.Phase}, velocity {session.Motor.Body.linearVelocity}.");
            Require(session.Run.Health == character.MaxHealth, $"{character.Name} took damage on the authored route.");
            fixture.Step(new PlayerCommand(interactPressed: true));
            Require(session.Phase == RunPhase.Cleared, $"{character.Name} could not interact with the exit.");
        }

        private static void VerifyJump(PhysicsFixture fixture)
        {
            Character spider = Character.Roster[0];
            float shortJump = JumpHeight(fixture, spider, false);
            float fullJump = JumpHeight(fixture, spider, true);
            Require(shortJump > 0.1f && fullJump > shortJump + 1f,
                $"Jump release did not change height. Released {shortJump:F2}, held {fullJump:F2}.");

            GameSession session = fixture.Session;
            session.Start(spider, 10);
            Rigidbody2D body = session.Motor.Body;
            body.position += Vector2.up * 0.24f;
            fixture.Step(new PlayerCommand(jumpPressed: true, jumpHeld: true));
            Require(body.linearVelocity.y < 0f, "The buffered jump activated before landing.");
            fixture.Advance(12, new PlayerCommand(move: 1, jumpHeld: true));
            Require(body.linearVelocity.y > 0f && body.linearVelocity.x > 0f,
                "A buffered press did not jump on landing with air control.");
        }

        private static float JumpHeight(PhysicsFixture fixture, Character character, bool hold)
        {
            GameSession session = fixture.Session;
            session.Start(character, 10);
            fixture.Advance(8);
            float start = session.Motor.Body.position.y;
            float highest = start;
            fixture.Step(new PlayerCommand(jumpPressed: true, jumpHeld: hold));
            for (int frame = 0; frame < 50; frame++)
            {
                highest = Mathf.Max(highest, session.Motor.Body.position.y);
                fixture.Step(new PlayerCommand(jumpHeld: hold));
            }

            return highest - start;
        }

        private static void VerifyFallDamage(PhysicsFixture fixture)
        {
            GameSession session = fixture.Session;
            Character character = Character.Roster[2];
            session.Start(character, 11);
            fixture.Advance(8);

            // Walking off the first shelf misses the raised shelf and reaches the forest floor.
            for (int frame = 0; frame < 80 && session.Motor.Body.position.x < -0.35f; frame++)
                fixture.Step(new PlayerCommand(move: 1));
            fixture.Advance(40);
            Require(session.Motor.Body.position.y < -4f, "The long fall did not reach the forest floor.");
            Require(session.Run.Health == character.MaxHealth - 1, "The landing did not consume exactly one heart.");
        }

        private static void VerifyDefeatAndRestart(PhysicsFixture fixture)
        {
            GameSession session = fixture.Session;
            Character character = Character.Roster[2];
            session.Run.TakeDamage(session.Run.Health - 1);
            session.Motor.Body.position = new Vector2(-0.2f, 2f);
            session.Motor.Body.linearVelocity = Vector2.zero;
            fixture.Advance(80);
            Require(session.Phase == RunPhase.Defeated && !session.Run.Alive, "A fatal fall did not end the run.");

            RunState previous = session.Run;
            session.Start(character, 12);
            Require(session.Phase == RunPhase.Playing && session.Run != previous && session.Run.Seed == 12
                && session.Run.Health == character.MaxHealth, "Restart did not start a fresh run.");
            session.ReturnToSelection();
            Require(session.Phase == RunPhase.Selection && session.Motor == null && session.Run == null,
                "Returning to selection did not release the current run.");
        }
    }
}
