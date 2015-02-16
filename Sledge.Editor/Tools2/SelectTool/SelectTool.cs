using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OpenTK;
using OpenTK.Input;
using Sledge.Common.Mediator;
using Sledge.DataStructures.Geometric;
using Sledge.DataStructures.MapObjects;
using Sledge.DataStructures.Transformations;
using Sledge.EditorNew.Actions;
using Sledge.EditorNew.Actions.MapObjects.Operations;
using Sledge.EditorNew.Actions.MapObjects.Operations.EditOperations;
using Sledge.EditorNew.Actions.MapObjects.Selection;
using Sledge.EditorNew.Clipboard;
using Sledge.EditorNew.Properties;
using Sledge.EditorNew.Tools.DraggableTool;
using Sledge.EditorNew.Tools.SelectTool.TransformationHandles;
using Sledge.EditorNew.UI;
using Sledge.EditorNew.UI.Viewports;
using Sledge.Settings;
using Select = Sledge.Settings.Select;

namespace Sledge.EditorNew.Tools.SelectTool
{
    public class SelectTool : BaseDraggableTool
    {
        // todo - select tool - widgets

        private BoxDraggableState emptyBox;
        private SelectionBoxDraggableState selectionBox;

        private MapObject ChosenItemFor3DSelection { get; set; }
        private List<MapObject> IntersectingObjectsFor3DSelection { get; set; }

        public SelectTool()
        {
            selectionBox = new SelectionBoxDraggableState(this);
            selectionBox.BoxColour = Color.Yellow;
            selectionBox.FillColour = Color.FromArgb(View.SelectionBoxBackgroundOpacity, Color.White);
            selectionBox.State.Changed += SelectionBoxChanged;
            States.Add(selectionBox);

            emptyBox = new BoxDraggableState(this);
            emptyBox.BoxColour = Color.Yellow;
            emptyBox.FillColour = Color.FromArgb(View.SelectionBoxBackgroundOpacity, Color.White);
            emptyBox.State.Changed += EmptyBoxChanged;
            States.Add(emptyBox);
        }

        public override IEnumerable<string> GetContexts()
        {
            yield return "Select Tool";
        }

        public override Image GetIcon()
        {
            return Resources.Tool_Select;
        }

        public override string GetName()
        {
            return "SelectTool";
        }

        public override void ToolSelected(bool preventHistory)
        {
            IgnoreGroupingChanged();

            Mediator.Subscribe(EditorMediator.SelectionChanged, this);
            Mediator.Subscribe(EditorMediator.DocumentTreeStructureChanged, this);
            Mediator.Subscribe(EditorMediator.DocumentTreeObjectsChanged, this);
            Mediator.Subscribe(EditorMediator.IgnoreGroupingChanged, this);

            SelectionChanged();
        }

        #region Selection/document changed
        private void DocumentTreeStructureChanged()
        {
            SelectionChanged();
        }

        private void DocumentTreeObjectsChanged(IEnumerable<MapObject> objects)
        {
            if (objects.Any(x => x.IsSelected))
            {
                SelectionChanged();
            }
        }

        private void SelectionChanged()
        {
            if (Document == null) return;
            UpdateBoxBasedOnSelection();

            //foreach (var widget in _widgets) widget.SelectionChanged();
        }

        /// <summary>
        /// Updates the box based on the currently selected objects.
        /// </summary>
        private void UpdateBoxBasedOnSelection()
        {
            if (Document.Selection.IsEmpty())
            {
                emptyBox.State.Start = emptyBox.State.End = null;
                if (emptyBox.State.Action == BoxAction.Drawn) emptyBox.State.Action = BoxAction.Idle;
                selectionBox.State.Start = selectionBox.State.End = null;
                selectionBox.State.Action = BoxAction.Idle;
            }
            else
            {
                emptyBox.State.Start = emptyBox.State.End = null;
                emptyBox.State.Action = BoxAction.Idle;

                var box = Document.Selection.GetSelectionBoundingBox();
                selectionBox.State.Start = box.Start;
                selectionBox.State.End = box.End;
                selectionBox.State.Action = BoxAction.Drawn;
            }
        }
        #endregion

        #region Ignore Grouping
        private bool IgnoreGrouping()
        {
            return Document.Map.IgnoreGrouping;
        }

