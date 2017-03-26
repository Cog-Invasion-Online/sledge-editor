﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using LogicAndTrick.Oy;
using Sledge.Common.Context;
using Sledge.Common.Documents;
using Sledge.Shell.Controls;

namespace Sledge.Shell.Forms
{
    /// <summary>
    /// The application's base window
    /// </summary>
    public partial class Shell : BaseForm
    {
        private readonly List<IDocument> _documents;
        private readonly object _lock = new object();

        public Shell()
        {
            _documents = new List<IDocument>();

            InitializeComponent();
            InitializeShell();
        }

        /// <summary>
        /// Setup the shell pre-startup
        /// </summary>
        private void InitializeShell()
        {
            DocumentTabs.TabPages.Clear();
            
            Oy.Subscribe<IContext>("Context:Changed", ContextChanged);

            Oy.Subscribe<IDocument>("Document:Opened", OpenDocument);
            Oy.Subscribe<IDocument>("Document:Closed", CloseDocument);

            Oy.Subscribe<string>("Shell:OpenCommandBox", OpenCommandBox);
        }

        protected override void OnLoad(EventArgs e)
        {
            // Bootstrap the shell
            Bootstrapping.Startup(this).ContinueWith(Bootstrapping.Initialise);

            // Set up bootstrapping for shutdown
            Closing += DoClosing;
        }

        private async void DoClosing(object sender, CancelEventArgs e)
        {
            // Try to close all the open documents
            foreach (var doc in _documents.ToArray())
            {
                await Oy.Publish("Document:RequestClose", doc);
                if (_documents.Contains(doc))
                {
                    e.Cancel = true;
                    return;
                }
            }

            // Close anything else
            if (!await Bootstrapping.ShuttingDown())
            {
                e.Cancel = true;
                return;
            }

            // Unsubscribe the event (no infinite loops!) and close for good
            Closing -= DoClosing;
            Enabled = false;
            e.Cancel = true;
            await Bootstrapping.Shutdown();
            Close();
        }

        /// <summary>
        /// Get the list of docking panels in the shell
        /// </summary>
        /// <returns>The list of docking panels</returns>
        internal IEnumerable<DockedPanel> GetDockPanels()
        {
            yield return LeftSidebar;
            yield return RightSidebar;
            yield return BottomSidebar;
        }
        
        // Subscriptions

        private async Task OpenDocument(IDocument document)
        {
            lock (_lock)
            {
                if (_documents.Contains(document)) return;
                _documents.Add(document);
                document.PropertyChanged += DocumentNameChanged;
                DocumentTabs.TabPages.Add(new TabPage { Text = document.Name, Tag = document });
                TabChanged(DocumentTabs, EventArgs.Empty);
            }
        }

        private async Task CloseDocument(IDocument document)
        {
            lock (_lock)
            {
                if (!_documents.Contains(document)) return;
                _documents.Remove(document);
                document.PropertyChanged -= DocumentNameChanged;
                var page = DocumentTabs.TabPages.OfType<TabPage>().FirstOrDefault(x => x.Tag == document);
                if (page != null) DocumentTabs.TabPages.Remove(page);
                TabChanged(DocumentTabs, EventArgs.Empty);
            }
        }

        private void DocumentNameChanged(object sender, PropertyChangedEventArgs e)
        {
            var doc = sender as IDocument;
            var page = DocumentTabs.TabPages.OfType<TabPage>().FirstOrDefault(x => x.Tag == doc);
            if (page != null && doc != null) page.Text = doc.Name;
        }

        private async Task ContextChanged(IContext context)
        {

        }

        private async Task OpenCommandBox(string obj)
        {
            var cb = new CommandBox();
            cb.Location = new Point(Location.X + (Size.Width - cb.Width) / 2, Location.Y + (Size.Height - cb.Height) / 4);
            cb.StartPosition = FormStartPosition.Manual;
            cb.Show(this);
        }

        // Form events

        private void TabChanged(object sender, EventArgs e)
        {
            if (DocumentTabs.SelectedTab != null)
            {
                var doc = DocumentTabs.SelectedTab.Tag as IDocument;
                if (doc != null)
                {
                    var currentControl = DocumentContainer.Controls.OfType<Control>().FirstOrDefault();
                    if (currentControl != doc.Control)
                    {
                        DocumentContainer.Controls.Clear();
                        DocumentContainer.Controls.Add((Control) doc.Control);
                        DocumentContainer.Controls[0].Dock = DockStyle.Fill;
                    }
                    Oy.Publish("Document:Activated", doc);
                    Oy.Publish("Context:Add", new ContextInfo("ActiveDocument", doc));
                }
                else
                {
                    Oy.Publish("Context:Remove", new ContextInfo("ActiveDocument"));
                }
            }
            else
            {
                DocumentContainer.Controls.Clear();
                Oy.Publish("Context:Remove", new ContextInfo("ActiveDocument"));
            }
        }

        private void RequestClose(object sender, int index)
        {
            var doc = DocumentTabs.TabPages[index].Tag as IDocument;
            if (doc != null)
            {
                Oy.Publish("Document:RequestClose", doc);
            }
        }
    }
}
