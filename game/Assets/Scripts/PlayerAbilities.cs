using UnityEngine;

namespace Insectum.Game
{
    public sealed class PlayerAbilities
    {
        private const int DigIntervalSteps = 9;
        private readonly RunState run;
        private readonly PlayerMotor motor;
        private readonly HexTerrain terrain;
        private readonly CombatArena combat;
        private readonly CharacterTuning tuning;
        private int digCooldown;
        private int digFeedbackSteps;
        private Vector2 previousShellPosition;
        private bool hasShellHistory;

        internal int ArtCueSteps { get; private set; }
        internal string DigFeedback { get; private set; }

        public bool Shelled { get; private set; }
        public bool Flying { get; private set; }
        public bool Climbing { get; private set; }

        internal PlayerAbilities(RunState run, PlayerMotor motor, HexTerrain terrain, CombatArena combat)
        {
            this.run = run;
            this.motor = motor;
            this.terrain = terrain;
            this.combat = combat;
            tuning = CharacterTuning.Current;
        }

        internal MotorControl Step(PlayerCommand command)
        {
            if (ArtCueSteps > 0)
                ArtCueSteps--;

            switch (run.Character.Insect)
            {
                case Insect.Spider:
                    return StepSpider(command);
                case Insect.Beetle:
                    return StepBeetle(command);
                case Insect.Dragonfly:
                    return StepDragonfly(command);
                case Insect.Worm:
                    StepExcavation(command);
                    break;
            }

            return default;
        }

        internal void ResetModes()
        {
            ArtCueSteps = 0;
            Shelled = false;
            Flying = false;
            Climbing = false;
            hasShellHistory = false;
            digCooldown = 0;
            digFeedbackSteps = 0;
            DigFeedback = null;
        }

        internal bool AbsorbDamage(DamageHit hit)
        {
            if (!Shelled)
                return false;

            if (run.TrySpendEnergy(tuning.beetleEnergyPerHeart * hit.Hearts))
                return true;

            Shelled = false;
            hasShellHistory = false;
            return false;
        }

        private MotorControl StepSpider(PlayerCommand command)
        {
            if (command.AbilityPressed && run.TrySpendEnergy(tuning.spiderLeapEnergy))
            {
                Climbing = false;
                ArtCueSteps = 10;
                return MotorControl.BoostedJump(tuning.spiderJumpHeightMultiplier);
            }

            int facing = motor.FacingFor(command);
            Climbing = command.Move != 0 && !command.JumpPressed
                && (Climbing || motor.Body.linearVelocity.y <= 0.05f) && motor.HasWall(facing);

            if (Climbing)
                return MotorControl.Powered(new Vector2(facing * tuning.spiderClimbSpeed, tuning.spiderClimbSpeed));

            return default;
        }

        private MotorControl StepBeetle(PlayerCommand command)
        {
            Vector2 position = motor.Body.position;
            if (Shelled && hasShellHistory && motor.LastVerticalSpeed <= -tuning.beetleCrushSpeed)
                combat.Crush(previousShellPosition, position, run.Character.Radius, tuning.beetleCrushDamage);

            if (command.AbilityPressed)
                Shelled = !Shelled;

            previousShellPosition = position;
            hasShellHistory = Shelled;
            return Shelled ? MotorControl.Momentum : default;
        }

        private MotorControl StepDragonfly(PlayerCommand command)
        {
            if (command.AbilityPressed)
                Flying = !Flying && run.Energy > 0f;
            if (!Flying)
                return default;

            float cost = Mathf.Min(run.Energy, tuning.dragonflyEnergyPerSecond * PlayerMotor.StepSeconds);
            run.TrySpendEnergy(cost);
            Flying = run.Energy > 0f;
            if (!Flying)
                return default;

            var direction = new Vector2(command.Move, command.Aim);
            return MotorControl.Powered(Vector2.ClampMagnitude(direction, 1f) * tuning.dragonflyFlightSpeed,
                tuning.dragonflyFlightAcceleration);
        }

        private void StepExcavation(PlayerCommand command)
        {
            if (digFeedbackSteps > 0 && --digFeedbackSteps == 0)
                DigFeedback = null;
            if (digCooldown > 0)
                digCooldown--;
            if ((!command.AbilityPressed && !command.AbilityHeld) || digCooldown > 0)
                return;

            Vector2 direction = command.Aim < 0 ? Vector2.down : Vector2.right * motor.FacingFor(command);
            ExcavationResult result = terrain.ExcavateAdjacent(motor.Body.position, direction,
                run.Character.Radius + 0.76f, run.TrySpendEnergy);
            if (result == ExcavationResult.Removed)
            {
                ArtCueSteps = 8;
                DigFeedback = null;
                digFeedbackSteps = 0;
            }
            else if (command.AbilityPressed || result == ExcavationResult.InsufficientEnergy)
            {
                DigFeedback = result switch
                {
                    ExcavationResult.Reinforced => "Reinforced rock",
                    ExcavationResult.InsufficientEnergy => "Need more energy to dig",
                    _ => "No diggable dirt in reach"
                };
                digFeedbackSteps = 60;
            }
            digCooldown = DigIntervalSteps;
        }
    }
}
