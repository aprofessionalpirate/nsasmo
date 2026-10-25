using System.Threading;
using System.Threading.Tasks;

namespace MultiSpideyWinForms
{
    public abstract class SpideyUdpBase
    {
        private readonly object _lastLocationLock = new object();
        protected readonly ushort _port;

        protected Task _udpTask;
        protected CancellationTokenSource _udpTaskCancellationToken;

        private string _myLastLocation;
        public string MyLastLocation
        {
            get { lock (_lastLocationLock) { return _myLastLocation; } }
            set { lock (_lastLocationLock) { _myLastLocation = value; } }
        }

        public SpideyUdpBase(ushort port)
        {
            _port = port;
        }

        protected bool IsServerTaskStopped()
        {
            return _udpTask == null || _udpTask.IsCompleted;
        }

        public void Stop()
        {
            if (_udpTask != null)
            {
                _udpTaskCancellationToken.Cancel();
                _udpTask.Wait();
            }
        }
    }
}
