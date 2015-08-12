﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using OpenTK;
using Sledge.Common;
using Sledge.Common.Mediator;
using Sledge.DataStructures.GameData;
using Sledge.DataStructures.Geometric;
using Sledge.DataStructures.MapObjects;
using Sledge.Editor.Actions;
using Sledge.Editor.Actions.MapObjects.Operations;
using Sledge.Editor.Actions.MapObjects.Selection;
using Sledge.Editor.Extensions;
using Sledge.Editor.Properties;
using Sledge.Editor.Rendering;
using Sledge.Editor.UI;
using Sledge.Editor.UI.Sidebar;
using Sledge.Rendering.Cameras;
using Sledge.Rendering.Scenes;
using Sledge.Rendering.Scenes.Elements;
using Sledge.Settings;
using Line = Sledge.Rendering.Scenes.Renderables.Line;
using Select = Sledge.Settings.Select;

namespace Sledge.Editor.Tools
{
    public class EntityTool : BaseTool
    {
        private enum EntityState
        {
            None,
            Drawn,
            Moving
        }

        private Coordinate _location;
        private EntityState _state;
        private ToolStripItem[] _menu;
        private EntitySidebarPanel _sidebarPanel;

        public EntityTool()
        {
            Usage = ToolUsage.Both;
            _location = new Coordinate(0, 0, 0);
            _state = EntityState.None;
            _sidebarPanel = new EntitySidebarPanel();
        }

        public override Image GetIcon()
        {
            return Resources.Tool_Entity;
        }

        public override string GetName()
        {
            return "Entity Tool";
        }

        public override string GetContextualHelp()
        {
            return "In the 3D view, *click* a face to place an entity there.\n" +
                   "In the 2D view, *click* to place a point and press *enter* to create.\n" +
                   "*Right click* in the 2D view to quickly choose and create any entity type.";
        }

        public override IEnumerable<KeyValuePair<string, Control>> GetSidebarControls()
        {
            yield return new KeyValuePair<string, Control>(GetName(), _sidebarPanel);
        }

        public override void DocumentChanged()
        {
            System.Threading.Tasks.Task.Factory.StartNew(BuildMenu);
            _sidebarPanel.RefreshEntities(Document);
        }

        private void BuildMenu()
        {
            if (_menu != null) foreach (var item in _menu) item.Dispose();
            _menu = null;
            if (Document == null) return;

            var items = new List<ToolStripItem>();
            var classes = Document.GameData.Classes.Where(x => x.ClassType != ClassType.Base && x.ClassType != ClassType.Solid).ToList();
            var groups = classes.GroupBy(x => x.Name.Split('_')[0]);
            foreach (var g in groups)
            {
                var mi = new ToolStripMenuItem(g.Key);
                var l = g.ToList();
                if (l.Count == 1)
                {
                    var cls = l[0];
                    mi.Text = cls.Name;
                    mi.Tag = cls;
                    mi.Click += ClickMenuItem;
                }
                else
                {
                    var subs = l.Select(x =>
                    {
                        var item = new ToolStripMenuItem(x.Name) { Tag = x };
                        item.Click += ClickMenuItem;
                        return item;
                    }).OfType<ToolStripItem>().ToArray();
                    mi.DropDownItems.AddRange(subs);
                }
                items.Add(mi);
            }
            _menu = items.ToArray();
        }

        private void ClickMenuItem(object sender, EventArgs e)
        {
            CreateEntity(_location, ((ToolStripItem)sender).Tag as GameDataObject);
        }

        public override HotkeyTool? GetHotkeyToolType()
        {
            return HotkeyTool.Entity;
        }

        // 3D interaction

        protected override void MouseDown(MapViewport viewport, PerspectiveCamera camera, ViewportEvent e)
        {
            if (e.Button != MouseButtons.Left) return;

            // Get the ray that is cast from the clicked point along the viewport frustrum
            var ray = viewport.CastRayFromScreen(e.X, e.Y);

            // Grab all the elements that intersect with the ray
            var hits = Document.Map.WorldSpawn.GetAllNodesIntersectingWith(ray);

            // Sort the list of intersecting elements by distance from ray origin and grab the first hit
            var hit = hits
                .Select(x => new { Item = x, Intersection = x.GetIntersectionPoint(ray) })
                .Where(x => x.Intersection != null)
                .OrderBy(x => (x.Intersection - ray.Start).VectorMagnitude())
                .FirstOrDefault();

            if (hit == null) return; // Nothing was clicked

            CreateEntity(hit.Intersection);
        }

        // 2D interaction

        protected override void MouseEnter(MapViewport viewport, OrthographicCamera camera, ViewportEvent e)
        {
            viewport.Control.Cursor = Cursors.Cross;
        }

        protected override void MouseLeave(MapViewport viewport, OrthographicCamera camera, ViewportEvent e)
        {
            viewport.Control.Cursor = Cursors.Cross;
        }

