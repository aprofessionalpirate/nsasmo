using System.Threading;
using System.Threading.Tasks;

namespace MultiSpideyWinForms
{
    public abstract class SpideyUdpBase
    {
        protected readonly ushort _port;

        protected Task _udpTask;
        protected CancellationTokenSource _udpTaskCancellationToken;

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
