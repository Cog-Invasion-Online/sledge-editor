﻿using System.Windows.Forms;
using Sledge.Shell.Controls;

namespace Sledge.Shell.Forms
{
    partial class Shell
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
            this.components = new System.ComponentModel.Container();
            this.MenuStrip = new System.Windows.Forms.MenuStrip();
            this.StatusStrip = new System.Windows.Forms.StatusStrip();
            this.ToolStripContainer = new System.Windows.Forms.ToolStripContainer();
            this.DocumentContainer = new System.Windows.Forms.Panel();
            this.DocumentTabs = new Sledge.Shell.Controls.ClosableTabControl();
            this.RightSidebar = new Sledge.Shell.Controls.DockedPanel();
            this.RightSidebarContainer = new Sledge.Shell.Controls.SidebarContainer();
            this.LeftSidebar = new Sledge.Shell.Controls.DockedPanel();
            this.LeftSidebarContainer = new Sledge.Shell.Controls.SidebarContainer();
            this.BottomSidebar = new Sledge.Shell.Controls.DockedPanel();
            this.ToolStripContainer.ContentPanel.SuspendLayout();
            this.ToolStripContainer.SuspendLayout();
            this.RightSidebar.SuspendLayout();
            this.LeftSidebar.SuspendLayout();
            this.SuspendLayout();
            // 
            // MenuStrip
            // 
            this.MenuStrip.Location = new System.Drawing.Point(0, 0);
            this.MenuStrip.Name = "MenuStrip";
            this.MenuStrip.Size = new System.Drawing.Size(708, 24);
            this.MenuStrip.TabIndex = 0;
            this.MenuStrip.Text = "menuStrip1";
            // 
            // StatusStrip
            // 
            this.StatusStrip.Location = new System.Drawing.Point(0, 431);
            this.StatusStrip.Name = "StatusStrip";
            this.StatusStrip.Size = new System.Drawing.Size(708, 22);
            this.StatusStrip.TabIndex = 1;
            this.StatusStrip.Text = "statusStrip1";
            // 
            // ToolStripContainer
            // 
            // 
            // ToolStripContainer.ContentPanel
            // 
            this.ToolStripContainer.ContentPanel.Controls.Add(this.DocumentContainer);
            this.ToolStripContainer.ContentPanel.Controls.Add(this.DocumentTabs);
            this.ToolStripContainer.ContentPanel.Controls.Add(this.RightSidebar);
            this.ToolStripContainer.ContentPanel.Controls.Add(this.LeftSidebar);
            this.ToolStripContainer.ContentPanel.Controls.Add(this.BottomSidebar);
            this.ToolStripContainer.ContentPanel.Size = new System.Drawing.Size(708, 382);
            this.ToolStripContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ToolStripContainer.Location = new System.Drawing.Point(0, 24);
            this.ToolStripContainer.Name = "ToolStripContainer";
            this.ToolStripContainer.Size = new System.Drawing.Size(708, 407);
            this.ToolStripContainer.TabIndex = 2;
            this.ToolStripContainer.Text = "ToolStripContainer";
            // 
            // DocumentContainer
            // 
            this.DocumentContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.DocumentContainer.Location = new System.Drawing.Point(8, 24);
            this.DocumentContainer.Name = "DocumentContainer";
            this.DocumentContainer.Size = new System.Drawing.Size(500, 350);
            this.DocumentContainer.TabIndex = 3;
            // 
            // DocumentTabs
            // 
            this.DocumentTabs.Dock = System.Windows.Forms.DockStyle.Top;
            this.DocumentTabs.Location = new System.Drawing.Point(8, 0);
            this.DocumentTabs.Name = "DocumentTabs";
            this.DocumentTabs.SelectedIndex = 0;
            this.DocumentTabs.Size = new System.Drawing.Size(500, 24);
            this.DocumentTabs.TabIndex = 4;
            this.DocumentTabs.RequestClose += new Sledge.Shell.Controls.ClosableTabControl.RequestCloseEventHandler(this.RequestClose);
            this.DocumentTabs.SelectedIndexChanged += new System.EventHandler(this.TabChanged);
            // 
            // RightSidebar
            // 
            this.RightSidebar.Controls.Add(this.RightSidebarContainer);
            this.RightSidebar.Dock = System.Windows.Forms.DockStyle.Right;
            this.RightSidebar.DockDimension = 200;
            this.RightSidebar.Hidden = false;
            this.RightSidebar.Location = new System.Drawing.Point(508, 0);
            this.RightSidebar.Name = "RightSidebar";
            this.RightSidebar.Padding = new System.Windows.Forms.Padding(8, 0, 0, 0);
            this.RightSidebar.Size = new System.Drawing.Size(200, 374);
            this.RightSidebar.TabIndex = 2;
            // 
            // RightSidebarContainer
            // 
            this.RightSidebarContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.RightSidebarContainer.Location = new System.Drawing.Point(8, 0);
            this.RightSidebarContainer.Name = "RightSidebarContainer";
            this.RightSidebarContainer.Size = new System.Drawing.Size(192, 374);
            this.RightSidebarContainer.TabIndex = 0;
            // 
            // LeftSidebar
            // 
            this.LeftSidebar.Controls.Add(this.LeftSidebarContainer);
            this.LeftSidebar.Dock = System.Windows.Forms.DockStyle.Left;
            this.LeftSidebar.DockDimension = 8;
            this.LeftSidebar.Hidden = true;
            this.LeftSidebar.Location = new System.Drawing.Point(0, 0);
            this.LeftSidebar.Name = "LeftSidebar";
            this.LeftSidebar.Padding = new System.Windows.Forms.Padding(0, 0, 8, 0);
            this.LeftSidebar.Size = new System.Drawing.Size(8, 374);
            this.LeftSidebar.TabIndex = 1;
            // 
            // LeftSidebarContainer
            // 
            this.LeftSidebarContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.LeftSidebarContainer.Location = new System.Drawing.Point(0, 0);
            this.LeftSidebarContainer.Name = "LeftSidebarContainer";
            this.LeftSidebarContainer.Size = new System.Drawing.Size(0, 374);
            this.LeftSidebarContainer.TabIndex = 0;
            // 
            // BottomSidebar
            // 
            this.BottomSidebar.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.BottomSidebar.DockDimension = 8;
            this.BottomSidebar.Hidden = true;
            this.BottomSidebar.Location = new System.Drawing.Point(0, 374);
            this.BottomSidebar.Name = "BottomSidebar";
            this.BottomSidebar.Padding = new System.Windows.Forms.Padding(0, 8, 0, 0);
            this.BottomSidebar.Size = new System.Drawing.Size(708, 8);
            this.BottomSidebar.TabIndex = 0;
            // 
            // Shell
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(708, 453);
            this.Controls.Add(this.ToolStripContainer);
            this.Controls.Add(this.StatusStrip);
            this.Controls.Add(this.MenuStrip);
            this.MainMenuStrip = this.MenuStrip;
            this.Name = "Shell";
            this.Text = "Sledge Shell";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.ToolStripContainer.ContentPanel.ResumeLayout(false);
            this.ToolStripContainer.ResumeLayout(false);
            this.ToolStripContainer.PerformLayout();
            this.RightSidebar.ResumeLayout(false);
            this.LeftSidebar.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip MenuStrip;
        private System.Windows.Forms.StatusStrip StatusStrip;
        private System.Windows.Forms.ToolStripContainer ToolStripContainer;
        private Sledge.Shell.Controls.DockedPanel BottomSidebar;
        private Sledge.Shell.Controls.DockedPanel RightSidebar;
        private Sledge.Shell.Controls.DockedPanel LeftSidebar;
        private System.Windows.Forms.Panel DocumentContainer;
        private Sledge.Shell.Controls.ClosableTabControl DocumentTabs;
        internal SidebarContainer RightSidebarContainer;
        internal SidebarContainer LeftSidebarContainer;
    }
}