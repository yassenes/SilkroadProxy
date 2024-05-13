using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using LkjFramework;
using NLog;

namespace LkjAgent
{
    public class AgentService
    {
        private static readonly Logger logger = LogManager.GetLogger("AgentService");

        private Listener _listener;
        private ListenerSettings _listenerSettings;
        private List<AgentProxy> _userList;

        private int _activeUsers = 0, _passiveUsers = 0;
        public int ActiveUsers => _activeUsers;
        public int PassiveUsers => _passiveUsers;

        public AgentService(ListenerSettings listenerSettings)
        {
            _listenerSettings = listenerSettings;
            _userList = new List<AgentProxy>();
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
            AgentProxy agentProxy = new AgentProxy(this, e);
            agentProxy.Start(_listenerSettings.RemoteIP, _listenerSettings.RemotePort);
        }

        public void AddUser(AgentProxy agentProxy)
        {
            _userList.Add(agentProxy);
        }
        public void RemoveUser(AgentProxy agentProxy)
        {
            _userList.Remove(agentProxy);
        }

        public void PassiveIncrement()
        {
            Interlocked.Increment(ref _passiveUsers);
        }
        public void PassiveDecrement()
        {
            if (_passiveUsers != 0)
                Interlocked.Decrement(ref _passiveUsers);
        }
        public void ActiveIncrement()
        {
            Interlocked.Increment(ref _activeUsers);
        }
        public void ActiveDecrement()
        {
            if (_activeUsers != 0)
                Interlocked.Decrement(ref _activeUsers);
        }
    }
}
