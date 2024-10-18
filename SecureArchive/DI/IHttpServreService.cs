using SecureArchive.Models.DB;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.DI;

internal interface IListSource {
    IList<FileEntry> GetFileList();
}
internal interface IHttpServreService {
    IObservable<bool> Running { get; }
    bool Start(int port);
    void Stop();
    IListSource? ListSource { get; set; }
}
