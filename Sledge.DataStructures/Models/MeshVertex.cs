using System.Collections.Generic;
using Sledge.DataStructures.Geometric;
using OpenTK.Graphics;

namespace Sledge.DataStructures.Models
{
    public class MeshVertex
    {
        public CoordinateF Location { get; set; }
        public CoordinateF Normal { get; set; }
        public Color4 Color { get; set; }
        public IEnumerable<BoneWeighting> BoneWeightings { get; private set; }
        public float TextureU { get; set; }
        public float TextureV { get; set; }

        public MeshVertex(CoordinateF location, CoordinateF normal, IEnumerable<BoneWeighting> boneWeightings, float textureU, float textureV)
        {
            Location = location;
            Normal = normal;
            BoneWeightings = boneWeightings;
            TextureU = textureU;
            TextureV = textureV;
            Color = Color4.White;
        }

        public MeshVertex(CoordinateF location, CoordinateF normal, Bone bone, float textureU, float textureV)
        {
            Location = location;
            Normal = normal;
            BoneWeightings = new List<BoneWeighting> {new BoneWeighting(bone, 1)};
            TextureU = textureU;
            TextureV = textureV;
            Color = Color4.White;
        }
    }
}