using System.Security.Cryptography;
using SpideyNet.Game;
using SpideyNet.Core;

namespace SpideyNet.Client;

internal sealed class MainWindow : Form
{
    private readonly Session session = new();
    private readonly TextBox endpoint = new() { Text = "ws://localhost:5080", Dock = DockStyle.Fill };
    private readonly TextBox room = new() { Text = Convert.ToHexString(RandomNumberGenerator.GetBytes(12)), Dock = DockStyle.Fill };
    private readonly TextBox name = new() { Text = Environment.UserName, MaxLength = 24, Dock = DockStyle.Fill };
    private readonly ComboBox games = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, DisplayMember = "Title" };
    private readonly ComboBox mode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox interactions = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox shared = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly Button connect = new() { Text = "Connect", AutoSize = true };
    private readonly Button disconnect = new() { Text = "Disconnect", AutoSize = true, Enabled = false };
    private readonly Button fire = new() { Text = "Fire web · V", AutoSize = true, Enabled = false };
    private readonly Button scan = new() { Text = "Refresh DOSBox", AutoSize = true };
    private readonly Label status = new() { Text = "Start SPIDEY.EXE in DOSBox and enter a room.", AutoSize = true };
    private readonly Label location = new() { AutoSize = true };
    private readonly ListView roster = new() { View = View.Details, FullRowSelect = true, Dock = DockStyle.Fill };
    private readonly System.Windows.Forms.Timer refresh = new() { Interval = 100 };
    private bool closing;

    internal MainWindow()
    {
        Text = "SpideyNet · DOS multiplayer";
        MinimumSize = new Size(720, 640);
        Size = new Size(860, 720);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(19, 24, 34);
        ForeColor = Color.WhiteSmoke;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 2, RowCount = 13 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(layout);
        var title = new Label { Text = "SPIDEYNET", Font = new Font("Segoe UI", 24, FontStyle.Bold), AutoSize = true };
        layout.Controls.Add(title, 0, 0);
        layout.SetColumnSpan(title, 2);
        AddField(layout, "Relay address", endpoint, 1);
        AddField(layout, "Room key", room, 2);
        AddField(layout, "Your name", name, 3);
        AddField(layout, "DOSBox", games, 4);
        mode.Items.AddRange([InjectionMode.Observe, InjectionMode.Experimental]);
        mode.SelectedItem = InjectionMode.Experimental;
        AddField(layout, "Rendering", mode, 5);
        interactions.Items.AddRange([InteractionMode.Disabled, InteractionMode.Experimental]);
        interactions.SelectedItem = InteractionMode.Disabled;
        AddField(layout, "Interactions", interactions, 6);
        shared.Items.AddRange([SharedMode.Independent, SharedMode.Experimental]);
        shared.SelectedItem = SharedMode.Experimental;
        AddField(layout, "Enemies/puzzles", shared, 7);

        var warning = new Label { Text = "Experimental injection can still crash DOSBox. Observe sends positions without writes.", AutoSize = true, ForeColor = Color.FromArgb(255, 195, 100) };
        layout.Controls.Add(warning, 0, 8);
        layout.SetColumnSpan(warning, 2);
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        var copy = new Button { Text = "Copy room key", AutoSize = true };
        fire.Click += (_, _) => session.FireWeb();
        buttons.Controls.AddRange([connect, disconnect, scan, copy, fire]);
        layout.Controls.Add(buttons, 0, 9);
        layout.SetColumnSpan(buttons, 2);
        layout.Controls.Add(status, 0, 10);
        layout.SetColumnSpan(status, 2);
        layout.Controls.Add(location, 0, 11);
        layout.SetColumnSpan(location, 2);
        roster.Columns.Add("Player", 220);
        roster.Columns.Add("Room", 80);
        roster.Columns.Add("Position", 160);
        layout.Controls.Add(roster, 0, 12);
        layout.SetColumnSpan(roster, 2);
        layout.RowStyles.Clear();
        for (var row = 0; row < 12; row++) { layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); }
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        scan.Click += (_, _) => Scan();
        copy.Click += (_, _) => Clipboard.SetText(room.Text);
        connect.Click += (_, _) => Connect();
        disconnect.Click += async (_, _) => { await session.DisconnectAsync(); SetConnected(ConnectionState.Disconnected); };
        refresh.Tick += (_, _) => PaintState();
        FormClosing += CloseAsync;
        Scan();
        refresh.Start();
    }

    private static void AddField(TableLayoutPanel layout, string caption, Control control, int row)
    {
        layout.Controls.Add(new Label { Text = caption, AutoSize = true, Margin = new Padding(0, 8, 8, 8) }, 0, row);
        layout.Controls.Add(control, 1, row);
    }

    private void Scan()
    {
        games.DataSource = GameProcess.Find();
    }

    private void Connect()
    {
        try
        {
            if (games.SelectedItem is not GameProcess game) { throw new InvalidOperationException("Launch DOSBox, then refresh."); }
            var interactionMode = (InteractionMode)interactions.SelectedItem!;
            var sharedMode = (SharedMode)shared.SelectedItem!;
            if (sharedMode == SharedMode.Experimental && (InjectionMode)mode.SelectedItem! != InjectionMode.Experimental)
            {
                throw new ArgumentException("Shared enemies and puzzles require experimental injection.");
            }
            if (interactionMode == InteractionMode.Experimental && (InjectionMode)mode.SelectedItem! != InjectionMode.Experimental)
            {
                throw new ArgumentException("Player interaction requires experimental injection.");
            }
            session.Connect(game.Id, new Uri(endpoint.Text.Trim()), room.Text.Trim(), name.Text.Trim(),
                (InjectionMode)mode.SelectedItem!, interactionMode, sharedMode);
            SetConnected(ConnectionState.Connected);
        }
        catch (Exception error) when (error is ArgumentException or UriFormatException or InvalidOperationException)
        {
            MessageBox.Show(this, error.Message, "Cannot connect", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void SetConnected(ConnectionState state)
    {
        var connected = state == ConnectionState.Connected;
        connect.Enabled = !connected;
        disconnect.Enabled = connected;
        // Keep the displayed process consistent with the active memory attachment.
        scan.Enabled = !connected;
        fire.Enabled = connected && (InteractionMode)interactions.SelectedItem! == InteractionMode.Experimental;
        foreach (var control in new Control[] { games, endpoint, room, name, mode, interactions, shared }) { control.Enabled = !connected; }
    }

    private void PaintState()
    {
        var view = session.View;
        status.Text = view.Status;
        var locationText = view.Local is null
            ? string.IsNullOrEmpty(view.Room) ? "Start a new game in DOSBox, then connect." : view.Room
            : $"{view.Room} · X {view.Local.X}, Y {view.Local.Y}";
        if (!string.IsNullOrEmpty(view.Shared))
        {
            locationText += $" · {view.Shared}";
        }

        // Assign the complete label once; removing and appending ownership made it blink.
        location.Text = locationText;
        roster.BeginUpdate();
        roster.Items.Clear();
        foreach (var player in view.Players)
        {
            roster.Items.Add(new ListViewItem([player.Name, player.Frame.Level.ToString(), $"{player.Frame.X}, {player.Frame.Y}"]));
        }
        roster.EndUpdate();
    }

    private async void CloseAsync(object? sender, FormClosingEventArgs args)
    {
        if (closing) { return; }
        args.Cancel = true;
        refresh.Stop();
        Enabled = false;
        await session.DisconnectAsync();
        refresh.Dispose();
        closing = true;
        Close();
    }
}
