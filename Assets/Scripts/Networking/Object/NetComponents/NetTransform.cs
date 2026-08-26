using System;
using UnityEngine;

namespace Networking
{
    [DisallowMultipleComponent]
    public sealed class NetTransform : NetBehaviour
    {
        [SerializeField]
        private float interpolationSharpness = 15f;

        [SerializeField]
        private float snapDistance = 5f;

        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private bool _hasTarget;

        public override NetComponentType ComponentType =>
            NetComponentType.Transform;

        public override void OnNetSpawn()
        {
            _targetPosition =
                transform.position;

            _targetRotation =
                transform.rotation;

            _hasTarget = false;
        }

        public override void OnNetDespawn()
        {
            _hasTarget = false;
        }

        public override void WriteState(
            PacketWriter writer)
        {
            var message =
                new TransformSnapshotMessage(
                    transform.position,
                    transform.rotation);

            message.Write(writer);
        }

        public override void ReadState(
            PacketReader reader,
            uint serverTick)
        {
            if (!TransformSnapshotMessage.TryRead(
                    reader,
                    out TransformSnapshotMessage message))
            {
                throw new InvalidOperationException(
                    "Received an invalid transform snapshot.");
            }

            _targetPosition =
                message.Position;

            _targetRotation =
                message.Rotation;

            if (!_hasTarget ||
                Vector3.Distance(
                    transform.position,
                    _targetPosition) >= snapDistance)
            {
                transform.SetPositionAndRotation(
                    _targetPosition,
                    _targetRotation);
            }

            _hasTarget = true;
        }

        public void Interpolate(float deltaTime)
        {
            if (!_hasTarget ||
                deltaTime <= 0f)
            {
                return;
            }

            float amount =
                1f -
                Mathf.Exp(
                    -interpolationSharpness *
                    deltaTime);

            transform.position =
                Vector3.Lerp(
                    transform.position,
                    _targetPosition,
                    amount);

            transform.rotation =
                Quaternion.Slerp(
                    transform.rotation,
                    _targetRotation,
                    amount);
        }
    }
}