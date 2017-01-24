namespace MultiSpideyWinForms
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
            this.btnFindSpidey = new System.Windows.Forms.Button();
            this.btnFindPlayer = new System.Windows.Forms.Button();
            this.lblPlayer2Loc = new System.Windows.Forms.Label();
            this.player3Sprite = new System.Windows.Forms.PictureBox();
            this.player2Sprite = new System.Windows.Forms.PictureBox();
            this.txtIP = new System.Windows.Forms.TextBox();
            this.lblIP = new System.Windows.Forms.Label();
            this.lblPlayer3Loc = new System.Windows.Forms.Label();
            this.btnStart = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.txtName = new System.Windows.Forms.TextBox();
            this.btnReset = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.player3Sprite)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.player2Sprite)).BeginInit();
            this.SuspendLayout();
            // 
            // hostPanel
            // 
            this.hostPanel.BackColor = System.Drawing.SystemColors.Info;
            this.hostPanel.Location = new System.Drawing.Point(127, 127);
            this.hostPanel.Name = "hostPanel";
            this.hostPanel.Size = new System.Drawing.Size(640, 400);
            this.hostPanel.TabIndex = 0;
            // 
            // btnFindSpidey
            // 
            this.btnFindSpidey.Location = new System.Drawing.Point(9, 12);
            this.btnFindSpidey.Name = "btnFindSpidey";
            this.btnFindSpidey.Size = new System.Drawing.Size(152, 42);
            this.btnFindSpidey.TabIndex = 0;
            this.btnFindSpidey.Text = "Find Spidey";
            this.btnFindSpidey.UseVisualStyleBackColor = true;
            this.btnFindSpidey.Click += new System.EventHandler(this.btnFindSpidey_Click);
            // 
            // btnFindPlayer
            // 
            this.btnFindPlayer.Enabled = false;
            this.btnFindPlayer.Location = new System.Drawing.Point(9, 60);
            this.btnFindPlayer.Name = "btnFindPlayer";
            this.btnFindPlayer.Size = new System.Drawing.Size(152, 43);
            this.btnFindPlayer.TabIndex = 1;
            this.btnFindPlayer.Text = "Get Ready";
            this.btnFindPlayer.UseVisualStyleBackColor = true;
            this.btnFindPlayer.Click += new System.EventHandler(this.btnFindPlayer_Click);
            // 
            // lblPlayer2Loc
            // 
            this.lblPlayer2Loc.AutoSize = true;
            this.lblPlayer2Loc.Location = new System.Drawing.Point(412, 23);
            this.lblPlayer2Loc.Name = "lblPlayer2Loc";
            this.lblPlayer2Loc.Size = new System.Drawing.Size(65, 20);
            this.lblPlayer2Loc.TabIndex = 3;
            this.lblPlayer2Loc.Text = "Player 2";
            // 
            // player3Sprite
            // 
            this.player3Sprite.BackColor = System.Drawing.Color.Transparent;
            this.player3Sprite.Image = ((System.Drawing.Image)(resources.GetObject("player3Sprite.Image")));
            this.player3Sprite.Location = new System.Drawing.Point(51, 127);
            this.player3Sprite.Name = "player3Sprite";
            this.player3Sprite.Size = new System.Drawing.Size(36, 84);
            this.player3Sprite.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.player3Sprite.TabIndex = 4;
            this.player3Sprite.TabStop = false;
            // 
            // player2Sprite
            // 
            this.player2Sprite.BackColor = System.Drawing.Color.Transparent;
            this.player2Sprite.Image = ((System.Drawing.Image)(resources.GetObject("player2Sprite.Image")));
            this.player2Sprite.Location = new System.Drawing.Point(9, 127);
            this.player2Sprite.Name = "player2Sprite";
            this.player2Sprite.Size = new System.Drawing.Size(36, 84);
            this.player2Sprite.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.player2Sprite.TabIndex = 5;
            this.player2Sprite.TabStop = false;
            // 
            // txtIP
            // 
            this.txtIP.Location = new System.Drawing.Point(167, 35);
            this.txtIP.Name = "txtIP";
            this.txtIP.Size = new System.Drawing.Size(223, 26);
            this.txtIP.TabIndex = 8;
            // 
            // lblIP
            // 
            this.lblIP.AutoSize = true;
            this.lblIP.Location = new System.Drawing.Point(167, 12);
            this.lblIP.Name = "lblIP";
            this.lblIP.Size = new System.Drawing.Size(185, 20);
            this.lblIP.TabIndex = 9;
            this.lblIP.Text = "IP (leave blank if hosting)";
            // 
            // lblPlayer3Loc
            // 
            this.lblPlayer3Loc.AutoSize = true;
            this.lblPlayer3Loc.Location = new System.Drawing.Point(412, 60);
            this.lblPlayer3Loc.Name = "lblPlayer3Loc";
            this.lblPlayer3Loc.Size = new System.Drawing.Size(65, 20);
            this.lblPlayer3Loc.TabIndex = 10;
            this.lblPlayer3Loc.Text = "Player 3";
            // 
            // btnStart
            // 
            this.btnStart.Enabled = false;
            this.btnStart.Location = new System.Drawing.Point(550, 12);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(152, 43);
            this.btnStart.TabIndex = 11;
            this.btnStart.Text = "Start";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(717, 12);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(51, 20);
            this.label1.TabIndex = 13;
            this.label1.Text = "Name";
            // 
            // txtName
            // 
            this.txtName.Location = new System.Drawing.Point(721, 35);
            this.txtName.Name = "txtName";
            this.txtName.Size = new System.Drawing.Size(223, 26);
            this.txtName.TabIndex = 12;
            // 
            // btnReset
            // 
            this.btnReset.Location = new System.Drawing.Point(550, 61);
            this.btnReset.Name = "btnReset";
            this.btnReset.Size = new System.Drawing.Size(152, 42);
            this.btnReset.TabIndex = 14;
            this.btnReset.Text = "Reset all";
            this.btnReset.UseVisualStyleBackColor = true;
            this.btnReset.Click += new System.EventHandler(this.btnReset_Click);
            // 
            // MultiSpidey
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1065, 769);
            this.Controls.Add(this.btnReset);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.txtName);
            this.Controls.Add(this.btnStart);
            this.Controls.Add(this.lblPlayer3Loc);
            this.Controls.Add(this.lblIP);
            this.Controls.Add(this.txtIP);
            this.Controls.Add(this.player2Sprite);
            this.Controls.Add(this.player3Sprite);
            this.Controls.Add(this.lblPlayer2Loc);
            this.Controls.Add(this.btnFindPlayer);
            this.Controls.Add(this.btnFindSpidey);
            this.Controls.Add(this.hostPanel);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "MultiSpidey";
            this.Text = "The Not So Amazing Multiplayer Spiderman";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            ((System.ComponentModel.ISupportInitialize)(this.player3Sprite)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.player2Sprite)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Panel hostPanel;
        private System.Windows.Forms.Button btnFindSpidey;
        private System.Windows.Forms.Button btnFindPlayer;
        private System.Windows.Forms.Label lblPlayer2Loc;
        private System.Windows.Forms.PictureBox player3Sprite;
        private System.Windows.Forms.PictureBox player2Sprite;
        private System.Windows.Forms.TextBox txtIP;
        private System.Windows.Forms.Label lblIP;
        private System.Windows.Forms.Label lblPlayer3Loc;
        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtName;
        private System.Windows.Forms.Button btnReset;
    }
}

