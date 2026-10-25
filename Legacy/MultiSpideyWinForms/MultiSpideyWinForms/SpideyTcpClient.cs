using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace MultiSpideyWinForms
{
    public class SpideyTcpClient
    {
        private Task _clientTask;
        private CancellationTokenSource _clientTaskCancellationToken;

        private readonly IPAddress _ipAddress;
        private readonly ushort _port;

        public SpideyTcpClient(IPAddress ipAddress, ushort port)
        {
            _ipAddress = ipAddress;
            _port = port;
        }

        public void Start(SpideyUdpClient udpClient, IProgress<byte> onReceivePlayerNumber, IProgress<ConnectedPlayerInformation> onConnected, IProgress<bool> onServerStarted, IProgress<ConnectedPlayerUdpEndPoint> onReceiveUdpInfo, string myName)
        {
            if (IsClientTaskStopped())
            {
                var lTask = new Task<Task>(async () => await ConnectToServer(udpClient, onReceivePlayerNumber, onConnected, onServerStarted, onReceiveUdpInfo, myName));
                _clientTask = lTask.Unwrap();
                //_serverTask.ObserveExceptions();
                _clientTaskCancellationToken = new CancellationTokenSource();
                lTask.Start();
            }
        }

        protected bool IsClientTaskStopped()
        {
            return _clientTask == null || _clientTask.IsCompleted;
        }

        public void Stop()
        {
            if (_clientTask != null)
            {
                _clientTaskCancellationToken.Cancel();
                _clientTask.Wait();
            }
        }

        private async Task ConnectToServer(SpideyUdpClient udpClient, IProgress<byte> onReceivePlayerNumber, IProgress<ConnectedPlayerInformation> onConnected, IProgress<bool> onServerStarted, IProgress<ConnectedPlayerUdpEndPoint> onReceiveUdpInfo, string myName)
        {
            var tcpClient = new TcpClient();
            var serverStarted = false;
            var mapPlayerEndpoints = new Dictionary<byte, IPEndPoint>();
            try
            {
                await tcpClient.ConnectAsync(_ipAddress, _port);
                using (var reader = new StreamReader(tcpClient.GetStream()))
                {
                    using (var writer = new StreamWriter(tcpClient.GetStream()))
                    {
                        SpideyTcpMessage.SendSpideySenseMessage(writer, myName);

                        var messageType = Convert.ToByte(reader.ReadLine());
                        if (messageType != SpideyTcpMessage.TINGLING)
                        {
                            return;
                        }
                        SpideyTcpMessage.ParseTinglingMessage(reader, out string hostPlayerName, out byte myPlayerNumber);

                        udpClient.UpdatePlayerNumberAndStartClient(myPlayerNumber);
                        onReceivePlayerNumber.Report(myPlayerNumber);
                        onConnected.Report(new ConnectedPlayerInformation(myPlayerNumber, myName));
                        onConnected.Report(new ConnectedPlayerInformation(1, hostPlayerName));

                        while (!serverStarted && !_clientTaskCancellationToken.IsCancellationRequested)
                        {
                            var nextInstruction = await reader.ReadLineAsync().WithWaitCancellation(_clientTaskCancellationToken.Token);
                            if (nextInstruction == null)
                            {
                                // Disconnection or cancellation?
                                break;
                            }
                            messageType = Convert.ToByte(nextInstruction);
                            switch (messageType)
                            {
                                case SpideyTcpMessage.PLAYER_INFO:
                                    SpideyTcpMessage.ParsePlayerInfoMessage(reader, out byte otherPlayerNumber, out string otherPlayerName);
                                    onConnected.Report(new ConnectedPlayerInformation(otherPlayerNumber, otherPlayerName));
                                    break;
                                case SpideyTcpMessage.UDP_INFO:
                                    SpideyTcpMessage.ParseUdpInfoMessage(reader, out byte otherUdpPlayerNumber, out IPEndPoint otherPlayerUdpEndpoint);
                                    mapPlayerEndpoints.Add(otherUdpPlayerNumber, otherPlayerUdpEndpoint);
                                    onReceiveUdpInfo.Report(new ConnectedPlayerUdpEndPoint(otherUdpPlayerNumber, new IPEndPoint(otherPlayerUdpEndpoint.Address, otherPlayerUdpEndpoint.Port)));
                                    break;
                                case SpideyTcpMessage.START:
                                    serverStarted = true;
                                    break;
                            }
                        }
                    }
                }
            }
            catch (SocketException)
            {
                // TODO - clear clients
                serverStarted = false;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                // TODO - log it
                throw ex;
            }
            finally
            {
                tcpClient.Close();
                if (serverStarted)
                {
                    onServerStarted.Report(true);
                }
            }
        }
    }
}