        private void IgnoreGroupingChanged()
        {
            var selected = Document.Selection.GetSelectedObjects().ToList();
            var select = new List<MapObject>();
            var deselect = new List<MapObject>();
            if (Document.Map.IgnoreGrouping)
            {
                deselect.AddRange(selected.Where(x => x.HasChildren));
            }
            else
            {
                var parents = selected.Select(x => x.FindTopmostParent(y => y is Group || y is Entity) ?? x).Distinct();
                foreach (var p in parents)
                {
                    var children = p.FindAll();
                    var leaves = children.Where(x => !x.HasChildren);
                    if (leaves.All(selected.Contains)) select.AddRange(children.Where(x => !selected.Contains(x)));
                    else deselect.AddRange(children.Where(selected.Contains));
                }
            }
            if (deselect.Any() || select.Any())
            {
                Document.PerformAction("Apply group selection", new ChangeSelection(select, deselect));
            }
        }
        #endregion

        #region Perform selection

        /// <summary>
        /// If ignoreGrouping is disabled, this will convert the list of objects into their topmost group or entity.
        /// If ignoreGrouping is enabled, this will remove objects that have children from the list.
        /// </summary>
        /// <param name="objects">The object list to normalise</param>
        /// <param name="ignoreGrouping">True if grouping is being ignored</param>
        /// <returns>The normalised list of objects</returns>
        private static IEnumerable<MapObject> NormaliseSelection(IEnumerable<MapObject> objects, bool ignoreGrouping)
        {
            return ignoreGrouping
                       ? objects.Where(x => !x.HasChildren)
                       : objects.Select(x => x.FindTopmostParent(y => y is Group || y is Entity) ?? x).Distinct().SelectMany(x => x.FindAll());
        }

        /// <summary>
        /// Deselect (first) a list of objects and then select (second) another list.
        /// </summary>
        /// <param name="objectsToDeselect">The objects to deselect</param>
        /// <param name="objectsToSelect">The objects to select</param>
        /// <param name="deselectAll">If true, this will ignore the objectToDeselect parameter and just deselect everything</param>
        /// <param name="ignoreGrouping">If true, object groups will be ignored</param>
        private void SetSelected(IEnumerable<MapObject> objectsToDeselect, IEnumerable<MapObject> objectsToSelect, bool deselectAll, bool ignoreGrouping)
        {
            if (objectsToDeselect == null) objectsToDeselect = new MapObject[0];
            if (objectsToSelect == null) objectsToSelect = new MapObject[0];

            if (deselectAll)
            {
                objectsToDeselect = Document.Selection.GetSelectedObjects();
                // _lastTool = null;
            }

            // Normalise selections
            objectsToDeselect = NormaliseSelection(objectsToDeselect.Where(x => x != null), ignoreGrouping);
            objectsToSelect = NormaliseSelection(objectsToSelect.Where(x => x != null), ignoreGrouping);

            // Don't bother deselecting the objects we're about to select
            objectsToDeselect = objectsToDeselect.Where(x => !objectsToSelect.Contains(x));

            // Perform selections
            var deselected = objectsToDeselect.ToList();
            var selected = objectsToSelect.ToList();

            Document.PerformAction("Selection changed", new ChangeSelection(selected, deselected));
        }

        #endregion

        #region 3D interaction

        protected override void MouseDoubleClick(IViewport3D viewport, ViewportEvent e)
        {
            // Don't show Object Properties while navigating the view, because mouse cursor will be hidden
            if (Input.IsKeyDown(Key.Space)) return;

            if (Select.DoubleClick3DAction == DoubleClick3DAction.Nothing) return;
            if (!Document.Selection.IsEmpty())
            {
                if (Select.DoubleClick3DAction == DoubleClick3DAction.ObjectProperties)
                {
                    Mediator.Publish(HotkeysMediator.ObjectProperties);
                }
                else if (Select.DoubleClick3DAction == DoubleClick3DAction.TextureTool)
                {
                    Mediator.Publish(HotkeysMediator.SwitchTool, HotkeyTool.Texture);
                }
            }
        }

