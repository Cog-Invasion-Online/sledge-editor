﻿using System;
using System.Runtime.Serialization;
using Sledge.DataStructures.Geometric;

namespace Sledge.BspEditor.Primitives.MapData
{
    [Serializable]
    public class Camera : IMapData, ISerializable
    {
        public Coordinate EyePosition { get; set; }
        public Coordinate LookPosition { get; set; }
        public bool IsActive { get; set; }

        public Camera()
        {
            EyePosition = new Coordinate(0, 0, 0);
            LookPosition = new Coordinate(0, 0, 0);
            IsActive = false;
        }

        protected Camera(SerializationInfo info, StreamingContext context)
        {
            EyePosition = (Coordinate)info.GetValue("EyePosition", typeof(Coordinate));
            LookPosition = (Coordinate)info.GetValue("LookPosition", typeof(Coordinate));
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("EyePosition", EyePosition);
            info.AddValue("LookPosition", LookPosition);
        }

        public decimal Length
        {
            get { return (LookPosition - EyePosition).VectorMagnitude(); }
            set { LookPosition = EyePosition + Direction * value; }
        }

        public Coordinate Direction
        {
            get { return (LookPosition - EyePosition).Normalise(); }
            set { LookPosition = EyePosition + value.Normalise() * Length; }
        }
    }
}