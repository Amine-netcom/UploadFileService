using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UploadFileService.Model;

namespace UploadFileService.Services
{
    public class FileCleanupService : IHostedService, IDisposable
    {
        private readonly ILogger<FileCleanupService> _logger;
        private Timer _timer;

        private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(24);
        private readonly TimeSpan _maxFileAge = TimeSpan.FromDays(7);
        
        private readonly Config _config;

        public FileCleanupService(ILogger<FileCleanupService> logger, IOptions<Config> config)
        {
            _logger = logger;
            _config = config.Value;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("File cleanup service is starting.");

            _timer = new Timer(DoCleanup, null, TimeSpan.Zero, _cleanupInterval);

            return Task.CompletedTask;
        }

        private void DoCleanup(object state)
        {
            try
            {
                _logger.LogInformation("File cleanup task started.");

                var tmpDirectory = _config.tmp_path;
                var cutoff = DateTime.UtcNow - _maxFileAge;
                
                var oldFiles = Directory.GetFiles(tmpDirectory)
                                         .Select(f => new FileInfo(f))
                                         .Where(fi => fi.LastWriteTimeUtc < cutoff);
                
                foreach (var file in oldFiles)
                {
                    file.Delete();
                    _logger.LogInformation($"Deleted old file: {file.FullName}");
                }

                _logger.LogInformation("File cleanup task completed.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error cleaning up files: {ex.Message}");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("File cleanup service is stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
