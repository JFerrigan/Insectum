using System;
using UnityEngine;

namespace Insectum.Game
{
    public enum MotorMode
    {
        Normal,
        Momentum,
        Powered
    }

    public readonly struct MotorControl
    {
        public MotorMode Mode { get; }
        public Vector2 TargetVelocity { get; }
        public float Acceleration { get; }

        private MotorControl(MotorMode mode, Vector2 targetVelocity, float acceleration)
        {
            Mode = mode;
            TargetVelocity = targetVelocity;
            Acceleration = acceleration;
        }

        public static MotorControl Momentum => new(MotorMode.Momentum, Vector2.zero, 0f);
        public static MotorControl Powered(Vector2 velocity, float acceleration = float.PositiveInfinity) =>
            new(MotorMode.Powered, velocity, acceleration);
    }

    public sealed class PlayerMotor
    {
        public const float StepSeconds = 1f / 60f;
        public const float FallDamageSpeed = 15f;

        private const float GroundSpeed = 6.5f;
        private const float JumpSpeed = 12.5f;
        private const float ReleasedJumpSpeed = 4.2f;
        private const float Gravity = 38f;
        private const float TerminalSpeed = 24f;
        private const float JumpBufferSeconds = 0.12f;
        private const float CoyoteSeconds = 0.09f;

        private readonly RaycastHit2D[] hits = new RaycastHit2D[8];
        private readonly ContactFilter2D terrainFilter = new() { useTriggers = false };
        private float jumpBuffer;
        private float coyoteTime;
        private float previousVelocityY;
        private bool jumping;

        public Rigidbody2D Body { get; }
        public Collider2D Shape { get; }
        public int Facing { get; private set; }
        public bool Grounded { get; private set; }
        public float LastVerticalSpeed => previousVelocityY;
        public event Action<float> Landed;

        public PlayerMotor(GameObject actor, float radius, PhysicsMaterial2D surface, int initialFacing = 1)
        {
            Facing = initialFacing < 0 ? -1 : 1;
            Body = actor.AddComponent<Rigidbody2D>();
            Body.gravityScale = 0f;
            Body.freezeRotation = true;
            Body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var circle = actor.AddComponent<CircleCollider2D>();
            circle.radius = radius;
            circle.sharedMaterial = surface;
            Shape = circle;
        }

        public int FacingFor(PlayerCommand command) => command.Move == 0 ? Facing : Math.Sign(command.Move);

        public bool HasWall(int direction)
        {
            int count = Body.Cast(Vector2.right * direction, terrainFilter, hits, 0.09f);
            for (int i = 0; i < count; i++)
                if (hits[i].normal.x * direction <= -0.75f && Mathf.Abs(hits[i].normal.y) <= 0.55f)
                    return true;
            return false;
        }

        public void Launch(Vector2 velocity)
        {
            ClearJump();
            Body.linearVelocity = velocity;
            previousVelocityY = velocity.y;
        }

        internal void Warp(Vector2 position)
        {
            Body.position = position;
            Grounded = false;
            Launch(Vector2.zero);
            Physics2D.SyncTransforms();
        }

        public void ApplyImpulse(Vector2 velocity) => Launch(Body.linearVelocity + velocity);

        public void Step(PlayerCommand command, MotorControl control = default)
        {
            Vector2 velocity = Body.linearVelocity;
            bool grounded = velocity.y <= 0.05f && HasFloor();
            bool landed = grounded && !Grounded;
            float impactSpeed = Mathf.Max(0f, -previousVelocityY);
            Grounded = grounded;

            if (control.Mode == MotorMode.Normal)
                velocity = StepNormal(command, velocity, grounded);
            else
            {
                ClearJump();
                if (control.Mode == MotorMode.Momentum)
                    velocity.y = ApplyGravity(velocity.y);
                else
                {
                    Facing = FacingFor(command);
                    velocity = Vector2.MoveTowards(velocity, control.TargetVelocity, control.Acceleration * StepSeconds);
                }
            }

            Body.linearVelocity = velocity;
            previousVelocityY = velocity.y;
            if (landed)
                Landed?.Invoke(impactSpeed);
        }

        private Vector2 StepNormal(PlayerCommand command, Vector2 velocity, bool grounded)
        {
            if (grounded)
            {
                coyoteTime = CoyoteSeconds;
                jumping = false;
            }

            if (command.JumpPressed)
                jumpBuffer = JumpBufferSeconds;
            Facing = FacingFor(command);

            float horizontal = Mathf.Clamp(command.Move, -1, 1);
            float acceleration = grounded ? (horizontal == 0f ? 65f : 55f) : 28f;
            velocity.x = Mathf.MoveTowards(velocity.x, horizontal * GroundSpeed, acceleration * StepSeconds);
            velocity.y = ApplyGravity(velocity.y);

            if (jumpBuffer > 0f && coyoteTime > 0f)
            {
                velocity.y = JumpSpeed;
                jumping = true;
                jumpBuffer = 0f;
                coyoteTime = 0f;
            }

            if (jumping && !command.JumpHeld && velocity.y > ReleasedJumpSpeed)
                velocity.y = ReleasedJumpSpeed;

            jumpBuffer -= StepSeconds;
            coyoteTime -= StepSeconds;
            return velocity;
        }

        private static float ApplyGravity(float speed) => Mathf.Max(-TerminalSpeed, speed - Gravity * StepSeconds);

        private void ClearJump()
        {
            jumpBuffer = 0f;
            coyoteTime = 0f;
            jumping = false;
        }

        private bool HasFloor()
        {
            int count = Body.Cast(Vector2.down, terrainFilter, hits, 0.065f);
            for (int i = 0; i < count; i++)
                if (hits[i].normal.y >= 0.68f)
                    return true;

            return false;
        }
    }
}
