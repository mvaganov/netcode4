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
		//public byte[] networkInputBuffer = new byte[1024];
		public NetBuffer networkInputBuffer;
		Action<NetBuffer> onReceived;
		Func<NetBuffer> getDataToWrite;
		private bool receivedDataLastUpdate = false;

		public Client() {
			client = new TcpClient();
		}

		public async Task ConnectAsync(IPEndPoint ipEndPoint) {
			await client.ConnectAsync(ipEndPoint.Address, ipEndPoint.Port);
			stream = client.GetStream();
			networkInputBuffer = new NetBuffer(new byte[1024], 0);
			readTask = stream.ReadAsync(networkInputBuffer.Buffer);
		}

		public async Task Work(IPEndPoint ipEndPoint, Action<NetBuffer> onReceived, Func<NetBuffer> getDataToWrite) {
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
			NetBuffer data = getDataToWrite();
			if (data != null) {
				writeTask = stream.WriteAsync(data.Buffer, 0, data.Count);
				stream.Flush();
			}
		}

		private bool KeepReading() {
			if (stream.CanRead && readTask.IsCompleted) {
				int received = readTask.Result;
				if (received <= 0) { return false; }
				networkInputBuffer.Count = received;
				onReceived.Invoke(networkInputBuffer);
				receivedDataLastUpdate = true;
				readTask = stream.ReadAsync(networkInputBuffer.Buffer);
			} else if (receivedDataLastUpdate) {
				receivedDataLastUpdate = false;
				networkInputBuffer.Count = 0;
				onReceived.Invoke(networkInputBuffer);
			}
			return true;
		}
	}
}
