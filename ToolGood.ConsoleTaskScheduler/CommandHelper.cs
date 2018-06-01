using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ToolGood.ConsoleTaskScheduler
{
    public class ServerNameEventArgs : EventArgs
    {
        public ServerNameEventArgs(string name, string[] args, Dictionary<string, List<string>> dict, CommandArguments argument)
        {
            Name = name;
            Args = args;
            ArgsDictionary = dict;
            CommandArguments = argument;
        }
        public string Name { get; private set; }
        public string[] Args { get; private set; }
        public Dictionary<string, List<string>> ArgsDictionary { get; private set; }
        public CommandArguments CommandArguments { get; private set; }
    }

    public class ServerCommandEventArgs : EventArgs
    {
        public ServerCommandEventArgs(string name, string args)
        {
            Name = name;
            Content = args;
        }
        public string Name { get; private set; }
        public string Content { get; private set; }
    }

    public class ServerCommandEventArgs2 : EventArgs
    {
        public ServerCommandEventArgs2(string name, string args, Dictionary<string, List<string>> dict)
        {
            Name = name;
            Args = args.Split(' ');
            ArgsDictionary = dict;
        }
        public string Name { get; private set; }
        public string[] Args { get; private set; }
        public Dictionary<string, List<string>> ArgsDictionary { get; private set; }
    }

    public class CommandHelper
    {
        public event EventHandler<ServerNameEventArgs> OnStart;
        public event EventHandler<ServerNameEventArgs> OnHelp;
        public event EventHandler<ServerCommandEventArgs> OnServerStop;
        public event EventHandler<ServerCommandEventArgs> OnServerContinue;
        public event EventHandler<ServerCommandEventArgs> OnServerPause;
        public event EventHandler<ServerCommandEventArgs> OnServerShow;
        public event EventHandler<ServerCommandEventArgs> OnServerHidden;
        public event EventHandler<ServerCommandEventArgs2> OnServerCommand;

        private readonly string _name;
        private PipeServer server;

        #region 显示 隐藏 
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();// 获取控制台句柄

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        const int SW_HIDE = 0;// 隐藏

        const int SW_SHOW = 5;// 显示 
        #endregion


        public CommandHelper(string name)
        {
            _name = name;
        }

        public void DoWork(string[] args)
        {
            var dict = GetDictionary(args);
            var commandArgument = Parse(dict);

            #region 判断是否cmd打开
            var hWnd = GetConsoleWindow();
            GetWindowThreadProcessId(hWnd, out int processId);
            Process process = Process.GetProcessById(processId);
            commandArgument.IsCmdOpen = process.ProcessName.ToLower() == "cmd";
            if (args.Length == 0 && commandArgument.IsCmdOpen == false)
            {
                commandArgument.Help = true;
            }
            #endregion


            var name = _name;
            if (string.IsNullOrEmpty(commandArgument.Name) == false)
            {
                name = name + "-" + commandArgument.Name;
            }


            if (commandArgument.Help)
            {
                OnHelp?.Invoke(this, new ServerNameEventArgs(name, args, dict, commandArgument));
            }
            else if (commandArgument.Start)
            {
                if (commandArgument.Hidden)
                {
                    ShowWindow(GetConsoleWindow(), SW_HIDE);
                }
                OnStart?.Invoke(this, new ServerNameEventArgs(name, args, dict, commandArgument));
                server = new PipeServer(name);
                server.OnReceiveContent += Server_OnReceiveContent;
                server.Start().Wait();
            }
            else
            {
                if (commandArgument.Show)
                {
                    Send(name, "-show").Wait();
                }
                if (commandArgument.Hidden)
                {
                    Send(name, "-hide").Wait();
                }
                if (commandArgument.Stop)
                {
                    Send(name, "-stop").Wait();
                }
                else if (commandArgument.Pause)
                {
                    Send(name, "-pause").Wait();
                }
                else if (commandArgument.Continue)
                {
                    Send(name, "-continue").Wait();
                }
                else if (string.IsNullOrEmpty(commandArgument.Command) == false)
                {
                    Send(name, "-command " + commandArgument.Command).Wait();
                }
            }
        }

        private void Server_OnReceiveContent(string content)
        {
            if (content == "-show")
            {
                var hWnd = GetConsoleWindow();
                ShowWindow(hWnd, SW_SHOW);
                SetForegroundWindow(hWnd);
                OnServerShow?.Invoke(this, new ServerCommandEventArgs(server.PipeName, content));
            }
            else if (content == "-hide")
            {
                ShowWindow(GetConsoleWindow(), SW_HIDE);
                OnServerHidden?.Invoke(this, new ServerCommandEventArgs(server.PipeName, content));
            }
            else if (content == "-stop")
            {
                server.Stop();
                OnServerStop?.Invoke(this, new ServerCommandEventArgs(server.PipeName, content));
            }
            else if (content == "-continue")
            {
                OnServerContinue?.Invoke(this, new ServerCommandEventArgs(server.PipeName, content));
            }
            else if (content == "-pause")
            {
                OnServerPause?.Invoke(this, new ServerCommandEventArgs(server.PipeName, content));
            }
            else if (content.StartsWith("-command"))
            {
                var text = content.Substring("-command".Length);
                if (text[0] == '"' || text[0] == '\'')
                {
                    text = text.Substring(1, text.Length - 2);
                }
                OnServerCommand?.Invoke(this, new ServerCommandEventArgs2(server.PipeName, content, GetDictionary(text)));
            }
        }

        private async Task Send(string pipeName, string content, int timeOut = 5000)
        {
            try
            {
                using (NamedPipeClientStream client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous))
                {
                    client.Connect(timeOut);
                    if (client.IsConnected)
                    {
                        byte[] output = Encoding.UTF8.GetBytes(content);
                        await client.WriteAsync(output, 0, Math.Min(output.Length, 256)).ConfigureAwait(false);
                        await client.FlushAsync();
                    }
                }
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine(ex);
            }
        }

        #region 解析 命名行
        private const string Command_REGEX = @"((?:/|--?)([^ -/]+)((?: (?:""[^""]*""|'[^']*'|[^/-][^ -]*))*)|[^ -/]+)";
        private const string Command_REGEX2 = @"(""[^""]*""|'[^']*'|[^/-][^ ]*)";

        private Dictionary<string, List<string>> GetDictionary(string[] args = null)
        {
            if (args == null || args.Length == 0) return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var line = string.Join(" ", args);
            return GetDictionary(line);
        }
        private Dictionary<string, List<string>> GetDictionary(string line)
        {
            var ret = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            var ms = Regex.Matches(line, Command_REGEX);
            foreach (Match match in ms)
            {
                if (match.Groups[2].Success)
                {
                    var list = new List<string>();
                    if (match.Groups[3].Success)
                    {
                        var ms2 = Regex.Matches(match.Groups[3].Value, Command_REGEX2);
                        foreach (Match item in ms2)
                        {
                            list.Add(item.Value);
                        }
                    }
                    ret[match.Groups[2].Value] = list;
                }
                else
                {
                    ret[match.Groups[1].Value] = new List<string>();
                }
            }
            return ret;
        }
        private CommandArguments Parse(Dictionary<string, List<string>> dict)
        {
            CommandArguments commandArgument = new CommandArguments();
            commandArgument.Help = dict.ContainsKey("Help") || dict.ContainsKey("?");
            commandArgument.Start = dict.ContainsKey("Start") || dict.ContainsKey("Run");
            commandArgument.Stop = dict.ContainsKey("Stop") || dict.ContainsKey("Exit");
            commandArgument.Continue = dict.ContainsKey("Continue");
            commandArgument.Pause = dict.ContainsKey("Pause");
            commandArgument.Show = dict.ContainsKey("Show");
            commandArgument.Hidden = dict.ContainsKey("Hidden") || dict.ContainsKey("Hide");
            if (dict.ContainsKey("Name") && dict["Name"].Count > 0)
            {
                var name = dict["Name"][0];
                if (name[0] == '\'' || name[0] == '"')
                {
                    name = name.Substring(1, name.Length - 2);
                }
                commandArgument.Name = name;
            }
            if (dict.ContainsKey("Command") && dict["Command"].Count > 0)
            {
                var name = dict["Command"][0];
                if (name[0] == '\'' || name[0] == '"')
                {
                    name = name.Substring(1, name.Length - 2);
                }
                commandArgument.Command = name;
            }
            return commandArgument;
        }
        #endregion

        #region PipeServer
        private class PipeServer
        {
            private readonly CancellationToken _cancel;
            private readonly CancellationTokenSource _cancelSource;
            public readonly string PipeName;

            public event Action<string> OnReceiveContent;

            public PipeServer(string pipeName)
            {
                PipeName = pipeName;
                _cancelSource = new CancellationTokenSource();
                _cancel = _cancelSource.Token;
            }

            public async Task Start()
            {
                try
                {
                    using (NamedPipeServerStream server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous)) { }
                }
                catch (Exception)
                {
                    Console.WriteLine("[thread: {0}] -> Services are not available.", Thread.CurrentThread.ManagedThreadId);
                    return;
                }
                Console.WriteLine("[thread: {0}] -> Starting server listener.", Thread.CurrentThread.ManagedThreadId);
                while (!_cancel.IsCancellationRequested)
                {
                    await Listener();
                }
            }

            public void Stop()
            {
                _cancelSource.Cancel();
            }

            private async Task Listener()
            {
                using (NamedPipeServerStream server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
                {
                    await Task.Factory.FromAsync(server.BeginWaitForConnection, server.EndWaitForConnection, null);
                    await ReadData(server);
                    if (server.IsConnected)
                    {
                        server.Disconnect();
                    }
                }
            }

            private async Task ReadData(NamedPipeServerStream server)
            {
                byte[] buffer = new byte[256];
                int length = await server.ReadAsync(buffer, 0, buffer.Length, _cancel);
                byte[] chunk = new byte[length];
                Array.Copy(buffer, chunk, length);
                string content = Encoding.UTF8.GetString(chunk);
                Console.WriteLine("[thread: {0}] -> {1}: {2}", Thread.CurrentThread.ManagedThreadId, DateTime.Now, content);
                OnReceiveContent?.Invoke(content);
            }
        }
        #endregion
    }

    public class CommandArguments
    {
        public bool Help { get; set; }

        public bool Start { get; set; } // Run

        public bool Stop { get; set; } //Exit

        public bool Continue { get; set; }

        public bool Pause { get; set; }

        public bool Show { get; set; }

        public bool Hidden { get; set; }

        public string Name { get; set; }

        public string Command { get; set; }

        public bool IsCmdOpen { get; set; }

    }
}