        private Coordinate GetIntersectionPoint(MapObject obj, Line line)
        {
            if (obj == null) return null;

            var solid = obj as Solid;
            if (solid == null) return obj.GetIntersectionPoint(line);

            return solid.Faces.Where(x => x.Opacity > 0 && !x.IsHidden)
                .Select(x => x.GetIntersectionPoint(line))
                .Where(x => x != null)
                .OrderBy(x => (x - line.Start).VectorMagnitude())
                .FirstOrDefault();
        }

        /// <summary>
        /// When the mouse is pressed in the 3D view, we want to select the clicked object.
        /// </summary>
        /// <param name="viewport">The viewport that was clicked</param>
        /// <param name="e">The click event</param>
        protected override void MouseDown(IViewport3D viewport, ViewportEvent e)
        {
            // Do not perform selection if space is down
            if (View.Camera3DPanRequiresMouseClick && Input.IsKeyDown(Key.Space)) return;

            // First, get the ray that is cast from the clicked point along the viewport frustrum
            var ray = viewport.CastRayFromScreen(e.X, e.Y);

            // Grab all the elements that intersect with the ray
            var hits = Document.Map.WorldSpawn.GetAllNodesIntersectingWith(ray);

            // Sort the list of intersecting elements by distance from ray origin
            IntersectingObjectsFor3DSelection = hits
                .Select(x => new { Item = x, Intersection = GetIntersectionPoint(x, ray) })
                .Where(x => x.Intersection != null)
                .OrderBy(x => (x.Intersection - ray.Start).VectorMagnitude())
                .Select(x => x.Item)
                .ToList();

            // By default, select the closest object
            ChosenItemFor3DSelection = IntersectingObjectsFor3DSelection.FirstOrDefault();

            // If Ctrl is down and the object is already selected, we should deselect it instead.
            var list = new[] { ChosenItemFor3DSelection };
            var desel = ChosenItemFor3DSelection != null && Input.Ctrl && ChosenItemFor3DSelection.IsSelected;
            SetSelected(desel ? list : null, desel ? null : list, !Input.Ctrl, IgnoreGrouping());
        }

        protected override void MouseUp(IViewport3D viewport, ViewportEvent e)
        {
            IntersectingObjectsFor3DSelection = null;
            ChosenItemFor3DSelection = null;
            viewport.ReleaseInputLock(this);
        }

        protected override void MouseWheel(IViewport3D viewport, ViewportEvent e)
        {
            // If we're not in 3D cycle mode, carry on
            if (IntersectingObjectsFor3DSelection == null || ChosenItemFor3DSelection == null)
            {
                return;
            }

            var desel = new List<MapObject>();
            var sel = new List<MapObject>();

            // Select (or deselect) the current element
            if (ChosenItemFor3DSelection.IsSelected) desel.Add(ChosenItemFor3DSelection);
            else sel.Add(ChosenItemFor3DSelection);

            // Get the index of the current element
            var index = IntersectingObjectsFor3DSelection.IndexOf(ChosenItemFor3DSelection);
            if (index < 0) return;

            // Move the index in the mouse wheel direction, cycling if needed
            var dir = e.Delta / Math.Abs(e.Delta);
            index = (index + dir) % IntersectingObjectsFor3DSelection.Count;
            if (index < 0) index += IntersectingObjectsFor3DSelection.Count;

            ChosenItemFor3DSelection = IntersectingObjectsFor3DSelection[index];

            // Select (or deselect) the new current element
            if (ChosenItemFor3DSelection.IsSelected) desel.Add(ChosenItemFor3DSelection);
            else sel.Add(ChosenItemFor3DSelection);

            SetSelected(desel, sel, false, IgnoreGrouping());
        }

        // todo - select tool - capturing mouse wheel (input lock)

        #endregion

        #region 2D interaction

        protected override void OnDraggableClicked(IViewport2D viewport, ViewportEvent e, Coordinate position, IDraggable draggable)
        {
            var ctrl = Input.Ctrl;
            if (draggable == emptyBox || ctrl)
            {
                var desel = new List<MapObject>();
                var sel = new List<MapObject>();
                var seltest = SelectionTest(viewport, e);
                if (seltest != null)
                {
                    if (!ctrl || !seltest.IsSelected) sel.Add(seltest);
                    else desel.Add(seltest);
                }
                SetSelected(desel, sel, !ctrl, IgnoreGrouping());
            }
            else if (selectionBox.State.Action == BoxAction.Drawn && draggable is ResizeTransformHandle && ((ResizeTransformHandle) draggable).Handle == ResizeHandle.Center)
            {
                selectionBox.Cycle();
            }
            e.Handled = !ctrl || draggable == emptyBox;
        }

