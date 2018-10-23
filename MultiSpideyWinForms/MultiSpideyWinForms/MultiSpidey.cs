using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using Timer = System.Threading.Timer;

// TODO
// Properly reset on reset all
// Handle player disconnection + maybe have server just always running?
// Interpolation of player position
// Add debug mode where can switch on and off various bytes that are transmitted
// Rebase code

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
        private readonly object _infoLock = new object();
        private readonly object _startupTimerLock = new object();
        private readonly object _memoryTimerLock = new object();

        private SpideyWindow _spideyWindow;
        private Timer _startupTimer = null;
        private Timer _memoryTimer = null;

        private SpideyTcpServer _tcpServer;
        private SpideyTcpClient _tcpClient;
        private SpideyUdpServer _udpServer;
        private SpideyUdpClient _udpClient;
        private SpideyUdpBase _udpBase;

        private ConnectedPlayerInformation _myInfo;
        private IProgress<ConnectedPlayerInformation> _onLocationUpdate;

        private bool _isHost = false;
        private IPAddress _serverIp;
        private ushort _port;

        private SemaphoreSlim signalToStartHosting = new SemaphoreSlim(0, 1);

        private List<UdpClient> udpWebSwing = new List<UdpClient>();

        public MultiSpidey()
        {
            _onLocationUpdate = new Progress<ConnectedPlayerInformation>(OnLocationUpdate);
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
            lock (_startupTimerLock)
            {
                _startupTimer = new Timer(AttachToDosbox);
                _startupTimer.Change(0, 1000);
            }
        }

        private void StopStartupTimer()
        {
            lock (_startupTimerLock)
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
            lock (_startupTimerLock)
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

                lock (_startupTimerLock)
                {
                    _startupTimer = new Timer(FindPlayerInMemory);
                    _startupTimer.Change(0, 1000);
                }
            }));
        }

        private void FindPlayerInMemory(object state)
        {
            lock (_startupTimerLock)
            {
                var timer = state as Timer;
                if (timer != _startupTimer || _startupTimer == null) return;

                string error;
                if (!MemoryScanner.GetMemoryAddresses(out error, _spideyWindow.Handle))
                {
                    Invoke(new Action(() => { MessageBox.Show(error); }));
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
            StopMemoryTimer();
            _spideyWindow = null;
            // TODO - figure out why I can't just call .Wait() here and await inside Stop()
            // It seems to wait forever even though the task completes
            if (_udpClient != null) _udpClient.Stop();
            if (_udpServer != null) _udpServer.Stop();
            if (_tcpClient != null) _tcpClient.Stop();
            if (_tcpServer != null) _tcpServer.Stop();

            base.OnHandleDestroyed(e);
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            _isHost = false;
            StopStartupTimer();
            StopMemoryTimer();
            _spideyWindow = null;
            StartStartupTimer();
        }

        private void btnHost_Click(object sender, EventArgs e)
        {
            if (!ValidNameEntered(out string name))
                return;
            if (!ValidPortEntered(out _port))
                return;

            _myInfo = new ConnectedPlayerInformation(1, name);
            OnTcpPlayerConnected(_myInfo);

            _isHost = true;
            btnHost.Enabled = false;
            btnJoin.Enabled = false;
            txtIP.Enabled = false;
            txtName.Enabled = false;

            _tcpServer = new SpideyTcpServer(_port);
            var onConnected = new Progress<ConnectedPlayerInformation>(OnTcpPlayerConnected);
            _tcpServer.Start(onConnected, _myInfo.Data);

            _udpServer = new SpideyUdpServer(_port);
            _udpBase = _udpServer;
            var onReceiveUdpInfo = new Progress<ConnectedPlayerUdpEndPoint>(OnReceiveUdpInfo);
            _udpServer.Start(_tcpServer, _onLocationUpdate, onReceiveUdpInfo);
        }

        private void btnJoin_Click(object sender, EventArgs e)
        {
            if (!ValidNameEntered(out string name))
                return;
            if (!ValidIpEntered(out _serverIp))
                return;
            if (!ValidPortEntered(out _port))
                return;

            _myInfo = new ConnectedPlayerInformation(0, name);

            btnHost.Enabled = false;
            btnJoin.Enabled = false;
            txtIP.Enabled = false;
            txtName.Enabled = false;

            _udpClient = new SpideyUdpClient(_serverIp, _port);
            _udpBase = _udpClient;
            var onUdpClientConnected = new Progress<bool>(OnUdpClientConnected);
            _udpClient.Start(onUdpClientConnected, _onLocationUpdate);

            _tcpClient = new SpideyTcpClient(_serverIp, _port);
            var onReceivePlayerNumber = new Progress<byte>(OnReceivePlayerNumber);
            var onConnected = new Progress<ConnectedPlayerInformation>(OnTcpPlayerConnected);
            var onServerStarted = new Progress<bool>(OnServerStarted);
            var onReceiveUdpInfo = new Progress<ConnectedPlayerUdpEndPoint>(OnReceiveUdpInfo);
            _tcpClient.Start(_udpClient, onReceivePlayerNumber, onConnected, onServerStarted, onReceiveUdpInfo, name);

            OnReceiveUdpInfo(new ConnectedPlayerUdpEndPoint(1, new IPEndPoint(_serverIp, _port)));
        }

        private void OnUdpClientConnected(bool connected)
        {
            // TODO?
        }

        private void OnReceivePlayerNumber(byte myPlayerNumber)
        {
            _myInfo = new ConnectedPlayerInformation(myPlayerNumber, _myInfo.Data);
        }

        private void OnReceiveUdpInfo(ConnectedPlayerUdpEndPoint playerUdpInfo)
        {
            var udpClient = new UdpClient();
            udpClient.Connect(playerUdpInfo.EndPoint);
            udpWebSwing.Add(udpClient);
        }

        private void OnTcpPlayerConnected(ConnectedPlayerInformation connectedPlayerInfo)
        {
            var displayInfo = new ListViewItem(new[] { connectedPlayerInfo.Data, "Not started" });
            displayInfo.Tag = connectedPlayerInfo.Number;
            lstPlayers.Items.Add(displayInfo);
            if (_isHost && lstPlayers.Items.Count > 1)
            {
                btnStart.Enabled = true;
            }
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

        private async void btnStart_Click(object sender, EventArgs e)
        {
            btnStart.Enabled = false;

            await _tcpServer.StartGame();

            StartMemoryTimer();
        }

        private void OnServerStarted(bool started)
        {
            if (!started) return;

            _udpClient.StartGame();
            StartMemoryTimer();
        }

        private void OnLocationUpdate(ConnectedPlayerInformation connectedPlayerInfo)
        {
            try
            {
                var players = lstPlayers.Items.Cast<ListViewItem>();
                var player = players.FirstOrDefault(p => Convert.ToInt32(p.Tag) == connectedPlayerInfo.Number);
                if (player.SubItems[1].Text != connectedPlayerInfo.Data)
                {
                    player.SubItems[1].Text = connectedPlayerInfo.Data;
                }
            }
            catch (Exception ex)
            {
                // TODO - handle this better, can fail when trying to update when shutting down
            }
        }

        private void StartMemoryTimer()
        {
            lock (_memoryTimerLock)
            {
                _memoryTimer = new Timer(ReadFromMemory);
                _memoryTimer.Change(0, 15);
            }
        }

        private void StopMemoryTimer()
        {
            lock (_memoryTimerLock)
            {
                if (_memoryTimer != null)
                {
                    try
                    {
                        _memoryTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                    _memoryTimer.Dispose();
                    _memoryTimer = null;
                }
            }
        }

        private void ReadFromMemory(object state)
        {
            string location;
            lock (_memoryTimerLock)
            {
                var timer = state as Timer;
                if (timer != _memoryTimer || _memoryTimer == null) return;

                var spideyData = MemoryScanner.ReadSpideyData();
                var locationData = MemoryScanner.ReadLocationData();

                var message = SpideyUdpMessage.CreateSpidermanMessage(_myInfo.Number, spideyData, locationData);

                foreach (var udpClient in udpWebSwing)
                {
                    udpClient.Send(message, message.Length);
                }

                location = SpideyUdpMessage.AsciiEncoding.GetString(locationData).TrimEnd();
                _onLocationUpdate.Report(new ConnectedPlayerInformation(_myInfo.Number, location));

                _udpBase.MyLastLocation = location;
            }
        }
    }
}
