using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.DI;
internal interface ITaskQueueService {
    void PushTask(Action action);
    void PushTask(Func<Task> action);

    void PushTask(Action<IMainThreadService> action);

    void PushTask(Func<IMainThreadService, Task> action);
}