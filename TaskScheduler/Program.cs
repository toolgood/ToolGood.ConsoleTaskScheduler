using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Quartz;

namespace TaskScheduler
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            var hostBuilder = Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) => {
                    services.AddQuartz(config => {
                        config.UseMicrosoftDependencyInjectionJobFactory();
                        config.AddJobTrigger<Jobs.ServiceJobA>(hostContext.Configuration);
                        config.AddJobTrigger<Jobs.ServiceJobB>(hostContext.Configuration);
                    });
                    services.AddQuartzHostedService(options => {
                        options.AwaitApplicationStarted = true;
                        options.WaitForJobsToComplete = true;
                    });
                });
            hostBuilder.Build().Run();
        }
    }
    public static class QuartzConfigureExtenstion
    {
        public static void AddJobTrigger<T>(this IServiceCollectionQuartzConfigurator quartz, IConfiguration config) where T : IJob
        {
            var cronExpression = config[$"Quartz:{typeof(T).Name}"];
            if (string.IsNullOrEmpty(cronExpression))
                throw new ArgumentNullException("The service job schedule is null or empty.");
            var jobKey = new JobKey(typeof(T).Name);
            quartz.AddJob<T>(c => c.WithIdentity(jobKey));
            quartz.AddTrigger(c => c.ForJob(jobKey).WithIdentity($"{typeof(T).Name}_trigger").WithCronSchedule(cronExpression));
        }
        public static void AddJobTrigger<T>(this IServiceCollectionQuartzConfigurator quartz, IConfiguration config, string defCronExpression) where T : IJob
        {
            var cronExpression = config[$"Quartz:{typeof(T).Name}"];
            if (string.IsNullOrEmpty(cronExpression))
                cronExpression = defCronExpression;
            var jobKey = new JobKey(typeof(T).Name);
            quartz.AddJob<T>(c => c.WithIdentity(jobKey));
            quartz.AddTrigger(c => c.ForJob(jobKey).WithIdentity($"{typeof(T).Name}_trigger").WithCronSchedule(cronExpression));
        }
        public static void AddJobTrigger<T>(this IServiceCollectionQuartzConfigurator quartz, string cronExpression) where T : IJob
        {
            var jobKey = new JobKey(typeof(T).Name);
            quartz.AddJob<T>(c => c.WithIdentity(jobKey));
            quartz.AddTrigger(c => c.ForJob(jobKey).WithIdentity($"{typeof(T).Name}_trigger").WithCronSchedule(cronExpression));
        }
    }
}
