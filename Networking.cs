using System.Net;
using System.Threading.Tasks;

namespace networking {
	public static class Networking {
		public const string localhost = "localhost";

		public static async Task<IPEndPoint> GetEndPoint(string host, int port) {
			IPAddress ipAddress = await GetIpAddress(host);
			IPEndPoint ipEndPoint = new IPEndPoint(ipAddress, port);
			return ipEndPoint;
		}

		public static async Task<IPAddress> GetIpAddress(string host) {
			IPHostEntry ipHostInfo = await Dns.GetHostEntryAsync(host);
			return ipHostInfo.AddressList[0];
		}


	}
}
