using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Threading.Timer;

// TODO
// Properly reset on reset all
// Handle player disconnection
// Interpolation of player position

namespace MultiSpideyWinForms
{
    public enum LoadStatus
    {
        NotStarted,
        WaitingForDosbox,
        WaitingForPlayer,
        Ready
    }

    public partial class MultiSpidey : Form
    {
        public const string START_GAME = "STARTTHEGAMEALREADY";

        private readonly object infoLock = new object();
        private readonly object _timerLock = new object();

        private const int Port = 6015;

        private SpideyWindow _spideyWindow;
        private Timer _startupTimer = null;

        private SpideyTcpServer _tcpServer;
        private SpideyTcpClient _tcpClient;
        private ConnectedPlayerInformation _myInfo;

        private bool _isHost = false;



        private volatile bool _requestTimerStop = false;
        private Timer memoryTimer;
        private UdpClient udpClient;

        private SemaphoreSlim signalToStartHosting = new SemaphoreSlim(0, 1);

        private int playerNumber = 1;
        private int players = 1;
        private byte[] player1Info = new byte[30];
        private byte[] player2Info = new byte[30];
        private byte[] player3Info = new byte[30];
        private string player1Name = "Player 1";
        private string player2Name = "Player 2";
        private string player3Name = "Player 3";
        private string MyLocation = "";

        public MultiSpidey()
        {
            InitializeComponent();
        }

        private void MultiSpidey_Shown(object sender, EventArgs e)
        {
            StartStartupTimer();
        }

        private void btnRescan_Click(object sender, EventArgs e)
        {
            StartStartupTimer();
        }

        private void StartStartupTimer()
        {
            btnRescan.Enabled = false;
            SetLoadStatus(LoadStatus.WaitingForDosbox);
            lock (_timerLock)
            {
                _startupTimer = new Timer(AttachToDosbox);
                _startupTimer.Change(0, 1000);
            }
        }

