using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DocumentProcessor.Web
{
    public class TestBackgroundService : BackgroundService
    {
        private readonly ILogger<TestBackgroundService> _logger;
        private Timer? _timer;

        public TestBackgroundService(ILogger<TestBackgroundService> logger)
        {
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("TestBackgroundService is starting.");

            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));

            return Task.CompletedTask;
        }

        private void DoWork(object? state)
        {
            _logger.LogInformation($"TestBackgroundService is working at: {DateTime.UtcNow}");
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("TestBackgroundService is stopping.");
            _timer?.Change(Timeout.Infinite, 0);
            return base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            _timer?.Dispose();
            base.Dispose();
        }
    }
}