using UnityEngine;

namespace Vampire.Gameplay
{
    public class PlayerKeyboardController : MonoBehaviour
    {
        public enum ControlScheme
        {
            Player1_WASD,
            Player2_Arrows
        }

        [SerializeField] private ControlScheme controlScheme = ControlScheme.Player1_WASD;
        [SerializeField] private Vampire.Character character;
        [SerializeField] private Sprite playerSpriteOverride;
        [SerializeField] private Color tintColor = Color.white;
        [SerializeField] private bool debugLogging = false;

        private SpriteRenderer spriteRenderer;
        private Vector2 currentInput;

        private void Awake()
        {
            if (character == null)
                character = GetComponent<Vampire.Character>();

            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                if (playerSpriteOverride != null)
                {
                    spriteRenderer.sprite = playerSpriteOverride;
                }
                if (tintColor != default(Color))
                {
                    spriteRenderer.color = tintColor;
                }
            }
        }

        private void Update()
        {
            if (character == null)
                return;

            // Read input in Update for responsiveness
            currentInput = ReadInput();

            // Handle animations and look direction
            if (currentInput != Vector2.zero)
            {
                character.StartWalkAnimation();
                character.LookDirection = currentInput;
            }
            else
            {
                character.StopWalkAnimation();
            }

            if (debugLogging && Time.frameCount % 60 == 0)
            {
                Debug.Log($"[{gameObject.name}] Input: {currentInput}, Velocity: {character.Velocity}");
            }
        }

        private void FixedUpdate()
        {
            if (character == null)
                return;

            // Apply movement in FixedUpdate for consistent physics
            character.Move(currentInput);
        }

        private Vector2 ReadInput()
        {
            float x = 0f, y = 0f;
            switch (controlScheme)
            {
                case ControlScheme.Player1_WASD:
                    x = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
                    y = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
                    break;
                case ControlScheme.Player2_Arrows:
                    x = (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
                    y = (Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
                    break;
            }
            Vector2 v = new Vector2(x, y);
            if (v.sqrMagnitude > 1f) v.Normalize();
            return v;
        }
    }
}
