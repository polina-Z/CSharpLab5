using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Globalization;
using System.Threading.Tasks;
using System.IO.Compression;

namespace FileWatcher
{
    class Logger
    {
        private FileSystemWatcher watcher;
        private readonly StringBuilder messages = new StringBuilder();
        private readonly Options options;
        bool enabled = true;
        private EncryptManager encryptManager = new EncryptManager();
        private ArchiveManager archiveManager = new ArchiveManager();
        
        public Logger(Options options)
        {
            this.options = options;

            if (!Directory.Exists(this.options.SourcePath))
            {
                Directory.CreateDirectory(this.options.SourcePath);
            }

            if (!Directory.Exists(this.options.TargetPath))
            {
                Directory.CreateDirectory(this.options.TargetPath);
            }

            watcher = new FileSystemWatcher(this.options.SourcePath);
            watcher.Deleted += OnDeleted;
            watcher.Created += OnCreated;
            watcher.Changed += OnChanged;
            watcher.Renamed += OnRenamed;
            watcher.Created += FileTransfer;
        }

        public void Start()
        {
            WriteToFileAsync($"Service was started at {DateTime.Now:dd/MM/yyyy HH:mm:ss}\n");
            watcher.EnableRaisingEvents = true;
            while (enabled)
            {
                Thread.Sleep(1000);
            }
        }

        public void Stop()
        {
            watcher.EnableRaisingEvents = false;
            enabled = false;
            messages.Clear();
            WriteToFileAsync($"Service was stopped at {DateTime.Now:dd/MM/yyyy HH:mm:ss}\n");
        }

