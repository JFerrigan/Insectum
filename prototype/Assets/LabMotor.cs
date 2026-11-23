using UnityEngine;

namespace Insectum
{
    public sealed class LabMotor
    {
        public const float WormRadius = 0.34f;
        private const float SpawnX = -3f;
        private const float GroundSpeed = 6.5f;
        private const float JumpSpeed = 12.5f;
        private const float Gravity = 38f;
        private const float FallDamageSpeed = 15f;

        private readonly CircleCollider2D shape;
        private readonly RaycastHit2D[] groundHits = new RaycastHit2D[8];
        private readonly ContactFilter2D terrainFilter = new() { useTriggers = false };
        private float jumpBuffer;
        private float coyoteTime;
        private float previousVelocityY;
        private bool wasGrounded;
        private bool jumping;

        public Rigidbody2D Body { get; }
        public bool IsWorm { get; private set; } = true;
        public int Hearts { get; private set; } = 4;
        public int Facing { get; private set; } = 1;
        public float Radius => shape.radius;

        public LabMotor()
        {
            var actor = new GameObject("Lab player");
            Body = actor.AddComponent<Rigidbody2D>();
            Body.gravityScale = 0f;
            Body.freezeRotation = true;
            Body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            shape = actor.AddComponent<CircleCollider2D>();
            shape.sharedMaterial = new PhysicsMaterial2D("Lab frictionless") { friction = 0f };
            Reset(true);
        }

        public void Reset(bool worm)
        {
            IsWorm = worm;
            shape.radius = worm ? WormRadius : 0.43f;
            Body.position = new Vector2(SpawnX, 1f + shape.radius + 0.06f);
            Body.linearVelocity = Vector2.zero;
            Body.WakeUp();
            Hearts = 4;
            Facing = 1;
            jumpBuffer = 0f;
            coyoteTime = 0f;
            previousVelocityY = 0f;
            wasGrounded = false;
            jumping = false;
        }

        public void QueueJump() => jumpBuffer = 0.12f;

        public void Step(float horizontal, bool holdJump, float dt)
        {
            Vector2 velocity = Body.linearVelocity;
            bool grounded = velocity.y <= 0.05f && HasFloor();
            if (grounded && !wasGrounded && previousVelocityY < -FallDamageSpeed)
                Hearts = Mathf.Max(0, Hearts - 1);

            if (Hearts == 0)
                horizontal = 0f;
            if (horizontal != 0f)
                Facing = horizontal > 0f ? 1 : -1;

            if (grounded)
            {
                coyoteTime = 0.09f;
                jumping = false;
            }

            float acceleration = grounded ? (horizontal == 0f ? 65f : 55f) : 28f;
            velocity.x = Mathf.MoveTowards(velocity.x, horizontal * GroundSpeed, acceleration * dt);
            velocity.y = Mathf.Max(-24f, velocity.y - Gravity * dt);

            if (jumpBuffer > 0f && coyoteTime > 0f && Hearts > 0)
            {
                velocity.y = JumpSpeed;
                jumping = true;
                coyoteTime = 0f;
                jumpBuffer = 0f;
            }
            if (jumping && !holdJump && velocity.y > 0f)
                velocity.y = Mathf.Min(velocity.y, 4.2f);

            Body.linearVelocity = velocity;
            previousVelocityY = velocity.y;
            wasGrounded = grounded;
            jumpBuffer -= dt;
            coyoteTime -= dt;
        }

        private bool HasFloor()
        {
            int count = Body.Cast(Vector2.down, terrainFilter, groundHits, 0.065f);
            for (int i = 0; i < count; i++)
            {
                if (groundHits[i].normal.y >= 0.68f)
                    return true;
            }
            return false;
        }
    }
}
