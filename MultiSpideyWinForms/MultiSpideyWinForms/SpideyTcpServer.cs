using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace MultiSpideyWinForms
{
    public class SpideyTcpServer
    {
        private Task _serverTask;
        private CancellationTokenSource _serverTaskCancellationToken;
        private SemaphoreSlim startHosting = new SemaphoreSlim(0, 1);
        private readonly ushort _port;

        private int _gameStarted;
        public bool GameStarted
        {
            get { return (Interlocked.CompareExchange(ref _gameStarted, 1, 1) == 1); }
            set
            {
                if (value) Interlocked.CompareExchange(ref _gameStarted, 1, 0);
                else Interlocked.CompareExchange(ref _gameStarted, 0, 1);
            }
        }

        public SpideyTcpServer(ushort port)
        {
            _port = port;
        }

        public void Start(IProgress<ConnectedPlayerInformation> onConnected, string myName)
        {
            if (IsServerTaskStopped())
            {
                var lTask = new Task<Task>(async () => await ListenForConnections(onConnected, myName));
                _serverTask = lTask.Unwrap();
                //_serverTask.ObserveExceptions();
                _serverTaskCancellationToken = new CancellationTokenSource();
                lTask.Start();
            }
        }

        protected bool IsServerTaskStopped()
        {
            return _serverTask == null || _serverTask.IsCompleted;
        }

        public async Task Stop()
        {
            if (_serverTask != null)
            {
                _serverTaskCancellationToken.Cancel();
                await _serverTask;
            }
        }

        private async Task ListenForConnections(IProgress<ConnectedPlayerInformation> onConnected, string myName)
        {
            var listenCancelToken = new CancellationTokenSource();
            Task connectionTask = null;
            try
            {
                var lTask = new Task<Task>(async () => await HandleNewConnections(onConnected, myName, listenCancelToken));
                connectionTask = lTask.Unwrap();
                //connectionTask.ObserveExceptions();
                lTask.Start();

                await startHosting.WaitAsync(_serverTaskCancellationToken.Token);

                listenCancelToken.Cancel();
                await connectionTask;
            }
            catch (OperationCanceledException)
            {
                listenCancelToken.Cancel();
                if (connectionTask != null) await connectionTask;
            }
            catch (Exception ex)
            {
                // TODO - log it
                throw ex;
            }
        }


        private async Task HandleNewConnections(IProgress<ConnectedPlayerInformation> onConnected, string myName, CancellationTokenSource listenCancelToken)
        {
            var tcpServer = new TcpListener(IPAddress.Any, _port);
            var tcpClients = new List<TcpConnectedPlayer>();

            try
            {
                tcpServer.Start();
                var playerNumber = 1;
                while (!listenCancelToken.IsCancellationRequested)
                {
                    var client = await tcpServer.AcceptTcpClientAsync().WithWaitCancellation(listenCancelToken.Token);
                    if (listenCancelToken.IsCancellationRequested)
                        break;
                    if (client == null)
                        continue;

                    var player = new TcpConnectedPlayer();

                    var reader = new StreamReader(client.GetStream());
                    var writer = new StreamWriter(client.GetStream());

                    ++playerNumber;
                    var playerName = reader.ReadLine();

                    player.Client = client;
                    player.Reader = reader;
                    player.Writer = writer;
                    player.PlayerInformation = new ConnectedPlayerInformation(playerNumber, playerName);

                    onConnected.Report(player.PlayerInformation);

                    writer.WriteLine(1);
                    writer.WriteLine(myName);
                    writer.WriteLine(playerNumber);

                    foreach (var tcpClient in tcpClients)
                    {
                        writer.WriteLine(tcpClient.PlayerInformation.Number);
                        writer.WriteLine(tcpClient.PlayerInformation.Name);
                        tcpClient.Writer.WriteLine(player.PlayerInformation.Number);
                        tcpClient.Writer.WriteLine(player.PlayerInformation.Name);
                        tcpClient.Writer.Flush();
                    }
                    writer.Flush();

                    tcpClients.Add(player);
                }
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
                if (GameStarted)
                {
                    foreach (var tcpClient in tcpClients)
                    {
                        tcpClient.Writer.WriteLine(MultiSpidey.START_GAME);
                        tcpClient.Writer.Flush();
                    }
                }
                foreach (var tcpClient in tcpClients)
                {
                    tcpClient.Reader.Close();
                    tcpClient.Writer.Close();
                    tcpClient.Client.Close();
                }
                tcpServer.Stop();
            }
        }
    }
}
