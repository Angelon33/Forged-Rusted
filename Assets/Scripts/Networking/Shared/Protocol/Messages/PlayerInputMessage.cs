using System;
using UnityEngine;

namespace Networking
{
    [Flags]
    public enum PlayerInputButtons : byte
    {
        None = 0,
        Jump = 1 << 0,
        Sprint = 1 << 1,
        Crouch = 1 << 2
    }

    public readonly struct PlayerInputMessage
    {
        public const int PayloadSize =
            sizeof(uint) +
            sizeof(uint) +
            sizeof(float) +
            sizeof(float) +
            sizeof(float) +
            sizeof(byte);

        public uint NetworkId { get; }

        public uint InputSequence { get; }

        public Vector2 Move { get; }

        public float Yaw { get; }

        public PlayerInputButtons Buttons { get; }

        public bool Jump =>
            (Buttons & PlayerInputButtons.Jump) != 0;

        public bool Sprint =>
            (Buttons & PlayerInputButtons.Sprint) != 0;

        public bool Crouch =>
            (Buttons & PlayerInputButtons.Crouch) != 0;

        public PlayerInputMessage(
            uint networkId,
            uint inputSequence,
            Vector2 move,
            float yaw,
            PlayerInputButtons buttons)
        {
            NetworkId = networkId;
            InputSequence = inputSequence;
            Move = Vector2.ClampMagnitude(move, 1f);
            Yaw = Mathf.Repeat(yaw, 360f);
            Buttons = buttons;
        }

        public PlayerInputMessage WithoutJump()
        {
            return new PlayerInputMessage(
                NetworkId,
                InputSequence,
                Move,
                Yaw,
                Buttons & ~PlayerInputButtons.Jump);
        }

        public void Write(PacketWriter writer)
        {
            writer.Write(NetworkId);
            writer.Write(InputSequence);
            writer.Write(Move.x);
            writer.Write(Move.y);
            writer.Write(Yaw);
            writer.Write((byte)Buttons);
        }

        public static bool TryRead(
            byte[] data,
            out PlayerInputMessage message)
        {
            message = default;

            if (data == null ||
                data.Length != PayloadSize)
            {
                return false;
            }

            try
            {
                var reader =
                    new PacketReader(data);

                uint networkId =
                    reader.ReadUInt32();

                uint inputSequence =
                    reader.ReadUInt32();

                float moveX =
                    reader.ReadFloat();

                float moveY =
                    reader.ReadFloat();

                float yaw =
                    reader.ReadFloat();

                var buttons =
                    (PlayerInputButtons)
                    reader.ReadByte();

                const PlayerInputButtons allowedButtons =
                    PlayerInputButtons.Jump |
                    PlayerInputButtons.Sprint |
                    PlayerInputButtons.Crouch;

                if (networkId == 0 ||
                    !IsFinite(moveX) ||
                    !IsFinite(moveY) ||
                    !IsFinite(yaw) ||
                    (buttons & ~allowedButtons) != 0)
                {
                    return false;
                }

                message =
                    new PlayerInputMessage(
                        networkId,
                        inputSequence,
                        new Vector2(moveX, moveY),
                        yaw,
                        buttons);

                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) &&
                   !float.IsInfinity(value);
        }
    }
}