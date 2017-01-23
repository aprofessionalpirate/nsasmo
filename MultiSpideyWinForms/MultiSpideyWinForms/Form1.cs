using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
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

namespace MultiSpideyWinForms
{
    public partial class Form1 : Form
    {
        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        
        [DllImport("user32.dll", EntryPoint = "GetWindowThreadProcessId", SetLastError = true,
             CharSet = CharSet.Unicode, ExactSpelling = true,
             CallingConvention = CallingConvention.StdCall)]
        private static extern long GetWindowThreadProcessId(long hWnd, long lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern long SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
    
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", CharSet = CharSet.Auto)]
        private static extern long GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", CharSet = CharSet.Auto)]
        private static extern long GetWindowLong64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLong")]
        private static extern IntPtr SetWindowLong32(IntPtr hWnd, Int32 nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLong64(IntPtr hWnd, Int32 nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern long SetWindowPos(IntPtr hwnd, long hWndInsertAfter, long x, long y, long cx, long cy, long wFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(IntPtr hwnd, int x, int y, int cx, int cy, bool repaint);

        [DllImport("user32.dll", EntryPoint = "PostMessageA", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hwnd, uint Msg, long wParam, long lParam);

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

        IntPtr spideyWindow = IntPtr.Zero;
        IntPtr dosBoxProcess = IntPtr.Zero;
        IntPtr originalParent = IntPtr.Zero;
        long originalWindowLong = 0;

        public Form1()
        {
            InitializeComponent();
            /*
            spideyWindow = WindowFinder.FindWindowsWithText("SPIDEY").FirstOrDefault();
            if (spideyWindow == null || spideyWindow == IntPtr.Zero) return;
            RECT spideyRect;
            GetWindowRect(spideyWindow, out spideyRect);

            Size = new Size((spideyRect.Right - spideyRect.Left)*2, (spideyRect.Bottom - spideyRect.Top)*2);
            panel1.Size = new Size((spideyRect.Right - spideyRect.Left) * 2, (spideyRect.Bottom - spideyRect.Top) * 2);
            */
        }

        public static long GetWindowLong(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 4)
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
            if (IntPtr.Size == 4)
            {
                return SetWindowLong32(hWnd, nIndex, (IntPtr)dwNewLong);
            }
            else
            {
                return SetWindowLong64(hWnd, nIndex, (IntPtr)dwNewLong);
            }
        }

        RECT spideyRect;
        private void btnFindSpidey_Click(object sender, EventArgs e)
        {
            spideyWindow = WindowFinder.FindWindowsWithText("SPIDEY").FirstOrDefault();
            if (spideyWindow == null || spideyWindow == IntPtr.Zero) return;
            btnFindPlayer.Enabled = true;
            btnFindSpidey.Enabled = false;

            originalParent = GetParent(spideyWindow);
            SetParent(spideyWindow, panel1.Handle);
            // Remove border and whatnot
            originalWindowLong = GetWindowLong(spideyWindow, GWL_STYLE);
            SetWindowLong(spideyWindow, GWL_STYLE, WS_VISIBLE);

            // Move the window to overlay it on this window
            MoveWindow(spideyWindow, 0, 0, Width, Height, true);

            GetWindowRect(spideyWindow, out spideyRect);

            //Size = new Size((spideyRect.Right - spideyRect.Left) * 2, (spideyRect.Bottom - spideyRect.Top) * 2);
            panel1.Size = new Size((spideyRect.Right - spideyRect.Left) * 2, (spideyRect.Bottom - spideyRect.Top) * 2);

        }

        /// <summary>
        /// Force redraw of control when size changes
        /// </summary>
        /// <param name="e">Not used</param>
        protected override void OnSizeChanged(EventArgs e)
        {
            this.Invalidate();
            base.OnSizeChanged(e);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="e"></param>
        protected override void OnHandleDestroyed(EventArgs e)
        {
            // Stop the application
            if (spideyWindow != null && spideyWindow != IntPtr.Zero)
            {
                // Post a colse message
                //PostMessage(spideyWindow, WM_CLOSE, 0, 0);

                // Delay for it to get the message
                System.Threading.Thread.Sleep(1000);

                // Clear internal handle
                spideyWindow = IntPtr.Zero;

            }

            base.OnHandleDestroyed(e);
        }
        
        /// <summary>
        /// Update display of the executable
        /// </summary>
        /// <param name="e">Not used</param>
        protected override void OnResize(EventArgs e)
        {
            if (spideyWindow != null && spideyWindow != IntPtr.Zero)
            {
                MoveWindow(spideyWindow, 0, 0, this.Width, this.Height, true);
            }
            base.OnResize(e);
        }

        const string MaryJaneIsDead = "4D617279204A616E6520697320444541442100";
        const string Credits = "4D7973746572696F202020202020202020202020202020204A2E4A6F6E6168204A616D65736F6E20202020202020202043686565736563616B652020202020202020202020202020456C76696520202020202020202020202020202020202020436F6C696E20202020202020202020202020202020202020466C6173682054686F6D70736F6E202020202020202020204A6F6520526F62657274736F6E2020202020202020202020444D502020202020202020202020202020202020202020204B69636B6168612074686520547269636B737465722020205357472020202020202020202020202020202020202020204B52412020202020202020202020202020202020202020204368616D706965202020202020202020202020202020202052696B2020202020202020202020202020202020202020204D617279204A616E652020202020202020202020202020205065746572205061726B657220202020202020202020202041756E74204D6179";

        // REQUIRED CONSTS
        const int PROCESS_QUERY_INFORMATION = 0x0400;
        const int MEM_COMMIT = 0x00001000;
        const int PAGE_READWRITE = 0x04;
        const int PROCESS_WM_READ = 0x0010;

        // REQUIRED METHODS
        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(int hProcess, int lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        static extern void GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

        // REQUIRED STRUCTS
        public struct MEMORY_BASIC_INFORMATION
        {
            public int BaseAddress;
            public int AllocationBase;
            public int AllocationProtect;
            public int RegionSize;
            public int State;
            public int Protect;
            public int lType;
        }

        public struct SYSTEM_INFO
        {
            public ushort processorArchitecture;
            ushort reserved;
            public uint pageSize;
            public IntPtr minimumApplicationAddress;
            public IntPtr maximumApplicationAddress;
            public IntPtr activeProcessorMask;
            public uint numberOfProcessors;
            public uint processorType;
            public uint allocationGranularity;
            public ushort processorLevel;
            public ushort processorRevision;
        }

        int spideyAddress = 0;
        int levelAddress = 0;
        private void btnFindPlayer_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtName.Text))
            {
                MessageBox.Show("Please enter a name");
                return;
            }

            myName = txtName.Text;

            // getting minimum & maximum address
            SYSTEM_INFO sys_info = new SYSTEM_INFO();
            GetSystemInfo(out sys_info);

            IntPtr proc_min_address = sys_info.minimumApplicationAddress;
            IntPtr proc_max_address = sys_info.maximumApplicationAddress;

            // saving the values as long ints so I won't have to do a lot of casts later
            long proc_min_address_l = (long)proc_min_address;
            long proc_max_address_l = (long)proc_max_address;

            Process process = Process.GetProcessesByName("DOSBox")[0];

            // opening the process with desired access level
            dosBoxProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_WM_READ, false, process.Id);

            // this will store any information we get from VirtualQueryEx()
            MEMORY_BASIC_INFORMATION mem_basic_info = new MEMORY_BASIC_INFORMATION();

            int bytesRead = 0;  // number of bytes read with ReadProcessMemory

            while (proc_min_address_l < proc_max_address_l)
            {
                // 28 = sizeof(MEMORY_BASIC_INFORMATION)
                VirtualQueryEx(dosBoxProcess, proc_min_address, out mem_basic_info, 28);

                // if this memory chunk is accessible
                if (mem_basic_info.Protect == PAGE_READWRITE && mem_basic_info.State == MEM_COMMIT)
                {
                    byte[] buffer = new byte[mem_basic_info.RegionSize];

                    // read everything in the buffer above
                    ReadProcessMemory((int)dosBoxProcess, mem_basic_info.BaseAddress, buffer, mem_basic_info.RegionSize, ref bytesRead);

                    var line = new StringBuilder();
                    // then output this in the file
                    for (int i = 0; i < mem_basic_info.RegionSize; i++)
                    {
                        //sw.WriteLine("0x{0} : {1}", (mem_basic_info.BaseAddress + i).ToString("X"), (char)buffer[i]);
                        line.Append(buffer[i].ToString("X2"));
                    }

                    int maryJaneIsDeadIndex = 0;
                    int creditsIndex = 0;

                    if ((maryJaneIsDeadIndex = line.ToString().IndexOf(MaryJaneIsDead)) > 0 &&
                        (creditsIndex = line.ToString().IndexOf(Credits)) > 0)
                    {
                        spideyAddress = mem_basic_info.BaseAddress + maryJaneIsDeadIndex / 2 + MaryJaneIsDead.Length / 2 + 32;
                        levelAddress = mem_basic_info.BaseAddress + creditsIndex / 2 + Credits.Length / 2 + 734;
                        break;
                    }
                }

                // move to the next memory chunk
                proc_min_address_l += mem_basic_info.RegionSize;
                proc_min_address = new IntPtr(proc_min_address_l);
            }

            if (spideyAddress > 0 && levelAddress > 0)
            {
                if (!string.IsNullOrEmpty(txtIP.Text))
                {
                    if (!IPAddress.TryParse(txtIP.Text, out ipAddress))
                    {
                        MessageBox.Show("Invalid IP Address");
                        return;
                    }

                    try
                    {
                        var ep = new IPEndPoint(ipAddress, 6012);
                        tcpClient = new TcpClient(ep);

                        Task.Factory.StartNew(ClientTask);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                        return;
                    }
                    isHosting = false;
                }
                else
                {
                    try
                    {
                        tcpServer = new TcpListener(IPAddress.Any, port);
                        tcpServer.Start();

                        Task.Factory.StartNew(ServerTask);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                        return;
                    }

                    playerNumber = 0;
                    isHosting = true;
                    btnStart.Enabled = true;
                }
                btnFindPlayer.Enabled = false;
                txtIP.Enabled = false;
                txtName.Enabled = false;
            }
            else
            {
                MessageBox.Show("Unable to find player, make sure spidey is in game");
            }
        }

        System.Threading.Timer memoryTimer;
        int port = 6012;
        private const byte SEND_NAME = 0x10;
        private const byte START_GAME = 0x11;
        bool isHosting = false;
        TcpListener tcpServer;
        TcpClient tcpClient;
        IPAddress ipAddress;
        private object arrayLock = new object();
        int playerNumber = 0;
        byte[] myLocation = new byte[31];
        string myName = "Player 1";
        string player2Name = "Player 2";
        string player3Name = "Player 3";
        private const string Start = "STARTTHEGAMEALREADY";

        private void ReadFromMemory(object state)
        {
            int bytesRead = 0;

            var spideyBuffer = new byte[6];
            ReadProcessMemory((int)dosBoxProcess, spideyAddress, spideyBuffer, 6, ref bytesRead);
            var left = (int)spideyBuffer[0];
            var leftScreen = (int)spideyBuffer[1];
            var right = (int)spideyBuffer[2];
            var rightScreen = (int)spideyBuffer[3];
            var top = (int)spideyBuffer[4];
            var bottom = (int)spideyBuffer[5];
            
            var levelBuffer = new byte[24];
            ReadProcessMemory((int)dosBoxProcess, levelAddress, levelBuffer, 24, ref bytesRead);
            

            string levelTitle = "";
            for (int i = 0; i < 24; i++)
            {
                levelTitle += (char)levelBuffer[i];
            }

            var spideyLeft = panel1.Left + ((left / 255.0) * (spideyRect.Right - spideyRect.Left) * 0.8);
            var spideyRight = panel1.Left + ((right / 255.0) * (spideyRect.Right - spideyRect.Left) * 0.8);
            var spideyTop = panel1.Top + (spideyRect.Bottom - spideyRect.Top) * 0.12 + ((top / 175.0) * (spideyRect.Bottom - spideyRect.Top) * 0.88);
            var spideyBottom = panel1.Top + (spideyRect.Bottom - spideyRect.Top) * 0.12 + ((bottom / 175.0) * (spideyRect.Bottom - spideyRect.Top) * 0.88);
            
            lblPlayer2Loc.BeginInvoke(new Action(() =>
            {
                lblPlayer2Loc.Text = levelTitle;
            }));

            player2Sprite.BeginInvoke(new Action(() =>
            {
                player2Sprite.Size = new Size((int)spideyRight - (int)spideyLeft, (int)spideyBottom - (int)spideyTop);
                player2Sprite.Left = (int)spideyLeft;
                player2Sprite.Top = (int)spideyTop;
            }));

            lock (clientsList)
            {
                foreach (var client in clientsList)
                {
                    var clientWriter = new StreamWriter(client.GetStream());
                    clientWriter.WriteLine(Start);
                    clientWriter.Flush();
                }
            }

            memoryTimer.Change(100, Timeout.Infinite);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Process process = Process.GetProcessesByName("DOSBox").FirstOrDefault();
            if (process != null)
                process.Kill();
            
            if (tsHost != null)
                tsHost.Cancel();
        }

        private void ClientTask()
        {
            try
            {
                var reader = new StreamReader(tcpClient.GetStream());
                var writer = new StreamWriter(tcpClient.GetStream());
                writer.WriteLine(txtName.Text);
                writer.Flush();

                playerNumber = int.Parse(reader.ReadLine());
                player2Name = reader.ReadLine();

                var gameStarted = false;
                while (!gameStarted)
                {
                    var line = reader.ReadLine();
                    if (line == Start)
                    {
                        memoryTimer = new System.Threading.Timer(ReadFromMemory, null, 0, Timeout.Infinite);
                        gameStarted = true;
                    }
                    else
                    {
                        player3Name = line;
                    }
                }

                while (true)
                {
                    // wait for signal from timer
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        volatile bool serverStart = false;
        int playerCounter = 0;
        List<TcpClient> clientsList = new List<TcpClient>();
        private object clientsListLock = new object();
        private void ServerTask()
        {
            try
            {
                while (true)
                {
                    var client = tcpServer.AcceptTcpClient();
                    if (serverStart)
                        break;
                    Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            var reader = new StreamReader(client.GetStream());
                            var writer = new StreamWriter(client.GetStream());
                            var clientName = reader.ReadLine();

                            Interlocked.Increment(ref playerCounter);
                            writer.WriteLine(playerCounter.ToString());
                            writer.WriteLine(myName);
                            writer.Flush();

                            lock (clientsList)
                            {
                                if (clientsList.Count == 0)
                                {
                                    player2Name = clientName;
                                }
                                else
                                {
                                    player3Name = clientName;
                                    writer.WriteLine(player2Name);
                                    writer.Flush();
                                }
                                foreach (var existingClients in clientsList)
                                {
                                    var existingClientWriter = new StreamWriter(existingClients.GetStream());
                                    existingClientWriter.WriteLine(clientName);
                                    existingClientWriter.Flush();
                                }
                                clientsList.Add(client);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                            lock (clientsList)
                            {
                                clientsList.Remove(client);
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        CancellationTokenSource tsHost;
        private void btnHost_Click(object sender, EventArgs e)
        {
            tsHost = new CancellationTokenSource();
            CancellationToken ct = tsHost.Token;
            Task.Factory.StartNew(() =>
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
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            btnStart.Enabled = false;
            try
            {
                lock (clientsList)
                {
                    serverStart = true;
                    foreach (var client in clientsList)
                    {
                        var clientWriter = new StreamWriter(client.GetStream());
                        clientWriter.WriteLine(Start);
                        clientWriter.Flush();
                    }
                }
                memoryTimer = new System.Threading.Timer(ReadFromMemory, null, 0, Timeout.Infinite);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            if (originalParent != IntPtr.Zero && spideyWindow != IntPtr.Zero)
            {
                SetParent(spideyWindow, originalParent);
            }

            if (originalWindowLong != 0 && spideyWindow != IntPtr.Zero)
            {
                SetWindowLong(spideyWindow, GWL_STYLE, originalWindowLong);
            }
        }
    }
}
