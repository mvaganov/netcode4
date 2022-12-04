using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace networking {
	public class CommandLineServer {
		private networking.Server server;
		private IPEndPoint ipEndPoint;
		private CommandLineInput cmdInput;
		private bool running;

		public async Task Start(int port) {
			server = new Server();
			ipEndPoint = await Networking.GetEndPoint(Networking.localhost, port);
			cmdInput = new CommandLineInput(">}|{<{|}", 100);
			cmdInput.BindKey(ConsoleKey.Escape, () => {
				Console.WriteLine("~\n"); // needed for some reason, because the console is eating characters
				running = false;
			});
			running = true;
			cmdInput.BindKey(ConsoleKey.Enter, () => {
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
						Task t = CommandLineClient.Create(Networking.localhost, port, false);
						break;
				}
			});
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
			cmdInput.UpdateAsciiAnimation();
			cmdInput.UpdateKeyInput();
			return running;
		}

		/// <param name="port"></param>
		/// <returns>false if this port is already bound, or if there is some other server error</returns>
		public static async Task<bool> CreateCommandLineEchoServer(int port) {
			CommandLineServer cls = new CommandLineServer();
			await cls.Start(port);
			return await cls.Work();
			//Server server = new Server();
			//IPEndPoint ipEndPoint = await Networking.GetEndPoint(Networking.localhost, port);
			//CommandLineInput cmdInput = new CommandLineInput(">}|{<{|}", 100);
			//bool running = true;
			//cmdInput.BindKey(ConsoleKey.Escape, () => {
			//	Console.WriteLine("~\n"); // needed for some reason, because the console is eating characters
			//	running = false;
			//});
			//cmdInput.BindKey(ConsoleKey.Enter, () => {
			//	string input = cmdInput.PopInputAsString();
			//	switch (input) {
			//		case "client":
			//			Console.WriteLine("starting client");
			//			// TODO
			//			//	disable the client's animation AND key sensitivity
			//			//	get the CommandInput for the client
			//			//	allow the server to turn off it's CommandInput in favor of the client's
			//			//	tab will swap between server and client CommandInput
			//			//	show wich one has command input at the animating prompt
			//			Task t = CommandLineClient.Create(Networking.localhost, port, false);
			//			break;
			//	}
			//});
			//bool result = await server.Work(ipEndPoint, HandleNewClient, HandleReceived, HandleServerKeyPresses);
			//async Task<bool> HandleNewClient(TcpClient client) {
			//	var message = $"DateTime: {DateTime.Now}";
			//	var dateTimeBytes = Encoding.UTF8.GetBytes(message);
			//	NetworkStream stream = client.GetStream();
			//	await stream.WriteAsync(dateTimeBytes);
			//	Console.WriteLine($"Sent message: \"{message}\"");
			//	return true;
			//}
			//void HandleReceived(ClientHandler client, NetBuffer buffer) {
			//	// TODO create protocol for enqueuing different kinds of message (with different Message.Purpose)
			//}
			//bool HandleServerKeyPresses() {
			//	cmdInput.UpdateAsciiAnimation();
			//	cmdInput.UpdateKeyInput();
			//	return running;
			//}
			//return result;
		}
	}
}
