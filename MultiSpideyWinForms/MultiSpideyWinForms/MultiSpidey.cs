using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
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
        /*
        [DllImport("user32.dll", EntryPoint = "GetWindowThreadProcessId", SetLastError = true,
             CharSet = CharSet.Unicode, ExactSpelling = true,
             CallingConvention = CallingConvention.StdCall)]
        private static extern long GetWindowThreadProcessId(long hWnd, long lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern long SetWindowPos(IntPtr hwnd, long hWndInsertAfter, long x, long y, long cx, long cy, long wFlags);

        [DllImport("user32.dll", EntryPoint = "PostMessageA", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hwnd, uint Msg, long wParam, long lParam);
        */

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

        private byte[] myLocation = new byte[31];
        private string myName = "Player 1";
        private string player2Name = "Player 2";
        private string player3Name = "Player 3";
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
                if (!MemoryScanner.GetMemoryAddresses(this))
                {
                    Invoke(new Action(() => { MessageBox.Show("Unable to find player, make sure spidey is in game"); }));
                    success.Report(false);
                    return;
                }
                memoryTimer = new Timer(ReadFromMemory, null, 0, Timeout.Infinite);
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

            // Move this logic into class
            var spideyBuffer = MemoryScanner.ReadSpideyPosition();
            var left = (int)spideyBuffer[0];
            var leftScreen = (int)spideyBuffer[1];
            var right = (int)spideyBuffer[2];
            var rightScreen = (int)spideyBuffer[3];
            var top = (int)spideyBuffer[4];
            var bottom = (int)spideyBuffer[5];
            
            var spideyLeft = hostPanel.Left + ((left / 255.0) * clientWidth * 0.8);
            var spideyRight = hostPanel.Left + ((right / 255.0) * clientWidth * 0.8);
            var spideyTop = hostPanel.Top + clientHeight * 0.12 + ((top / 175.0) * clientHeight * 0.88);
            var spideyBottom = hostPanel.Top + clientHeight * 0.12 + ((bottom / 175.0) * clientHeight * 0.88);

            player2Sprite.BeginInvoke(new Action(() =>
            {
                player2Sprite.Size = new Size((int)spideyRight - (int)spideyLeft, (int)spideyBottom - (int)spideyTop);
                player2Sprite.Left = (int)spideyLeft;
                player2Sprite.Top = (int)spideyTop;
            }));

            var levelTitle = MemoryScanner.ReadLevelTitle();

            lblPlayer2Loc.BeginInvoke(new Action(() =>
            {
                lblPlayer2Loc.Text = levelTitle;
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
                
                Invoke(new Action(() => { MessageBox.Show("Server stopped"); }));
            }
            catch (Exception ex)
            {
                Invoke(new Action(() => { MessageBox.Show(ex.Message); }));
            }
        }

        private async Task HandleNewConnections(IProgress<bool> canStart, CancellationTokenSource cancellation)
        {
            var tcpServer = new TcpListener(IPAddress.Any, Port);
            var tcpClients = new List<TcpClient>();

            try
            {
                tcpServer.Start();
                var playerCounter = 0;
                while (!cancellation.IsCancellationRequested)
                {
                    var client = await Task.Run(() => tcpServer.AcceptTcpClientAsync(), cancellation.Token);
                    if (cancellation.IsCancellationRequested)
                        break;
                    if (client == null)
                        continue;

                    canStart.Report(true);
                    playerCounter++;

                    using (var reader = new StreamReader(client.GetStream()))
                    {
                        using (var writer = new StreamWriter(client.GetStream()))
                        {
                            var clientName = reader.ReadLine();
                            writer.WriteLine(playerCounter.ToString());
                            writer.WriteLine(myName);
                            writer.Flush();

                            Invoke(new Action(() => { MessageBox.Show(clientName); }));
                        }
                    }

                    tcpClients.Add(client);
                }
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
                        using (var writer = new StreamWriter(tcpClient.GetStream()))
                        {
                            writer.WriteLine(Start);
                            writer.Flush();
                        }
                    }
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

            var serverStartedSignal = new Progress<bool>(s =>
            {
            }) as IProgress<bool>;

            Task.Run(() => ClientTask());
        }

        private async Task ClientTask()
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
                        var playerNumber = int.Parse(reader.ReadLine());
                        var serverName = reader.ReadLine();

                        var nextInstruction = await reader.ReadLineAsync();

                        if (nextInstruction == Start)
                            Invoke(new Action(() => { MessageBox.Show("Server started"); }));
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
            }
        }

        private bool GetName()
        {
            if (string.IsNullOrEmpty(txtName.Text))
            {
                MessageBox.Show("Please enter a name");
                return false;
            }
            myName = txtName.Text;
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
            signalToStartHosting.Release();
            
            udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, Port));

            btnStart.Enabled = false;
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
