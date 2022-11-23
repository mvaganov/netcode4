using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace networking {
	public class Client {
		public TcpClient client;
		public NetworkStream stream;
		public ValueTask<int> readTask;
		public Task writeTask;
		public byte[] networkInputBuffer = new byte[1024];
		Action<int, byte[]> onReceived;
		Func<byte[]> getDataToWrite;
		private bool receivedDataLastUpdate = false;

		public Client() {
			client = new TcpClient();
		}

		public async Task ConnectAsync(IPEndPoint ipEndPoint) {
			await client.ConnectAsync(ipEndPoint.Address, ipEndPoint.Port);
			stream = client.GetStream();
			readTask = stream.ReadAsync(networkInputBuffer);
		}

		public async Task Work(IPEndPoint ipEndPoint, Action<int, byte[]> onReceived, Func<byte[]> getDataToWrite) {
			this.onReceived = onReceived;
			this.getDataToWrite = getDataToWrite;
			await ConnectAsync(ipEndPoint);
			while (client.Connected) {
				KeepWriting();
				KeepReading();
			}
		}

		private void KeepWriting() {
			if (writeTask != null && !writeTask.IsCompleted) { return; }
			byte[] data = getDataToWrite();
			if (data != null) {
				writeTask = stream.WriteAsync(data, 0, data.Length);
				stream.Flush();
			}
		}

		private bool KeepReading() {
			if (stream.CanRead && readTask.IsCompleted) {
				int received = readTask.Result;
				if (received <= 0) { return false; }
				onReceived.Invoke(received, networkInputBuffer);
				receivedDataLastUpdate = true;
				readTask = stream.ReadAsync(networkInputBuffer);
			} else if (receivedDataLastUpdate) {
				receivedDataLastUpdate = false;
				onReceived.Invoke(0, networkInputBuffer);
			}
			return true;
		}
	}
}
