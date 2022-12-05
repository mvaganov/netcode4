using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace networking {
	public class CommandLineServer : ICommandLineContext {
		private networking.Server server;
		private IPEndPoint ipEndPoint;
		private CommandLineInput cmdInput;
		private bool running;
		private List<CommandLineClient> localClients;
		private List<ICommandLineContext> commandLineContexts;
		private int currentCommandLineContext;
		private int port;
		public CommandLineInput Input => cmdInput;

		public async Task Start(int port) {
			this.port = port;
			server = new Server();
			ipEndPoint = await Networking.GetEndPoint(Networking.localhost, port);
			string promptString = ipEndPoint.ToString();
			cmdInput = new CommandLineInput(()=> $"server {promptString}({server.clients.Count})", ">}|{<{|}", 100);
			running = true;
			cmdInput.BindKey(ConsoleKey.Escape, EndServer);
			cmdInput.BindKey(ConsoleKey.Tab, NextCommandLineContext);
			cmdInput.BindKey(ConsoleKey.Enter, RunCommandLineInputAsServerCommand);
			localClients = new List<CommandLineClient>();
			commandLineContexts = new List<ICommandLineContext>();
			commandLineContexts.Add(this);
			currentCommandLineContext = 0;
		}

		private void EndServer() {
			Console.WriteLine("~\n"); // needed for some reason, because the console is eating characters
			running = false;
		}

		private void NextCommandLineContext() {
			ICommandLineContext clc = currentCommandLineContext < commandLineContexts.Count ? commandLineContexts[currentCommandLineContext] : null;
			if (clc != null) {
				clc.Input.ConsoleClearPrompt();
			}
			++currentCommandLineContext;
			if (currentCommandLineContext >= commandLineContexts.Count) {
				currentCommandLineContext = 0;
			}
			for (int i = 0; i < commandLineContexts.Count; ++i) {
				bool enableThisOne = i == currentCommandLineContext;
				commandLineContexts[i].Input.Enabled = enableThisOne;
			}
		}

		public void RunCommandLineInputAsServerCommand() {
			string input = cmdInput.PopInputAsString();
			switch (input) {
				case "client":
					Console.WriteLine("starting client");
					// TODO
					//	disable the client's animation AND key sensitivity
					//	get the CommandInput for the client
					//	allow the server to turn off it's CommandInput in favor of the client's
					//	tab will swap between server and client CommandInput
					//	show wich one has command input at the animating prompt
					CommandLineClient clc = CommandLineClient.Create(Networking.localhost, port, false);
					clc.Input.BindKey(ConsoleKey.Tab, NextCommandLineContext);
					localClients.Add(clc);
					commandLineContexts.Add(clc);
					break;
			}
		}

		public async Task<bool> Work() {
			bool result = await server.Work(ipEndPoint, HandleNewClient, HandleReceived, HandleServerKeyPresses);
			return result;
		}

		async Task<bool> HandleNewClient(TcpClient client) {
			var message = $"DateTime: {DateTime.Now}";
			var dateTimeBytes = Encoding.UTF8.GetBytes(message);
			NetworkStream stream = client.GetStream();
			await stream.WriteAsync(dateTimeBytes);
			Console.WriteLine($"Sent message: \"{message}\"");
			return true;
		}

		void HandleReceived(ClientHandler client, NetBuffer buffer) {
			// TODO create protocol for enqueuing different kinds of message (with different Message.Purpose)
		}

		bool HandleServerKeyPresses() {
			for(int i = localClients.Count-1; i >= 0; --i) {
				Task t = localClients[i].Execution;
				if (t != null && !t.IsCompleted) {
					//t.RunSynchronously();
					// allow the task some time to execute
				} else {
					localClients.RemoveAt(i);
				}
			}
			//cmdInput.UpdateAsciiAnimation();
			//cmdInput.UpdateKeyInput();
			for(int i = 0; i < commandLineContexts.Count; ++i) {
				ICommandLineContext clc = commandLineContexts[i];
				if (clc != null && clc.Input.Enabled) {
					Console.Write(clc.Input.Prompt()+"\r");
					clc.Input.UpdatePrompt();
					clc.Input.UpdateKeyInput();
				}
			}
			return running;
		}

		/// <param name="port"></param>
		/// <returns>false if this port is already bound, or if there is some other server error</returns>
		public static async Task<bool> Create(int port) {
			CommandLineServer cls = new CommandLineServer();
			await cls.Start(port);
			return await cls.Work();
		}
	}
}
