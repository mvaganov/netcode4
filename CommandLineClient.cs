using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace networking {
	public class CommandLineClient {
		private IPEndPoint ipEndPoint;
		private networking.Client client;
		private byte[] toSend;
		private StringBuilder receivedDataAsString;
		private CommandLineInput cmdInput;
		private bool handleCommandLine;

		public async Task Start(string host, int port, bool useCommandLine = true) {
			ipEndPoint = await Networking.GetEndPoint(host, port);
			if (ipEndPoint == null) {
				throw new Exception($"unable to connect to {host}:{port}");
			}
			client = new Client();
			receivedDataAsString = new StringBuilder();
			cmdInput = new CommandLineInput();
			toSend = null;
			cmdInput.BindKey(ConsoleKey.Enter, ClientQueueMessageToSend);
			handleCommandLine = useCommandLine;
		}

		void ClientQueueMessageToSend() {
			if (toSend != null) {
				throw new Exception($"multiple messages being queued, change {nameof(toSend)} into a Queue");
			}
			toSend = cmdInput.PopInputAsBytes();
		}

		public async Task Work() {
			try {
				await client.Work(ipEndPoint, ClientRead, ClientUpdateWrite);
			} catch (Exception e) {
				Console.WriteLine(e);
				if (handleCommandLine) {
					Console.ReadKey();
				}
			}
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
			if (!handleCommandLine) { return null; }
			cmdInput.UpdateAsciiAnimation();
			cmdInput.UpdateKeyInput();
			NetBuffer result = null;
			if (toSend != null) {
				result = new NetBuffer(toSend);
				toSend = null;
			}
			return result;
		}

		public static async Task Create(string host, int port, bool useCommandLine = true) {
			CommandLineClient clc = new CommandLineClient();
			await clc.Start(host, port, useCommandLine);
			await clc.Work();
			//IPEndPoint ipEndPoint = await Networking.GetEndPoint(host, port);
			//if (ipEndPoint == null) { throw new Exception($"unable to connect to {host}:{port}"); }
			//Client client = new Client();
			//StringBuilder receivedDataAsString = new StringBuilder();
			//CommandLineInput cmdInput = new CommandLineInput();
			//byte[] toSend = null;
			//cmdInput.BindKey(ConsoleKey.Enter, ClientQueueMessageToSend);
			//try {
			//	await client.Work(ipEndPoint, ClientRead, ClientUpdateWrite);
			//} catch (Exception e) {
			//	Console.WriteLine(e);
			//	Console.ReadKey();
			//}
			//void ClientRead(NetBuffer buffer) {
			//	if (buffer.Count == 0) {
			//		Console.WriteLine(ipEndPoint + " RECEIVED:" + receivedDataAsString.ToString());
			//		receivedDataAsString.Clear();
			//		return;
			//	}
			//	string message = buffer.ToUtf8();
			//	receivedDataAsString.Append(message);
			//}
			//void ClientQueueMessageToSend() {
			//	if (toSend != null) {
			//		throw new Exception($"multiple messages being queued, change {nameof(toSend)} into a Queue");
			//	}
			//	toSend = cmdInput.PopInputAsBytes();
			//}
			//NetBuffer ClientUpdateWrite() {
			//	if (showAnimation) {
			//		cmdInput.UpdateAsciiAnimation();
			//	}
			//	cmdInput.UpdateKeyInput();
			//	NetBuffer result = null;
			//	if (toSend != null) {
			//		result = new NetBuffer(toSend);
			//		toSend = null;
			//	}
			//	return result;
			//}
			//Console.WriteLine("done with client!");
		}
	}
}
