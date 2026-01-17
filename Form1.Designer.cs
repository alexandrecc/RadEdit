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
            toolStripButtons = new ToolStrip();
            toolStripBoldButton = new ToolStripButton();
            toolStripUnderlineButton = new ToolStripButton();
            toolStripItalicButton = new ToolStripButton();
            toolStripPopHtmlButton = new ToolStripButton();
            toolStripPopRtfButton = new ToolStripButton();
            splitContainer1 = new SplitContainer();
            richTextBox1 = new RichTextBox();
            panelHtmlBar = new Panel();
            panelHtmlHost = new Panel();
            buttonHtmlBack = new Button();
            buttonHtmlForward = new Button();
            textBoxHtmlUrl = new TextBox();
            buttonHtmlBrowse = new Button();
            buttonHtmlClear = new Button();
            buttonHtmlGo = new Button();
            webView2 = new Microsoft.Web.WebView2.WinForms.WebView2();
            toolStrip1.SuspendLayout();
            toolStripButtons.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)webView2).BeginInit();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            panelHtmlBar.SuspendLayout();
            SuspendLayout();
            // 
            // toolStrip1
            // 
            toolStrip1.Dock = DockStyle.Top;
            toolStrip1.GripStyle = ToolStripGripStyle.Hidden;
            toolStrip1.ImageScalingSize = new Size(24, 24);
            toolStrip1.Items.AddRange(new ToolStripItem[] { toolStripTitleLabel, toolStripNameLabel });
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
            // toolStripButtons
            // 
            toolStripButtons.Dock = DockStyle.Top;
            toolStripButtons.GripStyle = ToolStripGripStyle.Hidden;
            toolStripButtons.ImageScalingSize = new Size(24, 24);
            toolStripButtons.Items.AddRange(new ToolStripItem[] { toolStripBoldButton, toolStripItalicButton, toolStripUnderlineButton, toolStripPopHtmlButton, toolStripPopRtfButton });
            toolStripButtons.Location = new Point(0, 27);
            toolStripButtons.Name = "toolStripButtons";
            toolStripButtons.Padding = new Padding(4, 0, 1, 0);
            toolStripButtons.Size = new Size(800, 27);
            toolStripButtons.TabIndex = 1;
            toolStripButtons.Text = "toolStripButtons";
            // 
            // toolStripBoldButton
            // 
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
            toolStripItalicButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
            toolStripItalicButton.Font = new Font("Segoe UI", 9F, FontStyle.Italic);
            toolStripItalicButton.Name = "toolStripItalicButton";
            toolStripItalicButton.Size = new Size(23, 24);
            toolStripItalicButton.Text = "I";
            toolStripItalicButton.ToolTipText = "Toggle italics";
            toolStripItalicButton.Click += ToolStripItalicButton_Click;
            // 
            // toolStripPopHtmlButton
            // 
            toolStripPopHtmlButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
            toolStripPopHtmlButton.Name = "toolStripPopHtmlButton";
            toolStripPopHtmlButton.Size = new Size(63, 24);
            toolStripPopHtmlButton.Text = "Pop HTML";
            toolStripPopHtmlButton.ToolTipText = "Pop out the HTML view";
            toolStripPopHtmlButton.Click += ToolStripPopHtmlButton_Click;
            // 
            // toolStripPopRtfButton
            // 
            toolStripPopRtfButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
            toolStripPopRtfButton.Name = "toolStripPopRtfButton";
            toolStripPopRtfButton.Size = new Size(57, 24);
            toolStripPopRtfButton.Text = "Pop RTF";
            toolStripPopRtfButton.ToolTipText = "Pop out the RichText view";
            toolStripPopRtfButton.Click += ToolStripPopRtfButton_Click;
            // 
            // splitContainer1
            // 
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.Location = new Point(0, 54);
            splitContainer1.Name = "splitContainer1";
            splitContainer1.Orientation = Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(richTextBox1);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(panelHtmlHost);
            splitContainer1.Panel2.Controls.Add(panelHtmlBar);
            splitContainer1.Size = new Size(800, 396);
            splitContainer1.SplitterDistance = 198;
            splitContainer1.TabIndex = 2;
            // 
            // richTextBox1
            // 
            richTextBox1.AcceptsTab = true;
            richTextBox1.DetectUrls = false;
            richTextBox1.Dock = DockStyle.Fill;
            richTextBox1.HideSelection = false;
            richTextBox1.Location = new Point(0, 0);
            richTextBox1.Name = "richTextBox1";
            richTextBox1.ScrollBars = RichTextBoxScrollBars.Vertical;
            richTextBox1.Size = new Size(800, 198);
            richTextBox1.TabIndex = 0;
            richTextBox1.Text = "";
            // 
            // panelHtmlBar
            // 
            panelHtmlBar.Controls.Add(buttonHtmlGo);
            panelHtmlBar.Controls.Add(buttonHtmlClear);
            panelHtmlBar.Controls.Add(buttonHtmlBrowse);
            panelHtmlBar.Controls.Add(textBoxHtmlUrl);
            panelHtmlBar.Controls.Add(buttonHtmlForward);
            panelHtmlBar.Controls.Add(buttonHtmlBack);
            panelHtmlBar.Dock = DockStyle.Top;
            panelHtmlBar.Location = new Point(0, 0);
            panelHtmlBar.Name = "panelHtmlBar";
            panelHtmlBar.Padding = new Padding(6, 3, 6, 3);
            panelHtmlBar.Size = new Size(800, 30);
            panelHtmlBar.TabIndex = 0;
            // 
            // buttonHtmlBack
            // 
            buttonHtmlBack.Dock = DockStyle.Left;
            buttonHtmlBack.Enabled = false;
            buttonHtmlBack.Location = new Point(6, 3);
            buttonHtmlBack.Name = "buttonHtmlBack";
            buttonHtmlBack.Size = new Size(32, 24);
            buttonHtmlBack.TabIndex = 0;
            buttonHtmlBack.Text = "<";
            buttonHtmlBack.UseVisualStyleBackColor = true;
            buttonHtmlBack.Click += ButtonHtmlBack_Click;
            // 
            // buttonHtmlForward
            // 
            buttonHtmlForward.Dock = DockStyle.Left;
            buttonHtmlForward.Enabled = false;
            buttonHtmlForward.Location = new Point(38, 3);
            buttonHtmlForward.Name = "buttonHtmlForward";
            buttonHtmlForward.Size = new Size(32, 24);
            buttonHtmlForward.TabIndex = 1;
            buttonHtmlForward.Text = ">";
            buttonHtmlForward.UseVisualStyleBackColor = true;
            buttonHtmlForward.Click += ButtonHtmlForward_Click;
            // 
            // panelHtmlHost
            // 
            panelHtmlHost.Controls.Add(webView2);
            panelHtmlHost.Dock = DockStyle.Fill;
            panelHtmlHost.Location = new Point(0, 30);
            panelHtmlHost.Name = "panelHtmlHost";
            panelHtmlHost.Size = new Size(800, 164);
            panelHtmlHost.TabIndex = 1;
            // 
            // textBoxHtmlUrl
            // 
            textBoxHtmlUrl.Dock = DockStyle.Fill;
            textBoxHtmlUrl.Location = new Point(70, 3);
            textBoxHtmlUrl.Name = "textBoxHtmlUrl";
            textBoxHtmlUrl.PlaceholderText = "Enter URL or HTML file path";
            textBoxHtmlUrl.Size = new Size(528, 23);
            textBoxHtmlUrl.TabIndex = 2;
            textBoxHtmlUrl.KeyDown += TextBoxHtmlUrl_KeyDown;
            // 
            // buttonHtmlBrowse
            // 
            buttonHtmlBrowse.Dock = DockStyle.Right;
            buttonHtmlBrowse.Location = new Point(598, 3);
            buttonHtmlBrowse.Name = "buttonHtmlBrowse";
            buttonHtmlBrowse.Size = new Size(68, 24);
            buttonHtmlBrowse.TabIndex = 3;
            buttonHtmlBrowse.Text = "Open";
            buttonHtmlBrowse.UseVisualStyleBackColor = true;
            buttonHtmlBrowse.Click += ButtonHtmlBrowse_Click;
            // 
            // buttonHtmlClear
            // 
            buttonHtmlClear.Dock = DockStyle.Right;
            buttonHtmlClear.Location = new Point(666, 3);
            buttonHtmlClear.Name = "buttonHtmlClear";
            buttonHtmlClear.Size = new Size(64, 24);
            buttonHtmlClear.TabIndex = 4;
            buttonHtmlClear.Text = "Clear";
            buttonHtmlClear.UseVisualStyleBackColor = true;
            buttonHtmlClear.Click += ButtonHtmlClear_Click;
            // 
            // buttonHtmlGo
            // 
            buttonHtmlGo.Dock = DockStyle.Right;
            buttonHtmlGo.Location = new Point(730, 3);
            buttonHtmlGo.Name = "buttonHtmlGo";
            buttonHtmlGo.Size = new Size(64, 24);
            buttonHtmlGo.TabIndex = 5;
            buttonHtmlGo.Text = "Go";
            buttonHtmlGo.UseVisualStyleBackColor = true;
            buttonHtmlGo.Click += ButtonHtmlGo_Click;
            // 
            // webView2
            // 
            webView2.AllowExternalDrop = true;
            webView2.CreationProperties = null;
            webView2.DefaultBackgroundColor = Color.White;
            webView2.Dock = DockStyle.Fill;
            webView2.Location = new Point(0, 0);
            webView2.Name = "webView2";
            webView2.Size = new Size(800, 164);
            webView2.TabIndex = 1;
            webView2.ZoomFactor = 1D;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(splitContainer1);
            Controls.Add(toolStripButtons);
            Controls.Add(toolStrip1);
            Name = "Form1";
            Text = "RadEdit";
            toolStrip1.ResumeLayout(false);
            toolStrip1.PerformLayout();
            toolStripButtons.ResumeLayout(false);
            toolStripButtons.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)webView2).EndInit();
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            panelHtmlBar.ResumeLayout(false);
            panelHtmlBar.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private ToolStrip toolStrip1;
        private ToolStripLabel toolStripTitleLabel;
        private ToolStripLabel toolStripNameLabel;
        private ToolStrip toolStripButtons;
        private ToolStripButton toolStripBoldButton;
        private ToolStripButton toolStripUnderlineButton;
        private ToolStripButton toolStripItalicButton;
        private ToolStripButton toolStripPopHtmlButton;
        private ToolStripButton toolStripPopRtfButton;
        private SplitContainer splitContainer1;
        private RichTextBox richTextBox1;
        private Panel panelHtmlBar;
        private Panel panelHtmlHost;
        private Button buttonHtmlBack;
        private Button buttonHtmlForward;
        private TextBox textBoxHtmlUrl;
        private Button buttonHtmlBrowse;
        private Button buttonHtmlClear;
        private Button buttonHtmlGo;
        private Microsoft.Web.WebView2.WinForms.WebView2 webView2;
    }
}