        protected override void OnDraggableDragStarted(IViewport2D viewport, ViewportEvent e, Coordinate position, IDraggable draggable)
        {
            var ctrl = Input.Ctrl;
            if (draggable == emptyBox && !ctrl && !Document.Selection.IsEmpty())
            {
                SetSelected(null, null, true, IgnoreGrouping());
            }
        }

        protected override void OnDraggableDragMoved(IViewport2D viewport, ViewportEvent e, Coordinate previousPosition, Coordinate position, IDraggable draggable)
        {
            base.OnDraggableDragMoved(viewport, e, previousPosition, position, draggable);
            if (selectionBox.State.Action == BoxAction.Resizing && draggable is ITransformationHandle)
            {
                var tform = selectionBox.GetTransformationMatrix(viewport, Document);
                if (tform.HasValue)
                {
                    Document.SetSelectListTransform(tform.Value);
                    var box = new Box(selectionBox.State.OrigStart, selectionBox.State.OrigEnd);
                    var trans = CreateMatrixMultTransformation(tform.Value);
                    Mediator.Publish(EditorMediator.SelectionBoxChanged, box.Transform(trans));
                }
            }
        }

        protected override void OnDraggableDragEnded(IViewport2D viewport, ViewportEvent e, Coordinate position, IDraggable draggable)
        {
            var tt = draggable as ITransformationHandle;
            if (selectionBox.State.Action == BoxAction.Resizing && tt != null)
            {
                // Execute the transform on the selection
                var tform = selectionBox.GetTransformationMatrix(viewport, Document);
                if (tform.HasValue)
                {
                    var createClone = Input.Shift && draggable is ResizeTransformHandle && ((ResizeTransformHandle) draggable).Handle == ResizeHandle.Center;
                    ExecuteTransform(tt.Name, CreateMatrixMultTransformation(tform.Value), createClone);
                }
            }
            Document.EndSelectionTransform();
            base.OnDraggableDragEnded(viewport, e, position, draggable);
        }

        private void EmptyBoxChanged(object sender, EventArgs e)
        {
            if (emptyBox.State.Action != BoxAction.Idle && selectionBox.State.Action != BoxAction.Idle)
            {
                selectionBox.State.Action = BoxAction.Idle;
                // We're drawing a selection box, so clear the current tool
                // SetCurrentTool(null);
            }
            if (emptyBox.State.Action == BoxAction.Drawn && Select.AutoSelectBox)
            {
                // BoxDrawnConfirm(emptyBox.State.Viewport);
            }
        }

        private void SelectionBoxChanged(object sender, EventArgs e)
        {
            
        }

        private MapObject SelectionTest(IViewport2D viewport, ViewportEvent e)
        {
            // Create a box to represent the click, with a tolerance level
            var unused = viewport.GetUnusedCoordinate(new Coordinate(100000, 100000, 100000));
            var tolerance = 4 / viewport.Zoom; // Selection tolerance of four pixels
            var used = viewport.Expand(new Coordinate(tolerance, tolerance, 0));
            var add = used + unused;
            var click = viewport.Expand(viewport.ScreenToWorld(e.X, viewport.Height - e.Y));
            var box = new Box(click - add, click + add);

            var centerHandles = Select.DrawCenterHandles;
            var centerOnly = Select.ClickSelectByCenterHandlesOnly;
            // Get the first element that intersects with the box, selecting or deselecting as needed
            return Document.Map.WorldSpawn.GetAllNodesIntersecting2DLineTest(box, centerHandles, centerOnly).FirstOrDefault();
        }

        protected override void KeyDown(IViewport2D viewport, ViewportEvent e)
        {
            var nudge = GetNudgeValue(e.KeyValue, Input.Ctrl);
            if (nudge != null && (selectionBox.State.Action == BoxAction.Drawn) && !Document.Selection.IsEmpty())
            {
                var translate = viewport.Expand(nudge);
                var transformation = Matrix4.CreateTranslation((float)translate.X, (float)translate.Y, (float)translate.Z);
                ExecuteTransform("Nudge", CreateMatrixMultTransformation(transformation), Input.Shift);
                SelectionChanged();
            }
            base.KeyDown(viewport, e);
        }

