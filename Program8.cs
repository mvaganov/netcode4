using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ChatProgram {
  internal class Program {

    public static string GetHttpTimeStampString(DateTime dateTime) {
      const string httpHeaderTimestampFormatUTC = "ddd, dd MMM yyyy HH:mm:ss";
      return dateTime.ToString(httpHeaderTimestampFormatUTC) + " GMT";
    }

    public static void Main_(string[] args) {
      IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8080);
      Console.WriteLine("Have endpoint: " + endpoint);
      string timestampServerStart = GetHttpTimeStampString(DateTime.UtcNow);
      Console.WriteLine(timestampServerStart);

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
              string serverPlatform = "Apache/2.4.4 (Win32) OpenSSL/0.9.8y PHP/5.4.16";
              string timestampNow = GetHttpTimeStampString(DateTime.UtcNow);
              string content = "HELLO <b>WORLD</b>!";
              string[] httpHeader = {
                "HTTP/1.1 200 OK",
                $"Date: {timestampNow}",
                $"Server: {serverPlatform}",
                $"Last-Modified: {timestampServerStart}",
                $"ETag: \"{timestampNow.GetHashCode().ToString("x")}\"",
                "Accept-Ranges: bytes",
                $"Content-Length: {content.Length}",
                "Keep-Alive: timeout=5, max=100",
                "Connection: Keep-Alive",
                "Content-Type: text/html",
              };
              string lineEnd = "\r\n";
              string response200 = string.Join(lineEnd, httpHeader) + lineEnd + lineEnd + content;
              Byte[] ecode200 = Encoding.ASCII.GetBytes(response200);
              clientStream.Write(ecode200, 0, ecode200.Length);
              clientStream.Flush();
              Console.WriteLine($"wrote: {response200}");
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