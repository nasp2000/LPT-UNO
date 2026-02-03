using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LPTUnoApp
{
    public class PrintManager : IDisposable
    {
        private readonly string _dataFolder;
        private readonly string _printedFolder;
        private readonly string _erroredFolder;
        private FileSystemWatcher? _watcher;
        private readonly BlockingCollection<string> _queue = new BlockingCollection<string>(new ConcurrentQueue<string>());
        private CancellationTokenSource? _cts;
        private Task? _workerTask;
        public string? PrinterName { get; set; }

        public PrintManager(string dataFolder)
        {
            _dataFolder = dataFolder;
            _printedFolder = Path.Combine(_dataFolder, "printed");
            _erroredFolder = Path.Combine(_dataFolder, "error");
            Directory.CreateDirectory(_dataFolder);
            Directory.CreateDirectory(_printedFolder);
            Directory.CreateDirectory(_erroredFolder);
        }

        public void Start()
        {
            if (_cts != null) return;
            _cts = new CancellationTokenSource();
            EnqueueExistingFiles();

            _watcher = new FileSystemWatcher(_dataFolder)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
                Filter = "*.txt",
                EnableRaisingEvents = true,
                IncludeSubdirectories = false
            };
            _watcher.Created += (s, e) => EnqueueFileWithDelay(e.FullPath);
            _watcher.Changed += (s, e) => EnqueueFileWithDelay(e.FullPath);

            _workerTask = Task.Run(() => ProcessQueue(_cts.Token));
        }

        public void Stop()
        {
            _watcher?.Dispose();
            _watcher = null;
            if (_cts == null) return;
            _cts.Cancel();
            _workerTask?.Wait(2000);
            _workerTask = null;
            _cts.Dispose();
            _cts = null;
            while (_queue.TryTake(out _)) ;
        }

        private void EnqueueExistingFiles()
        {
            var files = Directory.GetFiles(_dataFolder, "*.txt").OrderBy(f => File.GetCreationTimeUtc(f));
            foreach (var f in files) _queue.Add(f);
        }

        private void EnqueueFileWithDelay(string path)
        {
            // Wait briefly to allow file to finish writing
            Task.Delay(300).ContinueWith(_ =>
            {
                if (File.Exists(path)) _queue.Add(path);
            });
        }

        private void ProcessQueue(CancellationToken ct)
        {
            try
            {
                foreach (var file in _queue.GetConsumingEnumerable(ct))
                {
                    if (ct.IsCancellationRequested) break;
                    try
                    {
                        PrintFile(file);
                        var dest = Path.Combine(_printedFolder, Path.GetFileName(file));
                        File.Move(file, dest, true);
                    }
                    catch (Exception ex)
                    {
                        var dest = Path.Combine(_erroredFolder, Path.GetFileName(file));
                        try
                        {
                            File.Move(file, dest, true);
                        }
                        catch { }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch { }
        }

        private void PrintFile(string path)
        {
            var text = File.ReadAllText(path);
            var pd = new PrintDocument();
            if (!string.IsNullOrEmpty(PrinterName)) pd.PrinterSettings.PrinterName = PrinterName;
            pd.DocumentName = Path.GetFileName(path);

            // Store printing state
            using var sr = new StringReader(text);
            pd.PrintPage += (s, e) =>
            {
                float y = e.MarginBounds.Top;
                var font = new Font("Consolas", 9);
                float lineHeight = font.GetHeight(e.Graphics);
                string? line;
                int linesPrinted = 0;
                while ((line = sr.ReadLine()) != null)
                {
                    e.Graphics.DrawString(line, font, Brushes.Black, e.MarginBounds.Left, y);
                    y += lineHeight;
                    linesPrinted++;
                    if (y + lineHeight > e.MarginBounds.Bottom)
                    {
                        e.HasMorePages = true;
                        return;
                    }
                }
                e.HasMorePages = false;
            };

            pd.Print();
        }

        public void Dispose()
        {
            Stop();
            _queue.Dispose();
        }
    }
}