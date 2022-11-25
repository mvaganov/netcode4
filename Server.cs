using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace networking {

	public class Server {
		public TcpListener listener;
		public Dictionary<long,Client> clients;
		/// <summary>
		/// clients discovered to be disconnected, will be purged by <see cref="PurgeDeadClients"/>
		/// </summary>
		private HashSet<Client> _deadClients;
		private List<Message> _messageQueue;
		public NetBuffer _echoToAllBuffer;
		public NetBuffer individualClientReadBuffer;
		public bool running, success;
		private long nextClientId;

		public struct Client {
			public TcpClient tcp;
			private long _id;
			public long id => _id;
			public bool IsAlive => tcp != null && tcp.Connected;
			public Client(TcpClient tcp, long id) {
				this.tcp = tcp;
				_id = id;
			}
		}

		public enum WhatToDoWithMessage {
			Nothing, SendToAll, SendToAllExceptSender, SendToSpecificClients, EchoToSender
		}

		public struct Message {
			public NetBuffer message;
			public long sender;
			public HashSet<long> targets;
			public WhatToDoWithMessage whatToDo;

			public async Task Send(Server server) {
				switch (whatToDo) {
					case WhatToDoWithMessage.EchoToSender:
						await message.WriteFlushStream(server.clients[sender].tcp);
						break;
					case WhatToDoWithMessage.SendToAll:
						server._echoToAllBuffer.Add(message);
						break;
					case WhatToDoWithMessage.SendToSpecificClients:
						foreach(long id in targets) {
							if (!server.clients.TryGetValue(id, out Client c) || server.IsDeadCheck(c)) {
								await NetBuffer.FromString($"ERROR: client {id} failed").WriteFlushStream(server.clients[sender].tcp);
								break;
							}
							await message.WriteFlushStream(c.tcp);
						}
						break;
				}
			}
		}

		public static bool IsPortAvailable(int port) {
			// same info as from netstat command line tool
			IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
			TcpConnectionInformation[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpConnections();
			foreach (TcpConnectionInformation tcpi in tcpConnInfoArray) {
				if (tcpi.LocalEndPoint.Port == port) { return false; }
			}
			return true;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="ipEndPoint"><code>IPEndPoint ipEndPoint = await GetEndPoint(localhost, port);</code></param>
		/// <returns>true on success, false on failure</returns>
		public async Task<bool> Work(IPEndPoint ipEndPoint, Func<TcpClient, Task<bool>> onClientConnect,
		Action<Client, NetBuffer> onReceived, Func<bool> update) {
			listener = new TcpListener(ipEndPoint);
			try {
				listener.Start();
			} catch {
				return false; // will fail if a listener socket is already bound to the port
			}
			nextClientId = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
			clients = new Dictionary<long, Client>();
			_echoToAllBuffer = new NetBuffer();
			individualClientReadBuffer = new NetBuffer(new byte[1024], 0);
			_deadClients = new HashSet<Client>();
			running = true;
			success = false;
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
						clients[nextClientId] = new Client(clientHandler, nextClientId);
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

		private async Task<TcpClient> ServiceClientsWhileListening(Action<Client, NetBuffer> onReceived, Func<bool> update) {
			Task<TcpClient> tcpClientTask = listener.AcceptTcpClientAsync();
			while (!tcpClientTask.IsCompleted) {
				if (update != null && !update.Invoke()) { return null; }
				//await echoToAllBuffer.Read(clients, individualClientReadBuffer, onReceived);
				await Read(onReceived);
				await Broadcast();
			}
			return tcpClientTask.Result;
		}

		public async Task Read(Action<Client, NetBuffer> onReceived) {
			Client c;
			foreach(KeyValuePair<long, Client> kvp in clients) {
				c = kvp.Value;
				if (IsDeadCheck(c)) { continue; }
				await _echoToAllBuffer.Read(c.tcp, individualClientReadBuffer, OnReceived);
			}
			void OnReceived(NetBuffer buffer) => onReceived?.Invoke(c, buffer);
			PurgeDeadClients();
		}

		public bool IsDeadCheck(Client c) {
			if (!c.IsAlive) {
				_deadClients.Add(c);
				return true;
			}
			return false;
		}

		private void PurgeDeadClients() {
			if (_deadClients.Count == 0) { return; }
			foreach (Client c in _deadClients) {
				clients.Remove(c.id);
			}
			_deadClients.Clear();
		}

		public async Task Broadcast() {
			if (_echoToAllBuffer.Count == 0) { return; }
			Console.WriteLine($"broadcasting {_echoToAllBuffer.Count}b: \"{_echoToAllBuffer.ToUtf8()}\"");
			foreach (KeyValuePair<long, Client> kvp in clients) {
				Client c = kvp.Value;
				if (IsDeadCheck(c)) { continue; }
				try {
					await _echoToAllBuffer.WriteFlushStream(c.tcp);
				} catch {
					Console.WriteLine($"{c.id} failed, but was not considered a dead stream yet...");
					_deadClients.Add(c);
				}
			}
			PurgeDeadClients();
			_echoToAllBuffer.Clear();
		}
	}
}
