using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SecureArchive.Utils {
    internal class TaskQueue {
        public Queue<Func<Task>> _taskQueue = new Queue<Func<Task>>();
        public ReactivePropertySlim<bool> IsBusy = new ReactivePropertySlim<bool>(false);
        public TaskQueue() { }

        public void Push(Func<Task> action) {
            lock(this) {
                _taskQueue.Enqueue(action);
            }
            Execute();
        }

        private async void Execute() {
            lock(this) {
                if(IsBusy.Value) {
                    return;
                }
                IsBusy.Value = true;
            }
            while(true) {
                Func<Task> action;
                lock(this) {
                    if (_taskQueue.Count == 0) {
                        IsBusy.Value = false;
                        return;
                    }
                    action = _taskQueue.Dequeue();
                }
                await action();
            }
        }
    }
}
