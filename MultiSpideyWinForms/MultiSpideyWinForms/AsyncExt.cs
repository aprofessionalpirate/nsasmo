using System.Threading;
using System.Threading.Tasks;

namespace MultiSpideyWinForms
{
    public static class AsyncExt
    {
        public static Task<T> WithWaitCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            return task.IsCompleted
                ? task
                : task.ContinueWith(
                    completedTask => completedTask.GetAwaiter().GetResult(),
                    cancellationToken,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
        }
    }
}
