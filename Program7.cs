using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace networkingMinimal {
	public class Program {
		public const string localhost = "localhost";
		public const int defaultPort = 8765;

		public static void Main(string[] args) {
			Program p = new Program();
			int port = defaultPort;
			string host = localhost;
			bool canBeServer = !CheckPortIsUsed(port);
			Task t = p.Run(host, port, canBeServer);
			// not allowed to await in Main, so we do a blocking sleep.
			while (!t.IsCompleted) { Thread.Sleep(1); }
			Console.WriteLine($"finished {host}:{port}");
		}

		private static bool CheckPortIsUsed(int port) {
			IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
			IPEndPoint[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpListeners();
			foreach (IPEndPoint endpoint in tcpConnInfoArray) {
				if (endpoint.Port == port) {
					return true;
				}
			}
			return false;
		}

		public async Task Run(string host, int port, bool createServer) {
			if (createServer) {
				await CreateEchoServer(port);
			} else {
				await CreateClient(host, port);
			}
		}

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
			NetBuffer writeBuffer = new NetBuffer();
			byte[] readBuffer = new byte[1024];
			bool running = true, success = false;
			string animation = "/-\\|";
			int animationIndex = 0;
			int whenToAnimateNext = Environment.TickCount;
			try {
				Console.WriteLine("listening at " + ipEndPoint);
				while (running) {
					Console.Write("\r");
					TcpClient? handler = await ServiceClientsWhileListening(listener, IsServerGood, async () => {
						await Read(clients, writeBuffer, readBuffer);
						await writeBuffer.WriteFlush(clients);
					});
					if (handler == null) {
						running = false;
						Console.WriteLine("ended");
						break;
					}
					NetworkStream stream = handler.GetStream();
					Console.WriteLine("connected to new client " + handler.Client.RemoteEndPoint);
					clients.Add(handler);
					// example serialization. TODO make this in a delegate, with the corresponding delegate for reading.
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
			bool IsServerGood() {
				if (Console.KeyAvailable) {
					ConsoleKeyInfo keyInfo = Console.ReadKey();
					if (keyInfo.Key == ConsoleKey.Escape) {
						Console.WriteLine("~\n"); // needed because the console eats characters
						return false;
					}
				}
				if (Environment.TickCount >= whenToAnimateNext) {
					Console.Write(animation[animationIndex++] + "\r");
					if (animationIndex >= animation.Length) { animationIndex = 0; }
					whenToAnimateNext += 200;
				}
				return true;
			}
			return success;
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
		private static async Task Read(List<TcpClient> clients, NetBuffer buff, byte[] networkInputBuffer) {
			for (int i = clients.Count - 1; i >= 0; --i) {
				TcpClient c = clients[i];
				if (IsClearedDeadStream(c, i, clients)) { continue; }
				await buff.Read(c, networkInputBuffer);
			}
		}
		private static async Task<TcpClient?> ServiceClientsWhileListening(TcpListener listener,
		Func<bool> shouldContinue, Func<Task> whatToDoWhileWaiting) {
			Task<TcpClient> tcpClientTask = listener.AcceptTcpClientAsync();
			while (!tcpClientTask.IsCompleted) {
				if (!shouldContinue.Invoke()) {
					return null;
				}
				await whatToDoWhileWaiting?.Invoke();
			}
			return tcpClientTask.Result;
		}
		public static async Task CreateClient(string host, int port) {
			IPEndPoint ipEndPoint = await GetEndPoint(host, port);
			if (ipEndPoint == null) {
				throw new Exception($"unable to connect to {host}:{port}");
			}
			ClientCommunicator cc = new ClientCommunicator();
			try {
				await cc.ConnectAsync(ipEndPoint);
				StringBuilder stringBuilder = new StringBuilder();
				while (cc.client.Connected) {
					if (Console.KeyAvailable) {
						ConsoleKeyInfo keyInfo = Console.ReadKey();
						switch (keyInfo.Key) {
							case ConsoleKey.Enter:
								if (!cc.stream.CanWrite) {
									Console.WriteLine($"can't write {string.Join(" ", cc.consoleInput)}");
								} else if (cc.consoleInput.Count > 0) {
									byte[] outbytes = new byte[cc.consoleInput.Count];
									for (int i = 0; i < cc.consoleInput.Count; ++i) {
										outbytes[i] = (byte)cc.consoleInput[i];
									}
									await cc.stream.WriteAsync(outbytes);
									cc.stream.Flush();
								}
								cc.consoleInput.Clear();
								break;
							default:
								if (keyInfo.KeyChar != 0) {
									cc.consoleInput.Add(keyInfo.KeyChar);
								}
								break;
						}
					}
					stringBuilder.Clear();
					if (cc.stream.CanRead && cc.receivedTask.IsCompleted) {
						int received = cc.receivedTask.Result;
						Console.WriteLine($"received {received}");
						if (received <= 0) { break; }
						string message = Encoding.UTF8.GetString(cc.networkInputBuffer, 0, received);
						Console.WriteLine(message);
						stringBuilder.Append(message);
						cc.receivedTask = cc.stream.ReadAsync(cc.networkInputBuffer);
						Console.WriteLine("looking for more...");
					}
					if (stringBuilder.Length > 0) {
						Console.WriteLine($"Message received: \"{stringBuilder}\"");
					}
				}
			} catch (Exception e) {
				Console.WriteLine(e);
			}
			Console.WriteLine("done with client!");
		}
		private static bool IsClearedDeadStream(TcpClient c, int i, List<TcpClient> clients) {
			if (c == null || !c.Connected) {
				Console.WriteLine($"removing client {i} {c == null}");
				if (clients != null) {
					clients.RemoveAt(i);
				}
				return true;
			}
			return false;
		}

		public class NetBuffer {
			/// <summary>
			/// byte buffer for receiving and sending data
			/// </summary>
			private byte[] _buffer = Array.Empty<byte>();
			/// <summary>
			/// how much the buffer is filled
			/// </summary>
			private int _filled = 0;
			public byte[] Buffer => _buffer;
			public int Count => _filled;
			public byte this[int i] { get { return _buffer[i]; } set { _buffer[i] = value; } }
			public void Add(byte[] bytes) => Add(bytes, bytes.Length);
			public void Add(byte[] bytes, int count) {
				if (_buffer.Length < _filled + count) {
					Array.Resize(ref _buffer, _filled + count);
				}
				Array.Copy(bytes, 0, _buffer, _filled, count);
				_filled += count;
			}
			public void Clear() { _filled = 0; }
			public async Task WriteFlush(List<TcpClient> clients) {
				if (Count == 0) { return; }
				Console.WriteLine($"writing {Count} bytes: " + System.Text.Encoding.UTF8.GetString(Buffer, 0, Count));
				for (int i = clients.Count - 1; i >= 0; --i) {
					if (IsClearedDeadStream(clients[i], i, clients)) { continue; }
					await WriteFlush(clients[i]);
				}
				Clear();
			}
			public async Task WriteFlush(TcpClient c) {
				NetworkStream ns = c.GetStream();
				try {
					await ns.WriteAsync(Buffer, 0, Count);
					ns.Flush();
				} catch {}
			}
			/// <param name="client"></param>
			/// <param name="intermediateBuffer">where bytes being read directly from the socket go</param>
			/// <returns></returns>
			public async Task Read(TcpClient client, byte[] intermediateBuffer) {
				NetworkStream ns = client.GetStream();
				while (ns.DataAvailable) {
					int received = await ns.ReadAsync(intermediateBuffer);
					if (received <= 0) { return; }
					Add(intermediateBuffer, received);
				}
			}
		}

		public class ClientCommunicator {
			public TcpClient client;
			public NetworkStream? stream;
			public ValueTask<int> receivedTask;
			public List<char> consoleInput = new List<char>();
			public byte[] networkInputBuffer = new byte[1024];
			public ClientCommunicator() {
				client = new TcpClient();
			}
			public async Task ConnectAsync(IPEndPoint ipEndPoint) {
				await client.ConnectAsync(ipEndPoint.Address, ipEndPoint.Port);
				Console.WriteLine("connected.");
				stream = client.GetStream();
				receivedTask = stream.ReadAsync(networkInputBuffer);
			}
		}

	}
}
