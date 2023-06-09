using Microsoft.Extensions.Logging;
using SecureArchive.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.VisualStyles;

namespace SecureArchive.DI.Impl; 
internal class TaskQueueService : ITaskQueueService {
    IMainThreadService _mainThreadService;
    ILogger _logger;
    AtomicInteger _taskIdGenerator = new();

    struct QueueingTask {
        public int Id { get; }
        private Func<Task> _task;
        public QueueingTask(int id, Func<Task>task) {
            Id = id;
            _task = task;
        }
        public Task Execute() {
            return _task();
        }
    }

    public TaskQueueService(IMainThreadService mainThreadService, ILoggerFactory loggerFactory) {
        _mainThreadService = mainThreadService;
        _logger = loggerFactory.CreateLogger<TaskQueueService>();
    }

    public void PushTask(Action action) {
        Push(() => {
            action();
            return Task.CompletedTask;
        });
    }

    public void PushTask(Func<Task> action) {
        Push(action);
    }

    public void PushTask(Action<IMainThreadService> action) {
        Push(() => {
            action(_mainThreadService);
            return Task.CompletedTask;
        });
    }

    public void PushTask(Func<IMainThreadService,Task> action) {
        Push(() => {
            return action(_mainThreadService);
        });
    }

    private void Push(Func<Task> internalAction) {
        int id = _taskIdGenerator.IncrementAndGet();
        lock (this) {
            _logger.LogDebug("Task ({0}): Enqueued.", id);
            _taskQueue.Enqueue(new QueueingTask(id, internalAction));
        }
        Execute();
    }

    private Queue<QueueingTask> _taskQueue = new();
    private bool _executing = false;
    private void Execute() {
        lock(this) {
            if(_executing) return;
            _executing = true;
        }
        Task.Run( async () => {
            while (true) {
                QueueingTask task;
                lock (this) {
                    if (_taskQueue.Count > 0) {
                        task = _taskQueue.Dequeue();
                    }
                    else {
                        _executing = false;
                        return;
                    }
                }
                try {
                    await task.Execute();
                    _logger.LogDebug("Task ({0}): Completed.", task.Id);
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "Task ({0}): Error.", task.Id);
                }
            }
        });
    }
}
