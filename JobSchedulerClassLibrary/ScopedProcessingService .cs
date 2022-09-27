using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JobSchedulerClassLibrary
{
    public sealed class ScopedProcessingService : BackgroundService
    {
        private int executionCount = 0;
        private readonly ILogger _logger;
        private bool _action { get; set; } = false;

        public ScopedProcessingService(ILogger<ScopedProcessingService> logger, bool action)
        {
            _logger = logger;
            _action = action;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_action)
            {
                CancellationToken st = new CancellationToken(true);
                await DoWork(st);
            }
            else
                await DoWork(stoppingToken);
        }

        public async Task DoWork(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                executionCount++;

                _logger.LogInformation(
                    "Scoped Processing Service is working. Count: {Count}", executionCount);

                await Task.Delay(10000, stoppingToken);
            }
        }
    }
}
