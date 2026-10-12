#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;

namespace AbsoluteZero.Core.Solo
{
    // Only immutable, already serialized strings cross the worker boundary.
    // Failure is evidence loss, never a gameplay exception or an implicit pass.
    public sealed class PlaytestJsonlSink : IDisposable
    {
        readonly BlockingCollection<string> _queue;
        readonly Thread _writer;
        readonly string _partial, _complete;
        readonly Action _beforeDrain;
        int _dropped, _written, _closed;
        string _error;
        public int Dropped => Volatile.Read(ref _dropped);
        public int Written => Volatile.Read(ref _written);
        public string Error => Volatile.Read(ref _error);
        public int HighWater { get; private set; }
        public bool Complete { get; private set; }

        // Optional worker-side fault gate is for deterministic recorder tests only.
        public PlaytestJsonlSink(string path, int capacity = 4096, Action beforeDrain = null)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _complete = Path.GetFullPath(path); _partial = _complete + ".partial";
            Directory.CreateDirectory(Path.GetDirectoryName(_complete));
            if (File.Exists(_complete) || File.Exists(_partial)) throw new IOException("Evidence file already exists");
            _queue = new BlockingCollection<string>(capacity);
            _beforeDrain = beforeDrain;
            _writer = new Thread(WriteLoop) { IsBackground = true, Name = "AZ playtest JSONL" };
            _writer.Start();
        }
        public bool Append(string json)
        {
            if (string.IsNullOrEmpty(json) || json.IndexOf('\n') >= 0 || json.IndexOf('\r') >= 0)
                throw new ArgumentException("Expected one serialized JSON record", nameof(json));
            if (Volatile.Read(ref _closed) != 0 || Error != null)
            { Interlocked.Increment(ref _dropped); return false; }
            try
            {
                if (!_queue.TryAdd(json)) { Interlocked.Increment(ref _dropped); return false; }
                HighWater = Math.Max(HighWater, _queue.Count); return true;
            }
            catch (InvalidOperationException) { Interlocked.Increment(ref _dropped); return false; }
        }
        void WriteLoop()
        {
            try
            {
                _beforeDrain?.Invoke();
                using (var file = new FileStream(_partial, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                using (var writer = new StreamWriter(file, new UTF8Encoding(false)))
                {
                    while (!_queue.IsCompleted)
                    {
                        if (_queue.TryTake(out var json, 250))
                        { writer.WriteLine(json); Interlocked.Increment(ref _written); }
                        if (_queue.Count == 0) writer.Flush();
                    }
                    writer.Flush(); file.Flush(true);
                }
            }
            catch (Exception error) { Volatile.Write(ref _error, error.GetType().Name + ": " + error.Message); }
        }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _closed, 1) != 0) return;
            _queue.CompleteAdding();
            if (!_writer.Join(5000)) { Volatile.Write(ref _error, "Writer flush timeout"); return; }
            if (Error == null && Dropped == 0)
            {
                try { File.Move(_partial, _complete); Complete = true; }
                catch (Exception error) { Volatile.Write(ref _error, error.GetType().Name + ": " + error.Message); }
            }
            _queue.Dispose();
        }
    }
}
#endif
