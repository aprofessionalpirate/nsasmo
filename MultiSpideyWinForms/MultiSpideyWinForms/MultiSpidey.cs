using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Threading.Timer;

// TTD
// Fix workaround for getting width/height
// Use SetWindowPos instead of MoveWindow

namespace MultiSpideyWinForms
{
    public partial class MultiSpidey : Form
    {
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hwnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern long SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
    
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", CharSet = CharSet.Auto)]
        private static extern long GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", CharSet = CharSet.Auto)]
        private static extern long GetWindowLong64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLong")]
        private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLong64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(IntPtr hwnd, int x, int y, int cx, int cy, bool repaint);

        private const int SWP_NOOWNERZORDER = 0x200;
        private const int SWP_NOREDRAW = 0x8;
        private const int SWP_NOZORDER = 0x4;
        private const int SWP_SHOWWINDOW = 0x0040;
        private const int WS_EX_MDICHILD = 0x40;
        private const int SWP_FRAMECHANGED = 0x20;
        private const int SWP_NOACTIVATE = 0x10;
        private const int SWP_ASYNCWINDOWPOS = 0x4000;
        private const int SWP_NOMOVE = 0x2;
        private const int SWP_NOSIZE = 0x1;
        private const int GWL_STYLE = (-16);
        private const int WS_VISIBLE = 0x10000000;
        private const int WM_CLOSE = 0x10;
        private const int WS_CHILD = 0x40000000;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public static long GetWindowLong(IntPtr hWnd, int nIndex)
        {
            if (!MemoryScanner.Is64BitProcess)
            {
                return GetWindowLong32(hWnd, nIndex);
            }
            else
            {
                return GetWindowLong64(hWnd, nIndex);
            }
        }

        public static IntPtr SetWindowLong(IntPtr hWnd, int nIndex, long dwNewLong)
        {
            if (!MemoryScanner.Is64BitProcess)
            {
                return SetWindowLong32(hWnd, nIndex, (IntPtr)dwNewLong);
            }
            else
            {
                return SetWindowLong64(hWnd, nIndex, (IntPtr)dwNewLong);
            }
        }

        private const int Port = 6015;

        private IntPtr spideyWindow = IntPtr.Zero;
        private IntPtr originalParent = IntPtr.Zero;
        private long originalWindowLong = 0;

        private RECT spideyClientRect;
        private RECT spideyWindowRect;
        private int clientWidth;
        private int clientHeight;
        private int windowWidth;
        private int windowHeight;

        private volatile bool _requestTimerStop = false;
        private Timer memoryTimer;
        private UdpClient udpClient;

        private SemaphoreSlim signalToStartHosting = new SemaphoreSlim(0, 1);
        private volatile bool serverStarted = false;
        private IPAddress serverIp;

        private int playerNumber = 1;
        private readonly object infoLock = new object();
        private byte[] player1Info = new byte[30];
        private byte[] player2Info = new byte[30];
        private byte[] player3Info = new byte[30];
        private string player1Name = "Player 1";
        private string player2Name = "Player 2";
        private string player3Name = "Player 3";
        private string MyLocation = "";

        private const string Start = "STARTTHEGAMEALREADY";

        public MultiSpidey()
        {
            InitializeComponent();
        }

        private void btnFindSpidey_Click(object sender, EventArgs e)
        {
            spideyWindow = WindowFinder.FindWindowsWithText("SPIDEY").FirstOrDefault();
            if (spideyWindow == null || spideyWindow == IntPtr.Zero)
            {
                MessageBox.Show("Spidey not found");
                return;
            }

            btnFindPlayer.Enabled = true;
            btnFindSpidey.Enabled = false;

            originalWindowLong = GetWindowLong(spideyWindow, GWL_STYLE);
            originalParent = GetParent(spideyWindow);

            SetParent(spideyWindow, hostPanel.Handle);
            
            // Have to do this weird thing because height is incorrect on fresh DOSBox
            GetWindowRect(spideyWindow, out spideyWindowRect);
            var firstWindowWidth = spideyWindowRect.Right - spideyWindowRect.Left;
            var firstWindowHeight = spideyWindowRect.Bottom - spideyWindowRect.Top;
            SetParent(spideyWindow, originalParent);
            MoveWindow(spideyWindow, 0, 0, firstWindowWidth, firstWindowHeight, true);
            SetParent(spideyWindow, hostPanel.Handle);
            GetClientRect(spideyWindow, out spideyClientRect);
            GetWindowRect(spideyWindow, out spideyWindowRect);
            clientWidth = spideyClientRect.Right - spideyClientRect.Left;
            clientHeight = spideyClientRect.Bottom - spideyClientRect.Top;
            windowWidth = spideyWindowRect.Right - spideyWindowRect.Left;
            windowHeight = spideyWindowRect.Bottom - spideyWindowRect.Top;

            // Make panel big enough for spidey window
            // Must be done after attaching window otherwise it will have the incorrect size in high DPI displays
            hostPanel.Size = new Size(clientWidth, clientHeight);

            // Remove border and whatnot
            SetWindowLong(spideyWindow, GWL_STYLE, WS_VISIBLE);

            // Move the window to overlay it on this window
            MoveWindow(spideyWindow, 0, 0, windowWidth, windowHeight, true);
        }
        
