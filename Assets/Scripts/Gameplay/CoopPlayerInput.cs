using UnityEngine;
using UnityEngine.InputSystem;

namespace Vampire.Gameplay
{
    /// <summary>
    /// Input binding for co-op players
    /// Bridges PlayerInput with Character movement/actions
    /// </summary>
    public class CoopPlayerInput : MonoBehaviour
    {
        private enum InputMode
        {
            PlayerInputActions,
            SplitKeyboard
        }

        [Header("Mode")]
        [SerializeField] private InputMode inputMode = InputMode.PlayerInputActions;

        [Header("Input Actions (default)")]
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string lookActionName = "Look";
        [SerializeField] private string attackActionName = "Attack";

        [Header("Split Keyboard (optional)")]
        [SerializeField] private KeyCode upKey = KeyCode.W;
        [SerializeField] private KeyCode downKey = KeyCode.S;
        [SerializeField] private KeyCode leftKey = KeyCode.A;
        [SerializeField] private KeyCode rightKey = KeyCode.D;
        [SerializeField] private KeyCode attackKey = KeyCode.Space;

        private Character character;
        private PlayerInput playerInput;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction attackAction;

        private void Awake()
        {
            character = GetComponent<Character>();
            playerInput = GetComponent<PlayerInput>();

            if (inputMode == InputMode.PlayerInputActions && playerInput != null && playerInput.actions != null)
            {
                moveAction = playerInput.actions.FindAction(moveActionName);
                lookAction = playerInput.actions.FindAction(lookActionName);
                attackAction = playerInput.actions.FindAction(attackActionName);
            }
        }

        private void OnEnable()
        {
            if (inputMode == InputMode.PlayerInputActions)
            {
                if (moveAction != null)
                {
                    moveAction.performed += OnMovePerformed;
                    moveAction.canceled += OnMoveCanceled;
                }

                if (lookAction != null)
                {
                    lookAction.performed += OnLookPerformed;
                }

                if (attackAction != null)
                {
                    attackAction.performed += OnAttackPerformed;
                }
            }
        }

        private void OnDisable()
        {
            if (inputMode == InputMode.PlayerInputActions)
            {
                if (moveAction != null)
                {
                    moveAction.performed -= OnMovePerformed;
                    moveAction.canceled -= OnMoveCanceled;
                }

                if (lookAction != null)
                {
                    lookAction.performed -= OnLookPerformed;
                }

                if (attackAction != null)
                {
                    attackAction.performed -= OnAttackPerformed;
                }
            }
        }

        private void Update()
        {
            if (inputMode != InputMode.SplitKeyboard || character == null)
                return;

            Vector2 move = Vector2.zero;
            if (Input.GetKey(upKey)) move.y += 1f;
            if (Input.GetKey(downKey)) move.y -= 1f;
            if (Input.GetKey(leftKey)) move.x -= 1f;
            if (Input.GetKey(rightKey)) move.x += 1f;

            move = move.sqrMagnitude > 1f ? move.normalized : move;
            character.Move(move);

            if (move != Vector2.zero)
            {
                character.LookDirection = move;
                character.StartWalkAnimation();
            }
            else
            {
                character.StopWalkAnimation();
            }

            if (Input.GetKeyDown(attackKey))
            {
                // TODO: integrate with ability system if available
            }
        }

        private void OnMovePerformed(InputAction.CallbackContext context)
        {
            if (character == null)
                return;

            Vector2 moveInput = context.ReadValue<Vector2>();
            character.Move(moveInput.normalized);

            if (moveInput != Vector2.zero)
            {
                character.StartWalkAnimation();
            }
        }

        private void OnMoveCanceled(InputAction.CallbackContext context)
        {
            if (character == null)
                return;

            character.Move(Vector2.zero);
            character.StopWalkAnimation();
        }

        private void OnLookPerformed(InputAction.CallbackContext context)
        {
            if (character == null)
                return;

            Vector2 lookInput = context.ReadValue<Vector2>();
            if (lookInput != Vector2.zero)
            {
                character.LookDirection = lookInput.normalized;
            }
        }

        private void OnAttackPerformed(InputAction.CallbackContext context)
        {
            // TODO: Trigger ability/attack based on current ability
            // This would require integration with AbilityManager
            // character.TriggerAbility();
        }

        public PlayerInput GetPlayerInput()
        {
            return playerInput;
        }

        public Character GetCharacter()
        {
            return character;
        }

        public void ConfigureSplitKeyboard(KeyCode up, KeyCode down, KeyCode left, KeyCode right, KeyCode attack)
        {
            inputMode = InputMode.SplitKeyboard;
            upKey = up;
            downKey = down;
            leftKey = left;
            rightKey = right;
            attackKey = attack;
        }
    }
}
