using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace networking {

	public class Server {
		public TcpListener listener;
		public Dictionary<long, ClientHandler> clients;
		/// <summary>
		/// clients discovered to be disconnected, will be purged by <see cref="PurgeDeadClients"/>
		/// </summary>
		private HashSet<ClientHandler> _deadClients;
		private List<Message> _messageQueue;
		private NetBuffer _broadcastBuffer;
		private NetBuffer individualClientReadBuffer;
		public bool running, success;
		private long nextClientId;

		public NetBuffer BroadcastBuffer => _broadcastBuffer;

		public static bool IsPortAvailable(int port) {
			// same info as from netstat command line tool
			IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
			TcpConnectionInformation[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpConnections();
			foreach (TcpConnectionInformation tcpi in tcpConnInfoArray) {
				if (tcpi.LocalEndPoint.Port == port) { return false; }
			}
			return true;
		}

		private void Init() {
			nextClientId = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
			clients = new Dictionary<long, ClientHandler>();
			_broadcastBuffer = new NetBuffer();
			individualClientReadBuffer = new NetBuffer(new byte[1024], 0);
			_deadClients = new HashSet<ClientHandler>();
			_messageQueue = new List<Message>();
			running = true;
			success = false;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="ipEndPoint"><code>IPEndPoint ipEndPoint = await GetEndPoint(localhost, port);</code></param>
		/// <returns>true on success, false on failure</returns>
		public async Task<bool> Work(IPEndPoint ipEndPoint, Func<TcpClient, Task<bool>> onClientConnect,
		Action<ClientHandler, NetBuffer> onReceived, Func<bool> update) {
			listener = new TcpListener(ipEndPoint);
			try {
				listener.Start();
			} catch {
				return false; // will fail if a listener socket is already bound to the port
			}
			Init();
			try {
				Console.WriteLine("listening at " + ipEndPoint);
				while (running) {
					TcpClient clientHandler = await ServiceClientsWhileListening(onReceived, update);
					if (clientHandler == null) {
						running = false;
						Console.WriteLine("ended");
						break;
					}
					Console.WriteLine("connected to " + clientHandler.Client.RemoteEndPoint);
					bool clientValid = true;
					if (onClientConnect != null) {
						clientValid = await onClientConnect?.Invoke(clientHandler);
					}
					if (clientValid) {
						clients[nextClientId] = new ClientHandler(clientHandler, nextClientId);
						++nextClientId;
					}
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

		private async Task<TcpClient> ServiceClientsWhileListening(Action<ClientHandler, NetBuffer> onReceived, Func<bool> update) {
			Task<TcpClient> tcpClientTask = listener.AcceptTcpClientAsync();
			while (!tcpClientTask.IsCompleted) {
				if (update != null && !update.Invoke()) { return null; }
				await Read(onReceived);
				await WriteMessages();
				await Broadcast();
			}
			return tcpClientTask.Result;
		}

		public async Task Read(Action<ClientHandler, NetBuffer> onReceived) {
			ClientHandler c;
			foreach(KeyValuePair<long, ClientHandler> kvp in clients) {
				c = kvp.Value;
				if (IsDeadCheck(c)) { continue; }
				await _broadcastBuffer.Read(c.tcp, individualClientReadBuffer, OnReceived);
			}
			void OnReceived(NetBuffer buffer) => onReceived?.Invoke(c, buffer);
			PurgeDeadClients();
		}

		private async Task WriteMessages() {
			foreach(var message in _messageQueue) {
				await message.Send(this);
			}
		}

		public bool IsDeadCheck(ClientHandler c) {
			if (!c.IsAlive) {
				_deadClients.Add(c);
				return true;
			}
			return false;
		}

		private void PurgeDeadClients() {
			if (_deadClients.Count == 0) { return; }
			foreach (ClientHandler c in _deadClients) {
				clients.Remove(c.id);
			}
			_deadClients.Clear();
		}

		public async Task Broadcast() {
			if (_broadcastBuffer.Count == 0) { return; }
			Console.WriteLine($"broadcasting {_broadcastBuffer.Count}b: \"{_broadcastBuffer.ToUtf8()}\"");
			foreach (KeyValuePair<long, ClientHandler> kvp in clients) {
				ClientHandler c = kvp.Value;
				if (IsDeadCheck(c)) { continue; }
				try {
					await _broadcastBuffer.WriteFlushStream(c.tcp);
				} catch {
					Console.WriteLine($"{c.id} failed, but was not considered a dead stream yet...");
					_deadClients.Add(c);
				}
			}
			PurgeDeadClients();
			_broadcastBuffer.Clear();
		}
	}
}