        protected override void MouseDown(MapViewport viewport, OrthographicCamera camera, ViewportEvent e)
        {
            if (e.Button != MouseButtons.Left && e.Button != MouseButtons.Right) return;

            _state = EntityState.Moving;
            var loc = SnapIfNeeded(viewport.ScreenToWorld(e.X, viewport.Height - e.Y));
            _location = viewport.GetUnusedCoordinate(_location) + viewport.Expand(loc);
            Invalidate();
        }

        protected override void MouseUp(MapViewport viewport, OrthographicCamera camera, ViewportEvent e)
        {
            if (e.Button != MouseButtons.Left) return;
            _state = EntityState.Drawn;
            var loc = SnapIfNeeded(viewport.ScreenToWorld(e.X, viewport.Height - e.Y));
            _location = viewport.GetUnusedCoordinate(_location) + viewport.Expand(loc);
            Invalidate();
        }

        protected override void MouseMove(MapViewport viewport, OrthographicCamera camera, ViewportEvent e)
        {
            if (!Control.MouseButtons.HasFlag(MouseButtons.Left)) return;
            if (_state != EntityState.Moving) return;
            var loc = SnapIfNeeded(viewport.ScreenToWorld(e.X, viewport.Height - e.Y));
            _location = viewport.GetUnusedCoordinate(_location) + viewport.Expand(loc);
            Invalidate();
        }

        protected override void KeyDown(MapViewport viewport, OrthographicCamera camera, ViewportEvent e)
        {
            switch (e.KeyCode)
            {
                case Keys.Enter:
                    CreateEntity(_location);
                    _state = EntityState.None;
                    Invalidate();
                    break;
                case Keys.Escape:
                    _state = EntityState.None;
                    Invalidate();
                    break;
            }
        }

        private void CreateEntity(Coordinate origin, GameDataObject gd = null)
        {
            if (gd == null) gd = _sidebarPanel.GetSelectedEntity();
            if (gd == null) return;

            var col = gd.Behaviours.Where(x => x.Name == "color").ToArray();
            var colour = col.Any() ? col[0].GetColour(0) : Colour.GetDefaultEntityColour();

            var entity = new Entity(Document.Map.IDGenerator.GetNextObjectID())
            {
                EntityData = new EntityData(gd),
                ClassName = gd.Name,
                Colour = colour,
                Origin = origin
            };

            if (Select.SelectCreatedEntity) entity.IsSelected = true;

            IAction action = new Create(Document.Map.WorldSpawn.ID, entity);

            if (Select.SelectCreatedEntity && Select.DeselectOthersWhenSelectingCreation)
            {
                action = new ActionCollection(new ChangeSelection(new MapObject[0], Document.Selection.GetSelectedObjects()), action);
            }

            Document.PerformAction("Create entity: " + gd.Name, action);
            if (Select.SwitchToSelectAfterEntity)
            {
                Mediator.Publish(HotkeysMediator.SwitchTool, HotkeyTool.Selection);
            }
        }

        // Rendering

        protected override IEnumerable<SceneObject> GetSceneObjects()
        {
            var list = base.GetSceneObjects().ToList();

            if (_state != EntityState.None)
            {
                var vec = _location.ToVector3();
                var high = (float) Document.GameData.MapSizeHigh;
                var low = (float) Document.GameData.MapSizeLow;
                list.Add(new Line(Color.LimeGreen, new Vector3(low, vec.Y, vec.Z), new Vector3(high, vec.Y, vec.Z)));
                list.Add(new Line(Color.LimeGreen, new Vector3(vec.X, low, vec.Z), new Vector3(vec.X, high, vec.Z)));
                list.Add(new Line(Color.LimeGreen, new Vector3(vec.X, vec.Y, low), new Vector3(vec.X, vec.Y, high)));
            }

            return list;
        }

        protected override IEnumerable<Element> GetViewportElements(MapViewport viewport, OrthographicCamera camera)
        {
            var list = base.GetViewportElements(viewport, camera).ToList();

            if (_state != EntityState.None)
            {
                list.Add(new HandleElement(PositionType.World, HandleElement.HandleType.Square, new Position(_location.ToVector3()), 5)
                {
                    Color = Color.Transparent,
                    LineColor = Color.LimeGreen
                });
            }

            return list;
        }

        public override HotkeyInterceptResult InterceptHotkey(HotkeysMediator hotkeyMessage, object parameters)
        {
            switch (hotkeyMessage)
            {
                case HotkeysMediator.OperationsPasteSpecial:
                case HotkeysMediator.OperationsPaste:
                    return HotkeyInterceptResult.SwitchToSelectTool;
            }
            return HotkeyInterceptResult.Continue;
        }

        public override void OverrideViewportContextMenu(ViewportContextMenu menu, MapViewport vp, ViewportEvent e)
        {
            menu.Items.Clear();
            if (_location == null) return;

            var gd = _sidebarPanel.GetSelectedEntity();
            if (gd != null)
            {
                var item = new ToolStripMenuItem("Create " + gd.Name);
                item.Click += (sender, args) => CreateEntity(_location);
                menu.Items.Add(item);
                menu.Items.Add(new ToolStripSeparator());
            }

            if (_menu != null)
            {
                menu.Items.AddRange(_menu);
            }
        }
    }
}