        private async void FileTransfer(object sender, FileSystemEventArgs e)
        {
            if (!Directory.Exists(this.options.SourcePath))
            {
                await Task.Run(() => Directory.CreateDirectory(options.SourcePath));

                watcher = new FileSystemWatcher(this.options.SourcePath);
                watcher.Deleted += OnDeleted;
                watcher.Created += OnCreated;
                watcher.Changed += OnChanged;
                watcher.Renamed += OnRenamed;
                watcher.Created += FileTransfer;
            }

            if (!Directory.Exists(this.options.TargetPath))
            {
                await Task.Run(() => Directory.CreateDirectory(options.TargetPath));
            }

            if (messages.Length > 0)
            {
                await WriteToFileAsync(messages.ToString());
                messages.Clear();
            }

            try
            {
                var dirInfo = new DirectoryInfo(this.options.SourcePath);
                var filePath = Path.Combine(this.options.SourcePath, e.Name);
                var fileName = e.Name;
                var dt = DateTime.Now;
                var subPath = Path.Combine(dt.ToString("yyyy", DateTimeFormatInfo.InvariantInfo), dt.ToString("MM", DateTimeFormatInfo.InvariantInfo),
                              dt.ToString("dd", DateTimeFormatInfo.InvariantInfo));
                var newPathOne = Path.Combine(this.options.SourcePath, subPath, Path.GetFileNameWithoutExtension(fileName) + "_" +
                    dt.ToString(@"yyyy_MM_dd_HH_mm_ss", DateTimeFormatInfo.InvariantInfo) + Path.GetExtension(fileName));
                var newPath = Path.Combine(this.options.SourcePath, subPath, Path.GetFileNameWithoutExtension(fileName) + "_" +
                    dt.ToString(@"yyyy_MM_dd_HH_mm_ss", DateTimeFormatInfo.InvariantInfo) + "_1" + Path.GetExtension(fileName));
                var newPathTarget = Path.Combine(this.options.TargetPath, Path.GetFileNameWithoutExtension(fileName) + "_" +
                   dt.ToString(@"yyyy_MM_dd_HH_mm_ss", DateTimeFormatInfo.InvariantInfo) + Path.GetExtension(fileName));
                if (!dirInfo.Exists)
                {
                    await Task.Run(() => dirInfo.Create());
                }
                await Task.Run(() => dirInfo.CreateSubdirectory(subPath));

                if (options.NeedToEncrypt)
                {
                    await Task.Run(() => File.Move(filePath, newPathOne));
                    encryptManager.EncryptFile(newPathOne, newPath);
                }
                else
                {
                    await Task.Run(() => File.Move(filePath, newPath));
                }

                if (options.ArchiveOptions.NeedToCompress)
                {
                    var compressedPath = Path.ChangeExtension(newPath, "gz");
                    var newCompressedPath = Path.Combine(options.TargetPath, Path.GetFileName(compressedPath));
                    var decompressedPath = Path.ChangeExtension(newCompressedPath, "txt");
                    await CompressAsync(newPath, compressedPath);
                }
                else
                {
                    await Task.Run(() => File.Copy(newPath, Path.Combine(options.TargetPath, Path.GetFileName(newPath))));
                }

                var decryptPath = Path.Combine(options.TargetPath, Path.GetFileName(newPathOne));

                if (options.NeedToEncrypt)
                {
                    encryptManager.DecryptFile(Path.Combine(options.TargetPath, Path.GetFileName(newPath)), decryptPath);
                    await Task.Run(() => File.Delete(Path.Combine(options.TargetPath, Path.GetFileName(newPath))));
                }
                else
                {
                    await Task.Run(() => File.Move(Path.Combine(options.TargetPath, Path.GetFileName(newPathOne)), decryptPath));
                }

                if (options.ArchiveOptions.NeedToArchive)
                {
                    archiveManager.AddToArchive(decryptPath, options.TargetPath);
                    await Task.Run(() => File.Delete(decryptPath));
                }
            }
            catch (Exception ex)
            {
                using (StreamWriter sw = new StreamWriter(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exceptions.txt"), true))
                {
                    await sw.WriteLineAsync($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} Exception: {ex.Message}");
                }
            }
            
        }       

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            string filePath = e.FullPath;
            string fileEvent = "created";
            AddToMessages(filePath, fileEvent);
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            string filePath = e.OldFullPath;
            string fileEvent = "renamed to " + e.FullPath;
            AddToMessages(filePath, fileEvent);
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            string filePath = e.FullPath;
            string fileEvent = "changed";
            AddToMessages(filePath, fileEvent);
        }

        private void OnDeleted(object sender, FileSystemEventArgs e)
        {
            string filePath = e.FullPath;
            string fileEvent = "deleted";
            AddToMessages(filePath, fileEvent);
        }

        void AddToMessages(string filePath, string fileEvent)
        {
            messages.Append($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} file {filePath} was {fileEvent}\n");
        }

        public Task WriteToFileAsync(string message)
        {
            if (!Directory.Exists(options.SourcePath))
            {
                Directory.CreateDirectory(options.SourcePath);

                watcher = new FileSystemWatcher(options.SourcePath);
                watcher.Deleted += OnDeleted;
                watcher.Created += OnCreated;
                watcher.Changed += OnChanged;
                watcher.Renamed += OnRenamed;

                watcher.EnableRaisingEvents = true;
            }

            if (!Directory.Exists(options.TargetPath))
            {
                Directory.CreateDirectory(options.TargetPath);
            }

            using (StreamWriter sw = new StreamWriter(options.LogFilePath, true))
            {
                return sw.WriteAsync(message);
            }
        }

        async Task CompressAsync(string sourceFile, string compressedFile)
        {
            await Task.Run(() =>
            {
                using (FileStream sourceStream = new FileStream(sourceFile, FileMode.Open))
                {
                    using (FileStream targetStream = new FileStream(compressedFile, FileMode.OpenOrCreate))
                    {
                        using (GZipStream compressionStream = new GZipStream(targetStream, options.ArchiveOptions.Level))
                        {
                            sourceStream.CopyTo(compressionStream);
                        }
                    }
                }
            });
        }

        async Task DecompressAsync(string compressedFile, string targetFile)
        {
            await Task.Run(() =>
            {
                using (FileStream sourceStream = new FileStream(compressedFile, FileMode.Open))
                {
                    using (FileStream targetStream = new FileStream(targetFile, FileMode.OpenOrCreate))
                    {
                        using (GZipStream decompressionStream = new GZipStream(sourceStream, CompressionMode.Decompress))
                        {
                            decompressionStream.CopyTo(targetStream);
                        }
                    }
                }
            });
        }

    }
} 

