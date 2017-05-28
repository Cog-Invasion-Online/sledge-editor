using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Sledge.BspEditor.Primitives;
using Sledge.BspEditor.Primitives.MapObjectData;
using Sledge.BspEditor.Primitives.MapObjects;
using Sledge.BspEditor.Tools.Brush.Brushes.Controls;
using Sledge.Common;
using Sledge.Common.Shell.Components;
using Sledge.DataStructures.Geometric;

namespace Sledge.BspEditor.Tools.Brush.Brushes
{
    [Export(typeof(IBrush))]
    [OrderHint("B")]
    public class TetrahedronBrush : IBrush
    {
        private readonly BooleanControl _useCentroid;

        public TetrahedronBrush()
        {
            _useCentroid = new BooleanControl(this) { LabelText = "Top vertex at centroid", Checked = false };
        }

        public string Name => "Tetrahedron";

        public bool CanRound => true;

        public IEnumerable<BrushControl> GetControls()
        {
            yield return _useCentroid;
        }

        public IEnumerable<IMapObject> Create(UniqueNumberGenerator generator, Box box, string texture, int roundDecimals)
        {
            var useCentroid = _useCentroid.GetValue();

            // The lower Z plane will be the triangle, with the lower Y value getting the two corners
            var c1 = new Coordinate(box.Start.X, box.Start.Y, box.Start.Z).Round(roundDecimals);
            var c2 = new Coordinate(box.End.X, box.Start.Y, box.Start.Z).Round(roundDecimals);
            var c3 = new Coordinate(box.Center.X, box.End.Y, box.Start.Z).Round(roundDecimals);
            var centroid = new Coordinate((c1.X + c2.X + c3.X) / 3, (c1.Y + c2.Y + c3.Y) / 3, box.End.Z);
            var c4 = (useCentroid ? centroid : new Coordinate(box.Center.X, box.Center.Y, box.End.Z)).Round(roundDecimals);

            var faces = new[] {
                new[] { c1, c2, c3 },
                new[] { c4, c1, c3 },
                new[] { c4, c3, c2 },
                new[] { c4, c2, c1 }
            };

            var solid = new Solid(generator.Next("MapObject"));
            solid.Data.Add(new ObjectColor(Colour.GetRandomBrushColour()));

            foreach (var arr in faces)
            {
                var face = new Face(generator.Next("Face"))
                {
                    Plane = new Plane(arr[0], arr[1], arr[2]),
                    Texture = { Name = texture }
                };
                face.Vertices.AddRange(arr);
                solid.Data.Add(face);
            }
            solid.DescendantsChanged();
            yield return solid;
        }
    }
}
