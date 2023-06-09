using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureArchive.Utils {
    internal class AtomicInteger {
        private int _value;
        public int Get() { return Interlocked.Add(ref _value, 0); }
        public void Set(int v) { Interlocked.Exchange(ref _value, v); }
        public int GetAndSet(int v) { return Interlocked.Exchange(ref _value, v); }
        public int IncrementAndGet() { return Interlocked.Increment(ref _value); }
        public int DecrementAndGet() { return Interlocked.Decrement(ref _value); }
        public int GetAndAdd(int v) { return Interlocked.Add(ref _value, v); }
        public bool CompareAndSet(int expect, int value) { return Interlocked.CompareExchange(ref _value, value, expect) == expect; }
    }
}
