using System;
using System.Threading;
using System.Threading.Tasks;

namespace networking {
	public class Program {
		public const int defaultPort = 8765;
		public Task task;
		public int port;
		public string host;
		public string password;
		public static void Main(string[] args) {
			Program p = RunNewProgram(Networking.localhost, defaultPort);
			// not allowed to await in Main, so we have to add a manual blocking sleep.
			while (!p.task.IsCompleted) { Thread.Sleep(1); }
			Console.WriteLine($"finished {p.host}:{p.port}");
		}

		public static Program RunNewProgram(string host, int port) {
			Program p = new Program { port = port, host = host };
			p.task = p.Run();
			return p;
		}

		/// <summary>
		/// try to start a server. if the server is already started, start a client instead.
		/// </summary>
		/// <returns></returns>
		public async Task Run() {
			bool isServer = false;
			if (Server.IsPortAvailable(port)) {
				isServer = await CommandLineServer.Create(port);
			}
			if (!isServer) {
				Console.WriteLine($"connecting client to {host}:{port}");
				await CommandLineClient.CreateAsync(host, port);
			}
		}
	}
}
