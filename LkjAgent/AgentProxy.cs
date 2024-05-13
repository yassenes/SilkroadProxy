using System;
using System.Net.Sockets;
using LkjFramework;
using SilkroadSecurityAPI;

namespace LkjAgent
{
    public class AgentProxy
    {
        private AgentService _service;
        private AsyncProxy _proxy;
        bool _activeConn;

        public AgentProxy(AgentService agentService, TcpClient socket)
        {
            _service = agentService;
            _proxy = new AsyncProxy(socket);

            _proxy.OnConnect += OnConnect;
            _proxy.OnDisconnect += OnDisconnect;
            _proxy.OnReceive += OnReceive;
        }
        public void Start(string remoteIP, ushort remotePort)
        {
            _proxy.Start(remoteIP, remotePort);
        }

        private void OnConnect(object sender, EventArgs e)
        {
            _service.PassiveIncrement();

            _service.AddUser(this);
        }

        private void OnDisconnect(object sender, EventArgs e)
        {
            if (_activeConn)
                _service.ActiveDecrement();
            else
                _service.PassiveDecrement();

            _service.RemoveUser(this);
        }

        private bool OnReceive(bool client, Packet packet)
        {
            if (client)
            {
                switch (packet.Opcode)
                {
                    case 0x600d:
                        _proxy.CloseReason("massive packet");
                        return true;
                    case 0x2001:
                        {
                            string id = packet.ReadAscii();
                            byte flag = packet.ReadUInt8();

                            if (id != "SR_Client" || flag != 0)
                                _proxy.CloseReason("invalid module name|flag");
                            else
                            {
                                _service.PassiveDecrement();
                                _activeConn = true;
                                _service.ActiveIncrement();
                            }
                        }
                        return true;
                    case 0x7801:
                        return true;
                }
            }
            else
            {
                switch (packet.Opcode)
                {
                    case 0x3305:
                        {
                            byte worldId = packet.ReadUInt8();
                            byte jobData = packet.ReadUInt8();
                            bool party = packet.ReadBool();

                            Packet friendData = new Packet(0x3305);
                            for (int i = 0; i < packet.Size - 3; i++)
                                friendData.WriteUInt8(packet.ReadUInt8());

                            _proxy.SendPacket(true, friendData);
                        }
                        return true;
                }
            }
            return false;
        }
    }
}
