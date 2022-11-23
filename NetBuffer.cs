using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace networking {
	public class NetBuffer {
		/// <summary>
		/// byte buffer for receiving and sending data
		/// </summary>
		private byte[] _buffer;
		/// <summary>
		/// how much the buffer is filled
		/// </summary>
		private int _filled;
		
		public byte[] Buffer => _buffer;
		public int Count => _filled;
		public byte this[int i] { get { return _buffer[i]; } set { _buffer[i] = value; } }
		
		public NetBuffer() { _buffer = new byte[0]; _filled = 0; }
		
		public NetBuffer(byte[] buffer, int filled) { _buffer = buffer; _filled = filled; }
		
		public static NetBuffer Utf8(string value) {
			byte[] bytes = Encoding.UTF8.GetBytes(value);
			return new NetBuffer(bytes, bytes.Length);
		}
		
		public void AddUtf8(List<char> value) => Add(Encoding.UTF8.GetBytes(value.ToArray()));
		
		public void AddUtf8(string value) => Add(Encoding.UTF8.GetBytes(value));
		
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
				if (Program.IsClearedDeadStream(clients[i], i, clients)) { continue; }
				await WriteFlush(clients[i]);
			}
			Clear();
		}

		public async Task WriteFlush(TcpClient c) {
			NetworkStream ns = c.GetStream();
			await ns.WriteAsync(Buffer, 0, Count);
			ns.Flush();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="client"></param>
		/// <param name="totalBuffer"></param>
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
}
