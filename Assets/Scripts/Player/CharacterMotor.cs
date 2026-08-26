using UnityEngine;

namespace Networking
{
    public readonly struct CharacterMotorState
    {
        public Vector3 Position { get; }

        public Quaternion Rotation { get; }

        public float VerticalVelocity { get; }

        public float ControllerHeight { get; }

        public CharacterMotorState(
            Vector3 position,
            Quaternion rotation,
            float verticalVelocity,
            float controllerHeight)
        {
            Position = position;
            Rotation = rotation;
            VerticalVelocity = verticalVelocity;
            ControllerHeight = controllerHeight;
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NetObject))]
    public sealed class CharacterMotor : MonoBehaviour
    {
        [SerializeField]
        private float walkSpeed = 3f;

        [SerializeField]
        private float sprintMultiplier = 2f;

        [SerializeField]
        private float jumpSpeed = 5f;

        [SerializeField]
        private float gravity = 9.81f;

        [SerializeField]
        private float groundedVelocity = -0.5f;

        [SerializeField]
        private float crouchHeight = 1f;

        [SerializeField]
        private float heightTransitionSpeed = 10f;

        private CharacterController _controller;
        private float _standingHeight;
        private float _verticalVelocity;

        public bool SimulationEnabled =>
            _controller != null &&
            _controller.enabled;

        private void Awake()
        {
            _controller =
                GetComponent<CharacterController>();

            _standingHeight =
                _controller.height;

            // Remote replicas do not participate in collision
            // simulation. The server or prediction system enables
            // the CharacterController when appropriate.
            _controller.enabled = false;
        }

        public void SetSimulationEnabled(bool enabled)
        {
            if (_controller == null)
                return;

            _controller.enabled = enabled;

            if (!enabled)
                _verticalVelocity = 0f;
        }

        public void Simulate(
            PlayerInputMessage message,
            float deltaTime)
        {
            if (!SimulationEnabled ||
                deltaTime <= 0f)
            {
                return;
            }

            transform.rotation =
                Quaternion.Euler(
                    0f,
                    message.Yaw,
                    0f);

            float speed = walkSpeed;

            if (message.Sprint &&
                !message.Crouch)
            {
                speed *= sprintMultiplier;
            }

            Vector3 localMovement =
                new Vector3(
                    message.Move.x,
                    0f,
                    message.Move.y);

            Vector3 horizontalVelocity =
                transform.rotation *
                localMovement *
                speed;

            if (_controller.isGrounded)
            {
                _verticalVelocity =
                    groundedVelocity;

                if (message.Jump &&
                    !message.Crouch)
                {
                    _verticalVelocity =
                        jumpSpeed;
                }
            }
            else
            {
                _verticalVelocity -=
                    gravity * deltaTime;
            }

            UpdateHeight(
                message.Crouch,
                deltaTime);

            Vector3 velocity =
                horizontalVelocity;

            velocity.y =
                _verticalVelocity;

            _controller.Move(
                velocity * deltaTime);
        }

        public CharacterMotorState CaptureState()
        {
            return new CharacterMotorState(
                transform.position,
                transform.rotation,
                _verticalVelocity,
                _controller.height);
        }

        public void RestoreState(
            CharacterMotorState state)
        {
            if (_controller == null)
                return;

            bool wasEnabled =
                _controller.enabled;

            // Disabling the CharacterController avoids it fighting
            // an authoritative teleport during reconciliation.
            if (wasEnabled)
                _controller.enabled = false;

            transform.SetPositionAndRotation(
                state.Position,
                state.Rotation);

            _controller.height =
                state.ControllerHeight;

            _controller.center =
                Vector3.up *
                (state.ControllerHeight * 0.5f);

            if (wasEnabled)
                _controller.enabled = true;

            _verticalVelocity =
                state.VerticalVelocity;
        }

        private void UpdateHeight(
            bool crouching,
            float deltaTime)
        {
            float targetHeight =
                crouching
                    ? crouchHeight
                    : _standingHeight;

            float newHeight =
                Mathf.MoveTowards(
                    _controller.height,
                    targetHeight,
                    heightTransitionSpeed *
                    deltaTime);

            _controller.height =
                newHeight;

            _controller.center =
                Vector3.up *
                (newHeight * 0.5f);
        }
    }
}