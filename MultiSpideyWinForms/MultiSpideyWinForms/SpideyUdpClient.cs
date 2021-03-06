﻿using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace MultiSpideyWinForms
{
    public class SpideyUdpClient : SpideyUdpBase
    {
        private const int GAME_STARTED_TIMEOUT_MS = 2000;

        private readonly IPAddress _serverIp;

        private SemaphoreSlim _tcpConnected;
        private SemaphoreSlim _gameStarted;
        private byte _myPlayerNumber;
        private int _playerCount;

        public SpideyUdpClient(IPAddress serverIp, ushort port) : base(port)
        {
            _serverIp = serverIp;
        }

        public void Start(IProgress<bool> onConnected, IProgress<ConnectedPlayerInformation> onLocationUpdate)
        {
            if (IsServerTaskStopped())
            {
                _tcpConnected = new SemaphoreSlim(0, 1);
                _gameStarted = new SemaphoreSlim(0, 1);
                var lTask = new Task<Task>(async () => await StartListening(onConnected, onLocationUpdate));
                _udpTask = lTask.Unwrap();
                //_serverTask.ObserveExceptions();
                _udpTaskCancellationToken = new CancellationTokenSource();
                lTask.Start();
            }
        }

        public void UpdatePlayerNumberAndStartClient(byte playerNumber)
        {
            _myPlayerNumber = playerNumber;
            _tcpConnected.Release();
        }

        public async Task StartListening(IProgress<bool> onConnected, IProgress<ConnectedPlayerInformation> onLocationUpdate)
        {
            await _tcpConnected.WaitAsync();

            var myPlayerNumber = _myPlayerNumber;
            var udpClient = new UdpClient();
            var serverEndPoint = new IPEndPoint(_serverIp, _port);
            try
            {
                var gameStarted = false;
                while (!gameStarted && !_udpTaskCancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Do not call connect otherwise hole punching will not work
                        //udpClient.Connect(_serverIp, _port);
                        onConnected.Report(true);

                        var message = SpideyUdpMessage.CreateSpinAWebMessage(myPlayerNumber);
                        while (!gameStarted)
                        {
                            udpClient.Send(message, message.Length, serverEndPoint);
                            gameStarted = await _gameStarted.WaitAsync(GAME_STARTED_TIMEOUT_MS);
                        }
                    }
                    catch (SocketException)
                    {
                        onConnected.Report(false);
                        await Task.Delay(100);
                    }
                }

                while (!_udpTaskCancellationToken.IsCancellationRequested)
                {
                    var result = await udpClient.ReceiveAsync().WithWaitCancellation(_udpTaskCancellationToken.Token);
                    ParseMessage(result, onLocationUpdate);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (udpClient.Client.Connected) udpClient.Close();
            }
        }

        private void ParseMessage(UdpReceiveResult result, IProgress<ConnectedPlayerInformation> onLocationUpdate)
        {
            if (result.Buffer.Length < SpideyUdpMessage.MESSAGE_HEADER_SIZE)
                return;

            var message = result.Buffer;
            var messageType = message[0];
            byte playerNumber;
            switch (messageType)
            {
                case SpideyUdpMessage.SPIN_A_WEB:
                    // Not supported
                    break;
                case SpideyUdpMessage.SPIDERMAN:
                    if (!SpideyUdpMessage.ParseSpidermanMessage(message, out playerNumber, out byte[] spideyData, out byte levelData))
                        break;
                    var spideyLevel = SpideyLevels.GetSpideyLevel(levelData);
                    onLocationUpdate.Report(new ConnectedPlayerInformation(playerNumber, spideyLevel.Name.TrimEnd()));
                    int playerOffset;
                    if (playerNumber < _myPlayerNumber)
                    {
                        playerOffset = playerNumber - 1;
                    }
                    else
                    {
                        playerOffset = playerNumber - 2;
                    }
                    MemoryScanner.WriteSpideyData(spideyData, spideyLevel, playerOffset, _playerCount);
                    break;
                default:
                    break;
            }
        }

        public void StartGame(int playerCount)
        {
            _playerCount = playerCount;
            MemoryScanner.PrepSpideyData(_playerCount);
            _gameStarted.Release();
        }
    }
}
