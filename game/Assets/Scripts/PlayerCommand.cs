using UnityEngine;

namespace Insectum.Game
{
    public readonly struct PlayerCommand
    {
        public readonly int Move;
        public readonly int Aim;
        public readonly bool JumpPressed;
        public readonly bool JumpHeld;
        public readonly bool AttackPressed;
        public readonly bool AbilityPressed;
        public readonly bool AbilityHeld;
        public readonly bool InteractPressed;
        public readonly bool ItemPressed;

        public PlayerCommand(int move = 0, int aim = 0, bool jumpPressed = false, bool jumpHeld = false,
            bool attackPressed = false, bool abilityPressed = false, bool abilityHeld = false,
            bool interactPressed = false, bool itemPressed = false)
        {
            Move = move;
            Aim = aim;
            JumpPressed = jumpPressed;
            JumpHeld = jumpHeld;
            AttackPressed = attackPressed;
            AbilityPressed = abilityPressed;
            AbilityHeld = abilityHeld;
            InteractPressed = interactPressed;
            ItemPressed = itemPressed;
        }
    }

    internal sealed class KeyboardInput
    {
        private int move;
        private int aim;
        private bool jumpPressed;
        private bool jumpHeld;
        private bool attackPressed;
        private bool abilityPressed;
        private bool abilityHeld;
        private bool interactPressed;
        private bool itemPressed;

        public void Sample()
        {
            move = Held(KeyCode.D) - Held(KeyCode.A);
            aim = Held(KeyCode.W) - Held(KeyCode.S);
            jumpPressed |= Input.GetKeyDown(KeyCode.Space);
            jumpHeld = Input.GetKey(KeyCode.Space);
            attackPressed |= Input.GetKeyDown(KeyCode.J);
            abilityPressed |= Input.GetKeyDown(KeyCode.K);
            abilityHeld = Input.GetKey(KeyCode.K);
            interactPressed |= Input.GetKeyDown(KeyCode.E);
            itemPressed |= Input.GetKeyDown(KeyCode.L);
        }

        public PlayerCommand Consume()
        {
            var command = new PlayerCommand(move, aim, jumpPressed, jumpHeld, attackPressed, abilityPressed,
                abilityHeld, interactPressed, itemPressed);
            jumpPressed = attackPressed = abilityPressed = interactPressed = itemPressed = false;
            return command;
        }

        private static int Held(KeyCode key) => Input.GetKey(key) ? 1 : 0;
    }
}
