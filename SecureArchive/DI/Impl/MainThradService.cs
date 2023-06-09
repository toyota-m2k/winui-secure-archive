using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.DI.Impl;

internal class MainThradService : IMainThreadService {
    private TaskScheduler _taskScheduler;
    private int _threadId;
    public MainThradService() {
        _taskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
        _threadId = Environment.CurrentManagedThreadId;
    }
    public void Run(Action f) {
        if(IsMainThread) {
            f();
            return;
        }
        Task t = new Task(f);
        t.RunSynchronously(_taskScheduler);
    }
    public T Run<T>(Func<T> f) {
        if (IsMainThread) {
            return f();
        }
        Task<T> t = new Task<T>(f);
        t.RunSynchronously(_taskScheduler);
        return t.Result;
    }
    public void Run(Task t) {
        t.RunSynchronously(_taskScheduler);
    }
    public bool IsMainThread => _threadId == Environment.CurrentManagedThreadId;
}
