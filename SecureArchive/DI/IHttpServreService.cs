using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.DI; 
internal interface IHttpServreService {
    IObservable<bool> Running { get; }
    bool Start(int port);
    void Stop();
}
