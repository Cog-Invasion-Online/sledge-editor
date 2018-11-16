using System.Collections.Generic;
using System.Linq;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using Sledge.Common;
using Sledge.DataStructures.Models;
using Sledge.Graphics.Arrays;

namespace Sledge.Editor.Rendering.Arrays
{
    public class ModelArray : VBO<Model, MapObjectVertex>
    {
        private const int Textured = 0;
        private const int Wireframe = 1;

        public ModelArray(Model model)
            : base(new []{ model })
        {
        }

        public void RenderTextured(IGraphicsContext context)
        {
            foreach (var subset in GetSubsets<ITexture>(Textured))
            {
                ((ITexture) subset.Instance).Bind();
                Render(context, PrimitiveType.Triangles, subset);
            }
        }

        public void RenderWireframe(IGraphicsContext context)
        {
            foreach (var subset in GetSubsets(Wireframe))
            {
                Render(context, PrimitiveType.Lines, subset);
            }
        }

        protected override void CreateArray(IEnumerable<Model> objects)
        {
            foreach (var model in objects)
            {
                PushOffset(model);

                var transforms = model.GetTransforms();

                foreach (var g in model.GetActiveMeshes().GroupBy(x => x.SkinRef))
                {
                    StartSubset(Textured);
                    StartSubset(Wireframe);

                    foreach (var mesh in g)
                    {
                        foreach (var vertex in mesh.Vertices)
                        {
                            var transform = transforms[vertex.BoneWeightings.First().Bone.BoneIndex];
                            var c = vertex.Location * transform;
                            var n = vertex.Normal * transform;
                            var index = PushData(new[]
                            {
                                new MapObjectVertex
                                {
                                    Position = new Vector3(c.X, c.Y, c.Z),
                                    Normal = new Vector3(n.X, n.Y, n.Z),
                                    Colour = vertex.Color,
                                    Texture = new Vector2(vertex.TextureU, vertex.TextureV),
                                    IsSelected = 0
                                }
                            });
                            PushIndex(Textured, index, new[] {(uint) 0});
                            PushIndex(Wireframe, index, new[] { (uint)0 });
                        }
                    }
                    if (model.Textures.Count > 0)
                    {
                        var tex = model.Textures[g.Key];
                        PushSubset(Textured, tex.TextureObject);
                    }
                    PushSubset(Wireframe, (object)null);
                }

                

            }
        }
    }
}