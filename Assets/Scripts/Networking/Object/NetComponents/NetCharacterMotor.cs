using System;
using System.Collections.Generic;
using UnityEngine;

namespace Networking
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterMotor))]
    public sealed class NetCharacterMotor : NetBehaviour
    {
        public const int StateSize =
            (sizeof(float) * 9) +
            sizeof(uint);

        private const int MaximumPendingInputs = 256;

        private readonly List<PlayerInputMessage>
            _pendingInputs =
                new List<PlayerInputMessage>();

        private CharacterMotor _motor;

        private uint _lastProcessedInputSequence;
        private uint _lastReconciledInputSequence;

        private bool _hasReconciledInput;
        private bool _predictionEnabled;

        public bool PredictionEnabled =>
            _predictionEnabled;

        public int PendingInputCount =>
            _pendingInputs.Count;

        public override NetComponentType ComponentType =>
            NetComponentType.CharacterMotor;

        private void Awake()
        {
            _motor =
                GetComponent<CharacterMotor>();
        }

        public override void OnNetSpawn()
        {
            _pendingInputs.Clear();

            _lastProcessedInputSequence = 0;
            _lastReconciledInputSequence = 0;

            _hasReconciledInput = false;
            _predictionEnabled = false;
        }

        public override void OnNetDespawn()
        {
            StopPrediction();

            _lastProcessedInputSequence = 0;
        }

        public void StartPrediction()
        {
            NetworkRuntime runtime =
                NetworkRuntime.Current;

            if (runtime == null ||
                !runtime.RunsClient ||
                runtime.RunsServer)
            {
                return;
            }

            _pendingInputs.Clear();

            _lastReconciledInputSequence = 0;
            _hasReconciledInput = false;

            _predictionEnabled = true;

            _motor.SetSimulationEnabled(true);
        }

        public void StopPrediction()
        {
            _pendingInputs.Clear();
            _predictionEnabled = false;

            NetworkRuntime runtime =
                NetworkRuntime.Current;

            // The server owns the CharacterController on a
            // dedicated or listen server.
            if (runtime == null ||
                !runtime.RunsServer)
            {
                _motor?.SetSimulationEnabled(false);
            }
        }

        public void Predict(
            PlayerInputMessage message,
            float deltaTime)
        {
            if (!_predictionEnabled)
                return;

            _motor.Simulate(
                message,
                deltaTime);

            _pendingInputs.Add(message);

            if (_pendingInputs.Count >
                MaximumPendingInputs)
            {
                _pendingInputs.RemoveAt(0);
            }
        }

        public void SetLastProcessedInputSequence(
            uint inputSequence)
        {
            _lastProcessedInputSequence =
                inputSequence;
        }

        public override void WriteState(
            PacketWriter writer)
        {
            CharacterMotorState state =
                _motor.CaptureState();

            writer.Write(state.Position.x);
            writer.Write(state.Position.y);
            writer.Write(state.Position.z);

            writer.Write(state.Rotation.x);
            writer.Write(state.Rotation.y);
            writer.Write(state.Rotation.z);
            writer.Write(state.Rotation.w);

            writer.Write(state.VerticalVelocity);
            writer.Write(state.ControllerHeight);

            writer.Write(
                _lastProcessedInputSequence);
        }

        public override void ReadState(
            PacketReader reader,
            uint serverTick)
        {
            if (reader.Remaining != StateSize)
            {
                throw new InvalidOperationException(
                    "NetCharacterMotor state has an invalid size.");
            }

            var position =
                new Vector3(
                    reader.ReadFloat(),
                    reader.ReadFloat(),
                    reader.ReadFloat());

            var rotation =
                new Quaternion(
                    reader.ReadFloat(),
                    reader.ReadFloat(),
                    reader.ReadFloat(),
                    reader.ReadFloat());

            float verticalVelocity =
                reader.ReadFloat();

            float controllerHeight =
                reader.ReadFloat();

            uint acknowledgedInput =
                reader.ReadUInt32();

            if (!IsFinite(position) ||
                !IsFinite(rotation) ||
                !IsFinite(verticalVelocity) ||
                !IsFinite(controllerHeight) ||
                controllerHeight <= 0f)
            {
                throw new InvalidOperationException(
                    "NetCharacterMotor state contains invalid values.");
            }

            float magnitudeSquared =
                rotation.x * rotation.x +
                rotation.y * rotation.y +
                rotation.z * rotation.z +
                rotation.w * rotation.w;

            if (magnitudeSquared < 0.0001f)
            {
                throw new InvalidOperationException(
                    "NetCharacterMotor rotation is invalid.");
            }

            if (!_predictionEnabled)
                return;

            // Sequence zero means the server has not processed
            // any input from this player yet.
            if (acknowledgedInput == 0)
                return;

            // Ignore duplicate or older authoritative states.
            if (_hasReconciledInput &&
                !IsNewer(
                    acknowledgedInput,
                    _lastReconciledInputSequence))
            {
                return;
            }

            _hasReconciledInput = true;

            _lastReconciledInputSequence =
                acknowledgedInput;

            // Anything at or before the acknowledged sequence has
            // already been represented by the server state.
            _pendingInputs.RemoveAll(
                message =>
                    !IsNewer(
                        message.InputSequence,
                        acknowledgedInput));

            _motor.RestoreState(
                new CharacterMotorState(
                    position,
                    rotation.normalized,
                    verticalVelocity,
                    controllerHeight));

            const float tickDelta =
                1f / 33f;

            // Reapply inputs sent after the acknowledged input.
            for (int index = 0;
                 index < _pendingInputs.Count;
                 index++)
            {
                _motor.Simulate(
                    _pendingInputs[index],
                    tickDelta);
            }
        }

        private static bool IsNewer(
            uint candidate,
            uint reference)
        {
            return candidate != reference &&
                   unchecked(
                       (int)(candidate - reference)) > 0;
        }

        private static bool IsFinite(
            Vector3 value)
        {
            return IsFinite(value.x) &&
                   IsFinite(value.y) &&
                   IsFinite(value.z);
        }

        private static bool IsFinite(
            Quaternion value)
        {
            return IsFinite(value.x) &&
                   IsFinite(value.y) &&
                   IsFinite(value.z) &&
                   IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) &&
                   !float.IsInfinity(value);
        }
    }
}