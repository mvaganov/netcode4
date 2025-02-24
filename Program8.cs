using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ChatProgram {
  internal class Program {

    public static void Main(string[] args) {
      IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8080);
      Console.WriteLine("Have endpoint: " + endpoint);
      string httpTimestamp = DateTime.UtcNow.ToString("ddd, dd MMM yyyy HH:mm:ss ") + "GMT";
      Console.WriteLine(httpTimestamp);

      char input = (char)Console.Read();
      if (input == 's') {
        TcpListener listener = new TcpListener(endpoint);
        Console.WriteLine("Listener: " + listener);
        listener.Start();
        Console.WriteLine("listener started");
        Task<TcpClient> clientTask = listener.AcceptTcpClientAsync();
        Console.WriteLine("client connected: " + clientTask.Result.Client.RemoteEndPoint);
        NetworkStream clientStream = clientTask.Result.GetStream();
        byte[] inputBuffer = new byte[1024];
        bool pretendTobeHttpServer = true;
        do {
          if (clientStream.DataAvailable) {
            int receivedBytes = clientStream.Read(inputBuffer, 0, inputBuffer.Length);
            Console.WriteLine("Received: " + Encoding.ASCII.GetString(inputBuffer, 0, receivedBytes));
            if (pretendTobeHttpServer) {
              string content = "HELLO <b>WORLD</b>!";
              string response200 = $"HTTP/1.1 200 OK\r\nDate: {httpTimestamp}\r\nServer: Apache/2.4.4 (Win32) OpenSSL/0.9.8y PHP/5.4.16\r\nLast-Modified: Sat, 30 Mar 2013 11:28:59 GMT\r\nETag: \"ca-4d922b19fd4c0\"\r\nAccept-Ranges: bytes\r\nContent-Length: {content.Length}\r\nKeep-Alive: timeout=5, max=100\r\nConnection: Keep-Alive\r\nContent-Type: text/html\r\n\r\n" +
              content;
              Byte[] ecode200 = Encoding.ASCII.GetBytes(response200);
              clientStream.Write(ecode200, 0, ecode200.Length);
              clientStream.Flush();
            }
          }
          if (Console.KeyAvailable) {
            input = Console.ReadKey(true).KeyChar;
            byte[] sendLetter = { (byte)input };
            clientStream.Write(sendLetter, 0, 1);
            clientStream.Flush();
          }
        } while (input != 'q');
      } else {
        TcpClient tcpClient = new TcpClient();
        Console.WriteLine("Connecting");
        tcpClient.Connect(endpoint);
        Console.WriteLine("Connected");
        NetworkStream networkStream = tcpClient.GetStream();
        Byte[] bytes = Encoding.ASCII.GetBytes("Hi");
        networkStream.Write(bytes, 0, bytes.Length);
        networkStream.Flush();
        Console.WriteLine("Receiving message");
        byte[] inputBuffer = new byte[1024];
        do {
          if (networkStream.DataAvailable) {
            int receivedBytes = networkStream.Read(inputBuffer, 0, inputBuffer.Length);
            Console.WriteLine("Received: " + Encoding.ASCII.GetString(inputBuffer, 0, receivedBytes));
          }
          if (Console.KeyAvailable) {
            input = Console.ReadKey(true).KeyChar;
            byte[] sendLetter = { (byte)input };
            networkStream.Write(sendLetter, 0, 1);
            networkStream.Flush();
          }
        } while (input != 'q');
      }



    }
  }
}