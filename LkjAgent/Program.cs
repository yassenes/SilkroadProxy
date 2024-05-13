using System;
using System.Threading;
using LkjFramework;
using System.Timers;
using Timer = System.Timers.Timer;
using System.Threading.Tasks;
using System.Linq;

namespace LkjAgent
{
    class Program
    {
        static ManualResetEvent _quitEvent = new ManualResetEvent(false);
        private static Timer _consoleTitleTimer = new Timer();
        private static DateTime lastDateExecute_;
        static void Main(string[] args)
        {
            Console.WindowWidth = 120;
            Console.BufferHeight = 4000;

            Console.CancelKeyPress += (sender, eArgs) => {
                _quitEvent.Set();
                eArgs.Cancel = true;
            };

            ListenerSettings listenerSettings;
            listenerSettings.ListenIP = "192.168.68.104";
            listenerSettings.ListenPort = 12343;
            listenerSettings.RemoteIP = "192.168.68.104";
            listenerSettings.RemotePort = 15884;

            AgentService service = new AgentService(listenerSettings);
            service.CreateListener();

            _consoleTitleTimer.Elapsed += (sender, e) => OnTimedEvent(sender, e, service);
            _consoleTitleTimer.Interval = 200;
            _consoleTitleTimer.Enabled = true;

            new PlannedQuery().Run();

            _quitEvent.WaitOne();
        }


        private static void OnTimedEvent(object sender, ElapsedEventArgs e, AgentService service)
        {
            Console.Title = $"[{typeof(Program).Assembly.GetName().Name}: {service.ActiveUsers}, {service.PassiveUsers}]";
        }
    }
}
