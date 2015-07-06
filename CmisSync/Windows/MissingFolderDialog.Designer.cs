namespace CmisSync
{
    partial class MissingFolderDialog
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MissingFolderDialog));
            this.btnMoved = new System.Windows.Forms.Button();
            this.lblMessage = new System.Windows.Forms.Label();
            this.btnRemove = new System.Windows.Forms.Button();
            this.btnResync = new System.Windows.Forms.Button();
            this.lblLocalPathCaption = new System.Windows.Forms.Label();
            this.lblRemotePathCaption = new System.Windows.Forms.Label();
            this.lblQuestion = new System.Windows.Forms.Label();
            this.lblLocalPath = new System.Windows.Forms.Label();
            this.lblRemotePath = new System.Windows.Forms.Label();
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.flowLayoutPanel2 = new System.Windows.Forms.FlowLayoutPanel();
            this.flowLayoutPanel1.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.flowLayoutPanel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnMoved
            // 
            resources.ApplyResources(this.btnMoved, "btnMoved");
            this.btnMoved.Name = "btnMoved";
            this.btnMoved.UseVisualStyleBackColor = true;
            this.btnMoved.Click += new System.EventHandler(this.btnMoved_Click);
            // 
            // lblMessage
            // 
            resources.ApplyResources(this.lblMessage, "lblMessage");
            this.lblMessage.Name = "lblMessage";
            this.lblMessage.Click += new System.EventHandler(this.lblMessage_Click);
            // 
            // btnRemove
            // 
            resources.ApplyResources(this.btnRemove, "btnRemove");
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.UseVisualStyleBackColor = true;
            this.btnRemove.Click += new System.EventHandler(this.btnRemove_Click);
            // 
            // btnResync
            // 
            resources.ApplyResources(this.btnResync, "btnResync");
            this.btnResync.Name = "btnResync";
            this.btnResync.UseVisualStyleBackColor = true;
            this.btnResync.Click += new System.EventHandler(this.btnResync_Click);
            // 
            // lblLocalPathCaption
            // 
            resources.ApplyResources(this.lblLocalPathCaption, "lblLocalPathCaption");
            this.lblLocalPathCaption.Name = "lblLocalPathCaption";
            // 
            // lblRemotePathCaption
            // 
            resources.ApplyResources(this.lblRemotePathCaption, "lblRemotePathCaption");
            this.lblRemotePathCaption.Name = "lblRemotePathCaption";
            // 
            // lblQuestion
            // 
            resources.ApplyResources(this.lblQuestion, "lblQuestion");
            this.lblQuestion.Name = "lblQuestion";
            // 
            // lblLocalPath
            // 
            resources.ApplyResources(this.lblLocalPath, "lblLocalPath");
            this.lblLocalPath.Name = "lblLocalPath";
            // 
            // lblRemotePath
            // 
            resources.ApplyResources(this.lblRemotePath, "lblRemotePath");
            this.lblRemotePath.Name = "lblRemotePath";
            // 
            // flowLayoutPanel1
            // 
            resources.ApplyResources(this.flowLayoutPanel1, "flowLayoutPanel1");
            this.flowLayoutPanel1.Controls.Add(this.btnMoved);
            this.flowLayoutPanel1.Controls.Add(this.btnRemove);
            this.flowLayoutPanel1.Controls.Add(this.btnResync);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            // 
            // tableLayoutPanel1
            // 
            resources.ApplyResources(this.tableLayoutPanel1, "tableLayoutPanel1");
            this.tableLayoutPanel1.Controls.Add(this.lblRemotePath, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.lblLocalPathCaption, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.lblRemotePathCaption, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.lblLocalPath, 1, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            // 
            // flowLayoutPanel2
            // 
            resources.ApplyResources(this.flowLayoutPanel2, "flowLayoutPanel2");
            this.flowLayoutPanel2.Controls.Add(this.lblMessage);
            this.flowLayoutPanel2.Controls.Add(this.tableLayoutPanel1);
            this.flowLayoutPanel2.Controls.Add(this.lblQuestion);
            this.flowLayoutPanel2.Controls.Add(this.flowLayoutPanel1);
            this.flowLayoutPanel2.Name = "flowLayoutPanel2";
            // 
            // MissingFolderDialog
            // 
            resources.ApplyResources(this, "$this");
            this.AccessibleRole = System.Windows.Forms.AccessibleRole.Dialog;
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ControlBox = false;
            this.Controls.Add(this.flowLayoutPanel2);
            this.Name = "MissingFolderDialog";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.Load += new System.EventHandler(this.RepositoryAction_Load);
            this.flowLayoutPanel1.ResumeLayout(false);
            this.flowLayoutPanel1.PerformLayout();
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.flowLayoutPanel2.ResumeLayout(false);
            this.flowLayoutPanel2.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnMoved;
        private System.Windows.Forms.Label lblMessage;
        private System.Windows.Forms.Button btnRemove;
        private System.Windows.Forms.Button btnResync;
        private System.Windows.Forms.Label lblLocalPathCaption;
        private System.Windows.Forms.Label lblRemotePathCaption;
        private System.Windows.Forms.Label lblQuestion;
        private System.Windows.Forms.Label lblLocalPath;
        private System.Windows.Forms.Label lblRemotePath;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel2;
    }
}