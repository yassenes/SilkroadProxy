using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using NLog;

namespace LkjFramework
{
    public struct ListenerSettings
    {
        public string ListenIP, RemoteIP;
        public ushort ListenPort, RemotePort;
    }
    public class Listener
    {
        private static readonly Logger logger = LogManager.GetLogger("Listener");

        private TcpListener _listener;
        private bool _stopped;

        public event EventHandler<TcpClient> ConnectEventHandler;

        public Listener(string ipAddress, ushort port)
        {
            _listener = new TcpListener(IPAddress.Parse(ipAddress), port);
        }

        public void Run()
        {
            try
            {
                _listener.Start();
            }
            catch (SocketException ex)
            {
                logger.Debug($"Listener couldn't start. (Reason: {ex})");
            }

            _ = Task.Run(() => PostAccept());
        }

        public void Stop()
        {
            _stopped = true;
        }

        public async Task PostAccept()
        {
            while (!_stopped)
            {
                if (_listener.Pending())
                {
                    TcpClient acceptedSocket = null;
                    try
                    {
                        acceptedSocket = await _listener.AcceptTcpClientAsync();
                    }
                    catch (SocketException ex)
                    {
                        logger.Debug($"Socket couldn't be accepted. (Reason: {ex})");
                    }
                    finally
                    {
                        if (acceptedSocket != null)
                            ConnectEventHandler?.Invoke(this, acceptedSocket);
                    }
                }

                await Task.Delay(10);
            }
        }
    }
}
