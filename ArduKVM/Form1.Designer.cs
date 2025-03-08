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
            cbPorts = new ComboBox();
            label1 = new Label();
            btnApply = new Button();
            notifyIcon = new NotifyIcon(components);
            workerSerial = new System.ComponentModel.BackgroundWorker();
            timerPps = new System.Windows.Forms.Timer(components);
            timerInput = new System.Windows.Forms.Timer(components);
            workerMouse = new System.ComponentModel.BackgroundWorker();
            SuspendLayout();
            // 
            // cbPorts
            // 
            cbPorts.DropDownStyle = ComboBoxStyle.DropDownList;
            cbPorts.FormattingEnabled = true;
            cbPorts.Location = new Point(63, 12);
            cbPorts.Name = "cbPorts";
            cbPorts.Size = new Size(151, 27);
            cbPorts.TabIndex = 0;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(12, 15);
            label1.Name = "label1";
            label1.Size = new Size(45, 19);
            label1.TabIndex = 1;
            label1.Text = "Port: ";
            // 
            // btnApply
            // 
            btnApply.Location = new Point(234, 10);
            btnApply.Name = "btnApply";
            btnApply.Size = new Size(94, 29);
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
            // Form1
            // 
            AutoScaleDimensions = new SizeF(9F, 19F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(338, 55);
            Controls.Add(btnApply);
            Controls.Add(label1);
            Controls.Add(cbPorts);
            Name = "Form1";
            Text = "Form1";
            FormClosing += Form1_FormClosing;
            Load += Form1_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private ComboBox cbPorts;
        private Label label1;
        private Button btnApply;
        private NotifyIcon notifyIcon;
        private System.ComponentModel.BackgroundWorker workerSerial;
        private System.Windows.Forms.Timer timerPps;
        private System.Windows.Forms.Timer timerInput;
        private System.ComponentModel.BackgroundWorker workerMouse;
    }
}