        private void StopStartupTimer()
        {
            lock (_timerLock)
            {
                if (_startupTimer != null)
                {
                    try
                    {
                        _startupTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                    _startupTimer.Dispose();
                    _startupTimer = null;
                }
            }
        }

        private void AttachToDosbox(object state)
        {
            IEnumerable<IntPtr> handles;
            lock (_timerLock)
            {
                var timer = state as Timer;
                if (timer != _startupTimer || _startupTimer == null) return;

                handles = WindowManager.FindSpideyWindows();
                if (handles == null || handles.Count() == 0 || handles.All(h => h == IntPtr.Zero))
                {
                    return;
                }

                StopStartupTimer();
            }

            Invoke(new Action(() =>
            {
                var chosenHandle = IntPtr.Zero;
                foreach (var handle in handles)
                {
                    var result = MessageBox.Show("Connect to SPIDEY#" + handle.ToString() + "?", "DOSBox found", MessageBoxButtons.YesNo);
                    if (result == DialogResult.Yes)
                    {
                        chosenHandle = handle;
                        break;
                    }
                }

                if (chosenHandle == IntPtr.Zero)
                {
                    btnRescan.Enabled = true;
                    SetLoadStatus(LoadStatus.NotStarted);
                    return;
                }

                _spideyWindow = WindowManager.GetSpideyWindow(chosenHandle);
                SetLoadStatus(LoadStatus.WaitingForPlayer);

                lock (_timerLock)
                {
                    _startupTimer = new Timer(FindPlayerInMemory);
                    _startupTimer.Change(0, 1000);
                }
            }));
        }

        private void FindPlayerInMemory(object state)
        {
            lock (_timerLock)
            {
                var timer = state as Timer;
                if (timer != _startupTimer || _startupTimer == null) return;

                if (!MemoryScanner.GetMemoryAddresses(this, _spideyWindow.Handle))
                {
                    return;
                }

                StopStartupTimer();
            }

            Invoke(new Action(() =>
            {
                SetLoadStatus(LoadStatus.Ready);
                btnHost.Enabled = true;
                btnJoin.Enabled = true;
            }));
        }

        private void SetLoadStatus(LoadStatus loadStatus)
        {
            switch (loadStatus)
            {
                case LoadStatus.NotStarted:
                    statusStrip.BackColor = Color.Wheat;
                    lblLoadStatus.Text = "Not started";
                    break;
                case LoadStatus.WaitingForDosbox:
                    statusStrip.BackColor = Color.Red;
                    lblLoadStatus.Text = "Waiting for DOSBox to load...";
                    break;
                case LoadStatus.WaitingForPlayer:
                    statusStrip.BackColor = Color.Orange;
                    lblLoadStatus.Text = "DOSBox found, setting up memory...";
                    break;
                case LoadStatus.Ready:
                    statusStrip.BackColor = Color.LightGreen;
                    lblLoadStatus.Text = "Ready to host or join";
                    break;
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            StopStartupTimer();
            _requestTimerStop = true;
            _spideyWindow = null;

            base.OnHandleDestroyed(e);
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            _isHost = false;
            StopStartupTimer();
            _spideyWindow = null;
            StartStartupTimer();
        }

        private void btnHost_Click(object sender, EventArgs e)
        {
            if (!ValidNameEntered(out string name))
                return;
            if (!ValidPortEntered(out ushort port))
                return;

            _myInfo = new ConnectedPlayerInformation(1, name);
            OnTcpPlayerConnected(_myInfo);

            _isHost = true;
            btnHost.Enabled = false;
            btnJoin.Enabled = false;
            txtIP.Enabled = false;
            txtName.Enabled = false;

            _tcpServer = new SpideyTcpServer(port);
            var onConnected = new Progress<ConnectedPlayerInformation>(OnTcpPlayerConnected);

            _tcpServer.Start(onConnected, _myInfo.Name);
        }

        private void btnJoin_Click(object sender, EventArgs e)
        {
            if (!ValidNameEntered(out string name))
                return;
            if (!ValidIpEntered(out IPAddress serverIp))
                return;
            if (!ValidPortEntered(out ushort port))
                return;

            _myInfo = new ConnectedPlayerInformation(0, name);

            btnHost.Enabled = false;
            btnJoin.Enabled = false;
            txtIP.Enabled = false;
            txtName.Enabled = false;

            _tcpClient = new SpideyTcpClient(serverIp, port);
            var onReceivePlayerNumber = new Progress<int>(OnReceivePlayerNumber);
            var onConnected = new Progress<ConnectedPlayerInformation>(OnTcpPlayerConnected);
            var onServerStarted = new Progress<bool>(OnServerStarted);
            _tcpClient.Start(onReceivePlayerNumber, onConnected, onServerStarted, name);
        }

        private void OnReceivePlayerNumber(int myPlayerNumber)
        {
            _myInfo = new ConnectedPlayerInformation(myPlayerNumber, _myInfo.Name);
        }

        private void OnTcpPlayerConnected(ConnectedPlayerInformation connectedPlayerInfo)
        {
            var displayInfo = new ListViewItem(new[] { connectedPlayerInfo.Name, "Not started" });
            displayInfo.Tag = connectedPlayerInfo.Number;
            lstPlayers.Items.Add(displayInfo);
            if (_isHost && lstPlayers.Items.Count > 1)
            {
                btnStart.Enabled = true;
            }
        }

        private void OnServerStarted(bool started)
        {
            /*
            var udpClient = new UdpClient();
            udpClient.Client.ReceiveTimeout = 5000;
            udpClient.Connect(serverIp, Port);
            memoryTimer = new Timer(ReadFromMemory);
            memoryTimer.Change(0, Timeout.Infinite);
            */
        }

        private void ReadFromMemory(object state)
        {
            if (_requestTimerStop)
            {
                memoryTimer.Change(Timeout.Infinite, Timeout.Infinite);
                return;
            }

            var myInfo = new byte[31];
            myInfo[0] = (byte)playerNumber;
            var spideyBuffer = MemoryScanner.ReadSpideyInfo();
            var location = MemoryScanner.ReadLevelTitle();

            Buffer.BlockCopy(spideyBuffer, 0, myInfo, 1, spideyBuffer.Length);
            Buffer.BlockCopy(location, 0, myInfo, spideyBuffer.Length + 1, location.Length);
            
            var levelTitle = new StringBuilder();

            for (int i = 0; i < 24; i++)
            {
                levelTitle.Append((char)location[i]);
            }

            try
            {
                lock (infoLock)
                {
                    if (playerNumber == 1)
                    {
                        player1Info = myInfo;
                    }
                    else if (playerNumber == 2)
                    {
                        player2Info = myInfo;
                        udpClient.Send(player2Info, player2Info.Length);
                    }
                    else if (playerNumber == 3)
                    {
                        player3Info = myInfo;
                        udpClient.Send(player3Info, player3Info.Length);
                    }

                    if (playerNumber >= 2)
                    {
                        var ipEndPoint = new IPEndPoint(IPAddress.Any, Port);
                        var result = udpClient.Receive(ref ipEndPoint);
                        if (result.Length >= 31)
                        {
                            var clientPlayerNumber = (int)result[0];

                            var clientPosition = result.Skip(1).Take(6).ToArray();
                            var clientLocation = result.Skip(7).Take(24).ToArray();
                            SetPlayerPosition(clientPlayerNumber, clientPosition, clientLocation);
                            if (result.Length == 62)
                            {
                                clientPlayerNumber = (int)result[31];

                                clientPosition = result.Skip(32).Take(6).ToArray();
                                clientLocation = result.Skip(38).Take(24).ToArray();
                                SetPlayerPosition(clientPlayerNumber, clientPosition, clientLocation);
                            }
                        }
                    }

                    MyLocation = levelTitle.ToString();
                }
            }
            catch (Exception ex)
            {

            }
            /*
            var playerLabel = lblPlayer1Loc;
            if (playerNumber == 2)
            {
                playerLabel = lblPlayer2Loc;
            }
            else if (playerNumber == 3)
            {
                playerLabel = lblPlayer3Loc;
            }

            playerLabel.BeginInvoke(new Action(() =>
            {
                playerLabel.Text = levelTitle.ToString();
            }));*/

            memoryTimer.Change(100, Timeout.Infinite);
        }

        private bool ValidNameEntered(out string name)
        {
            name = txtName.Text;
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Please enter a name");
                return false;
            }
            return true;
        }

        private bool ValidPortEntered(out ushort port)
        {
            if (string.IsNullOrEmpty(txtPort.Text) || !ushort.TryParse(txtPort.Text, out port))
            {
                port = 0;
                MessageBox.Show("Please enter a valid port");
                return false;
            }
            return true;
        }

        private bool ValidIpEntered(out IPAddress serverIp)
        {
            if (string.IsNullOrEmpty(txtIP.Text) || !IPAddress.TryParse(txtIP.Text, out serverIp))
            {
                serverIp = IPAddress.None;
                MessageBox.Show("Please enter a valid IP");
                return false;
            }
            return true;
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, Port));
            signalToStartHosting.Release();

