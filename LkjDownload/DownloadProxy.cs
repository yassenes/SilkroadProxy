using System;
using System.Net.Sockets;
using LkjFramework;
using SilkroadSecurityAPI;

namespace LkjDownload
{
    public class DownloadProxy
    {
        private DownloadService _service;
        public AsyncProxy _proxy;
        bool _activeConn, _downloadingFile;
        private TrafficFilter recvBytes, p2002;

        public DownloadProxy(DownloadService downloadService, TcpClient socket)
        {
            _service = downloadService;
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
        }

        private void OnDisconnect(object sender, EventArgs e)
        {
            if (_activeConn)
                _service.ActiveDecrement();
            else
                _service.PassiveDecrement();
        }

        private bool OnReceive(bool client, Packet packet)
        {
            if (client)
            {
                if (!recvBytes.Attempt(1000, packet.Size, 256))
                {
                    _proxy.CloseReason("traffic filter");
                    return false;
                }
                switch (packet.Opcode)
                {
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
                    case 0x2002:
                        {
                            if (packet.Size == 0)
                                _proxy.CloseReason("invalid ping packet");
                            else if (!p2002.Attempt(1500, 1, 4))
                                _proxy.CloseReason("ping flood");
                        }
                        return false;
                    case 0x6004:
                        {
                            if (_downloadingFile)
                            {
                                //banned
                                _proxy.CloseReason("tried to download multiple files at once");
                                return false;
                            }
                            if (packet.Size != 8)
                            {
                                //banned
                                _proxy.CloseReason("invalid file request packet");
                                return false;
                            }
                            _downloadingFile = true;
                        }
                        return false;
                    default:
                        {
                            // banned
                            _proxy.CloseReason($"illegal opcode 0x{packet.Opcode:X4}");
                        }
                        return true;
                }
            }
            else
            {
                switch (packet.Opcode)
                {
                    case 0xA004:
                        {
                            if (packet.ReadUInt8() != 1)
                            {
                                _proxy.SendPacket(true, packet);
                                _proxy.Close(false);
                                return false;
                            }
                            _downloadingFile = false;
                        }
                        return false;
                }
            }
            return false;
        }
    }
}