        #endregion

        #region Box confirm/cancel

        public override void KeyDown(IMapViewport viewport, ViewportEvent e)
        {
            if (e.KeyValue == Key.Enter || e.KeyValue == Key.KeypadEnter)
            {
                Confirm(viewport);
            }
            else if (e.KeyValue == Key.Escape)
            {
                Cancel(viewport);
            }
            base.KeyDown(viewport, e);
        }

        /// <summary>
        /// Once a box is confirmed, we select all element intersecting with the box (contained within if shift is down).
        /// </summary>
        /// <param name="viewport">The viewport that the box was confirmed in</param>
        private void Confirm(IMapViewport viewport)
        {
            // Only confirm the box if the empty box is drawn
            if (selectionBox.State.Action != BoxAction.Idle || emptyBox.State.Action != BoxAction.Drawn) return;

            Box boundingbox;
            if (GetSelectionBox(emptyBox.State, out boundingbox))
            {
                // If the shift key is down, select all brushes that are fully contained by the box
                // If select by handles only is on, select all brushes with centers inside the box
                // Otherwise, select all brushes that intersect with the box
                Func<Box, IEnumerable<MapObject>> selector = x => Document.Map.WorldSpawn.GetAllNodesIntersectingWith(x);
                if (Select.BoxSelectByCenterHandlesOnly) selector = x => Document.Map.WorldSpawn.GetAllNodesWithCentersContainedWithin(x);
                if (Input.Shift) selector = x => Document.Map.WorldSpawn.GetAllNodesContainedWithin(x);

                var nodes = selector(boundingbox).ToList();
                SetSelected(null, nodes, false, IgnoreGrouping());
            }

            SelectionChanged();
        }

        private void Cancel(IMapViewport viewport)
        {
            if (selectionBox.State.Action != BoxAction.Idle && !Document.Selection.IsEmpty())
            {
                SetSelected(null, null, true, IgnoreGrouping());
            }
            selectionBox.State.Action = emptyBox.State.Action = BoxAction.Idle;
            SelectionChanged();
        }

        #endregion

        #region Transform stuff

        /// <summary>
        /// Runs the transform on all the currently selected objects
        /// </summary>
        /// <param name="transformationName">The name of the transformation</param>
        /// <param name="transform">The transformation to apply</param>
        /// <param name="clone">True to create a clone before transforming the original.</param>
        private void ExecuteTransform(string transformationName, IUnitTransformation transform, bool clone)
        {
            if (clone) transformationName += "-clone";
            var objects = Document.Selection.GetSelectedParents().ToList();
            var name = String.Format("{0} {1} object{2}", transformationName, objects.Count, (objects.Count == 1 ? "" : "s"));

            var cad = new CreateEditDelete();
            var action = new ActionCollection(cad);

            if (clone)
            {
                // Copy the selection, transform it, and reselect
                var copies = ClipboardManager.CloneFlatHeirarchy(Document, Document.Selection.GetSelectedObjects()).ToList();
                foreach (var mo in copies)
                {
                    mo.Transform(transform, Document.Map.GetTransformFlags());
                    if (Select.KeepVisgroupsWhenCloning) continue;
                    foreach (var o in mo.FindAll()) o.Visgroups.Clear();
                }
                cad.Create(Document.Map.WorldSpawn.ID, copies);
                var sel = new ChangeSelection(copies.SelectMany(x => x.FindAll()), Document.Selection.GetSelectedObjects());
                action.Add(sel);
            }
            else
            {
                // Transform the selection
                cad.Edit(objects, new TransformEditOperation(transform, Document.Map.GetTransformFlags()));
            }

            // Execute the action
            Document.PerformAction(name, action);
        }

        private IUnitTransformation CreateMatrixMultTransformation(Matrix4 mat)
        {
            return new UnitMatrixMult(mat);
        }

        #endregion

        public override void ToolDeselected(bool preventHistory)
        {
            Mediator.UnsubscribeAll(this);
        }

        public override HotkeyTool? GetHotkeyToolType()
        {
            return HotkeyTool.Selection;
        }

        public override HotkeyInterceptResult InterceptHotkey(HotkeysMediator hotkeyMessage, object parameters)
        {
            return HotkeyInterceptResult.Continue;
        }
    }
}