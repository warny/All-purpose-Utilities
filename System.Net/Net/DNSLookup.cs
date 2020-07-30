using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Utils.Net.DNS;

namespace Utils.Net
{
	public class DNSLookup
	{
        public DNSLookup() {
            NameServers = new NetworkParameters().DnsServers;
        }

        public DNSLookup(params IPAddress[] nameServers) {
            NameServers = nameServers;
        }

        public IPAddress[] NameServers { get; set; }

		public DNSHeader Request(string type, string name, DNSClass @class = DNSClass.ALL)
		{
			DNSHeader request = new DNSHeader();
			request.Requests.Add(new DNSRequestRecord(type, name, @class));
            var requestDatagram = request.ToByteArray();
            foreach (var NameServer in NameServers)
            {
                try
                {
                    var responseDatagram = UdpTransport(NameServer, 53, requestDatagram);
                    return new DNSHeader(responseDatagram);
                }
                catch
                {
                }
            }
            throw new Exception("Impossible d'executer la requête");
		}


        #region procédures de transport
        /// <summary>
        /// Produit une requête udp et attend un retour
        /// </summary>
        /// <param name="Server">Adresse de la machine distante</param>
        /// <param name="Port">Port à requêter</param>
        /// <param name="packet">Paquet à envoyer</param>
        /// <returns>Résultat de la réponse</returns>
        private static byte[] UdpTransport(IPAddress Server, int Port, byte[] packet)
        {
            // on dimensionne une réponse de 512 octets = taille udp maximum
            Byte[] byteRes = new Byte[512];
            int intByteRec = 0;

            Socket sokUDP = new Socket(Server.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            sokUDP.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 5000);

            EndPoint RemoteHost = new IPEndPoint(Server, Port);

            sokUDP.SendTo(packet, packet.Length, SocketFlags.None, RemoteHost);

            intByteRec = sokUDP.ReceiveFrom(byteRes, byteRes.Length, SocketFlags.None, ref RemoteHost);
            try { sokUDP.Close(); } catch {; }
            byte[] ret = new byte[intByteRec];
            Array.Copy(byteRes, 0, ret, 0, intByteRec);
            return ret;
        }
        #endregion

    }
}
