using System;
using System.Collections.Concurrent;
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
        private SemaphoreSlim _startHosting;
        private readonly ushort _port;
        private ConcurrentDictionary<byte, IPEndPoint> _mapPlayerEndpoints;

        private int _gameStartedUnderlying;
        private bool _gameStarted
        {
            get { return (Interlocked.CompareExchange(ref _gameStartedUnderlying, 1, 1) == 1); }
            set
            {
                if (value) Interlocked.CompareExchange(ref _gameStartedUnderlying, 1, 0);
                else Interlocked.CompareExchange(ref _gameStartedUnderlying, 0, 1);
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
                _startHosting = new SemaphoreSlim(0, 1);
                _mapPlayerEndpoints = new ConcurrentDictionary<byte, IPEndPoint>();
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

        public void Stop()
        {
            if (_serverTask != null)
            {
                _serverTaskCancellationToken.Cancel();
                _serverTask.Wait();
            }
        }

        public async Task StopAsync()
        {
            if (_serverTask != null)
            {
                _serverTaskCancellationToken.Cancel();
                await _serverTask;
            }
        }

        public async Task StartGame()
        {
            if (!IsServerTaskStopped())
            {
                _gameStarted = true;
                _startHosting.Release();
                await StopAsync();
            }
        }

        public bool AddUdpClientInformation(byte playerNumber, IPEndPoint ipEndpoint)
        {
            if (_mapPlayerEndpoints.ContainsKey(playerNumber)) return false;
            _mapPlayerEndpoints.TryAdd(playerNumber, new IPEndPoint(ipEndpoint.Address, ipEndpoint.Port));
            return true;
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

                await _startHosting.WaitAsync(_serverTaskCancellationToken.Token);

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
                byte playerNumber = 1;
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

                    var messageType = Convert.ToByte(reader.ReadLine());
                    if (messageType != SpideyTcpMessage.SPIDEY_SENSE)
                    {
                        break;
                    }
                    SpideyTcpMessage.ParseSpideySenseMessage(reader, out string playerName);

                    ++playerNumber;
                    player.Client = client;
                    player.Reader = reader;
                    player.Writer = writer;
                    player.PlayerInformation = new ConnectedPlayerInformation(playerNumber, playerName);

                    onConnected.Report(player.PlayerInformation);

                    SpideyTcpMessage.SendTinglingMessage(writer, myName, playerNumber);

                    foreach (var tcpClient in tcpClients)
                    {
                        SpideyTcpMessage.SendPlayerInfoMessage(writer, tcpClient.PlayerInformation.Number, tcpClient.PlayerInformation.Data);
                        SpideyTcpMessage.SendPlayerInfoMessage(tcpClient.Writer, player.PlayerInformation.Number, player.PlayerInformation.Data);
                    }

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
                if (_gameStarted)
                {
                    MemoryScanner.PrepSpideyData(tcpClients.Count);
                    foreach (var tcpClient in tcpClients)
                    {
                        foreach (var playerEndpoint in _mapPlayerEndpoints)
                        {
                            if (playerEndpoint.Key == tcpClient.PlayerInformation.Number) continue;
                            SpideyTcpMessage.SendUdpInfoMessage(tcpClient.Writer, playerEndpoint.Key, playerEndpoint.Value);
                        }
                        SpideyTcpMessage.SendStartMessage(tcpClient.Writer);
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
