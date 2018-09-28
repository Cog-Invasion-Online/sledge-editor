using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using LogicAndTrick.Oy;
using Sledge.BspEditor.Documents;
using Sledge.BspEditor.Modification;
using Sledge.BspEditor.Primitives.MapObjects;
using Sledge.Common.Shell.Components;
using Sledge.Common.Shell.Context;
using Sledge.Common.Shell.Hooks;
using Sledge.Common.Translations;
using Sledge.Shell;

namespace Sledge.BspEditor.Editing.Components.Properties
{
    /// <summary>
    /// This is the main way to edit properties of an object, including
    /// entity data, flags, visgroups, outputs, and anything else.
    /// 
    /// Each tab is a standalone panel that has its own state and context.
    /// If any tab is changed, they are all saved together when the user
    /// saves the dialog.
    /// </summary>
    [Export(typeof(IDialog))]
    [Export(typeof(IInitialiseHook))]
    [AutoTranslate]
    public sealed partial class ObjectPropertiesDialog : Form, IInitialiseHook, IDialog
    {
        private readonly Lazy<Form> _parent;
        private readonly IEnumerable<Lazy<IObjectPropertyEditorTab>> _tabs;
        private readonly IContext _context;

        private List<Subscription> _subscriptions;
        private Dictionary<IObjectPropertyEditorTab, TabPage> _pages;
        private MapDocument _currentDocument;
        private List<IMapObject> _forcedSelection;

        public Task OnInitialise()
        {
            _pages = new Dictionary<IObjectPropertyEditorTab, TabPage>();
            this.InvokeLater(() =>
            {
                foreach (var tab in _tabs.Select(x => x.Value).OrderBy(x => x.OrderHint))
                {
                    var page = new TabPage(tab.Name) {Tag = tab};
                    tab.Control.Dock = DockStyle.Fill;
                    page.Controls.Add(tab.Control);
                    _pages[tab] = page;
                }
            });

            Oy.Subscribe<List<IMapObject>>("BspEditor:ObjectProperties:OpenWithSelection", OpenWithSelection);
            return Task.CompletedTask;
        }

        public string Title
        {
            get => Text;
            set => this.InvokeLater(() => Text = value);
        }

        public string Apply
        {
            get => btnApply.Text;
            set => this.InvokeLater(() => btnApply.Text = value);
        }

        public string OK
        {
            get => btnOk.Text;
            set => this.InvokeLater(() => btnOk.Text = value);
        }

        public string Cancel
        {
            get => btnCancel.Text;
            set => this.InvokeLater(() => btnCancel.Text = value);
        }

        public string ResetUnsavedChanges
        {
            get => btnReset.Text;
            set => this.InvokeLater(() => btnReset.Text = value);
        }

        public string UnsavedChanges { get; set; }
        public string DoYouWantToSaveFirst { get; set; }

        [ImportingConstructor]
        public ObjectPropertiesDialog(
            [Import("Shell")] Lazy<Form> parent,
            [ImportMany] IEnumerable<Lazy<IObjectPropertyEditorTab>> tabs,
            [Import] Lazy<IContext> context
        )
        {
            _parent = parent;
            _tabs = tabs;
            _context = context.Value;

            InitializeComponent();
            CreateHandle();
        }

        private async Task OpenWithSelection(List<IMapObject> selection)
        {
            _forcedSelection = selection;
            await Oy.Publish("Context:Add", new ContextInfo("BspEditor:ObjectProperties"));
        }

        /// <summary>
        /// Get the current selection, using the forced if it exists, otherwise using the document selection.
        /// </summary>
        private List<IMapObject> GetCurrentSelection()
        {
            return _forcedSelection?.ToList()
                   ?? _currentDocument?.Selection.GetSelectedParents().ToList()
                   ?? new List<IMapObject>();
        }

        /// <summary>
        /// Update the visibility of all the loaded tabs based on the current selection and context.
        /// </summary>
        private void UpdateTabVisibility(IContext context, List<IMapObject> objects)
        {
            var changed = false;
            tabPanel.SuspendLayout();

            var currentlyVisibleTabs = tabPanel.TabPages.OfType<TabPage>().Select(x => _pages.FirstOrDefault(p => p.Value == x).Key).ToList();
            var newVisibleTabs = _tabs.Where(x => x.Value.IsInContext(context, objects)).OrderBy(x => x.Value.OrderHint).Select(x => x.Value).ToList();

            // Add tabs which aren't visible and should be
            foreach (var add in newVisibleTabs.Except(currentlyVisibleTabs).ToList())
            {
                // Locate the next or previous tab in the visible tab set so we can insert the new tab before/after it
                var prevCv = currentlyVisibleTabs.Where(x => String.Compare(x.OrderHint, add.OrderHint, StringComparison.Ordinal) < 0).OrderByDescending(x => x.OrderHint).FirstOrDefault();
                var nextCv = currentlyVisibleTabs.Where(x => String.Compare(x.OrderHint, add.OrderHint, StringComparison.Ordinal) > 0).OrderBy(x => x.OrderHint).FirstOrDefault();
                var idx = prevCv != null ? tabPanel.TabPages.IndexOf(_pages[prevCv]) + 1
                        : nextCv != null ? tabPanel.TabPages.IndexOf(_pages[nextCv])
                        : 0;

                // Add the tab the the currently visible set for later index testing
                tabPanel.TabPages.Insert(idx, _pages[add]);
                currentlyVisibleTabs.Add(add);
                changed = true;
            }

            // Remove tables which are visible and shouldn't be
            foreach (var rem in currentlyVisibleTabs.Except(newVisibleTabs))
            {
                tabPanel.TabPages.Remove(_pages[rem]);
                changed = true;
            }

            if (changed) tabPanel.SelectedIndex = tabPanel.TabCount > 0 ? 0 : -1;

            tabPanel.ResumeLayout(changed);
        }

