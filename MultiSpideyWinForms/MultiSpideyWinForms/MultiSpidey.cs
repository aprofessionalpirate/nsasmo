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

// TTD
// Rewrite this class to properly do UDP connections
// Fix workaround for getting width/height
// Use SetWindowPos instead of MoveWindow
// Interpolation of player position

namespace MultiSpideyWinForms
{
    public partial class MultiSpidey : Form
    {
        private const int Port = 6015;

        private SpideyWindow spideyWindow;

        private volatile bool _requestTimerStop = false;
        private Timer memoryTimer;
        private UdpClient udpClient;

        private SemaphoreSlim signalToStartHosting = new SemaphoreSlim(0, 1);
        private volatile bool serverStarted = false;
        private IPAddress serverIp;

        private int playerNumber = 1;
        private int players = 1;
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
            if (WindowManager.AttachSpideyWindow(hostPanel.Handle, out SpideyWindow spideyWindow))
            {
                btnFindPlayer.Enabled = true;
                btnFindSpidey.Enabled = false;

                // Make panel big enough for spidey window
                // Must be done after attaching window otherwise it will have the incorrect size in high DPI displays
                hostPanel.Size = new Size(spideyWindow.BorderlessWidth, spideyWindow.BorderlessHeight);
            }
            else
            {
                MessageBox.Show("Unable to attach spidey window");
            }
        }
        
        protected override void OnMove(EventArgs e)
        {
            WindowManager.UpdateSpideyWindow(spideyWindow);
            Invalidate();
            base.OnMove(e);
        }

        protected override void OnResize(EventArgs e)
        {
            WindowManager.UpdateSpideyWindow(spideyWindow);
            Invalidate();
            base.OnResize(e);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            _requestTimerStop = true;
            if (WindowManager.DetachSpideyWindow(spideyWindow))
            {
                spideyWindow = null;
            }

            base.OnHandleDestroyed(e);
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            if (WindowManager.DetachSpideyWindow(spideyWindow))
            {
                spideyWindow = null;
            }
            btnFindPlayer.Enabled = false;
            btnFindSpidey.Enabled = true;
        }

        // TODO - should just do this on startup
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
                if (!MemoryScanner.GetMemoryAddresses(this, spideyWindow.Handle))
                {
                    Invoke(new Action(() => { MessageBox.Show("Unable to find player, make sure spidey is in game"); }));
                    success.Report(false);
                    return;
                }
                success.Report(true);
            });
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
                        players = 2;
                        player2Name = clientName;
                        Invoke(new Action(() => { lblPlayer2Name.Text = player2Name; }));
                    }
                    else if (playerCounter == 3)
                    {
                        players = 3;
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
                udpClient = new UdpClient();
                udpClient.Client.ReceiveTimeout = 5000;
                udpClient.Connect(serverIp, Port);
                memoryTimer = new Timer(ReadFromMemory);
                memoryTimer.Change(0, Timeout.Infinite);
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
            memoryTimer = new Timer(ReadFromMemory);
            memoryTimer.Change(0, Timeout.Infinite);
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
            }
        }

        private void SetPlayerPosition(int clientPlayerNumber, byte[] position, byte[] location)
        {
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

            var spideyLeft = hostPanel.Left + ((left / 255.0) * spideyWindow.BorderlessWidth * 0.8);
            var spideyRight = hostPanel.Left + ((right / 255.0) * spideyWindow.BorderlessWidth * 0.8);
            var spideyTop = hostPanel.Top + spideyWindow.BorderlessHeight * 0.12 + ((top / 175.0) * spideyWindow.BorderlessHeight * 0.88);
            var spideyBottom = hostPanel.Top + spideyWindow.BorderlessHeight * 0.12 + ((bottom / 175.0) * spideyWindow.BorderlessHeight * 0.88);
            
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
