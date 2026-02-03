using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LPTUnoApp
{
    public class MoveManager : IDisposable
    {
        private readonly string _downloadsFolder;
        private readonly string _dataFolder;
        private FileSystemWatcher? _watcher;
        private bool _running;

        public event Action<string>? FileMoved;
        public event Action<string>? Error;

        public MoveManager(string downloadsFolder, string dataFolder)
        {
            _downloadsFolder = downloadsFolder;
            _dataFolder = dataFolder;
            Directory.CreateDirectory(_dataFolder);
        }

        public void Start()
        {
            if (_running) return;
            _running = true;
            EnqueueExistingFiles();

            _watcher = new FileSystemWatcher(_downloadsFolder)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
                Filter = "*_????-??-??_??-??-??.*",
                EnableRaisingEvents = true,
                IncludeSubdirectories = false
            };
            _watcher.Created += async (s, e) => await HandleFile(e.FullPath);
            _watcher.Changed += async (s, e) => await HandleFile(e.FullPath);
        }

        public void Stop()
        {
            _watcher?.Dispose();
            _watcher = null;
            _running = false;
        }

        private void EnqueueExistingFiles()
        {
            try
            {
                var files = Directory.GetFiles(_downloadsFolder, "*_????-??-??_??-??-??.*").OrderBy(f => File.GetLastWriteTimeUtc(f)).ToList();
                foreach (var f in files) _ = HandleFile(f);
            }
            catch { }
        }

        private async Task HandleFile(string path)
        {
            try
            {
                // Wait briefly to allow browser to finish writing
                await Task.Delay(5000);
                if (!File.Exists(path)) return;

                var name = Path.GetFileName(path);
                var dest = Path.Combine(_dataFolder, name);
                try
                {
                    File.Move(path, dest);
                    FileMoved?.Invoke(dest);
                }
                catch
                {
                    try
                    {
                        File.Copy(path, dest, true);
                        File.Delete(path);
                        FileMoved?.Invoke(dest);
                    }
                    catch (Exception ex)
                    {
                        Error?.Invoke(ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Error?.Invoke(ex.Message);
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}