            Task.Run(UdpServerTask);

            btnStart.Enabled = false;
            memoryTimer = new Timer(ReadFromMemory);
            memoryTimer.Change(0, Timeout.Infinite);
        }

        private async Task UdpServerTask()
        {
            /*
            while (serverStarted)
            {
                var result = await udpClient.ReceiveAsync();
                if (result.Buffer.Length < 31)
                    continue;

                var clientPlayerNumber = (int)result.Buffer[0];

                var position = result.Buffer.Skip(1).Take(6).ToArray();
                var location = result.Buffer.Skip(7).Take(24).ToArray();

                SetPlayerPosition(clientPlayerNumber, position, location);
                
                if (clientPlayerNumber == 2)
                    player2Info = result.Buffer;
                else
                    player3Info = result.Buffer;

                lock (infoLock)
                {
                    if (players == 2)
                    {
                        udpClient.Send(player1Info, player1Info.Length, result.RemoteEndPoint);
                    }
                    else if (players == 3)
                    {
                        var infoToSend = new byte[62];
                        Buffer.BlockCopy(player1Info, 0, infoToSend, 0, player1Info.Length);

                        if (clientPlayerNumber == 2)
                            Buffer.BlockCopy(player3Info, 0, infoToSend, player1Info.Length, player3Info.Length);
                        else
                            Buffer.BlockCopy(player2Info, 0, infoToSend, player1Info.Length, player2Info.Length);

                        udpClient.Send(infoToSend, infoToSend.Length, result.RemoteEndPoint);
                    }
                }
            }*/
        }

        private void SetPlayerPosition(int clientPlayerNumber, byte[] position, byte[] location)
        {
            /*
            var playerBox = player2Sprite;
            var playerLabel = lblPlayer1Loc;
            
            if (clientPlayerNumber == 2)
            {
                playerLabel = lblPlayer2Loc;
            }
            else if (clientPlayerNumber == 3)
            {
                playerLabel = lblPlayer3Loc;
            }

            if ((clientPlayerNumber == 3) || (clientPlayerNumber == 2 && playerNumber == 3))
            {
                playerBox = player3Sprite;
            }

            var left = (int)position[0];
            var leftScreen = (int)position[1];
            var right = (int)position[2];
            var rightScreen = (int)position[3];
            var top = (int)position[4];
            var bottom = (int)position[5];

            var spideyLeft = hostPanel.Left + ((left / 255.0) * _spideyWindow.BorderlessWidth * 0.8);
            var spideyRight = hostPanel.Left + ((right / 255.0) * _spideyWindow.BorderlessWidth * 0.8);
            var spideyTop = hostPanel.Top + _spideyWindow.BorderlessHeight * 0.12 + ((top / 175.0) * _spideyWindow.BorderlessHeight * 0.88);
            var spideyBottom = hostPanel.Top + _spideyWindow.BorderlessHeight * 0.12 + ((bottom / 175.0) * _spideyWindow.BorderlessHeight * 0.88);
            
            var levelTitle = new StringBuilder();

            for (int i = 0; i < 24; i++)
            {
                levelTitle.Append((char)location[i]);
            }

            var sameLocation = false;
            lock (infoLock)
            {
                sameLocation = MyLocation == levelTitle.ToString();
            }

            if (sameLocation)
            {
                playerBox.BeginInvoke(new Action(() =>
                {
                    playerBox.Visible = true;
                    playerBox.Size = new Size((int)spideyRight - (int)spideyLeft, (int)spideyBottom - (int)spideyTop);
                    playerBox.Left = (int)spideyLeft;
                    playerBox.Top = (int)spideyTop;
                }));
            }
            else
            {
                playerBox.BeginInvoke(new Action(() =>
                {
                    playerBox.Visible = false;
                }));
            }

            playerLabel.BeginInvoke(new Action(() =>
            {
                playerLabel.Text = levelTitle.ToString();
            }));*/
        }
    }
}
