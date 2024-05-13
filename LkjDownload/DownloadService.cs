using System.Net.Sockets;
using System.Threading;
using LkjFramework;
using NLog;

namespace LkjDownload
{
    public class DownloadService
    {
        private static readonly Logger logger = LogManager.GetLogger("DownloadService");

        private Listener _listener;
        private ListenerSettings _listenerSettings;

        private int _activeUsers = 0, _passiveUsers = 0;
        public int ActiveUsers => _activeUsers;
        public int PassiveUsers => _passiveUsers;
        public DownloadService(ListenerSettings listenerSettings)
        {
            _listenerSettings = listenerSettings;
        }
        public void CreateListener()
        {
            _listener = new Listener(_listenerSettings.ListenIP, _listenerSettings.ListenPort);
            _listener.ConnectEventHandler += OnConnect;
            _listener.Run();

            logger.Info($"TcpListener has started. [{_listenerSettings.ListenIP}:{_listenerSettings.ListenPort} -> {_listenerSettings.RemoteIP}:{_listenerSettings.RemotePort}]");
        }

        private void OnConnect(object sender, TcpClient e)
        {
            DownloadProxy downloadProxy = new DownloadProxy(this, e);
            downloadProxy.Start(_listenerSettings.RemoteIP, _listenerSettings.RemotePort);
        }

        public void PassiveIncrement()
        {
            Interlocked.Increment(ref _passiveUsers);
        }
        public void PassiveDecrement()
        {
            Interlocked.Decrement(ref _passiveUsers);
        }
        public void ActiveIncrement()
        {
            Interlocked.Increment(ref _activeUsers);
        }
        public void ActiveDecrement()
        {
            Interlocked.Decrement(ref _activeUsers);
        }
    }
}
