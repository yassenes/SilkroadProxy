using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using LkjFramework;
using NLog;

namespace LkjGateway
{
    public class PerIPData
    {
        public string ip_;
        public bool banned_;
        public TrafficFilter filter_;
        public int connections_;

        public PerIPData(string ip)
        {
            ip_ = ip;
            connections_ = 0;
            banned_ = false;
        }
    }
    public class GatewayService
    {
        private static readonly Logger logger = LogManager.GetLogger("GatewayService");

        private Listener _listener;
        private ListenerSettings _listenerSettings;
        public List<PerIPData> _perIPList;

        private int _activeUsers = 0, _passiveUsers = 0;
        public int ActiveUsers => _activeUsers;
        public int PassiveUsers => _passiveUsers;
        public GatewayService(ListenerSettings listenerSettings)
        {
            _listenerSettings = listenerSettings;
            _perIPList = new List<PerIPData>();
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
            Console.WriteLine("New connection");

            new GatewayProxy(this, e).Start(_listenerSettings.RemoteIP, _listenerSettings.RemotePort);
        }

        public PerIPData GetPerIPData(string ip)
        {
            PerIPData perIPData = null;

            if (_perIPList.Exists(x => x.ip_ == ip))
                perIPData = _perIPList.Find(x => x.ip_ == ip);
            else
            {
                perIPData = new PerIPData(ip);
                _perIPList.Add(perIPData);
            }

            return perIPData;
        }

        public void BanIP(string ip)
        {
            _perIPList.Where(p => p.ip_ == ip).ToList().ForEach(s => s.banned_ = true);
        }

        public void AssignFilter(string ip, TrafficFilter filter)
        {
            _perIPList.Where(p => p.ip_ == ip).ToList().ForEach(s => s.filter_ = filter);
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
