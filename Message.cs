using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace networking {
	public struct Message {
		public NetBuffer message;
		public long sender;
		public HashSet<long> targets;
		public Purpose whatToDo;

		public enum Purpose {
			Nothing, SendToAll, SendToAllExcept, SendToSpecificClients, EchoToSender
		}

		public async Task Send(Server server) {
			ClientHandler c;
			switch (whatToDo) {
				case Purpose.EchoToSender:
					if (!server.clients.TryGetValue(sender, out c) || server.IsDeadCheck(c)) {
						Console.WriteLine("If this is happening, a client sent a message without being connected to the server...");
						break;
					}
					await message.WriteFlushStream(server.clients[sender].tcp);
					break;
				case Purpose.SendToAll:
					server.BroadcastBuffer.Add(message);
					break;
				case Purpose.SendToSpecificClients:
					foreach (long id in targets) {
						if (!server.clients.TryGetValue(id, out c) || server.IsDeadCheck(c)) {
							if (!server.clients.TryGetValue(sender, out c) || server.IsDeadCheck(c)) {
								Console.WriteLine("If this is happening, a client sent a message without being connected to the server...");
								break;
							}
							await NetBuffer.FromString($"ERROR: client {id} failed").WriteFlushStream(server.clients[sender].tcp);
							break;
						}
						await message.WriteFlushStream(c.tcp);
					}
					break;
				case Purpose.SendToAllExcept:
					foreach (KeyValuePair<long, ClientHandler> kvp in server.clients) {
						c = kvp.Value;
						if (targets.Contains(c.id)) { continue; }
						await message.WriteFlushStream(c.tcp);
					}
					break;
			}
		}
	}
}
