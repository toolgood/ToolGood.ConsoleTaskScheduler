using Microsoft.Extensions.Logging;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaskScheduler.Jobs
{

    [DisallowConcurrentExecution]
    public class ServiceJobA : IJob
    {
        private readonly ILogger<ServiceJobA> _logger;

        public ServiceJobA(ILogger<ServiceJobA> logger)
        {
            _logger = logger;
        }

        public Task Execute(IJobExecutionContext context)
        {
            Task.Run(() =>
            {
                _logger.LogInformation($"DateTime.Now: {DateTime.Now.ToLongTimeString()}, The service job A has been excuted.");
            });
            return Task.CompletedTask;
        }
    }
}
