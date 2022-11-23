using System;
using System.Collections.Generic;
using System.Linq;
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
			bool isServer = await CreateEchoServer(port);
			if (!isServer) {
				Console.WriteLine($"connecting client to {host}:{port}");
				await CreateClient(host, port);
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

		public static async Task CreateClient(string host, int port) {
			IPEndPoint ipEndPoint = await GetEndPoint(host, port);
			if (ipEndPoint == null) { throw new Exception($"unable to connect to {host}:{port}"); }
			Client client = new Client();
			StringBuilder sb = new StringBuilder();
			List<char> consoleInput = new List<char>();
			try {
				await client.Work(ipEndPoint, ClientRead, ClientWrite);
			} catch (Exception e) {
				Console.WriteLine(e);
			}
			void ClientRead(int count, byte[] data) {
				if (count == 0) {
					Console.WriteLine("RECEIVED:" + sb.ToString());
					sb.Clear();
					return;
				}
				string message = Encoding.UTF8.GetString(data, 0, count);
				Console.WriteLine(message);
				sb.Append(message);
			}
			byte[] ClientWrite() {
				if (!Console.KeyAvailable) { return null; }
				ConsoleKeyInfo keyInfo = Console.ReadKey();
				switch (keyInfo.Key) {
					case ConsoleKey.Enter:
						if (consoleInput.Count > 0) {
							byte[] outbytes = Encoding.UTF8.GetBytes(consoleInput.ToArray());
							consoleInput.Clear();
							return outbytes;
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
		public static async Task<bool> CreateEchoServer(int port) {
			IPEndPoint ipEndPoint = await GetEndPoint(localhost, port);
			TcpListener listener = new TcpListener(ipEndPoint);
			try {
				listener.Start();
			} catch {
				return false; // will fail if a listener socket is already bound to the port
			}

			List<TcpClient> clients = new List<TcpClient>();
			NetBuffer netBuffer = new NetBuffer();
			byte[] smallBuffer = new byte[1024];
			bool running = true, success = false;
			try {
				Console.WriteLine("listening at " + ipEndPoint);
				while (running) {
					TcpClient handler = await ServiceClientsWhileListening(listener, clients, netBuffer, smallBuffer);
					if (handler == null) {
						running = false;
						Console.WriteLine("ended");
						break;
					}
					//Console.WriteLine("done listening");
					NetworkStream stream = handler.GetStream();
					Console.WriteLine("connected to " + handler.Client.RemoteEndPoint);
					clients.Add(handler);

					var message = $"DateTime: {DateTime.Now}";
					var dateTimeBytes = Encoding.UTF8.GetBytes(message);
					await stream.WriteAsync(dateTimeBytes);

					Console.WriteLine($"Sent message: \"{message}\"");
				}
				success = true;
			} catch (Exception e) {
				Console.WriteLine("SERVER ERROR\n" + e);
				success = false;
			} finally {
				listener.Stop();
			}
			return success;
		}
		private static async Task<TcpClient> ServiceClientsWhileListening(TcpListener listener,
		List<TcpClient> clients, NetBuffer buff, byte[] networkInputBuffer) {
			Task<TcpClient> tcpClientTask = listener.AcceptTcpClientAsync();
			string animation = "/-\\|";
			int animationIndex = 0;
			int whenToAnimateNext = Environment.TickCount;
			Console.Write("\r");
			while (!tcpClientTask.IsCompleted) {
				if (Environment.TickCount > whenToAnimateNext) {
					if (Console.KeyAvailable) {
						ConsoleKeyInfo keyInfo = Console.ReadKey();
						if (keyInfo.Key == ConsoleKey.Escape) {
							Console.WriteLine("~\n"); // needed for some reason, because the console is eating characters
							return null;
						}
					}
					Console.Write(animation[animationIndex++] + "\r");
					if (animationIndex >= animation.Length) { animationIndex = 0; }
					whenToAnimateNext += 200;
				}
				await Read(clients, buff, networkInputBuffer);
				await buff.WriteFlush(clients);
				//await Write(buff, clients);
			}
			return tcpClientTask.Result;
		}
		private static async Task Read(List<TcpClient> clients, NetBuffer buff, byte[] networkInputBuffer) {
			for (int i = clients.Count - 1; i >= 0; --i) {
				TcpClient c = clients[i];
				if (IsClearedDeadStream(c, i, clients)) { continue; }
				await buff.Read(c, networkInputBuffer);
			}
		}

		public static bool IsClearedDeadStream(TcpClient c, int i, List<TcpClient> clients) {
			if (c == null || !c.Connected) {
				Console.WriteLine($"removing client {i} {c == null}");
				if (clients != null) {
					clients.RemoveAt(i);
				}
				return true;
			}
			return false;
		}

	}
}
