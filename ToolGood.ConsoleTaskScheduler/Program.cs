using Quartz;
using Quartz.Impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ToolGood.ConsoleTaskScheduler
{
    class Program
    {
        private static IScheduler scheduler = null;

        static void Main(string[] args)
        {
            var name = AppDomain.CurrentDomain.BaseDirectory.Replace("\\", "_").Replace(":", "_");
            CommandHelper command = new CommandHelper(name);
            command.OnHelp += (sender, e) => {
                Console.WriteLine("-----------------------------------------------------------");
                Console.WriteLine("-help            显示帮助（默认）");
                Console.WriteLine("-start,-run      运行程序");
                Console.WriteLine("-stop,-exit      关闭程序");
                Console.WriteLine("-pause           暂停计划任务");
                Console.WriteLine("-continue        继续计划任务");
                Console.WriteLine("-command 'XXXX'  执行命令");
                Console.WriteLine("-show            显示窗口");
                Console.WriteLine("-hidden,-hide    隐藏窗口");
                Console.WriteLine("-----------------------------------------------------------");
                Console.Write("请按下任意按键");

                Console.ReadKey();
            };
            command.OnStart += async (sender, e) => {
                ISchedulerFactory factory = new StdSchedulerFactory();
                scheduler = await factory.GetScheduler();
                await scheduler.Start();
            };
            command.OnServerPause += async (ss, ee) => {
                await scheduler.PauseAll();
            };
            command.OnServerContinue += async (ss, ee) => {
                await scheduler.ResumeAll();
            };
            command.OnServerStop += async (ss, ee) => {
                await scheduler.Shutdown(false);
            };
            command.OnServerCommand += (ss, ee) => {

            };
            command.DoWork(args);
        }
    }
}
