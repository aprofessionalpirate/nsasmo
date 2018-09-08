using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace MultiSpideyWinForms
{
    public static class TaskExtensions
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

        // This is to allow exceptions thrown on the task to stop the program
        public static Task ObserveExceptions(this Task task)
        {
            return task.ContinueWith((t) =>
            {
                ThreadPool.QueueUserWorkItem((w) =>
                {
                    if (t.Exception != null)
                    {
                        if (FoundUnexpectedExceptions(t.Exception))
                        {
                            // This allows the stack trace to remain intact
                            ExceptionDispatchInfo.Capture(t.Exception).Throw();
                        }
                    }
                });
            }, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.PreferFairness);
        }

        private static bool FoundUnexpectedExceptions(AggregateException aAggregateException)
        {
            var lFoundUnexpectedExceptions = false;
            foreach (var lException in aAggregateException.InnerExceptions)
            {
                if (lException is TaskCanceledException) continue;
                if (lException is AggregateException lAggregateException)
                {
                    lFoundUnexpectedExceptions |= FoundUnexpectedExceptions(lAggregateException);
                }
                else
                {
                    lFoundUnexpectedExceptions = true;
                }
            }

            return lFoundUnexpectedExceptions;
        }
    }
}