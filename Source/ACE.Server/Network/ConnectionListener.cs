using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using ACE.Server.Managers;

using log4net;

namespace ACE.Server.Network
{
    public class ConnectionListener
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly ILog packetLog = LogManager.GetLogger(System.Reflection.Assembly.GetEntryAssembly(), "Packets");

        public Socket Socket { get; private set; }

        private ManualResetEvent ReceiveComplete = new ManualResetEvent(false);

        private bool Listening = true;

        private IPEndPoint listenerEndpoint;

        private readonly uint listeningPort;

        private readonly byte[] buffer = new byte[ClientPacket.MaxPacketSize];

        private readonly IPAddress listeningHost;

        public ConnectionListener(IPAddress host, uint port)
        {
            log.DebugFormat("ConnectionListener ctor, host {0} port {1}", host, port);
            listeningHost = host;
            listeningPort = port;
            try
            {
                log.DebugFormat("Binding ConnectionListener, host {0} port {1}", listeningHost, listeningPort);
                listenerEndpoint = new IPEndPoint(listeningHost, (int)listeningPort);
                Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                Socket.Bind(listenerEndpoint);
            }
            catch (Exception exception)
            {
                log.FatalFormat("Network Socket has thrown: {0}", exception.Message);
            }
        }

        public void Start()
        {
            try
            {
                Listen();
            }
            catch (Exception exception)
            {
                log.FatalFormat("Network Socket has thrown: {0}", exception.Message);
            }
        }

        public void Shutdown()
        {
            Listening = false;
            log.DebugFormat("Shutting down ConnectionListener, host {0} port {1}", listeningHost, listeningPort);
            if (Socket != null && Socket.IsBound)
                Socket.Close();
        }

        private void Listen()
        {
            while (Listening)
            {
                try
                {
                    ReceiveComplete.Reset();
                    EndPoint clientEndPoint = new IPEndPoint(listeningHost, 0);
                    Socket.BeginReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref clientEndPoint, OnDataReceieve, Socket);
                    ReceiveComplete.WaitOne();
                }
                catch (SocketException error)
                {
                    if ((error.SocketErrorCode == SocketError.ConnectionAborted) ||
                        (error.SocketErrorCode == SocketError.ConnectionRefused) ||
                        (error.SocketErrorCode == SocketError.ConnectionReset) ||
                        (error.SocketErrorCode == SocketError.OperationAborted))
                    {
                        continue;
                    }

                    log.FatalFormat("Network Socket has thrown: {0} {1}", error.ErrorCode, error.Message);
                    break;
                }
                catch (Exception exception)
                {
                    log.FatalFormat("Network Socket has thrown: {0}", exception.Message);
                    break;
                }
            }
        }

        private void OnDataReceieve(IAsyncResult result)
        {
            EndPoint clientEndPoint = null;
            try
            {
                clientEndPoint = new IPEndPoint(listeningHost, 0);
                int dataSize = Socket.EndReceiveFrom(result, ref clientEndPoint);

                byte[] data = new byte[dataSize];
                Buffer.BlockCopy(buffer, 0, data, 0, dataSize);

                IPEndPoint ipEndpoint = (IPEndPoint)clientEndPoint;

                // TO-DO: generate ban entries here based on packet rates of endPoint, IP Address, and IP Address Range

                if (packetLog.IsDebugEnabled)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine($"Received Packet (Len: {data.Length}) [{ipEndpoint.Address}:{ipEndpoint.Port}=>{listenerEndpoint.Address}:{listenerEndpoint.Port}]");
                    sb.AppendLine(data.BuildPacketString());
                    packetLog.Debug(sb.ToString());
                }

                var packet = new ClientPacket(data);
                if (packet.IsValid)
                    WorldManager.ProcessPacket(packet, ipEndpoint, listenerEndpoint);
            }
            catch (SocketException socketException)
            {
                // If we get "Connection has been forcibly closed..." error, just eat the exception and continue on
                // This gets sent when the remote host terminates the connection (on UDP? interesting...)
                // TODO: There might be more, should keep an eye out. Logged message will help here.
                if (socketException.ErrorCode == 0x2746)
                {
                    log.DebugFormat("Network Socket on IP {2} has thrown {0}: {1}", socketException.ErrorCode, socketException.Message, clientEndPoint != null ? clientEndPoint.ToString() : "Unknown");
                }
                else
                {
                    log.FatalFormat("Network Socket on IP {2} has thrown {0}: {1}", socketException.ErrorCode, socketException.Message, clientEndPoint != null ? clientEndPoint.ToString() : "Unknown");
                    return;
                }
            }
            ReceiveComplete.Set();
        }
    }
}
