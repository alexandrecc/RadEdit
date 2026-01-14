namespace RadEdit
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
            toolStrip1 = new ToolStrip();
            toolStripTitleLabel = new ToolStripLabel();
            toolStripNameLabel = new ToolStripLabel();
            toolStripBoldButton = new ToolStripButton();
            toolStripUnderlineButton = new ToolStripButton();
            toolStripItalicButton = new ToolStripButton();
            richTextBox1 = new RichTextBox();
            webView2 = new Microsoft.Web.WebView2.WinForms.WebView2();
            toolStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)webView2).BeginInit();
            SuspendLayout();
            // 
            // toolStrip1
            // 
            toolStrip1.GripStyle = ToolStripGripStyle.Hidden;
            toolStrip1.ImageScalingSize = new Size(24, 24);
            toolStrip1.Items.AddRange(new ToolStripItem[] { toolStripTitleLabel, toolStripNameLabel, toolStripItalicButton, toolStripUnderlineButton, toolStripBoldButton });
            toolStrip1.Location = new Point(0, 0);
            toolStrip1.Name = "toolStrip1";
            toolStrip1.Padding = new Padding(4, 0, 1, 0);
            toolStrip1.Size = new Size(800, 27);
            toolStrip1.TabIndex = 0;
            toolStrip1.Text = "toolStrip1";
            // 
            // toolStripTitleLabel
            // 
            toolStripTitleLabel.AutoSize = false;
            toolStripTitleLabel.Margin = new Padding(5, 1, 10, 2); 
            toolStripTitleLabel.Name = "toolStripTitleLabel";
            toolStripTitleLabel.Size = new Size(130, 24); 
            toolStripTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
            toolStripTitleLabel.ToolTipText = "Current title";
            // 
            // toolStripNameLabel
            // 
            toolStripNameLabel.AutoSize = false;
            toolStripNameLabel.Margin = new Padding(0, 1, 10, 2);
            toolStripNameLabel.Name = "toolStripNameLabel";
            toolStripNameLabel.Size = new Size(360, 24);  
            toolStripNameLabel.TextAlign = ContentAlignment.MiddleCenter;
            toolStripNameLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            toolStripNameLabel.ToolTipText = "Current name";
            // 
            // toolStripBoldButton
            // 
            toolStripBoldButton.Alignment = ToolStripItemAlignment.Right;
            toolStripBoldButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
            toolStripBoldButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            toolStripBoldButton.Name = "toolStripBoldButton";
            toolStripBoldButton.Size = new Size(23, 24);
            toolStripBoldButton.Text = "B";
            toolStripBoldButton.ToolTipText = "Toggle bold";
            toolStripBoldButton.Click += ToolStripBoldButton_Click;
            // 
            // toolStripUnderlineButton
            // 
            toolStripUnderlineButton.Alignment = ToolStripItemAlignment.Right;
            toolStripUnderlineButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
            toolStripUnderlineButton.Font = new Font("Segoe UI", 9F, FontStyle.Underline);
            toolStripUnderlineButton.Name = "toolStripUnderlineButton";
            toolStripUnderlineButton.Size = new Size(23, 24);
            toolStripUnderlineButton.Text = "U";
            toolStripUnderlineButton.ToolTipText = "Toggle underline";
            toolStripUnderlineButton.Click += ToolStripUnderlineButton_Click;
            // 
            // toolStripItalicButton
            // 
            toolStripItalicButton.Alignment = ToolStripItemAlignment.Right;
            toolStripItalicButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
            toolStripItalicButton.Font = new Font("Segoe UI", 9F, FontStyle.Italic);
            toolStripItalicButton.Name = "toolStripItalicButton";
            toolStripItalicButton.Size = new Size(23, 24);
            toolStripItalicButton.Text = "I";
            toolStripItalicButton.ToolTipText = "Toggle italics";
            toolStripItalicButton.Click += ToolStripItalicButton_Click;
            // 
            // richTextBox1
            // 
            richTextBox1.AcceptsTab = true;
            richTextBox1.DetectUrls = false;
            richTextBox1.Dock = DockStyle.Fill;
            richTextBox1.HideSelection = false;
            richTextBox1.Location = new Point(0, 27);
            richTextBox1.Name = "richTextBox1";
            richTextBox1.ScrollBars = RichTextBoxScrollBars.Vertical;
            richTextBox1.Size = new Size(800, 423);
            richTextBox1.TabIndex = 1;
            richTextBox1.Text = "";
            // 
            // webView2
            // 
            webView2.AllowExternalDrop = true;
            webView2.CreationProperties = null;
            webView2.DefaultBackgroundColor = Color.White;
            webView2.Dock = DockStyle.Fill;
            webView2.Location = new Point(0, 27);
            webView2.Name = "webView2";
            webView2.Size = new Size(800, 423);
            webView2.TabIndex = 2;
            webView2.Visible = false;
            webView2.ZoomFactor = 1D;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(webView2);
            Controls.Add(richTextBox1);
            Controls.Add(toolStrip1);
            Name = "Form1";
            Text = "RadEdit";
            toolStrip1.ResumeLayout(false);
            toolStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)webView2).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private ToolStrip toolStrip1;
        private ToolStripLabel toolStripTitleLabel;
        private ToolStripLabel toolStripNameLabel;
        private ToolStripButton toolStripBoldButton;
        private ToolStripButton toolStripUnderlineButton;
        private ToolStripButton toolStripItalicButton;
        private RichTextBox richTextBox1;
        private Microsoft.Web.WebView2.WinForms.WebView2 webView2;
    }
}
