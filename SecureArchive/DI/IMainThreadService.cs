using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.DI; 
internal interface IMainThreadService {
    void Run(Action f);
    T Run<T>(Func<T> f);
    public void Run(Task t);
    public bool IsMainThread { get; }
}
