using System;
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

        public void Start(IProgress<int> onReceivePlayerNumber, IProgress<ConnectedPlayerInformation> onConnected, IProgress<bool> onServerStarted, string myName)
        {
            if (IsClientTaskStopped())
            {
                var lTask = new Task<Task>(async () => await ConnectToServer(onReceivePlayerNumber, onConnected, onServerStarted, myName));
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

        public async Task Stop()
        {
            if (_clientTask != null)
            {
                _clientTaskCancellationToken.Cancel();
                await _clientTask;
            }
        }

        private async Task ConnectToServer(IProgress<int> onReceivePlayerNumber, IProgress<ConnectedPlayerInformation> onConnected, IProgress<bool> onServerStarted, string myName)
        {
            var tcpClient = new TcpClient();
            var serverStarted = false;
            try
            {
                await tcpClient.ConnectAsync(_ipAddress, _port);
                using (var reader = new StreamReader(tcpClient.GetStream()))
                {
                    using (var writer = new StreamWriter(tcpClient.GetStream()))
                    {
                        writer.WriteLine(myName);
                        writer.Flush();
                        var hostPlayerNumber = int.Parse(reader.ReadLine());
                        var hostPlayerName = reader.ReadLine();
                        var myPlayerNumber = int.Parse(reader.ReadLine());

                        onReceivePlayerNumber.Report(myPlayerNumber);
                        onConnected.Report(new ConnectedPlayerInformation(myPlayerNumber, myName));
                        onConnected.Report(new ConnectedPlayerInformation(hostPlayerNumber, hostPlayerName));

                        while (!serverStarted && !_clientTaskCancellationToken.IsCancellationRequested)
                        {
                            var nextInstruction = await reader.ReadLineAsync();
                            if (nextInstruction == null)
                            {
                                // Disconnection
                                break;
                            }
                            if (nextInstruction == MultiSpidey.START_GAME)
                            {
                                serverStarted = true;
                            }
                            else if (int.TryParse(nextInstruction, out int playerNumber))
                            {
                                var playerName = reader.ReadLine();
                                onConnected.Report(new ConnectedPlayerInformation(playerNumber, playerName));
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
