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
			bool canBeServer = !IsPortUsed(port);
			Task t = p.Run(host, port, canBeServer);
			// main loop. not allowed to await in Main, so we do a blocking sleep.
			while (!t.IsCompleted) { Thread.Sleep(1); }
		}

		private static bool IsPortUsed(int port) {
			IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
			IPEndPoint[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpListeners();
			foreach (IPEndPoint endpoint in tcpConnInfoArray) {
				if (endpoint.Port == port) {
					return true;
				}
			}
			return false;
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

		public async Task Run(string host, int port, bool createServer) {
			if (createServer) {
				ServerImplementation server = new ServerImplementation();
				await ServerProcess(port, server.OnConnect, server.IsGood, server.Update);
			} else {
				ClientImplementation client = new ClientImplementation();
				await ClientProcess(host, port, client.OnConnect, client.IsGood, client.Update);
			}
		}

		/// <param name="port"></param>
		/// <returns>false if this port is already bound, or if there is some other server error</returns>
		public static async Task<bool> ServerProcess(int port,
		Func<TcpClient,Task> onConnect, Func<bool> isRunning, Func<Task> update) {
			IPEndPoint ipEndPoint = await GetEndPoint(localhost, port);
			TcpListener listener = new TcpListener(ipEndPoint);
			try {
				listener.Start();
			} catch {
				return false; // will fail if a listener socket is already bound to the port
			}
			bool running = true, success;
			try {
				Console.WriteLine("listening at " + ipEndPoint);
				while (running) {
					TcpClient? newClient = await UpdateWhileListening(listener, isRunning, update);
					if (newClient == null) {
						running = false;
						break;
					}
					await onConnect?.Invoke(newClient);
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
		private static async Task<TcpClient?> UpdateWhileListening(TcpListener listener,
		Func<bool> IsRunning, Func<Task> Update) {
			Task<TcpClient> tcpClientTask = listener.AcceptTcpClientAsync();
			while (!tcpClientTask.IsCompleted) {
				if (!IsRunning.Invoke()) {
					return null;
				}
				await Update?.Invoke();
			}
			return tcpClientTask.Result;
		}
		public static async Task ClientProcess(string host, int port, Func<TcpClient, Task> onConnect, 
		Func<bool> isRunning, Func<Task> update) {
			try {
				IPEndPoint ipEndPoint = await GetEndPoint(host, port);
				if (ipEndPoint == null) {
					throw new Exception($"unable to find {host}:{port}");
				}
				TcpClient client = new TcpClient();
				await client.ConnectAsync(ipEndPoint.Address, ipEndPoint.Port);
				if (!client.Connected) {
					throw new Exception($"unable to connect to {host}:{port}");
				}
				await onConnect?.Invoke(client);
				while (isRunning.Invoke()) {
					await update.Invoke();
				}
			} catch (Exception e) {
				Console.Error.WriteLine(e);
			}
			Console.WriteLine("done with client!");
		}

		public class ServerImplementation {
			NetBuffer writeBuffer = new NetBuffer();
			List<TcpClient> clients = new List<TcpClient>();
			string animation = "/-\\|";
			int animationIndex = 0;
			int whenToAnimateNext = Environment.TickCount;

			public async Task OnConnect(TcpClient newClient) {
				NetworkStream stream = newClient.GetStream();
				Console.WriteLine("connected to new client " + newClient.Client.RemoteEndPoint);
				clients.Add(newClient);
				// example serialization. TODO make this in a delegate, with the corresponding delegate for reading.
				var message = $"DateTime: {DateTime.Now}";
				var dateTimeBytes = Encoding.UTF8.GetBytes(message);
				await stream.WriteAsync(dateTimeBytes);
				Console.WriteLine($"Sent message: \"{message}\"");
			}

			public bool IsGood() {
				if (Console.KeyAvailable) {
					ConsoleKeyInfo keyInfo = Console.ReadKey();
					if (keyInfo.Key == ConsoleKey.Escape) {
						Console.WriteLine("~\n"); // needed because the console eats characters
						return false;
					}
				}
				int now = Environment.TickCount;
				if (now >= whenToAnimateNext) {
					Console.Write(animation[animationIndex++] + "\r");
					if (animationIndex >= animation.Length) { animationIndex = 0; }
					whenToAnimateNext = now + 200;
				}
				return true;
			}

			public async Task Update() {
				await ReadClientsIntoBuffer();
				await SendWriteBufferToClients();
			}

			private async Task ReadClientsIntoBuffer() {
				for (int i = clients.Count - 1; i >= 0; --i) {
					TcpClient client = clients[i];
					if (ClearedDeadStreams(client, i)) { continue; }
					await writeBuffer.Read(client);
				}
			}
			public async Task SendWriteBufferToClients() {
				if (writeBuffer.Count == 0) { return; }
				Console.WriteLine($"writing {writeBuffer.Count} bytes: " +
					Encoding.UTF8.GetString(writeBuffer.Buffer, 0, writeBuffer.Count));
				for (int i = clients.Count - 1; i >= 0; --i) {
					TcpClient client = clients[i];
					if (ClearedDeadStreams(client, i)) { continue; }
					await writeBuffer.WriteAndFlush(client);
				}
				writeBuffer.Clear();
			}
			private bool ClearedDeadStreams(TcpClient client, int i) {
				if (client == null || !client.Connected) {
					string status = client == null ? "deleted" : "disconnected";
					Console.WriteLine($"removing client {i} ({status})");
					if (clients != null) {
						clients.RemoveAt(i);
					}
					return true;
				}
				return false;
			}
		}

		public class ClientImplementation {
			public TcpClient client;
			public NetworkStream? stream;
			public ValueTask<int> receivedTask;
			public List<char> consoleInput = new List<char>();
			public byte[] networkInputBuffer = new byte[1024]; // TODO use NetBuffer instead
			public ClientImplementation() { }

			public async Task OnConnect(TcpClient connectedClient) {
				client = connectedClient;
				Console.WriteLine("connected.");
				stream = client.GetStream();
				receivedTask = stream.ReadAsync(networkInputBuffer);
			}

			public bool IsGood() => client.Connected;

			public async Task Update() {
				if (Console.KeyAvailable) {
					ConsoleKeyInfo keyInfo = Console.ReadKey();
					switch (keyInfo.Key) {
						case ConsoleKey.Enter: await FinishConsoleInput(); break;
						case ConsoleKey.Escape: client.Close(); break;
						default: AddConsoleInput(keyInfo.KeyChar); break;
					}
				}
				if (stream.CanRead && receivedTask.IsCompleted) {
					int received = receivedTask.Result;
					if (received <= 0) {
						Console.Error.WriteLine($"{received} bytes received");
						return;
					}
					string message = Encoding.UTF8.GetString(networkInputBuffer, 0, received);
					Console.WriteLine(message);
					receivedTask = stream.ReadAsync(networkInputBuffer);
				}
			}

			public async Task FinishConsoleInput() {
				if (!stream.CanWrite) {
					Console.WriteLine($"can't write '{string.Join("' '", consoleInput)}'");
				} else if (consoleInput.Count > 0) {
					byte[] outbytes = new byte[consoleInput.Count];
					for (int i = 0; i < consoleInput.Count; ++i) {
						outbytes[i] = (byte)consoleInput[i];
					}
					await stream.WriteAsync(outbytes);
					stream.Flush();
				}
				consoleInput.Clear();
			}

			public void AddConsoleInput(char keyChar) {
				if (keyChar == 0) { return; }
				consoleInput.Add(keyChar);
			}
		}
		/// <summary>
		/// byte array with additional functionality for reading/writing to <see cref="TcpClient"/>
		/// </summary>
		public class NetBuffer {
			/// <summary>
			/// byte buffer for receiving and sending data
			/// </summary>
			private byte[] _buffer = Array.Empty<byte>();
			/// <summary>
			/// how much the buffer is filled
			/// </summary>
			private int _filled = 0;
			/// <summary>
			/// seperate buffer that reads data directly before being added to <see cref="_buffer"/>
			/// </summary>
			private byte[] _networkReadBuffer;
			private int DefaultNetworkReadBufferSize = 1024;
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
			public async Task WriteAndFlush(TcpClient c) {
				NetworkStream ns = c.GetStream();
				try {
					await ns.WriteAsync(Buffer, 0, Count);
					ns.Flush();
				} catch { }
			}
			public async Task Read(TcpClient client) {
				NetworkStream ns = client.GetStream();
				if (_networkReadBuffer == null) {
					_networkReadBuffer = new byte[DefaultNetworkReadBufferSize];
				}
				while (ns.DataAvailable) {
					int received = await ns.ReadAsync(_networkReadBuffer);
					if (received <= 0) { return; }
					Add(_networkReadBuffer, received);
				}
			}
		}
	}
}