        protected override void OnMove(EventArgs e)
        {
            if (spideyWindow != null && spideyWindow != IntPtr.Zero)
            {
                // Have move it off 0, 0 first otherwise it won't update
                MoveWindow(spideyWindow, 1, 1, windowWidth, windowHeight, true);
                MoveWindow(spideyWindow, 0, 0, windowWidth, windowHeight, true);
            }
            Invalidate();
            base.OnMove(e);
        }

        protected override void OnResize(EventArgs e)
        {
            if (spideyWindow != null && spideyWindow != IntPtr.Zero)
            {
                // Have move it off 0, 0 first otherwise it won't update
                MoveWindow(spideyWindow, 1, 1, windowWidth, windowHeight, true);
                MoveWindow(spideyWindow, 0, 0, windowWidth, windowHeight, true);
            }
            Invalidate();
            base.OnResize(e);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            _requestTimerStop = true;
            DetachSpideyWindow();

            base.OnHandleDestroyed(e);
        }

        private void btnFindPlayer_Click(object sender, EventArgs e)
        {
            btnFindPlayer.Enabled = false;

            var success = new Progress<bool>(s =>
                                            {
                                                btnFindPlayer.Enabled = !s;
                                                btnHost.Enabled = s;
                                                btnJoin.Enabled = s;
                                            }) as IProgress<bool>;

            Task.Run(() =>
            {
                if (!MemoryScanner.GetMemoryAddresses(this, spideyWindow))
                {
                    Invoke(new Action(() => { MessageBox.Show("Unable to find player, make sure spidey is in game"); }));
                    success.Report(false);
                    return;
                }
                success.Report(true);
            });
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
            var spideyBuffer = MemoryScanner.ReadSpideyPosition();
            var location = MemoryScanner.ReadLevelTitle();

            Buffer.BlockCopy(spideyBuffer, 0, myInfo, 1, spideyBuffer.Length);
            Buffer.BlockCopy(location, 0, myInfo, spideyBuffer.Length + 1, location.Length);
            
            var levelTitle = new StringBuilder();

            for (int i = 0; i < 24; i++)
            {
                levelTitle.Append((char)location[i]);
            }

            lock (infoLock)
            {
                if (playerNumber == 1)
                {
                    player1Info = myInfo;
                }
                else if (playerNumber == 2)
                {
                    player2Info = myInfo;
                }
                else if (playerNumber == 3)
                {
                    player3Info = myInfo;
                }
                MyLocation = levelTitle.ToString();
            }

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
            }));

            memoryTimer.Change(100, Timeout.Infinite);
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            DetachSpideyWindow();
            btnFindPlayer.Enabled = false;
            btnFindSpidey.Enabled = true;
        }

        private void DetachSpideyWindow()
        {
            if (originalWindowLong != 0 && spideyWindow != IntPtr.Zero)
            {
                SetParent(spideyWindow, originalParent);
                SetWindowLong(spideyWindow, GWL_STYLE, originalWindowLong);
                MoveWindow(spideyWindow, 0, 0, windowWidth, windowHeight, true);
                spideyWindow = IntPtr.Zero;
                originalParent = IntPtr.Zero;
                originalWindowLong = 0;
            }
        }

        private void btnHost_Click(object sender, EventArgs e)
        {
            if (!GetName())
                return;

            playerNumber = 1;
            player1Name = txtName.Text;
            lblPlayer1Name.ForeColor = Color.Red;
            lblPlayer1Name.Text = player1Name;
            btnHost.Enabled = false;
            btnJoin.Enabled = false;
            txtIP.Enabled = false;
            txtName.Enabled = false;

            var canStart = new Progress<bool>(s =>
            {
                btnStart.Enabled = true;
            }) as IProgress<bool>;

            Task.Run(() => ServerTask(canStart));
        }

        private async Task ServerTask(IProgress<bool> canStart)
        {
            try
            {
                var cancellation = new CancellationTokenSource();
                var connectionTask = Task.Run(() => HandleNewConnections(canStart, cancellation));
                                
                // Wait for signal here to start the game
                await signalToStartHosting.WaitAsync();
                
                cancellation.Cancel();
                await connectionTask;
            }
            catch (Exception ex)
            {
                Invoke(new Action(() => { MessageBox.Show(ex.Message); }));
            }
        }

        private async Task HandleNewConnections(IProgress<bool> canStart, CancellationTokenSource cancellation)
        {
            var tcpServer = new TcpListener(IPAddress.Any, Port);
            var tcpClients = new List<TcpClientWithName>();

            try
            {
                tcpServer.Start();
                var playerCounter = 1;
                while (!cancellation.IsCancellationRequested)
                {
                    var client = await tcpServer.AcceptTcpClientAsync().WithWaitCancellation(cancellation.Token);
                    if (cancellation.IsCancellationRequested)
                        break;
                    if (client == null)
                        continue;

                    canStart.Report(true);
                    playerCounter++;

                    var clientWithName = new TcpClientWithName();

                    var reader = new StreamReader(client.GetStream());
                    var writer = new StreamWriter(client.GetStream());

                    clientWithName.Client = client;
                    clientWithName.Reader = reader;
                    clientWithName.Writer = writer;

                    var clientName = reader.ReadLine();
                    clientWithName.PlayerNumber = playerCounter;
                    clientWithName.Name = clientName;

                    if (playerCounter == 2)
                    {
                        player2Name = clientName;
                        Invoke(new Action(() => { lblPlayer2Name.Text = player2Name; }));
                    }
                    else if (playerCounter == 3)
                    {
                        player3Name = clientName;
                        Invoke(new Action(() => { lblPlayer3Name.Text = player3Name; }));
                    }

                    writer.WriteLine(playerCounter.ToString());
                    writer.WriteLine(player1Name);

                    foreach (var tcpClient in tcpClients)
                    {
                        writer.WriteLine(tcpClient.PlayerNumber);
                        writer.WriteLine(tcpClient.Name);
                        tcpClient.Writer.WriteLine(playerCounter);
                        tcpClient.Writer.WriteLine(clientName);
                        tcpClient.Writer.Flush();
                    }
                    writer.Flush();

                    tcpClients.Add(clientWithName);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Invoke(new Action(() => { MessageBox.Show(ex.Message); }));
            }
            finally
            {
                if (serverStarted)
                {
                    foreach (var tcpClient in tcpClients)
                    {
                        tcpClient.Writer.WriteLine(Start);
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

        private void btnJoin_Click(object sender, EventArgs e)
        {
            if (!GetName())
                return;

            if (!GetIp())
                return;

            btnHost.Enabled = false;
            btnJoin.Enabled = false;
            txtIP.Enabled = false;
            txtName.Enabled = false;

            var myName = txtName.Text;

            var serverStartedSignal = new Progress<bool>(s =>
            {
                Task.Run(UdpClientTask);
                memoryTimer = new Timer(ReadFromMemory, null, 0, Timeout.Infinite);
            }) as IProgress<bool>;

            Task.Run(() => ClientTask(myName, serverStartedSignal));
        }

        private async Task ClientTask(string myName, IProgress<bool> serverStartedSignal)
        {
            var tcpClient = new TcpClient();
            try
            {
                await tcpClient.ConnectAsync(serverIp, Port);
                using (var reader = new StreamReader(tcpClient.GetStream()))
                {
                    using (var writer = new StreamWriter(tcpClient.GetStream()))
                    {
                        writer.WriteLine(myName);
                        writer.Flush();
                        playerNumber = int.Parse(reader.ReadLine());
                        player1Name = reader.ReadLine();
                        Invoke(new Action(() => { lblPlayer1Name.Text = player1Name; }));

                        if (playerNumber == 2)
                        {
                            player2Name = myName;
                            Invoke(new Action(() => { lblPlayer2Name.Text = player2Name; lblPlayer2Name.ForeColor = Color.Red; }));
                        }
                        else if (playerNumber == 3)
                        {
                            player3Name = myName;
                            Invoke(new Action(() => { lblPlayer3Name.Text = player3Name; lblPlayer3Name.ForeColor = Color.Red; }));
                        }
                        
                        while (!serverStarted)
                        {
                            var nextInstruction = await reader.ReadLineAsync();
                            if (nextInstruction == Start)
                                serverStarted = true;
                            else if (nextInstruction == "2")
                            {
                                player2Name = reader.ReadLine();
                                Invoke(new Action(() => { lblPlayer2Name.Text = player2Name; }));
                            }
                            else if (nextInstruction == "3")
                            {
                                player3Name = reader.ReadLine();
                                Invoke(new Action(() => { lblPlayer3Name.Text = player3Name; }));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Invoke(new Action(() => { MessageBox.Show(ex.Message); }));
            }
            finally
            {
                tcpClient.Close();
                if (serverStarted)
                    serverStartedSignal.Report(true);
            }
        }

        private bool GetName()
        {
            if (string.IsNullOrEmpty(txtName.Text))
            {
                MessageBox.Show("Please enter a name");
                return false;
            }
            return true;
        }

        private bool GetIp()
        {
            if (string.IsNullOrEmpty(txtIP.Text) || !IPAddress.TryParse(txtIP.Text, out serverIp))
            {
                MessageBox.Show("Please enter a valid IP");
                return false;
            }
            return true;
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            serverStarted = true;
            udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, Port));
            signalToStartHosting.Release();

            Task.Run(UdpServerTask);

            btnStart.Enabled = false;
            memoryTimer = new Timer(ReadFromMemory, null, 0, Timeout.Infinite);
        }

        private async Task UdpServerTask()
        {
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
            }
        }

        private async Task UdpClientTask()
        {
            udpClient = new UdpClient();
            udpClient.Connect(serverIp, Port);
            while (serverStarted)
            {
                await Task.Delay(100);
                lock (infoLock)
                {
                    if (playerNumber == 2)
                    {
                        udpClient.Send(player2Info, player2Info.Length);
                    }
                    else if (playerNumber == 3)
                    {
                        udpClient.Send(player3Info, player3Info.Length);
                    }
                }
            }
        }

        private void SetPlayerPosition(int playerNumber, byte[] position, byte[] location)
        {
            var playerBox = player2Sprite;
            var playerLabel = lblPlayer2Loc;

            if (playerNumber == 3)
            {
                playerBox = player3Sprite;
                playerLabel = lblPlayer3Loc;
            }

            var left = (int)position[0];
            var leftScreen = (int)position[1];
            var right = (int)position[2];
            var rightScreen = (int)position[3];
            var top = (int)position[4];
            var bottom = (int)position[5];

            var spideyLeft = hostPanel.Left + ((left / 255.0) * clientWidth * 0.8);
            var spideyRight = hostPanel.Left + ((right / 255.0) * clientWidth * 0.8);
            var spideyTop = hostPanel.Top + clientHeight * 0.12 + ((top / 175.0) * clientHeight * 0.88);
            var spideyBottom = hostPanel.Top + clientHeight * 0.12 + ((bottom / 175.0) * clientHeight * 0.88);
            
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
            }));
        }

        /*
        private void OldHost()
        {
            CancellationTokenSource tsHost;
            tsHost = new CancellationTokenSource();
            CancellationToken ct = tsHost.Token;
            Task.Run(() =>
            {
                UdpClient udpServer = new UdpClient(6102);

                while (true)
                {
                    var remoteEP = new IPEndPoint(IPAddress.Any, 6102);
                    var data = udpServer.Receive(ref remoteEP);
                    if (ct.IsCancellationRequested)
                        break;
                    udpServer.Send(new byte[] { 1 }, 1, remoteEP);
                }
            }, ct);
        }*/
    }
}
