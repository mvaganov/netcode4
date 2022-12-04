using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
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
		private long bytesSent, bytesRead;

		public Client() {
			client = new TcpClient();
		}

		public async Task ConnectAsync(IPEndPoint ipEndPoint) {
			await client.ConnectAsync(ipEndPoint.Address, ipEndPoint.Port);
			stream = client.GetStream();
			networkInputBuffer = new NetBuffer(new byte[1024], 0);
			readTask = stream.ReadAsync(networkInputBuffer.Buffer);
		}

		// TODO onConnect, onDisconnect
		public async Task Work(IPEndPoint ipEndPoint, Action<TcpClient> onConnect, Action<NetBuffer> onReceived, Func<NetBuffer> getDataToWrite) {
			this.onReceived = onReceived;
			this.getDataToWrite = getDataToWrite;
			await ConnectAsync(ipEndPoint);
			onConnect?.Invoke(client);
			while (client.Connected) {
				KeepWriting();
				KeepReading();
			}
			Console.WriteLine("server connection is closed");
			if (bytesRead > 0) {
				Console.ReadKey();
			}
		}

		private void KeepWriting() {
			if (writeTask != null && !writeTask.IsCompleted) { return; }
			NetBuffer data = getDataToWrite();
			if (data != null) {
				writeTask = stream.WriteAsync(data.Buffer, 0, data.Count);
				bytesSent += data.Count;
				stream.Flush();
			}
		}

		private bool KeepReading() {
			if (stream.CanRead && readTask.IsCompleted) {
				int received = !readTask.IsFaulted ? readTask.Result : -1;
				if (received <= 0) { return false; }
				receivedDataLastUpdate = true;
				CallbackBufferReadPartial(received);
				readTask = stream.ReadAsync(networkInputBuffer.Buffer);
			} else if (receivedDataLastUpdate) {
				receivedDataLastUpdate = false;
				CallbackBufferReadProbablyFinished();
			}
			return true;
		}
	
		private void CallbackBufferReadPartial(int received) {
			networkInputBuffer.Count = received;
			bytesRead += received;
			onReceived.Invoke(networkInputBuffer);
		}
		
		private void CallbackBufferReadProbablyFinished() {
			networkInputBuffer.Count = 0;
			onReceived.Invoke(networkInputBuffer);
		}
	}
}
