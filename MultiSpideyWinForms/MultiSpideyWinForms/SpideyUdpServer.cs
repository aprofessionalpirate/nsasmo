using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace MultiSpideyWinForms
{
    public class SpideyUdpServer : SpideyUdpBase
    {
        public SpideyUdpServer(ushort port) : base(port)
        {
        }

        public void Start(SpideyTcpServer tcpServer, IProgress<ConnectedPlayerInformation> onLocationUpdate, IProgress<ConnectedPlayerUdpEndPoint> onReceiveUdpInfo)
        {
            if (IsServerTaskStopped())
            {
                var lTask = new Task<Task>(async () => await StartListening(tcpServer, onLocationUpdate, onReceiveUdpInfo));
                _udpTask = lTask.Unwrap();
                //_serverTask.ObserveExceptions();
                _udpTaskCancellationToken = new CancellationTokenSource();
                lTask.Start();
            }
        }

        private async Task StartListening(SpideyTcpServer tcpServer, IProgress<ConnectedPlayerInformation> onLocationUpdate, IProgress<ConnectedPlayerUdpEndPoint> onReceiveUdpInfo)
        {
            var udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, _port));
            try
            {
                //var playerInfo = new List<byte[]>(numberOfPlayers);

                while (!_udpTaskCancellationToken.IsCancellationRequested)
                {
                    var result = await udpClient.ReceiveAsync().WithWaitCancellation(_udpTaskCancellationToken.Token);
                    ParseMessage(result, tcpServer, onLocationUpdate, onReceiveUdpInfo);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                udpClient.Close();
            }
        }

        private void ParseMessage(UdpReceiveResult result, SpideyTcpServer tcpServer, IProgress<ConnectedPlayerInformation> onLocationUpdate, IProgress<ConnectedPlayerUdpEndPoint> onReceiveUdpInfo)
        {
            if (result.Buffer.Length < SpideyUdpMessage.MESSAGE_HEADER_SIZE)
                return;

            var message = result.Buffer;
            var messageType = message[0];
            byte playerNumber;
            switch (messageType)
            {
                case SpideyUdpMessage.SPIN_A_WEB:
                    if (!SpideyUdpMessage.ParseSpinAWebMessage(message, out playerNumber))
                        break;
                    
                    var added = tcpServer.AddUdpClientInformation(playerNumber, result.RemoteEndPoint);
                    if (added) onReceiveUdpInfo.Report(new ConnectedPlayerUdpEndPoint(playerNumber, new IPEndPoint(result.RemoteEndPoint.Address, result.RemoteEndPoint.Port)));
                    break;
                case SpideyUdpMessage.SPIDERMAN:
                    if (!SpideyUdpMessage.ParseSpidermanMessage(message, out playerNumber, out byte[] spideyData, out byte[] locationData))
                        break;
                    onLocationUpdate.Report(new ConnectedPlayerInformation(playerNumber, SpideyUdpMessage.AsciiEncoding.GetString(locationData).TrimEnd()));
                    //SetPlayerPosition(clientPlayerNumber, spideyData, location);
                    
                    break;
                default:
                    break;
            }
        }
    }
}