        /// <summary>
        /// Don't close the dialog, but check if changes are made and hide it if the check passes
        /// </summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            ConfirmIfChanged().ContinueWith(Close);
        }

        /// <summary>
        /// Conditionally close the dialog. Doesn't perform any change detection.
        /// </summary>
        private void Close(Task<bool> actuallyClose)
        {
            if (actuallyClose.Result) Oy.Publish("Context:Remove", new ContextInfo("BspEditor:ObjectProperties"));
        }

        /// <inheritdoc />
        public bool IsInContext(IContext context)
        {
            return context.HasAny("BspEditor:ObjectProperties");
        }

        /// <inheritdoc />
        public void SetVisible(IContext context, bool visible)
        {
            this.InvokeLater(() =>
            {
                if (visible)
                {
                    var doc = context.Get<MapDocument>("ActiveDocument");

                    #pragma warning disable 4014 // Intentionally unawaited
                    DocumentActivated(doc);
                    #pragma warning restore 4014

                    if (!Visible) Show(_parent.Value);
                    Subscribe();
                }
                else
                {
                    _forcedSelection = null;
                    Hide();
                    Unsubscribe();
                }
            });
        }

        private void Subscribe()
        {
            if (_subscriptions != null) return;
            _subscriptions = new List<Subscription>
            {
                Oy.Subscribe<Change>("MapDocument:Changed", DocumentChanged),
                Oy.Subscribe<MapDocument>("Document:Activated", DocumentActivated)
            };
        }

        private void Unsubscribe()
        {
            if (_subscriptions == null) return;
            _subscriptions.ForEach(x => x.Dispose());
            _subscriptions = null;
        }

        /// <summary>
        /// Checks for changes and prompts the user if they want to save.
        /// </summary>
        private Task<bool> ConfirmIfChanged()
        {
            if (_currentDocument == null) return Task.FromResult(true);
            if (!_tabs.Any(x => x.Value.HasChanges)) return Task.FromResult(true);
            var result = MessageBox.Show(DoYouWantToSaveFirst, UnsavedChanges, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1);

            if (result == DialogResult.Yes) return Save(); // Save first, then close
            if (result == DialogResult.No) return Task.FromResult(true); // Don't save, and close
            return Task.FromResult(false); // Don't save, don't close
        }

        /// <summary>
        /// Saves the current changes (if any)
        /// </summary>
        private async Task<bool> Save()
        {
            if (_currentDocument == null) return true;
            var changed = _tabs.Select(x => x.Value).Where(x => x.HasChanges).ToList();
            if (!changed.Any()) return true;

            var list = GetCurrentSelection();
            var changes = changed.SelectMany(x => x.GetChanges(_currentDocument, list));
            var tsn = new Transaction(changes);

            await MapDocumentOperation.Perform(_currentDocument, tsn);
            return true;
        }

        /// <summary>
        /// Undoes any pending changes in the form
        /// </summary>
        private Task Reset(Task t = null)
        {
            return UpdateSelection();
        }

        /// <summary>
        /// A different document has been activated, clear the selection and reset to the new document
        /// </summary>
        private async Task DocumentActivated(MapDocument doc)
        {
            _currentDocument = doc;

            // If the selection is forced, reset it
            var forced = _forcedSelection;
            if (forced != null && !forced.All(x => ReferenceEquals(doc.Map.Root.FindByID(x.ID), x)))
            {
                // Special case for root object
                if (forced.Count == 1 && forced[0] is Root) forced[0] = _currentDocument.Map.Root;
                // Otherwise clear the forced selection
                else _forcedSelection = null;
            }

            await UpdateSelection();
        }

        /// <summary>
        /// The active document has been modified, ensure the selection is still correct.
        /// </summary>
        private async Task DocumentChanged(Change change)
        {
            // If the selection is forced, make sure any deleted objects are removed from the selection
            _forcedSelection?.RemoveAll(x => change.Removed.Contains(x));

            await UpdateSelection();
        }

        /// <summary>
        /// Update all tabs with the new selection.
        /// </summary>
        private async Task UpdateSelection()
        {
            var list = GetCurrentSelection();

            foreach (var tab in _tabs)
            {
                await tab.Value.SetObjects(_currentDocument, list);
            }

            this.InvokeLater(() => UpdateTabVisibility(_context, list));
        }

        private void ApplyClicked(object sender, EventArgs e) => Save().ContinueWith(Reset);
        private void OkClicked(object sender, EventArgs e) => Save().ContinueWith(Close);
        private void CancelClicked(object sender, EventArgs e) => ConfirmIfChanged().ContinueWith(Close);
        private void ResetClicked(object sender, EventArgs e) => Reset();
    }
}
