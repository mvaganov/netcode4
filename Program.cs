using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace networking {
	public class Program {
		public const string localhost = "localhost";
		public const int defaultPort = 8765;

		public static void Main(string[] args) {
			Program p = new Program();
			int port = defaultPort;
			string host = localhost;
			Task t = p.Run(host, port);
			// not allowed to await in Main, so we to a blocking sleep.
			while (!t.IsCompleted) { Thread.Sleep(1); }
			Console.WriteLine($"finished {host}:{port}");
		}

		public async Task Run(string host, int port) {
			bool isServer = false;
			if (Server.IsPortAvailable(port)) {
				isServer = await CreateCommandLineEchoServer(port);
			}
			if (!isServer) {
				Console.WriteLine($"connecting client to {host}:{port}");
				await CreateCommandLineEchoClient(host, port);
			}
		}

		public static async Task<IPEndPoint> GetEndPoint(string host, int port) {
			IPAddress ipAddress = await GetIpAddress(host);
			IPEndPoint ipEndPoint = new IPEndPoint(ipAddress, port);
			return ipEndPoint;
		}

		public static async Task<IPAddress> GetIpAddress(string host) {
			IPHostEntry ipHostInfo = await Dns.GetHostEntryAsync(host);
			return ipHostInfo.AddressList[0];
		}

		public static async Task CreateCommandLineEchoClient(string host, int port) {
			IPEndPoint ipEndPoint = await GetEndPoint(host, port);
			if (ipEndPoint == null) { throw new Exception($"unable to connect to {host}:{port}"); }
			Client client = new Client();
			StringBuilder sb = new StringBuilder();
			List<char> consoleInput = new List<char>();
			string animation = "/-\\|";
			int animationIndex = 0, whenToAnimateNext = Environment.TickCount;
			const int AnimationFrameMs = 200;
			try {
				await client.Work(ipEndPoint, ClientRead, ClientUpdateWrite);
			} catch (Exception e) {
				Console.WriteLine(e);
			}
			void ClientRead(NetBuffer buffer) {
				if (buffer.Count == 0) {
					Console.WriteLine("RECEIVED:" + sb.ToString());
					sb.Clear();
					return;
				}
				string message = buffer.ToUtf8();
				sb.Append(message);
			}
			NetBuffer ClientUpdateWrite() {
				if (consoleInput.Count == 0 && Environment.TickCount >= whenToAnimateNext) {
					whenToAnimateNext += AnimationFrameMs;
					if (consoleInput.Count == 0) {
						Console.Write(animation[animationIndex++] + "\r");
						if (animationIndex >= animation.Length) { animationIndex = 0; }
					}
				}
				if (!Console.KeyAvailable) { return null; }
				ConsoleKeyInfo keyInfo = Console.ReadKey();
				switch (keyInfo.Key) {
					case ConsoleKey.Enter:
						if (consoleInput.Count > 0) {
							byte[] outbytes = Encoding.UTF8.GetBytes(consoleInput.ToArray());
							consoleInput.Clear();
							return new NetBuffer(outbytes);
						}
						break;
					default:
						if (keyInfo.KeyChar != 0) { consoleInput.Add(keyInfo.KeyChar); }
						break;
				}
				return null;
			}
			Console.WriteLine("done with client!");
		}

		/// <param name="host"></param>
		/// <param name="port"></param>
		/// <returns>false if this port is already bound, or if there is some other server error</returns>
		public static async Task<bool> CreateCommandLineEchoServer(int port) {
			Server server = new Server();
			IPEndPoint ipEndPoint = await GetEndPoint(localhost, port);
			string animation = ">}|{<{|}";
			const int AnimationFrameMs = 100;
			int animationIndex = 0, whenToAnimateNext = Environment.TickCount;
			bool result = await server.Work(ipEndPoint, HandleNewClient, HandleReceived, HandleServerKeyPresses);
			async Task<bool> HandleNewClient(TcpClient client) {
				var message = $"DateTime: {DateTime.Now}";
				var dateTimeBytes = Encoding.UTF8.GetBytes(message);
				NetworkStream stream = client.GetStream();
				await stream.WriteAsync(dateTimeBytes);
				Console.WriteLine($"Sent message: \"{message}\"");
				return true;
			}
			void HandleReceived(Server.Client client, NetBuffer buffer) {
			}
			bool HandleServerKeyPresses() {
				int now = Environment.TickCount;
				if (!Console.KeyAvailable && now < whenToAnimateNext) { return true; }
				whenToAnimateNext = now + AnimationFrameMs;
				Console.Write(server.clients.Count + " " + animation[animationIndex++] + "  \r");
				if (animationIndex >= animation.Length) { animationIndex = 0; }
				if (Console.KeyAvailable) {
					ConsoleKeyInfo keyInfo = Console.ReadKey();
					if (keyInfo.Key == ConsoleKey.Escape) {
						Console.WriteLine("~\n"); // needed for some reason, because the console is eating characters
						return false;
					}
				}
				return true;
			}
			return result;
		}
	}
}
