using System;
using System.Net.Sockets;
using LkjFramework;
using SilkroadSecurityAPI;

namespace LkjGateway
{
    public class GatewayProxy
    {
        private GatewayService _service;
        private AsyncProxy _proxy;
        private PerIPData _perIPData;
        bool _activeConn, _recvd6100;
        TrafficFilter general, p6106, p2002_6101, p6102, p6104, p6323;

        public GatewayProxy(GatewayService gatewayService, TcpClient socket)
        {
            _service = gatewayService;
            _proxy = new AsyncProxy(socket);

            _proxy.OnConnect += OnConnect;
            _proxy.OnDisconnect += OnDisconnect;
            _proxy.OnReceive += OnReceive;
        }
        public void Start(string remoteIP, ushort remotePort)
        {
            _perIPData = _service.GetPerIPData(_proxy.GetRemoteIP);

            if (!_perIPData.filter_.Attempt(1000, 1, 5))
            {
                _proxy.CloseReason("connection refused due to spam");
                if (!_perIPData.filter_.Attempt(1000, 0, 15))
                {
                    _perIPData.banned_ = true;
                    _proxy.CloseReason("connection flood, banned");
                }
            }

            //foreach (var p in _service._perIPList)
            //{
            //    if (p.ip_ == _proxy.GetRemoteIP)
            //    {
            //        Console.WriteLine("Exists: " + _proxy.GetRemoteIP); 
            //        if (!p.filter_.Attempt(1000, 1, 5))
            //        {
            //            _proxy.CloseReason("connection refused due to spam");
            //            if (!p.filter_.Attempt(1000, 0, 15))
            //            {
            //                p.banned_ = true;
            //                _proxy.CloseReason("connection flood, banned");
            //            }
            //        }
            //        _perIPData = p;
            //    }
            //}
            //if (_perIPData == null)
            //{
            //    _perIPData = new PerIPData(_proxy.GetRemoteIP);
            //    _service._perIPList.Add(_perIPData);
            //    Console.WriteLine("Added: " + _proxy.GetRemoteIP);
            //}

            if (_perIPData.banned_)
                _proxy.CloseReason("banned");
            else
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
                if (_perIPData.banned_)
                {
                    _proxy.CloseReason("banned");
                    return false;
                }
                if (!general.Attempt(1000, 1, 8))
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
                    case 0x6100:
                        {
                            if (_recvd6100)
                            {
                                _proxy.CloseReason("duplicated patch request");
                                return false;
                            }
                            _recvd6100 = true;
                            byte locale = packet.ReadUInt8();
                            string clientId = packet.ReadAscii();
                            uint version = packet.ReadUInt32();

                            if (packet.Size == 0 || clientId != "SR_Client")
                                _proxy.CloseReason("invalid patch request packet");

                        }
                        return false;
                    case 0x2002:
                    case 0x6101:
                    case 0x6106:
                        {
                            //if (packet.Size == 0)
                            //    _proxy.CloseReason("invalid ping / server list packet / 6106");
                            if (packet.Opcode == 0x6106)
                            {
                                if (!p6106.Attempt(1000, 1, 2))
                                    _proxy.CloseReason("6106 flood");
                            }
                            else if (!p2002_6101.Attempt(1500, 1, 4))
                                _proxy.CloseReason("ping / server list flood");
                        }
                        return false;
                    case 0x6102:
                        {
                            if (!p6102.Attempt(3000, 1, 2))
                                _proxy.CloseReason("login flood");
                        }
                        return false;
                    case 0x6104:
                        {
                            if (packet.Size != 1)
                                _proxy.CloseReason("invalid launcher news packet");
                            else if (!p6104.Attempt(3000, 1, 2))
                                _proxy.CloseReason("login flood");
                        }
                        return false;
                    case 0x6323:
                        {
                            if (!p6323.Attempt(1000, 1, 3))
                                _proxy.CloseReason("captcha flood");
                        }
                        return false;
                    //case 0x7009:
                    //    return false;
                    default:
                        {
                            //_service.BanIP(_proxy.GetRemoteIP);
                            _perIPData.banned_ = true;
                            _proxy.CloseReason($"illegal opcode 0x{packet.Opcode:X4}");
                        }
                        return true;
                }
            }
            else
            {
                switch (packet.Opcode)
                {
                    case 0x2322:
                        {
                            Packet captcha = new Packet(0x6323, false);
                            captcha.WriteAscii("1");
                            _proxy.SendPacket(false, captcha);
                        }
                        return true;
                    case 0xA102:
                        {
                            byte flag = packet.ReadUInt8();
                            if (flag == 1)
                            {
                                uint id = packet.ReadUInt32();
                                string ipAddress = packet.ReadAscii();
                                ushort port = packet.ReadUInt16();

                                Packet login_request = new Packet(0xA102, true);
                                login_request.WriteUInt8(flag);
                                login_request.WriteUInt32(id);
                                login_request.WriteAscii(ipAddress);
                                login_request.WriteUInt16(12343);

                                _proxy.SendPacket(true, login_request);
                            }
                        }
                        return true;
                    case 0xA100:
                        {
                            byte result = packet.ReadUInt8();

                            if (result == 0x02)
                            {
                                byte errorCode = packet.ReadUInt8();

                                if (errorCode == 0x02)
                                {
                                    string ipAddress = packet.ReadAscii(); // IP
                                    ushort port = packet.ReadUInt16();//Port
                                    uint version = packet.ReadUInt32();
                                    byte fileFlag = packet.ReadUInt8();
                                    Packet DLSrv = new Packet(0xA100, false, true);
                                    DLSrv.WriteByte(result);
                                    DLSrv.WriteByte(errorCode);
                                    DLSrv.WriteAscii(ipAddress);
                                    DLSrv.WriteShort(12342);
                                    DLSrv.WriteUInt32(version);
                                    DLSrv.WriteByte(fileFlag);
                                    while (fileFlag == 0x01)
                                    {
                                        uint fileID = packet.ReadUInt32();
                                        string fileName = packet.ReadAscii();
                                        string filePath = packet.ReadAscii();
                                        uint fileLength = packet.ReadUInt32();
                                        byte toBePacked = packet.ReadUInt8();
                                        fileFlag = packet.ReadUInt8();

                                        DLSrv.WriteUInt32(fileID);
                                        DLSrv.WriteAscii(fileName);
                                        DLSrv.WriteAscii(filePath);
                                        DLSrv.WriteUInt32(fileLength);
                                        DLSrv.WriteUInt8(toBePacked);
                                        DLSrv.WriteUInt8(fileFlag);
                                    }
                                    _proxy.SendPacket(true, DLSrv);
                                    return true;
                                }
                            }
                        }
                        return false;
                }
            }
            return false;
        }
    }
}
