using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace UnityMcp.Core;

public sealed class MainThreadScheduler
{
    private readonly ConcurrentQueue<IWorkItem> _queue = new ConcurrentQueue<IWorkItem>();

    public Task<T> Enqueue<T>(Func<T> work)
    {
        if (work == null)
        {
            throw new ArgumentNullException(nameof(work));
        }

        var item = new WorkItem<T>(work);
        _queue.Enqueue(item);
        return item.Task;
    }

    public int RunPending(int maxItems)
    {
        if (maxItems <= 0)
        {
            return 0;
        }

        var count = 0;
        while (count < maxItems && _queue.TryDequeue(out var item))
        {
            item.Run();
            count++;
        }

        return count;
    }

    private interface IWorkItem
    {
        void Run();
    }

    private sealed class WorkItem<T> : IWorkItem
    {
        private readonly Func<T> _work;
        private readonly TaskCompletionSource<T> _completion = new TaskCompletionSource<T>();

        public WorkItem(Func<T> work)
        {
            _work = work;
        }

        public Task<T> Task => _completion.Task;

        public void Run()
        {
            try
            {
                _completion.SetResult(_work());
            }
            catch (Exception ex)
            {
                _completion.SetException(ex);
            }
        }
    }
}
