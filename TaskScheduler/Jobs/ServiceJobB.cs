using Microsoft.Extensions.Logging;
using Quartz;

namespace TaskScheduler.Jobs
{
    [DisallowConcurrentExecution]
    public class ServiceJobB : IJob
    {
        private readonly ILogger<ServiceJobB> _logger;

        public ServiceJobB(ILogger<ServiceJobB> logger)
        {
            _logger = logger;
        }

        public Task Execute(IJobExecutionContext context)
        {
            Task.Run(() =>
            {
                _logger.LogInformation($"DateTime.Now: {DateTime.Now.ToLongTimeString()}, The service job B has been excuted.");
            });
            return Task.CompletedTask;
        }
    }
}
