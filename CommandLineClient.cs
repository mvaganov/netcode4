using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace networking {
	public class CommandLineClient : ICommandLineContext {
		private IPEndPoint ipEndPoint;
		private networking.Client client;
		private byte[] toSend;
		private StringBuilder receivedDataAsString;
		private CommandLineInput cmdInput;
		private string host;
		private int port;
		private Task execution;
		private string _name;

		public Task Execution => execution;
		public CommandLineInput Input => cmdInput;

		public string Name { get => GetName(); set => _name = value; }

		public string GetName() => _name;
		public CommandLineClient(string host, int port, bool handleCommandLine) {
			this.host = host;
			this.port = port;
			_name = $"connect {host}:{port} ...";
			cmdInput = new CommandLineInput(GetName);
			cmdInput.Enabled = handleCommandLine;
		}

		public async Task Start() {
			ipEndPoint = await Networking.GetEndPoint(host, port);
			if (ipEndPoint == null) {
				throw new Exception($"unable to connect to {host}:{port}");
			}
			client = new Client();
			receivedDataAsString = new StringBuilder();
			toSend = null;
			cmdInput.BindKey(ConsoleKey.Enter, ClientQueueMessageToSend);
		}

		void ClientQueueMessageToSend() {
			if (toSend != null) {
				throw new Exception($"multiple messages being queued, change {nameof(toSend)} into a Queue");
			}
			toSend = cmdInput.PopInputAsBytes();
		}

		public async Task Work() {
			try {
				await client.Work(ipEndPoint, ClientConnected, ClientRead, ClientUpdateWrite);
			} catch (Exception e) {
				Console.WriteLine(e);
				if (Input.Enabled) {
					Console.ReadKey();
				}
			}
		}

		private void ClientConnected(TcpClient tcp) {
			string ipString = tcp.Client.LocalEndPoint.ToString();
			_name = $"client {ipString}";
		}

		private void ClientRead(NetBuffer buffer) {
			if (buffer.Count == 0) {
				Console.WriteLine(ipEndPoint + " RECEIVED:" + receivedDataAsString.ToString());
				receivedDataAsString.Clear();
				return;
			}
			string message = buffer.ToUtf8();
			receivedDataAsString.Append(message);
		}

		private NetBuffer ClientUpdateWrite() {
			if (!Input.Enabled) { return null; }
			cmdInput.UpdatePrompt();
			cmdInput.UpdateKeyInput();
			NetBuffer result = null;
			if (toSend != null) {
				result = new NetBuffer(toSend);
				toSend = null;
			}
			return result;
		}

		public static CommandLineClient Create(string host, int port, bool useCommandLine = true) {
			CommandLineClient clc = new CommandLineClient(host, port, useCommandLine);
			clc.execution = clc.InternalExecute();
			return clc;
		}

		private async Task InternalExecute() {
			await Start();
			await Work();
		}

		public static async Task CreateAsync(string host, int port, bool useCommandLine = true) {
			CommandLineClient clc = Create(host, port, useCommandLine);
			await clc.Execution;
		}
	}
}
