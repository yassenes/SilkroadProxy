using System;
using System.Threading;
using LkjFramework;
using System.Timers;
using Timer = System.Timers.Timer;


namespace LkjDownload
{
    class Program
    {
        private static ManualResetEvent _quitEvent = new ManualResetEvent(false);
        private static Timer _consoleTitleTimer = new Timer();
        static void Main(string[] args)
        {
            Console.WindowWidth = 120;
            Console.BufferHeight = 4000;

            Console.CancelKeyPress += (sender, eArgs) => {
                _quitEvent.Set();
                eArgs.Cancel = true;
            };

            ListenerSettings listenerSettings;
            listenerSettings.ListenIP = "168.119.123.239";
            listenerSettings.ListenPort = 12342;
            listenerSettings.RemoteIP = "168.119.123.239";
            listenerSettings.RemotePort = 15881;

            DownloadService service = new DownloadService(listenerSettings);
            service.CreateListener();

            _consoleTitleTimer.Elapsed += (sender, e) => OnTimedEvent(sender, e, service);
            _consoleTitleTimer.Interval = 200;
            _consoleTitleTimer.Enabled = true;

            _quitEvent.WaitOne();
        }

        private static void OnTimedEvent(object sender, ElapsedEventArgs e, DownloadService service)
        {
            Console.Title = $"[{typeof(Program).Assembly.GetName().Name}: {service.ActiveUsers}, {service.PassiveUsers}]";
        }
    }
}
