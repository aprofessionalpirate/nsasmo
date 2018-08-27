﻿namespace MultiSpideyWinForms
{
    partial class MultiSpidey
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MultiSpidey));
            this.hostPanel = new System.Windows.Forms.Panel();
            this.lblPlayer1Name = new System.Windows.Forms.Label();
            this.player3Sprite = new System.Windows.Forms.PictureBox();
            this.player2Sprite = new System.Windows.Forms.PictureBox();
            this.txtIP = new System.Windows.Forms.TextBox();
            this.lblIP = new System.Windows.Forms.Label();
            this.lblPlayer2Name = new System.Windows.Forms.Label();
            this.btnStart = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.txtName = new System.Windows.Forms.TextBox();
            this.btnReset = new System.Windows.Forms.Button();
            this.btnHost = new System.Windows.Forms.Button();
            this.btnJoin = new System.Windows.Forms.Button();
            this.lblPlayer3Name = new System.Windows.Forms.Label();
            this.lblPlayer1Loc = new System.Windows.Forms.Label();
            this.lblPlayer2Loc = new System.Windows.Forms.Label();
            this.lblPlayer3Loc = new System.Windows.Forms.Label();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.lblLoadStatus = new System.Windows.Forms.ToolStripStatusLabel();
            ((System.ComponentModel.ISupportInitialize)(this.player3Sprite)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.player2Sprite)).BeginInit();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // hostPanel
            // 
            this.hostPanel.BackColor = System.Drawing.SystemColors.Info;
            this.hostPanel.Location = new System.Drawing.Point(111, 83);
            this.hostPanel.Margin = new System.Windows.Forms.Padding(2);
            this.hostPanel.Name = "hostPanel";
            this.hostPanel.Size = new System.Drawing.Size(427, 260);
            this.hostPanel.TabIndex = 0;
            // 
            // lblPlayer1Name
            // 
            this.lblPlayer1Name.AutoSize = true;
            this.lblPlayer1Name.Location = new System.Drawing.Point(118, 13);
            this.lblPlayer1Name.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblPlayer1Name.Name = "lblPlayer1Name";
            this.lblPlayer1Name.Size = new System.Drawing.Size(45, 13);
            this.lblPlayer1Name.TabIndex = 3;
            this.lblPlayer1Name.Text = "Player 1";
            // 
            // player3Sprite
            // 
            this.player3Sprite.BackColor = System.Drawing.Color.Transparent;
            this.player3Sprite.Image = ((System.Drawing.Image)(resources.GetObject("player3Sprite.Image")));
            this.player3Sprite.Location = new System.Drawing.Point(514, 13);
            this.player3Sprite.Margin = new System.Windows.Forms.Padding(2);
            this.player3Sprite.Name = "player3Sprite";
            this.player3Sprite.Size = new System.Drawing.Size(24, 55);
            this.player3Sprite.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.player3Sprite.TabIndex = 4;
            this.player3Sprite.TabStop = false;
            // 
            // player2Sprite
            // 
            this.player2Sprite.BackColor = System.Drawing.Color.Transparent;
            this.player2Sprite.Image = ((System.Drawing.Image)(resources.GetObject("player2Sprite.Image")));
            this.player2Sprite.Location = new System.Drawing.Point(486, 13);
            this.player2Sprite.Margin = new System.Windows.Forms.Padding(2);
            this.player2Sprite.Name = "player2Sprite";
            this.player2Sprite.Size = new System.Drawing.Size(24, 55);
            this.player2Sprite.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.player2Sprite.TabIndex = 5;
            this.player2Sprite.TabStop = false;
            // 
            // txtIP
            // 
            this.txtIP.Location = new System.Drawing.Point(8, 211);
            this.txtIP.Margin = new System.Windows.Forms.Padding(2);
            this.txtIP.Name = "txtIP";
            this.txtIP.Size = new System.Drawing.Size(101, 20);
            this.txtIP.TabIndex = 8;
            this.txtIP.Text = "127.0.0.1";
            // 
            // lblIP
            // 
            this.lblIP.AutoSize = true;
            this.lblIP.Location = new System.Drawing.Point(5, 196);
            this.lblIP.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblIP.Name = "lblIP";
            this.lblIP.Size = new System.Drawing.Size(17, 13);
            this.lblIP.TabIndex = 9;
            this.lblIP.Text = "IP";
            // 
            // lblPlayer2Name
            // 
            this.lblPlayer2Name.AutoSize = true;
            this.lblPlayer2Name.Location = new System.Drawing.Point(118, 34);
            this.lblPlayer2Name.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblPlayer2Name.Name = "lblPlayer2Name";
            this.lblPlayer2Name.Size = new System.Drawing.Size(45, 13);
            this.lblPlayer2Name.TabIndex = 10;
            this.lblPlayer2Name.Text = "Player 2";
            // 
            // btnStart
            // 
            this.btnStart.Enabled = false;
            this.btnStart.Location = new System.Drawing.Point(6, 239);
            this.btnStart.Margin = new System.Windows.Forms.Padding(2);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(101, 28);
            this.btnStart.TabIndex = 11;
            this.btnStart.Text = "Start";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(5, 84);
            this.label1.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(35, 13);
            this.label1.TabIndex = 13;
            this.label1.Text = "Name";
            // 
            // txtName
            // 
            this.txtName.Location = new System.Drawing.Point(6, 99);
            this.txtName.Margin = new System.Windows.Forms.Padding(2);
            this.txtName.Name = "txtName";
            this.txtName.Size = new System.Drawing.Size(103, 20);
            this.txtName.TabIndex = 12;
            this.txtName.Text = "Spiderman";
            // 
            // btnReset
            // 
            this.btnReset.Location = new System.Drawing.Point(6, 315);
            this.btnReset.Margin = new System.Windows.Forms.Padding(2);
            this.btnReset.Name = "btnReset";
            this.btnReset.Size = new System.Drawing.Size(101, 27);
            this.btnReset.TabIndex = 14;
            this.btnReset.Text = "Reset all";
            this.btnReset.UseVisualStyleBackColor = true;
            this.btnReset.Click += new System.EventHandler(this.btnReset_Click);
            // 
            // btnHost
            // 
            this.btnHost.Enabled = false;
            this.btnHost.Location = new System.Drawing.Point(6, 127);
            this.btnHost.Margin = new System.Windows.Forms.Padding(2);
            this.btnHost.Name = "btnHost";
            this.btnHost.Size = new System.Drawing.Size(101, 28);
            this.btnHost.TabIndex = 15;
            this.btnHost.Text = "Host";
            this.btnHost.UseVisualStyleBackColor = true;
            this.btnHost.Click += new System.EventHandler(this.btnHost_Click);
            // 
            // btnJoin
            // 
            this.btnJoin.Enabled = false;
            this.btnJoin.Location = new System.Drawing.Point(6, 159);
            this.btnJoin.Margin = new System.Windows.Forms.Padding(2);
            this.btnJoin.Name = "btnJoin";
            this.btnJoin.Size = new System.Drawing.Size(101, 28);
            this.btnJoin.TabIndex = 16;
            this.btnJoin.Text = "Join";
            this.btnJoin.UseVisualStyleBackColor = true;
            this.btnJoin.Click += new System.EventHandler(this.btnJoin_Click);
            // 
            // lblPlayer3Name
            // 
            this.lblPlayer3Name.AutoSize = true;
            this.lblPlayer3Name.Location = new System.Drawing.Point(118, 55);
            this.lblPlayer3Name.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblPlayer3Name.Name = "lblPlayer3Name";
            this.lblPlayer3Name.Size = new System.Drawing.Size(45, 13);
            this.lblPlayer3Name.TabIndex = 17;
            this.lblPlayer3Name.Text = "Player 3";
            // 
            // lblPlayer1Loc
            // 
            this.lblPlayer1Loc.AutoSize = true;
            this.lblPlayer1Loc.Location = new System.Drawing.Point(233, 13);
            this.lblPlayer1Loc.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblPlayer1Loc.Name = "lblPlayer1Loc";
            this.lblPlayer1Loc.Size = new System.Drawing.Size(61, 13);
            this.lblPlayer1Loc.TabIndex = 18;
            this.lblPlayer1Loc.Text = "Not Started";
            // 
            // lblPlayer2Loc
            // 
            this.lblPlayer2Loc.AutoSize = true;
            this.lblPlayer2Loc.Location = new System.Drawing.Point(233, 34);
            this.lblPlayer2Loc.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblPlayer2Loc.Name = "lblPlayer2Loc";
            this.lblPlayer2Loc.Size = new System.Drawing.Size(61, 13);
            this.lblPlayer2Loc.TabIndex = 19;
            this.lblPlayer2Loc.Text = "Not Started";
            // 
            // lblPlayer3Loc
            // 
            this.lblPlayer3Loc.AutoSize = true;
            this.lblPlayer3Loc.Location = new System.Drawing.Point(233, 55);
            this.lblPlayer3Loc.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblPlayer3Loc.Name = "lblPlayer3Loc";
            this.lblPlayer3Loc.Size = new System.Drawing.Size(61, 13);
            this.lblPlayer3Loc.TabIndex = 20;
            this.lblPlayer3Loc.Text = "Not Started";
            // 
            // statusStrip
            // 
            this.statusStrip.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblLoadStatus});
            this.statusStrip.Location = new System.Drawing.Point(0, 478);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Padding = new System.Windows.Forms.Padding(1, 0, 9, 0);
            this.statusStrip.Size = new System.Drawing.Size(710, 22);
            this.statusStrip.TabIndex = 21;
            // 
            // lblLoadStatus
            // 
            this.lblLoadStatus.Name = "lblLoadStatus";
            this.lblLoadStatus.Size = new System.Drawing.Size(0, 17);
            // 
            // MultiSpidey
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(710, 500);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.lblPlayer3Loc);
            this.Controls.Add(this.lblPlayer2Loc);
            this.Controls.Add(this.lblPlayer1Loc);
            this.Controls.Add(this.lblPlayer3Name);
            this.Controls.Add(this.btnJoin);
            this.Controls.Add(this.btnHost);
            this.Controls.Add(this.btnReset);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.txtName);
            this.Controls.Add(this.btnStart);
            this.Controls.Add(this.lblPlayer2Name);
            this.Controls.Add(this.lblIP);
            this.Controls.Add(this.txtIP);
            this.Controls.Add(this.player2Sprite);
            this.Controls.Add(this.player3Sprite);
            this.Controls.Add(this.lblPlayer1Name);
            this.Controls.Add(this.hostPanel);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "MultiSpidey";
            this.Text = "The Not So Amazing Multiplayer Spiderman";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.Shown += new System.EventHandler(this.MultiSpidey_Shown);
            ((System.ComponentModel.ISupportInitialize)(this.player3Sprite)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.player2Sprite)).EndInit();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Panel hostPanel;
        private System.Windows.Forms.Label lblPlayer1Name;
        private System.Windows.Forms.PictureBox player3Sprite;
        private System.Windows.Forms.PictureBox player2Sprite;
        private System.Windows.Forms.TextBox txtIP;
        private System.Windows.Forms.Label lblIP;
        private System.Windows.Forms.Label lblPlayer2Name;
        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtName;
        private System.Windows.Forms.Button btnReset;
        private System.Windows.Forms.Button btnHost;
        private System.Windows.Forms.Button btnJoin;
        private System.Windows.Forms.Label lblPlayer3Name;
        private System.Windows.Forms.Label lblPlayer1Loc;
        private System.Windows.Forms.Label lblPlayer2Loc;
        private System.Windows.Forms.Label lblPlayer3Loc;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel lblLoadStatus;
    }
}

