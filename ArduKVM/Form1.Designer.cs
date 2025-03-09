namespace ArduKVM
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            cbPort1 = new ComboBox();
            btnApply = new Button();
            notifyIcon = new NotifyIcon(components);
            workerSerial = new System.ComponentModel.BackgroundWorker();
            timerPps = new System.Windows.Forms.Timer(components);
            timerInput = new System.Windows.Forms.Timer(components);
            workerMouse = new System.ComponentModel.BackgroundWorker();
            cbInput1 = new ComboBox();
            label1 = new Label();
            label2 = new Label();
            cbInput2 = new ComboBox();
            cbPort2 = new ComboBox();
            label3 = new Label();
            cbInput3 = new ComboBox();
            cbPort3 = new ComboBox();
            label4 = new Label();
            cbInput4 = new ComboBox();
            cbPort4 = new ComboBox();
            cbCheckInputChanged = new CheckBox();
            SuspendLayout();
            // 
            // cbPort1
            // 
            cbPort1.DropDownStyle = ComboBoxStyle.DropDownList;
            cbPort1.Enabled = false;
            cbPort1.FormattingEnabled = true;
            cbPort1.Location = new Point(211, 12);
            cbPort1.Name = "cbPort1";
            cbPort1.Size = new Size(151, 27);
            cbPort1.TabIndex = 0;
            // 
            // btnApply
            // 
            btnApply.Location = new Point(22, 181);
            btnApply.Name = "btnApply";
            btnApply.Size = new Size(340, 38);
            btnApply.TabIndex = 2;
            btnApply.Text = "Apply";
            btnApply.UseVisualStyleBackColor = true;
            btnApply.Click += btnApply_Click;
            // 
            // notifyIcon
            // 
            notifyIcon.Icon = (Icon)resources.GetObject("notifyIcon.Icon");
            notifyIcon.Text = "ArduKVM";
            notifyIcon.Visible = true;
            notifyIcon.DoubleClick += notifyIcon_DoubleClick;
            // 
            // workerSerial
            // 
            workerSerial.DoWork += workerConnect_DoWork;
            // 
            // timerPps
            // 
            timerPps.Interval = 4;
            timerPps.Tick += timerPps_Tick;
            // 
            // timerInput
            // 
            timerInput.Enabled = true;
            timerInput.Interval = 1000;
            timerInput.Tick += timerInput_Tick;
            // 
            // workerMouse
            // 
            workerMouse.DoWork += workerMouse_DoWork;
            // 
            // cbInput1
            // 
            cbInput1.DropDownStyle = ComboBoxStyle.DropDownList;
            cbInput1.Enabled = false;
            cbInput1.FormattingEnabled = true;
            cbInput1.Location = new Point(22, 12);
            cbInput1.Name = "cbInput1";
            cbInput1.Size = new Size(151, 27);
            cbInput1.TabIndex = 3;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(179, 15);
            label1.Name = "label1";
            label1.Size = new Size(26, 19);
            label1.TabIndex = 4;
            label1.Text = "->";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(179, 51);
            label2.Name = "label2";
            label2.Size = new Size(26, 19);
            label2.TabIndex = 7;
            label2.Text = "->";
            // 
            // cbInput2
            // 
            cbInput2.DropDownStyle = ComboBoxStyle.DropDownList;
            cbInput2.Enabled = false;
            cbInput2.FormattingEnabled = true;
            cbInput2.Location = new Point(22, 48);
            cbInput2.Name = "cbInput2";
            cbInput2.Size = new Size(151, 27);
            cbInput2.TabIndex = 6;
            // 
            // cbPort2
            // 
            cbPort2.DropDownStyle = ComboBoxStyle.DropDownList;
            cbPort2.Enabled = false;
            cbPort2.FormattingEnabled = true;
            cbPort2.Location = new Point(211, 48);
            cbPort2.Name = "cbPort2";
            cbPort2.Size = new Size(151, 27);
            cbPort2.TabIndex = 5;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(179, 86);
            label3.Name = "label3";
            label3.Size = new Size(26, 19);
            label3.TabIndex = 10;
            label3.Text = "->";
            // 
            // cbInput3
            // 
            cbInput3.DropDownStyle = ComboBoxStyle.DropDownList;
            cbInput3.Enabled = false;
            cbInput3.FormattingEnabled = true;
            cbInput3.Location = new Point(22, 83);
            cbInput3.Name = "cbInput3";
            cbInput3.Size = new Size(151, 27);
            cbInput3.TabIndex = 9;
            // 
            // cbPort3
            // 
            cbPort3.DropDownStyle = ComboBoxStyle.DropDownList;
            cbPort3.Enabled = false;
            cbPort3.FormattingEnabled = true;
            cbPort3.Location = new Point(211, 83);
            cbPort3.Name = "cbPort3";
            cbPort3.Size = new Size(151, 27);
            cbPort3.TabIndex = 8;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(179, 120);
            label4.Name = "label4";
            label4.Size = new Size(26, 19);
            label4.TabIndex = 13;
            label4.Text = "->";
            // 
            // cbInput4
            // 
            cbInput4.DropDownStyle = ComboBoxStyle.DropDownList;
            cbInput4.Enabled = false;
            cbInput4.FormattingEnabled = true;
            cbInput4.Location = new Point(22, 117);
            cbInput4.Name = "cbInput4";
            cbInput4.Size = new Size(151, 27);
            cbInput4.TabIndex = 12;
            // 
            // cbPort4
            // 
            cbPort4.DropDownStyle = ComboBoxStyle.DropDownList;
            cbPort4.Enabled = false;
            cbPort4.FormattingEnabled = true;
            cbPort4.Location = new Point(211, 117);
            cbPort4.Name = "cbPort4";
            cbPort4.Size = new Size(151, 27);
            cbPort4.TabIndex = 11;
            // 
            // cbCheckInputChanged
            // 
            cbCheckInputChanged.AutoSize = true;
            cbCheckInputChanged.Location = new Point(22, 152);
            cbCheckInputChanged.Name = "cbCheckInputChanged";
            cbCheckInputChanged.Size = new Size(217, 23);
            cbCheckInputChanged.TabIndex = 14;
            cbCheckInputChanged.Text = "Check input change events";
            cbCheckInputChanged.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(9F, 19F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(382, 231);
            Controls.Add(cbCheckInputChanged);
            Controls.Add(label4);
            Controls.Add(cbInput4);
            Controls.Add(cbPort4);
            Controls.Add(label3);
            Controls.Add(cbInput3);
            Controls.Add(cbPort3);
            Controls.Add(label2);
            Controls.Add(cbInput2);
            Controls.Add(cbPort2);
            Controls.Add(label1);
            Controls.Add(cbInput1);
            Controls.Add(btnApply);
            Controls.Add(cbPort1);
            Name = "Form1";
            Text = "Form1";
            FormClosing += Form1_FormClosing;
            Load += Form1_Load;
            Shown += Form1_Shown;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private ComboBox cbPort1;
        private Button btnApply;
        private NotifyIcon notifyIcon;
        private System.ComponentModel.BackgroundWorker workerSerial;
        private System.Windows.Forms.Timer timerPps;
        private System.Windows.Forms.Timer timerInput;
        private System.ComponentModel.BackgroundWorker workerMouse;
        private ComboBox cbInput1;
        private Label label1;
        private Label label2;
        private ComboBox cbInput2;
        private ComboBox cbPort2;
        private Label label3;
        private ComboBox cbInput3;
        private ComboBox cbPort3;
        private Label label4;
        private ComboBox cbInput4;
        private ComboBox cbPort4;
        private CheckBox cbCheckInputChanged;
    }
}
