using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEngine;

namespace Networking {
    public class GameServer
    {

        private readonly Dictionary<byte, Peer> _peers = new();

        private INetworkTransport _transport;

        private List<ReceivedPacket> _packets = new();

        public GameServer(INetworkTransport transport)
        {
            this._transport = transport;
        }

        public void Start()
        {
            if (this._transport == null)
            {
                return;
            }

            _transport.Start();
            Debug.Log("Server started");
        }

        public void Close()
        {
            _transport.Dispose();
            Debug.Log("Server stopped");
        }

        public void Update()
        {
            _transport.Poll(_packets);

            foreach (var packet in _packets)
            {
                var reader = new PacketReader(packet.Data);
                byte version = reader.ReadByte();
                if(version != Packet.VERSION)
                {
                    continue;
                }
                HandleRaw(reader, packet);
            }
        }

        private void HandleRaw(PacketReader reader, ReceivedPacket raw)
        {

            PacketType type = (PacketType)reader.ReadByte();

            Peer peer = ResolvePeer(raw.Handle);

            HandlePacket(type, reader, peer);
        }

        private void HandlePacket(PacketType type, PacketReader reader, Peer peer)
        {
            switch (type)
            {
                case PacketType.Join_Request:
                    HandleJoin(peer);
                    break;

            }
        }

        private void HandleJoin(Peer peer)
        {
            PacketWriter writer = new PacketWriter();

            Join_Response response = new Join_Response(peer.Id);

            Debug.Log("RECEIVED JOIN REQUEST");
            Debug.Log("RESPONDING WITH ID: " + peer.Id);

            response.Serialize(ref writer);

            _transport.Send(writer.ToArray(), peer.Handle);
        }

        private Peer ResolvePeer(ITransportHandle handle)
        {
            foreach (var p in _peers.Values) {
                if (p.Handle.Equals(handle))
                    return p;
            }
            byte id = PeerIdProvider.Next();

            var peer = new Peer(id, handle);
            _peers[id] = peer;

            return peer;
        }
    }

    public static class PeerIdProvider
    {
        private static byte _nextId = 1;

        public static byte Next()
        {
            return _nextId++;
        }

        public static void Reset()
        {
            _nextId = 1;
        }
    }
}
