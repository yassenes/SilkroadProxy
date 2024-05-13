using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using SilkroadSecurityAPI;
using NLog;
using System.Net;

namespace LkjFramework
{
    public struct ProxyContext
    {
        public TcpClient socket;
        public Security security;
        public byte[] buffer;
        public bool client;
    }
    public class AsyncProxy : IDisposable
    {
        private static readonly Logger logger = LogManager.GetLogger("AsyncProxy");

        private ProxyContext _client, _server;
        private string _remoteIP;
        private bool _disconnected, _backendEstablished;
        private int identityPacketLen_;

        public string GetRemoteIP => _remoteIP;

        #region EventHandlers
        public delegate bool ReceiveEventHandler(bool client, Packet packet);
        public event ReceiveEventHandler OnReceive;
        public event EventHandler OnConnect;
        public event EventHandler OnDisconnect;
        #endregion

        #region Dispose
        private bool disposedValue;
        #endregion

        public AsyncProxy(TcpClient socket)
        {
            _client.socket = socket;

            _client.client = true;

            _remoteIP = ((IPEndPoint)_client.socket.Client.RemoteEndPoint).Address.ToString();
        }

        public void SetupCtx()
        {
            _client.security = new Security();
            _client.security.GenerateSecurity(true, true, true);

            _server.security = new Security();

            _client.buffer = new byte[4096];
            _server.buffer = new byte[4096];
        }

        public async void Start(string remoteIP, ushort remotePort)
        {
            try
            {
                _server.socket = new TcpClient();
                await _server.socket.ConnectAsync(remoteIP, remotePort);
            }
            catch (SocketException ex)
            {
                CloseReason("server is offline");
                logger.Debug($"can't connect server ({ex} [{remoteIP}:{remotePort}]");
                return;
            }
            finally
            {
                identityPacketLen_ = 0;

                SetupCtx();

                OnConnect?.Invoke(this, null);

                _ = Task.Run(() => PostReceive(_client));
                _ = Task.Run(() => PostReceive(_server));
            }

            identityPacketLen_ = 0;
        }

        public async Task PostReceive(ProxyContext ctx)
        {
            if (_disconnected)
                return;

            var refctx = ctx.client ? _server : _client;

            int bytesTransferred = 0;

            try
            {
                bytesTransferred = await ctx.socket.GetStream().ReadAsync(ctx.buffer, 0, ctx.buffer.Length);
            }
            catch
            {
                Close(false);
            }

            if (bytesTransferred == 0)
            {
                Close(false);
                return;
            }

            ctx.security.Recv(ctx.buffer, 0, bytesTransferred);

            var packetList = ctx.security.TransferIncoming();
            if (packetList != null)
            {
                foreach (var packet in packetList)
                {
                    if (packet.Opcode == 0x5000 || packet.Opcode == 0x9000)
                        continue;

                    if (packet.Opcode == 0x2001)
                    {
                        if (ctx.client)
                        {
                            if (identityPacketLen_ != 0)
                            {
                                CloseReason("duplicated identity packet");
                                return;
                            }
                            if (packet.Size > 64)
                            {
                                CloseReason("identify packet is too big");
                                return;
                            }

                            identityPacketLen_ = packet.Size;
                        }
                        else
                            _backendEstablished = true;
                    }

                    if (ctx.client && !_backendEstablished)
                    {
                        CloseReason("illegal packet");
                        return;
                    }

                    if (OnReceive(ctx.client, packet))
                        continue;

                    refctx.security.Send(packet);
                }
            }

            await PostSend(refctx);
            await PostReceive(ctx);
        }

        private async Task PostSend(ProxyContext ctx)
        {
            if (_disconnected)
                return;

            var packetList = ctx.security.TransferOutgoing();
            if (packetList != null)
            {
                foreach (var packet in packetList)
                {
                    try
                    {
                        await ctx.socket.GetStream().WriteAsync(packet.Key.Buffer, 0, packet.Key.Buffer.Length);
                    }
                    catch
                    {
                        Close(false);
                    }
                }
            }
        }

        public async void SendPacket(bool client, Packet packet)
        {
            var refctx = client ? _client : _server;
            refctx.security.Send(packet);
            await PostSend(refctx);
        }

        public void CloseReason(string reason)
        {
            logger.Debug($"User disconnected (IP: {GetRemoteIP}, Reason: {reason})");
            Close(false);
        }

        public void Close(bool hard)
        {
            if (!_disconnected)
            {
                _disconnected = true;

                if (hard)
                    _client.socket.LingerState = new LingerOption(true, 0);

                _client.socket.Close();
                _server.socket.Close();

                OnDisconnect?.Invoke(this, null);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (_client.socket.Connected || _client.socket != null)
                        _client.socket.Close();

                    if (_server.socket.Connected || _server.socket != null)
                        _server.socket.Close();
                }

                _client.socket = null;
                _client.security = null;
                _client.buffer = null;

                _server.socket = null;
                _server.security = null;
                _server.buffer = null;

                disposedValue = true;
            }
        }

        ~AsyncProxy()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
