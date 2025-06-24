using SecureArchive.Models.DB;
using SecureArchive.Utils;

namespace SecureArchive.Models.CryptoStream {
    internal class CryptoStreamHandler : ICryptoStreamHandler
    {
        private static UtLog _logger = new(typeof(CryptoStreamHandler));
        private Dictionary<FileEntry, CryptoStreamPool> _pools = new();

        public ICryptoStreamContainer LockStream(FileEntry fileEntry, long id)
        {
            lock (_pools)
            {
                try
                {
                    _logger.Debug($"[{id}] Lock: {fileEntry.Name}");
                    if (!_pools.TryGetValue(fileEntry, out var pool))
                    {
                        _logger.Debug($"[{id}] Create Pool for {fileEntry.Name}");
                        pool = new CryptoStreamPool(fileEntry, id);
                        _pools.Add(fileEntry, pool);
                    }
                    return pool.LockStream();
                }
                finally
                {
                    Sweep();
                }
            }
        }

        public void UnlockStream(ICryptoStreamContainer container, long id)
        {
            lock (_pools)
            {
                try
                {
                    _logger.Debug($"[{id}] Unlock: {container.FileEntry.Name}");
                    if (!_pools.TryGetValue(container.FileEntry, out var pool))
                    {
                        throw new Exception("UnlockStream: no entry");
                    }
                    pool.UnlockStream(container);
                }
                finally
                {
                    Sweep();
                }
            }
        }

        private void Sweep()
        {
            var keys = _pools.Keys.ToList();
            foreach (var key in keys)
            {
                if (_pools[key].Sweep())
                {
                    _logger.Debug($"Removing Pool for {key.Name}");
                    _pools.Remove(key);
                }
            }
        }



        //public void BackgroundSweep() {
        //    Task.Run(() => {
        //        while (true) {
        //            Sweep();
        //            Task.Delay(TimeSpan.FromMinutes(3)).Wait();
        //        }
        //    });
        //}

        public void Dispose()
        {
            lock (_pools)
            {
                foreach (var pool in _pools.Values)
                {
                    pool.Dispose();
                }
                _pools.Clear();
            }
        }

    }
}
