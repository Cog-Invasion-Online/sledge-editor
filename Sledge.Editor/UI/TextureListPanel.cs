﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Sledge.Editor.Documents;
using Sledge.Providers.Texture;
using Sledge.Rendering.Materials;

namespace Sledge.Editor.UI
{
    public sealed class TextureListPanel : Panel
    {
        public enum TextureSortOrder
        {
            None,
            Name,
            Width,
            Height,
            Size,
            Package
        }

        public delegate void TextureSelectedEventHandler(object sender, string item);
        public delegate void SelectionChangedEventHandler(object sender, IEnumerable<string> selection);

        public event TextureSelectedEventHandler TextureSelected;
        public event SelectionChangedEventHandler SelectionChanged;

        private void OnTextureSelected(string item)
        {
            TextureSelected?.Invoke(this, item);
        }
        
        private void OnSelectionChanged(IEnumerable<string> selection)
        {
            SelectionChanged?.Invoke(this, selection);
        }

        private readonly VScrollBar _scrollBar;

        private TextureCollection _collection;
        private readonly List<string> _textures;
        private int _imageSize;

        private readonly List<Rectangle> _rectangles;

        private TextureSortOrder _sortOrder;
        private bool _sortDescending;
        private bool _allowSelection;
        private bool _allowMultipleSelection;
        private string _lastSelectedItem;
        private readonly List<string> _selection;

        #region Properties

        public TextureCollection Collection
        {
            get { return _collection; }
            set
            {
                _collection = value;
                Refresh();
            }
        }

        public bool AllowSelection
        {
            get { return _allowSelection; }
            set
            {
                _allowSelection = value;
                if (!_allowSelection && _selection.Count > 0)
                {
                    _selection.Clear();
                    Refresh();
                }
            }
        }

        public bool AllowMultipleSelection
        {
            get { return _allowMultipleSelection; }
            set
            {
                _allowMultipleSelection = value;
                if (!_allowMultipleSelection && _selection.Count > 0)
                {
                    var first = _selection.First();
                    _selection.Clear();
                    _selection.Add(first);
                    Refresh();
                }
            }
        }

        public int ImageSize
        {
            get { return _imageSize; }
            set
            {
                _imageSize = value;
                UpdateRectangles();
            }
        }

        public TextureSortOrder SortOrder
        {
            get { return _sortOrder; }
            set
            {
                _sortOrder = value;
                UpdateRectangles();
            }
        }

        public bool SortDescending
        {
            get { return _sortDescending; }
            set
            {
                _sortDescending = value;
                UpdateRectangles();
            }
        }

        public bool EnableDrag { get; set; }

        #endregion
        
        public TextureListPanel()
        {
            _collection = new TextureCollection(new List<TexturePackage>());

            BackColor = Color.Black;
            VScroll = true;
            AutoScroll = true;
            DoubleBuffered = true;

            AllowSelection = true;
            AllowMultipleSelection = true;

            _scrollBar = new VScrollBar {Dock = DockStyle.Right};
            _scrollBar.ValueChanged += (sender, e) => Refresh();
            _textures = new List<string>();
            _selection = new List<string>();
            _imageSize = 128;

            _rectangles = new List<Rectangle>();

            Controls.Add(_scrollBar);

            UpdateRectangles();
        }

        #region Selection

        public void SetSelectedTextures(IEnumerable<string> items)
        {
            _selection.Clear();
            _selection.AddRange(items);
            OnSelectionChanged(_selection);
            Refresh();
        }

        public void ScrollToItem(string item)
        {
            var index = GetTextures().ToList().IndexOf(item);
            if (index < 0) return;

            var rec = _rectangles[index];
            var yscroll = Math.Max(0, Math.Min(rec.Top - 3, _scrollBar.Maximum - ClientRectangle.Height));
            _scrollBar.Value = yscroll;

            Refresh();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (KeyboardState.Ctrl || KeyboardState.Shift || _selection.Count != 1) return;

            var x = e.X;
            var y = _scrollBar.Value + e.Y;

            var clickedIndex = GetIndexAt(x, y);

            var item = GetTextures().ElementAt(clickedIndex);
            if (item == _selection[0])
            {
                OnTextureSelected(_selection[0]);
            }
        }

        private bool _down;
        private Point _downPoint;

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            
            if (!AllowSelection) return;
            if (!AllowMultipleSelection || !KeyboardState.Ctrl) _selection.Clear();

            if (e.Button == MouseButtons.Left)
            {
                _down = true;
                _downPoint = e.Location;
            }

            var x = e.X;
            var y = _scrollBar.Value + e.Y;

            var clickedIndex = GetIndexAt(x, y);

            var item = clickedIndex >= 0 && clickedIndex < _textures.Count ? GetTextures().ElementAt(clickedIndex) : null;

            if (item == null)
            {
                _selection.Clear();
            }
            else if (AllowMultipleSelection && KeyboardState.Ctrl && _selection.Contains(item))
            {
                _selection.Remove(item);
                _lastSelectedItem = null;
            }
            else if (AllowMultipleSelection && KeyboardState.Shift && _lastSelectedItem != null)
            {
                var bef = GetTextures().ToList().IndexOf(_lastSelectedItem);
                var start = Math.Min(bef, clickedIndex);
                var count = Math.Abs(clickedIndex - bef) + 1;
                _selection.AddRange(GetTextures().ToList().GetRange(start, count).Where(i => !_selection.Contains(i)));
            }
            else 
            {
                _selection.Add(item);
                _lastSelectedItem = item;
            }
            OnSelectionChanged(_selection);

            Refresh();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_down && EnableDrag && _selection.Any() && (Math.Abs(e.X - _downPoint.X) > 2 || Math.Abs(e.Y - _downPoint.Y) > 2))
            {
                _down = false;
                DoDragDrop(_selection.ToList(), DragDropEffects.Copy);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) _down = false;
            base.OnMouseUp(e);
        }

        private int GetIndexAt(int x, int y)
        {
            int pad = 3, font = 4 + SystemFonts.MessageBoxFont.Height;
            for (var i = 0; i < _rectangles.Count; i++)
            {
                var rec = _rectangles[i];
                if (rec.Left - pad <= x
                    && rec.Right + pad >= x
                    && rec.Top - pad <= y
                    && rec.Bottom + pad + font >= y)
                {
                    return i;
                }
            }
            return -1;
        }

        #endregion

        #region Add/Remove/Get Textures

        public IEnumerable<string> GetTextures()
        {
            // todo ?
            IEnumerable<string> sorted = new List<string>();
            switch (SortOrder)
            {
                case TextureSortOrder.None:
                    sorted = _textures;
                    break;
                case TextureSortOrder.Name:
                    sorted = _textures.OrderBy(x => x);
                    break;
                case TextureSortOrder.Width:
                    //sorted = _textures.OrderBy(x => x.Width).ThenBy(x => x.Name);
                    break;
                case TextureSortOrder.Height:
                    //sorted = _textures.OrderBy(x => x.Height).ThenBy(x => x.Name);
                    break;
                case TextureSortOrder.Size:
                    //sorted = _textures.OrderBy(x => x.Width * x.Height).ThenBy(x => x.Name);
                    break;
                case TextureSortOrder.Package:
                    //sorted = _textures.OrderBy(x => x.ToString()).ThenBy(x => x.Name);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            if (_sortDescending) return sorted.Reverse();
            return sorted;
        }

        public IEnumerable<string> GetSelectedTextures()
        {
            return _selection;
        }

        private ITextureStreamSource _streamSource;

        public async Task SetTextureList(IEnumerable<string> textures)
        {
            _textures.Clear();
            _lastSelectedItem = null;
            _selection.Clear();
            _textures.AddRange(textures);

            _streamSource?.Dispose();
            _streamSource = null;
            _streamSource = await _collection.GetStreamSource();

            OnSelectionChanged(_selection);
            UpdateRectangles();
        }

        public void Clear()
        {
            _textures.Clear();
            _lastSelectedItem = null;
            _selection.Clear();

            _streamSource?.Dispose();
            _streamSource = null;

            OnSelectionChanged(_selection);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Clear();
            base.Dispose(disposing);
        }

        #endregion

        #region Scrolling

        private void ScrollByAmount(int value)
        {
            var newValue = _scrollBar.Value + value;
            _scrollBar.Value = newValue < 0 ? 0 : Math.Min(newValue, Math.Max(0, _scrollBar.Maximum - ClientRectangle.Height));
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            ScrollByAmount(_scrollBar.SmallChange * (e.Delta / -120));
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.PageDown:
                    ScrollByAmount(_scrollBar.LargeChange);
                    break;
                case Keys.PageUp:
                    ScrollByAmount(-_scrollBar.LargeChange);
                    break;
                case Keys.End:
                    ScrollByAmount(int.MaxValue);
                    break;
                case Keys.Home:
                    ScrollByAmount(-int.MaxValue);
                    break;
                case Keys.Enter:
                    if (_selection.Count > 0) OnTextureSelected(_selection[0]);
                    break;
            }
            base.OnKeyDown(e);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            Focus();
            base.OnMouseEnter(e);
        }

        #endregion

        #region Updating Rectangles & Dimensions

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            UpdateRectangles();
        }

        private void UpdateRectangles()
        {
            int w = ClientRectangle.Width - _scrollBar.Width,
                pad = 3,
                font = 4 + SystemFonts.MessageBoxFont.Height,
                cx = 0,
                cy = 0,
                my = 0;
            _rectangles.Clear();
            var textures = GetTextures().ToList();
            // _collection.Precache(textures).Wait();
            foreach (var texture in textures)
            {
                var t = _collection.TryGetTextureItem(texture);
                //t.Wait();
                var ti = t ?? new TextureItem("missing", TextureFlags.None, 128, 128);
                //todo image size
                var rw = w - cx;
                var wid = (_imageSize > 0 ? _imageSize : ti.Width) + pad + pad;
                var hei = (_imageSize > 0 ? _imageSize : ti.Height) + pad + pad + font;
                if (rw < wid)
                {
                    // New row
                    cx = 0;
                    cy += my;
                    my = 0;
                }
                my = Math.Max(my, hei);
                var rect = new Rectangle(cx + pad, cy + pad, wid - pad - pad, hei - pad - pad - font);
                _rectangles.Add(rect);
                cx += wid;
            }
            _scrollBar.Maximum = cy + my;
            _scrollBar.SmallChange = (_imageSize > 0 ? _imageSize : 128) + pad + pad + font;
            _scrollBar.LargeChange = ClientRectangle.Height;

            if (_scrollBar.Value > _scrollBar.Maximum - ClientRectangle.Height)
            {
                _scrollBar.Value = Math.Max(0, _scrollBar.Maximum - ClientRectangle.Height);
            }
        }

        #endregion

        #region Rendering

        private readonly Dictionary<string, Bitmap> _renderCache = new Dictionary<string, Bitmap>();

        private void UpdateCacheableItems(int y, int height)
        {
            if (_streamSource == null) return;

            var texs = GetTextures().ToList();
            var cacheable = new HashSet<string>();
            for (var i = 0; i < texs.Count; i++)
            {
                var rec = _rectangles[i];
                if (rec.Bottom < y) continue;
                if (rec.Top > y + height) break;
                cacheable.Add(texs[i]);
            }
            foreach (var ti in _renderCache.Keys.ToList())
            {
                if (!cacheable.Contains(ti))
                {
                    _renderCache[ti].Dispose();
                    _renderCache.Remove(ti);
                }
            }

            if (_streamSource == null) return;

            foreach (var item in cacheable)
            {
                if (!_renderCache.ContainsKey(item))
                {
                    _renderCache[item] = new Bitmap(1, 1);
                    _streamSource.GetImage(item, ImageSize, ImageSize)
                        .ContinueWith(x => _renderCache[item] = x.Result);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            RenderTextures(e.Graphics);
        }

        private void RenderTextures(Graphics g)
        {
            if (_textures.Count == 0 || _streamSource == null) return;

            var y = _scrollBar.Value;
            var height = ClientRectangle.Height;
            UpdateCacheableItems(y, height);

            var texs = GetTextures().ToList();
            for (var i = 0; i < texs.Count; i++)
            {
                var rec = _rectangles[i];
                if (rec.Bottom < y) continue;
                if (rec.Top > y + height) break;
                var tex = texs[i];

                if (!_renderCache.ContainsKey(tex)) continue;
                var bmp = _renderCache[tex];
                if (bmp == null) continue;

                DrawImage(g, bmp, tex, rec.X, rec.Y - y, rec.Width, rec.Height);
            }
        }

        private void DrawImage(Graphics g, Image bmp, string ti, int x, int y, int w, int h)
        {
            if (bmp == null) return;

            var iw = bmp.Width;
            var ih = bmp.Height;
            if (iw > w && iw >= ih)
            {
                ih = (int)Math.Floor(h * (ih / (float)iw));
                iw = w;
            }
            else if (ih > h)
            {
                iw = (int)Math.Floor(w * (iw / (float)ih));
                ih = h;
            }
            g.FillRectangle(System.Drawing.Brushes.Black, x - 3, y - 3, w + 6, h + 10 + SystemFonts.MessageBoxFont.Height);

            g.DrawImage(bmp, x, y, iw, ih);
            if (_selection.Contains(ti))
            {
                g.DrawRectangle(Pens.Red, x - 1, y - 1, w + 2, h + 2);
                g.DrawRectangle(Pens.Red, x - 2, y - 2, w + 4, h + 4);
            }
            else
            {
                g.DrawRectangle(Pens.Gray, x - 2, y - 2, w + 4, h + 4);
            }
            g.DrawString(ti, SystemFonts.MessageBoxFont, System.Drawing.Brushes.White, x - 2, y + h + 3);
        }

        #endregion
    }
}
