using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.CodeDom.Compiler;
using System.CodeDom;
using Sledge.FileSystem;
using Sledge.DataStructures.Geometric;
using OpenTK.Graphics;

namespace Sledge.Providers.Model
{

    // GeomEnums
    public enum UsageHint
    {
        UH_client = 0,
        UH_stream,
        UH_dynamic,
        UH_static,
        UH_unspecified,
    }

    public enum ShadeModel
    {
        SM_uniform = 0,
        SM_smooth,
        SM_flat_first_vertex,
        SM_flat_last_vertex,
    }

    public enum Contents
    {
        C_other = 0,
        C_point,
        C_clip_point,
        C_vector,
        C_texcoord,
        C_color,
        C_index,
        C_morph_delta,
        C_matrix,
        C_normal,
    }

    public enum NumericType
    {
        NT_uint8 = 0,
        NT_uint16,
        NT_uint32,
        NT_packed_dcba,
        NT_packed_dabc,
        NT_float32,
        NT_float64,
        NT_stdfloat,
    }

    public enum PrimitiveType
    {
        PT_none,
        PT_polygons,
        PT_lines,
        PT_points,
        PT_patches,
    }

    public class TypedWritable
    {
        public virtual void read_datagram(BamReader manager, BinaryReader br)
        {
        }
    }

    public class InternalName : TypedWritable
    {
        public string _name;

        public InternalName()
        {
            _name = "";
        }

        public InternalName(string name)
        {
            _name = name;
        }

        public override void read_datagram(BamReader manager, BinaryReader br)
        {
            _name = br.ReadPandaString();
        }
    }

    public class GeomVertexColumn
    {
        public string name;
        public int num_components;
        public NumericType numeric_type;
        public Contents contents;
        public int start;
        public int column_alignment;
    }

    public class GeomVertexArrayFormat : TypedWritable
    {
        public int _stride;
        public int _total_bytes;
        public int _pad_to;
        public int _divisor;

        public List<GeomVertexColumn> _columns;

        public GeomVertexArrayFormat()
        {
            _columns = new List<GeomVertexColumn>();
        }

        public void add_column(GeomVertexColumn column)
        {
            _columns.Add(column);
        }

        public override void read_datagram(BamReader manager, BinaryReader br)
        {
            _stride = br.ReadUInt16();
            _total_bytes = br.ReadUInt16();
            _pad_to = br.ReadChar();
            _divisor = br.ReadUInt16();
            int num_columns = br.ReadUInt16();
            for (int i = 0; i < num_columns; i++)
            {
                GeomVertexColumn column = new GeomVertexColumn();
                InternalName name = (InternalName)manager.read_pointer(br);
                column.name = name._name;
                column.num_components = br.ReadChar();
                column.numeric_type = (NumericType)br.ReadChar();
                column.contents = (Contents)br.ReadChar();
                column.start = br.ReadUInt16();
                column.column_alignment = br.ReadChar();
                add_column(column);
            }
        }
    }

    public class GeomVertexFormat : TypedWritable
    {
        public List<GeomVertexArrayFormat> _arrays;
        public int _anim_type;
        public int _num_transforms;
        public bool _indexed_transforms;

        public GeomVertexFormat()
        {
            _arrays = new List<GeomVertexArrayFormat>();
        }

        public bool has_column(string column)
        {
            return get_array_with(column) != -1;
        }

        public int get_array_with(string column)
        {
            for (int i = 0; i < _arrays.Count; i++)
            {
                GeomVertexArrayFormat arr = _arrays[i];
                foreach (GeomVertexColumn col in arr._columns)
                {
                    if (col.name == column)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        public GeomVertexColumn get_column(string name)
        {
            int arr = get_array_with(name);

            if (arr == -1)
            {
                return null;
            }

            foreach (GeomVertexColumn col in _arrays[arr]._columns)
            {
                if (col.name == name)
                {
                    return col;
                }
            }

            return null;
        }

        public override void read_datagram(BamReader manager, BinaryReader br)
        {
            _anim_type = br.ReadChar();
            _num_transforms = br.ReadUInt16();
            _indexed_transforms = br.ReadBoolean();

            int num_arrays = br.ReadUInt16();
            for (int i = 0; i < num_arrays; i++)
            {
                GeomVertexArrayFormat array = (GeomVertexArrayFormat)manager.read_pointer(br);
                _arrays.Add(array);
            }
        }
    }

    public class GeomVertexArrayData : TypedWritable
    {
        public UsageHint _usage_hint;
        public GeomVertexArrayFormat _array_format;
        public byte[] _buffer;

        public override void read_datagram(BamReader manager, BinaryReader br)
        {
            _array_format = (GeomVertexArrayFormat)manager.read_pointer(br);
            _usage_hint = (UsageHint)br.ReadChar();

            uint length = br.ReadUInt32();
            _buffer = br.ReadByteArray((int)length);
        }
    }

    public class GeomVertexData : TypedWritable
    {
        public string _name;
        public GeomVertexFormat _format;
        public UsageHint _usage_hint;
        public List<GeomVertexArrayData> _arrays;

        public GeomVertexData()
        {
            _arrays = new List<GeomVertexArrayData>();
        }

        public int get_num_rows()
        {
            int stride = _format._arrays[0]._stride;
            int rows = _arrays[0]._buffer.Count() / stride;
            return rows;
        }

        public override void read_datagram(BamReader manager, BinaryReader br)
        {
            _name = br.ReadPandaString();
            _format = (GeomVertexFormat)manager.read_pointer(br);
            _usage_hint = (UsageHint)br.ReadChar();
            int num_arrays = br.ReadUInt16();
            for (int i = 0; i < num_arrays; i++)
            {
                _arrays.Add((GeomVertexArrayData)manager.read_pointer(br));
            }
        }

    }

    public class TexcoordF
    {
        public float _x;
        public float _y;

        public TexcoordF(float x, float y)
        {
            _x = x;
            _y = y;
        }

        public void divide(float scalar)
        {
            _x /= scalar;
            _y /= scalar;
        }

        public override string ToString()
        {
            return String.Format("TexcoordF({0}, {1})", _x, _y);
        }
    }

    public class GeomVertexReader
    {
        GeomVertexData _data;
        int _pointer;
        int _stride;
        GeomVertexArrayFormat _array;
        int _array_idx;
        GeomVertexColumn _column;

        public GeomVertexReader(GeomVertexData data)
        {
            _data = data;
        }

        public void set_row(int row)
        {
            _pointer = _column.start + (_array._stride * row);
        }

        public void set_column(string column)
        {
            GeomVertexFormat format = _data._format;
            _array_idx = format.get_array_with(column);
            _array = format._arrays[_array_idx];
            _stride = _array._stride;
            _column = format.get_column(column);
            set_row(0);
        }

        private void inc_pointer()
        {
            _pointer += _array._stride;
        }

        private void ReportPointer()
        {
            Console.WriteLine("Pointer: {0}, stride: {1}, column start: {2}", _pointer, _stride, _column.start);
        }

        public TexcoordF get_data_2f()
        {
            if (_column.num_components != 2)
            {
                inc_pointer();
                return new TexcoordF(0, 0);
            }

            byte[] buf = _data._arrays[_array_idx]._buffer;
            TexcoordF v2 = null;

            int p = _pointer;

            switch (_column.numeric_type)
            {
                case NumericType.NT_uint8:
                    {
                        v2 = new TexcoordF(buf[p], buf[p + 1]);
                        v2.divide(255);
                        break;
                    }

                case NumericType.NT_uint16:
                    {
                        p /= 2;
                        UInt16[] pi = new UInt16[buf.Length / 2];
                        Buffer.BlockCopy(buf, 0, pi, 0, buf.Length);
                        v2 = new TexcoordF(pi[p], pi[p + 1]);
                        v2.divide(65535);
                        break;
                    }

                case NumericType.NT_uint32:
                    {
                        p /= 4;
                        UInt32[] pi = new UInt32[buf.Length / 4];
                        Buffer.BlockCopy(buf, 0, pi, 0, buf.Length);
                        v2 = new TexcoordF(pi[p], pi[p + 1]);
                        v2.divide(4294967295);
                        break;
                    }

                case NumericType.NT_float32:
                    {
                        p /= 4;
                        float[] pi = new float[buf.Length / 4];
                        Buffer.BlockCopy(buf, 0, pi, 0, buf.Length);
                        v2 = new TexcoordF(pi[p], pi[p + 1]);
                        break;
                    }
                case NumericType.NT_float64:
                    {
                        p /= 8;
                        double[] pi = new double[buf.Length / 8];
                        Buffer.BlockCopy(buf, 0, pi, 0, buf.Length);
                        v2 = new TexcoordF((float)pi[p], (float)pi[p + 1]);
                        break;
                    }

                default:
                    Console.WriteLine("Error: Unknown numeric type {0}", _column.numeric_type);
                    break;

            }

            inc_pointer();
            return v2;
        }

        public CoordinateF get_data_3f()
        {
            //if (_column.num_components != 3)
            //{
            //    inc_pointer();
            //    Console.WriteLine("GetData3F: num components not 3 but {0}", _column.num_components);
            //    return new CoordinateF(0, 0, 0);
            //}

            byte[] buf = _data._arrays[_array_idx]._buffer;
            CoordinateF v3 = null;

            int p = _pointer;

            switch (_column.numeric_type)
            {
                case NumericType.NT_uint8:
                    {
                        v3 = new CoordinateF(buf[p], buf[p + 1], buf[p + 2]);
                        v3 = v3.ComponentDivide(new CoordinateF(255, 255, 255));
                        break;
                    }
                    
                case NumericType.NT_uint16:
                    {
                        p /= 2;
                        UInt16[] pi = new UInt16[buf.Length / 2];
                        Buffer.BlockCopy(buf, 0, pi, 0, buf.Length);
                        v3 = new CoordinateF(pi[p], pi[p + 1], pi[p + 2]);
                        v3 = v3.ComponentDivide(new CoordinateF(65535, 65535, 65535));
                        break;
                    }
                    
                case NumericType.NT_uint32:
                    {
                        p /= 4;
                        UInt32[] pi = new UInt32[buf.Length / 4];
                        Buffer.BlockCopy(buf, 0, pi, 0, buf.Length);
                        v3 = new CoordinateF(pi[p], pi[p + 1], pi[p + 2]);
                        v3 = v3.ComponentDivide(new CoordinateF(4294967295, 4294967295, 4294967295));
                        break;
                    }

                case NumericType.NT_float32:
                    {
                        p /= 4;
                        float[] pi = new float[buf.Length / 4];
                        Buffer.BlockCopy(buf, 0, pi, 0, buf.Length);
                        v3 = new CoordinateF(pi[p], pi[p + 1], pi[p + 2]);
                        break;
                    }
                case NumericType.NT_float64:
                    {
                        p /= 8;
                        double[] pi = new double[buf.Length / 8];
                        Buffer.BlockCopy(buf, 0, pi, 0, buf.Length);
                        v3 = new CoordinateF((float)pi[p], (float)pi[p + 1], (float)pi[p + 2]);
                        break;
                    }

                default:
                    Console.WriteLine("Error: Unknown numeric type {0}", _column.numeric_type);
                    break;
                    
            }

            inc_pointer();
            return v3;
        }
    }

    public class GeomPrimitive : TypedWritable
    {
        public ShadeModel _shade_model;
        public int _first_vertex;
        public int _num_vertices;
        public NumericType _index_type;
        public UsageHint _usage_hint;
        public GeomVertexArrayData _vertices;
        public List<uint> _ends;

        public override void read_datagram(BamReader manager, BinaryReader br)
        {
            _shade_model = (ShadeModel)br.ReadChar();
            _first_vertex = br.ReadInt32();
            _num_vertices = br.ReadInt32();
            _index_type = (NumericType)br.ReadChar();
            _usage_hint = (UsageHint)br.ReadChar();
            _vertices = (GeomVertexArrayData)manager.read_pointer(br);
            _ends = manager.read_pta_int(br);
        }

        public int DataSizeBytes()
        {
            return _vertices._buffer.Length;
        }

        public int GetNumVerts()
        {
            if (_num_vertices != -1)
            {
                return _num_vertices;
            }

            int stride = _vertices._array_format._stride;
            return DataSizeBytes() / stride;
        }

        public int GetNumPrims()
        {
            if (NumVertsPerPrim() == 0)
            {
                return _ends.Count;
            }

            return GetNumVerts() / NumVertsPerPrim();
        }

        public int GetVert(int i)
        {
            if (_vertices != null)
            {
                switch(_index_type)
                {
                    case NumericType.NT_uint8:
                        return _vertices._buffer[i];
                    case NumericType.NT_uint16:
                        {
                            UInt16[] pi = new UInt16[_vertices._buffer.Length / 2];
                            Buffer.BlockCopy(_vertices._buffer, 0, pi, 0, _vertices._buffer.Length);
                            return pi[i];
                        }
                    case NumericType.NT_uint32:
                        {
                            UInt32[] pi = new UInt32[_vertices._buffer.Length / 4];
                            Buffer.BlockCopy(_vertices._buffer, 0, pi, 0, _vertices._buffer.Length);
                            return (int)pi[i];
                        }
                        
                }
            }

            return _first_vertex + i;
        }

        public int GetPrimStart(int n)
        {
            if (NumVertsPerPrim() == 0)
            {
                if (n == 0)
                {
                    return 0;
                }
                else
                {
                    return (int)_ends[n - 1];
                }
            }

            return n * (NumVertsPerPrim() + 0);
        }

        public int GetPrimEnd(int n)
        {
            if (NumVertsPerPrim() == 0)
            {
                return (int)_ends[n];
            }

            return n * (NumVertsPerPrim() + 0) + NumVertsPerPrim();
        }

        public virtual int NumVertsPerPrim()
        {
            return 0;
        }
    }

    public class GeomTriangles : GeomPrimitive
    {
        public override int NumVertsPerPrim()
        {
            return 3;
        }

        public override void read_datagram(BamReader manager, BinaryReader br)
        {
            base.read_datagram(manager, br);
        }
    }

    public class GeomTristrips : GeomPrimitive
    {
        public override void read_datagram(BamReader manager, BinaryReader br)
        {
            base.read_datagram(manager, br);
        }
    }

    public class Geom : TypedWritable
    {
        public GeomVertexData _data;
        public List<GeomPrimitive> _primitives;
        public PrimitiveType _primitive_type;
        public ShadeModel _shade_model;
        public int _bounds_type;

        public Geom()
        {
            _primitives = new List<GeomPrimitive>();
        }

        public override void read_datagram(BamReader manager, BinaryReader br)
        {
            _data = (GeomVertexData)manager.read_pointer(br);
            int num_primitives = br.ReadUInt16();
            for (int i = 0; i < num_primitives; i++)
            {
                _primitives.Add((GeomPrimitive)manager.read_pointer(br));
            }
            _primitive_type = (PrimitiveType)br.ReadChar();
            _shade_model = (ShadeModel)br.ReadChar();
            br.ReadUInt16(); // reserved
            _bounds_type = br.ReadChar();
        }
    }

    public class Nameable : TypedWritable
    {
        public string _name;

        public override void read_datagram(BamReader manager, BinaryReader br)
        {
            _name = br.ReadPandaString();
        }
    }

    public struct TraverseData
    {
        public TransformState transform;
        public RenderState state;

        public TraverseData Compose(TraverseData other)
        {
            TraverseData dat = new TraverseData();
            dat.transform = transform.Compose(other.transform);
            dat.state = state.Compose(other.state);

            return dat;
        }

        public TraverseData Compose(TransformState otransform, RenderState ostate)
        {
            TraverseData dat = new TraverseData();
            dat.transform = transform.Compose(otransform);
            dat.state = state.Compose(ostate);

            return dat;
        }
    }

    public class TransformState : TypedWritable
    {
        public CoordinateF _pos;
        public CoordinateF _hpr;
        public CoordinateF _scale;
        public CoordinateF _shear;
        public QuaternionF _quat;
        public MatrixF _mat;
        public Flags _flags;

        public override string ToString()
        {
            return String.Format("Pos: {0}, Hpr: {1}, Scale: {2}", _pos.ToString(), _hpr.ToString(), _scale.ToString());
        }

        public enum Flags
        {
            F_is_identity = 0x00000001,
            F_is_singular = 0x00000002,
            F_singular_known = 0x00000004,  // set if we know F_is_singular
            F_components_given = 0x00000008,
            F_components_known = 0x00000010,  // set if we know F_has_components
            F_has_components = 0x00000020,
            F_mat_known = 0x00000040,  // set if _mat is defined
            F_is_invalid = 0x00000080,
            F_quat_given = 0x00000100,
            F_quat_known = 0x00000200,  // set if _quat is defined
            F_hpr_given = 0x00000400,
            F_hpr_known = 0x00000800,  // set if _hpr is defined
            F_uniform_scale = 0x00001000,
            F_identity_scale = 0x00002000,
            F_has_nonzero_shear = 0x00004000,
            F_is_destructing = 0x00008000,
            F_is_2d = 0x00010000,
            F_hash_known = 0x00020000,
            F_norm_quat_known = 0x00040000,
        }

        public TransformState()
        {
            _flags = Flags.F_is_identity;
            _pos = new CoordinateF(0, 0, 0);
            _hpr = new CoordinateF(0, 0, 0);
            _scale = new CoordinateF(1, 1, 1);
            _shear = new CoordinateF(0, 0, 0);
            _quat = QuaternionF.Identity;
            _mat = MatrixF.Identity;
        }

        public TransformState Compose(TransformState other)
        {
            CoordinateF pos = _pos;
            QuaternionF quat = _quat;
            CoordinateF scale = _scale;

            pos = pos + quat.Rotate(other._pos);
            quat = other._quat.Normalise() * quat;
            scale = other._scale.ComponentMultiply(scale);

            TransformState ts = new TransformState();
            ts._pos = pos;
            ts._quat = quat;
            ts._hpr = quat.GetEulerAngles();
            ts._scale = scale;
            ts._flags = Flags.F_components_given | Flags.F_quat_given | Flags.F_components_known | Flags.F_quat_known | Flags.F_has_components;

            return ts;
        }

        public override void read_datagram(BamReader manager, BinaryReader br)
        {
            _flags = (Flags)br.ReadUInt32();

            if ((_flags & Flags.F_components_given) != 0)
            {
                _pos = br.ReadCoordinateF();
                if ((_flags & Flags.F_quat_given) != 0)
                {
                    CoordinateF vector = br.ReadCoordinateF();
                    float temp = vector.X;
                    vector.X = vector.Y;
                    vector.Y = temp;
                    float scalar = br.ReadSingle();
                    _quat = new QuaternionF(vector, scalar);
                    _hpr = _quat.GetEulerAngles();
                }
                else
                {
                    _hpr = br.ReadCoordinateF();
                }

                _scale = br.ReadCoordinateF();
                _shear = br.ReadCoordinateF();
            }

            if ((_flags & Flags.F_mat_known) != 0)
            {
                float[] init = br.ReadSingleArray(16);
                _mat = new MatrixF(init);
                _pos = _mat.Shift;
                //_hpr = _mat.Y;
                _scale = new CoordinateF(_mat[0], _mat[5], _mat[10]);
            }
        }
    }

    public class RenderAttrib : TypedWritable
    {

    }

    public class BSPMaterialAttrib : RenderAttrib
    {
        public string _matfile;
        public GenericStructure _matstructure;
        public Texture _basetexture;

        public static Dictionary<string, GenericStructure> MatCache = new Dictionary<string, GenericStructure>();
        public static GenericStructure GetFromFile(string file, IFile root)
        {
            if (MatCache.ContainsKey(file))
            {
                return MatCache[file];
            }

            IFile parent = root.Parent;
            while (parent.Parent != null)
            {
                parent = parent.Parent;
            }
            IFile tfile = parent.TraversePath(file);
            StringBuilder str = new StringBuilder(Encoding.Default.GetString(tfile.ReadAll()));
            StringReader rdr = new StringReader(str.ToString());

            List<GenericStructure> gs = GenericStructure.Parse(rdr).ToList();
            if (gs.Count == 0)
            {
                MatCache[file] = null;
                return null;
            }

            MatCache[file] = gs.ElementAt(0);

            return MatCache[file];
        }

        public override void read_datagram(BamReader manager, BinaryReader br)
        {
            base.read_datagram(manager, br);

            _matfile = br.ReadPandaString();
            _matstructure = GetFromFile(_matfile, manager._file);
            if (_matstructure != null)
            {
                _basetexture = new Texture();
                _basetexture._name = "basetexture";
                _basetexture._filename = _matstructure.GetPropertyValue("$basetexture", true);
                _basetexture._alpha_filename = "";
            }
            else
            {
                _basetexture = null;
            }
        }
    }

    public class TextureAttrib : RenderAttrib
    {
        public Texture _texture;

        public override void read_datagram(BamReader manager, BinaryReader br)
        {
            base.read_datagram(manager, br);

            br.ReadBoolean();

            int num_off_stages = br.ReadUInt16();
            for (int i = 0; i < num_off_stages; i++)
            {
                manager.read_pointer(br);
            }

            int num_on_stages = br.ReadUInt16();
            for (int i = 0; i < num_on_stages; i++)
            {
                manager.read_pointer(br); // texture stage pointer
                Texture texture = (Texture)manager.read_pointer(br);
                if (i == 0)
                {
                    _texture = texture;
                }
                br.ReadUInt16();
                br.ReadInt32();

                bool has_sampler = br.ReadBoolean();
                if (has_sampler)
                {
                    manager.read_pointer(br);
                }
            }
        }
    }

    public class Texture : TypedWritable
    {
        public string _name;
        public string _filename;
        public string _alpha_filename;

        public int CompareTo(Texture other)
        {
            if (_name != other._name)
            {
                return _name.CompareTo(other._name);
            }
            else if (_filename != other._filename)
            {
                return _filename.CompareTo(other._filename);
            }

            return _alpha_filename.CompareTo(other._alpha_filename);
        }

        public override void read_datagram(BamReader manager, BinaryReader br)
        {
            base.read_datagram(manager, br);

            _name = br.ReadPandaString();
            _filename = br.ReadPandaString();
            _alpha_filename = br.ReadPandaString();
        }

        public override string ToString()
        {
            return _name + ": " + _filename + ", " + _alpha_filename;
        }
    }

    public class RenderState : TypedWritable
    {
        public List<RenderAttrib> _attribs;
        public List<int> _attrib_overrides;

        public RenderState()
        {
            _attrib_overrides = new List<int>();
            _attribs = new List<RenderAttrib>();
        }

        public bool HasAttrib(Type type)
        {
            foreach (RenderAttrib attr2 in _attribs)
            {
                if (attr2.GetType().Equals(type))
                {
                    return true;
                }
            }

            return false;
        }

        public int IndexOfAttrib(Type type)
        {
            for (int i = 0; i < _attribs.Count; i++)
            {
                if (_attribs[i].GetType().Equals(type))
                {
                    return i;
                }
            }

            return -1;
        }

        public RenderAttrib GetAttrib(Type type)
        {
            foreach (RenderAttrib attr in _attribs)
            {
                if (attr.GetType().Equals(type))
                {
                    return attr;
                }
            }

            return null;
        }

        public RenderState Compose(RenderState other)
        {
            RenderState state = new RenderState();

            for (int i = 0; i < _attribs.Count; i++)
            {
                if (!other.HasAttrib(_attribs[i].GetType()))
                {
                    state._attribs.Add(_attribs[i]);
                    state._attrib_overrides.Add(_attrib_overrides[i]);
                }
            }

            for (int i = 0; i < other._attribs.Count; i++)
            {
                if (!state.HasAttrib(other._attribs[i].GetType()))
                {
                    state._attribs.Add(other._attribs[i]);
                    state._attrib_overrides.Add(other._attrib_overrides[i]);
                    continue;
                }

                int idx = state.IndexOfAttrib(other._attribs[i].GetType());
                if (state._attrib_overrides[idx] < other._attrib_overrides[i])
                {
                    state._attribs[idx] = other._attribs[i];
                    state._attrib_overrides[idx] = other._attrib_overrides[i];
                }
            }

            return state;
        }

        public override void read_datagram(BamReader manager, BinaryReader br)
        {
            base.read_datagram(manager, br);

            int num_attribs = br.ReadUInt16();
            for (int i = 0; i < num_attribs; i++)
            {
                _attribs.Add((RenderAttrib)manager.read_pointer(br));
                _attrib_overrides.Add(br.ReadInt32());
            }
        }
    }

    public class PandaNode : Nameable
    {
        public List<PandaNode> _parents;
        public List<PandaNode> _children;
        public TransformState _transform;
        public RenderState _state;

        public PandaNode()
        {
            _parents = new List<PandaNode>();
            _children = new List<PandaNode>();
        }

        public override void read_datagram(BamReader manager, BinaryReader br)
        {
            base.read_datagram(manager, br);

            _state = (RenderState)manager.read_pointer(br);
            _transform = (TransformState)manager.read_pointer(br);
            manager.read_pointer(br);

            br.ReadUInt32(); // draw control mask
            br.ReadUInt32(); // draw show mask
            br.ReadUInt32(); // into collide mask
            br.ReadChar(); // bounds type

            uint num_tags = br.ReadUInt32();
            for (uint i = 0; i < num_tags; i++)
            {
                br.ReadPandaString(); // key
                br.ReadPandaString(); // value
            }

            int num_parents = br.ReadUInt16();
            for (int i = 0; i < num_parents; i++)
            {
                _parents.Add((PandaNode)manager.read_pointer(br));
            }

            int num_children = br.ReadUInt16();
            for (int i = 0; i < num_children; i++)
            {
                _children.Add((PandaNode)manager.read_pointer(br));
                br.ReadInt32();
            }

            int num_stashed = br.ReadUInt16();
            for (int i = 0; i < num_stashed; i++)
            {
                manager.read_pointer(br);
                br.ReadInt32();
            }
        }
    }

    public class GeomNode : PandaNode
    {
        public List<Geom> _geoms;
        public List<RenderState> _geom_states;

        public GeomNode() : base()
        {
            _geoms = new List<Geom>();
            _geom_states = new List<RenderState>();
        }

        public override void read_datagram(BamReader manager, BinaryReader br)
        {
            base.read_datagram(manager, br);

            int num_geoms = br.ReadUInt16();
            for (int i = 0; i < num_geoms; i++)
            {
                _geoms.Add((Geom)manager.read_pointer(br));
                _geom_states.Add((RenderState)manager.read_pointer(br));
            }
        }
    }

    enum BamEndian
    {
        BE_bigendian = 0,
        BE_littleendian,
        BE_native,
    }

    public enum BamObjectCode
    {
        BOC_push = 0,
        BOC_pop,
        BOC_adjunct,
        BOC_remove,
    }

    public class TypeHandle
    {
        public int id;
        public string name;
        public int num_parent_classes;
    }

    public class ToFillIn
    {
        public BinaryReader scan;
        public TypedWritable obj;
        public bool finished;
    }

    public class BamReader
    {
        int _file_major;
        int _file_minor;
        BamEndian _file_endian;
        bool _file_stdfloat_double;
        int _num_extra_objects;
        int _nesting_level;
        Dictionary<int, Object> _pta_map;
        Dictionary<int, TypeHandle> _typehandle_map;
        Dictionary<TypeHandle, List<TypeHandle>> _typehandle_record;
        Dictionary<int, TypedWritable> _created_objs;
        List<ToFillIn> _tofillin;
        List<Texture> _textures;
        public IFile _file;

        public BamReader()
        {
            _typehandle_map = new Dictionary<int, TypeHandle>();
            _typehandle_record = new Dictionary<TypeHandle, List<TypeHandle>>();
            _created_objs = new Dictionary<int, TypedWritable>();
            _tofillin = new List<ToFillIn>();
            _pta_map = new Dictionary<int, object>();
            _textures = new List<Texture>();
        }

        public List<uint> read_pta_int(BinaryReader scan)
        {
            List<uint> result = new List<uint>();
            int id = read_object_id(scan);
            if (id == 0)
            {
                // null pointer
                return null;
            }

            if (!_pta_map.ContainsKey(id))
            {
                uint size = scan.ReadUInt32();
                for (uint i = 0; i < size; i++) {
                    result.Add(scan.ReadUInt32());
                }
                _pta_map.Add(id, result);
            }
            else
            {
                result = (List<uint>)_pta_map[id];
            }

            return result;
        }

        private ToFillIn get_tfi_by_obj(TypedWritable obj)
        {
            foreach (ToFillIn tfi in _tofillin)
            {
                if (tfi.obj == obj)
                {
                    return tfi;
                }
            }
            return null;
        }

        public TypedWritable read_pointer(BinaryReader scan)
        {
            int object_id = read_object_id(scan);
            
            if (object_id != 0)
            {
                if (_file_minor < 21)
                {
                    _num_extra_objects++;
                }
                if (_created_objs.ContainsKey(object_id))
                {
                    TypedWritable obj = _created_objs[object_id];
                    ToFillIn tfi = get_tfi_by_obj(obj);
                    if (!tfi.finished)
                    {
                        // We need to fill in this object now.
                        tfi.finished = true;
                        obj.read_datagram(this, tfi.scan);
                    }
                    return obj;
                }
                else
                {
                    Console.WriteLine("BamProvider.read_pointer(), object id {0} does not exist!", object_id);
                }
            }

            return null;
        }

        private BinaryReader get_datagram(BinaryReader br)
        {
            // Get the size of this datagram
            UInt32 dg_size = br.ReadUInt32();
            // Extract those bytes and advance the master reader
            byte[] data = br.ReadBytes((int)dg_size);

            // Give a new reader which only contains the specific datagram
            return new BinaryReader(new MemoryStream(data));
        }

        private static string ToLiteral(string input)
        {
            using (var writer = new StringWriter())
            {
                using (var provider = CodeDomProvider.CreateProvider("CSharp"))
                {
                    provider.GenerateCodeFromExpression(new CodePrimitiveExpression(input), writer, null);
                    return writer.ToString();
                }
            }
        }

        public DataStructures.Models.Model ReadModel(BinaryReader br, IFile file)
        {
            _file = file;

            string magic = br.ReadFixedLengthString(Encoding.UTF8, 6);
            if (magic != BamProvider.magic_number)
            {
                Console.WriteLine("Bad magic number for bam file. Expected " + BamProvider.magic_number + ", got" + magic);
                return null;
            }

            // should be a 6 byte header datagram
            BinaryReader header = get_datagram(br);

            _file_major = header.ReadUInt16();
            _file_minor = header.ReadUInt16();

            if (_file_major != BamProvider.version_major ||
                _file_minor < BamProvider.first_minor_ver ||
                _file_minor > BamProvider.version_minor)
            {
                string exc = String.Format("Bam file is version {0}.{1}.\n", _file_major, _file_minor);
                if (BamProvider.version_minor == BamProvider.first_minor_ver)
                {
                    exc += String.Format("This program can only load version {0}.{1} bams.", BamProvider.version_minor, BamProvider.first_minor_ver);
                }
                else
                {
                    exc += String.Format("This program can only load version {0}.{1} through {2}.{3} bams.",
                        BamProvider.version_major, BamProvider.first_minor_ver, BamProvider.version_major, BamProvider.version_minor);
                }
                Console.WriteLine(exc);
                return null;
            }

            _file_endian = (BamEndian)header.ReadChar();

            _file_stdfloat_double = false;
            if (_file_minor >= 27)
            {
                _file_stdfloat_double = header.ReadBoolean();
            }

            // Now that we've read the header, start recursively reading the objects.
            TypedWritable obj = read_object(br);

            // Now fill in all the objects that we have read.
            for (int i = 0; i < _tofillin.Count; i++)
            {
                ToFillIn tfi = _tofillin[i];
                if (tfi.finished)
                {
                    continue;
                }
                tfi.finished = true;
                tfi.obj.read_datagram(this, tfi.scan);
            }

            // Hooray the BAM file has been read!
            // Find the root of the scene graph, should be the PandaNode with 0 parents
            PandaNode root = null;
            foreach (TypedWritable cobj in _created_objs.Values)
            {
                if (cobj is PandaNode)
                {
                    PandaNode pnode = (PandaNode)cobj;
                    if (pnode._parents.Count == 0)
                    {
                        root = pnode;
                    }
                }
            }

            if (root != null)
            {

                // Now, we can traverse the scene graph and build up the Sledge model.
                DataStructures.Models.Model mdl = new DataStructures.Models.Model();
                mdl.Name = root._name;

                mdl.Textures.Add(new DataStructures.Models.Texture
                {
                    Name = "default",
                    Index = 0,
                    Width = 1,
                    Height = 1,
                    Flags = 0,
                    Image = new System.Drawing.Bitmap(System.Drawing.Image.FromFile("white.jpg"))
                });
                mdl.Bones.Add(new DataStructures.Models.Bone(0, -1, null, "zero", new CoordinateF(0, 0, 0),
                    new CoordinateF(0, 0, 0), new CoordinateF(1, 1, 1), new CoordinateF(1, 1, 1)));

                TraverseData dat = new TraverseData();
                dat.state = root._state;
                dat.transform = root._transform;

                r_traverse_below(ref mdl, ref root, dat);
                return mdl;
            }

            return null;
        }

        public int HasTexture(Texture tex)
        {
            if (tex == null)
                return -1;

            for (int i = 0; i < _textures.Count; i++)
            {
                if (_textures[i].CompareTo(tex) == 0)
                {
                    return i + 1;
                }
            }

            return -1;
        }

        public void r_traverse_below(ref DataStructures.Models.Model mdl, ref PandaNode root, TraverseData tdata)
        {
            if (root is GeomNode)
            {
                GeomNode gn = (GeomNode)root;

                List<GeomVertexData> vdatas = new List<GeomVertexData>();
                for (int i = 0; i < gn._geoms.Count; i++)
                {
                    Geom geom = gn._geoms[i];
                    GeomVertexData data = geom._data;
                    GeomVertexReader reader = new GeomVertexReader(data);
                    TraverseData gdata = tdata.Compose(new TransformState(), gn._geom_states[i]);
                    
                    DataStructures.Models.Mesh mesh = new DataStructures.Models.Mesh(0);
                    mesh.SkinRef = 0;
                    if (gdata.state.HasAttrib(typeof(BSPMaterialAttrib)) || gdata.state.HasAttrib(typeof(TextureAttrib)))
                    {
                        Texture basetexture;
                        if (gdata.state.HasAttrib(typeof(BSPMaterialAttrib)))
                        {
                            BSPMaterialAttrib mattr = (BSPMaterialAttrib)gdata.state.GetAttrib(typeof(BSPMaterialAttrib));
                            basetexture = mattr._basetexture;
                        }
                        else
                        {
                            TextureAttrib tattr = (TextureAttrib)gdata.state.GetAttrib(typeof(TextureAttrib));
                            basetexture = tattr._texture;
                        }
                        
                        int texId = HasTexture(basetexture);
                        if (basetexture == null)
                            texId = 0;
                        else if (texId == -1)
                        {
                            try
                            {
                                texId = mdl.Textures.Count;
                                IFile parent = _file.Parent;
                                while (parent.Parent != null)
                                {
                                    parent = parent.Parent;
                                }
                                IFile tfile = parent.TraversePath(basetexture._filename);
                                System.Drawing.Bitmap texbmp = new System.Drawing.Bitmap(System.Drawing.Image.FromStream(new MemoryStream(tfile.ReadAll())));
                                DataStructures.Models.Texture tex = new DataStructures.Models.Texture()
                                {
                                    Name = basetexture._name,
                                    Index = texId,
                                    Image = texbmp,
                                    Width = texbmp.Width,
                                    Height = texbmp.Height,
                                    Flags = 0
                                };
                                mdl.Textures.Add(tex);
                                _textures.Add(basetexture);
                                Console.WriteLine("Added new texture {0}, {1}", tex.Name, basetexture._filename);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e.ToString());
                                texId = 0;
                            }
                        }

                        Console.WriteLine("Skinref for mesh is {0}", texId);
                        mesh.SkinRef = texId;
                    }

                    int prims = geom._primitives.Count;
                    for (int prim = 0; prim < prims; prim++)
                    {
                        GeomPrimitive gprim = geom._primitives[prim];
                        int prims2 = gprim.GetNumPrims();
                        for (int j = 0; j < prims2; j++)
                        {
                            int start = gprim.GetPrimStart(j);
                            int end = gprim.GetPrimEnd(j);

                            if (gprim is GeomTristrips)
                            {
                                for (int v = start; v < end - 2; v++)
                                {
                                    int idx = v - start;
                                    int[] add;
                                    if (idx % 2 == 0)//!= 0)
                                    {
                                        add = new int[] { v + 1, v, v + 2 };
                                    }
                                    else
                                    {
                                        add = new int[] { v, v + 1, v + 2 };
                                    }

                                    foreach (int vert in add)
                                    {
                                        int row = gprim.GetVert(vert);
                                        reader.set_column("vertex");
                                        reader.set_row(row);
                                        CoordinateF pos = reader.get_data_3f();

                                        pos = pos + gdata.transform._quat.Rotate(gdata.transform._pos);
                                        pos = pos.ComponentMultiply(gdata.transform._scale);
                                        pos = pos.ComponentMultiply(new CoordinateF(16, 16, 16));
                                        float temp = -pos.X;
                                        pos.X = pos.Y;
                                        pos.Y = temp;

                                        TexcoordF texcoord = new TexcoordF(0, 0);
                                        if (data._format.has_column("texcoord"))
                                        {
                                            reader.set_column("texcoord");
                                            reader.set_row(row);
                                            texcoord = reader.get_data_2f();
                                        }

                                        CoordinateF normal = new CoordinateF(0, 1, 0);
                                        if (data._format.has_column("normal"))
                                        {
                                            reader.set_column("normal");
                                            reader.set_row(row);
                                            normal = reader.get_data_3f();
                                        }

                                        //Color4 color = Color4.White;
                                        //if (data._format.has_column("color"))
                                        //{
                                        //    reader.set_column("color");
                                        //    reader.set_row(row);
                                        //    CoordinateF co_color = reader.get_data_3f();
                                        //    color = new Color4(co_color.X * 255, co_color.Y * 255, co_color.Z * 255, 255);
                                        //}

                                        var mvert = new DataStructures.Models.MeshVertex(pos, normal, mdl.Bones[0], texcoord._x, 1 - texcoord._y);
                                        //mvert.Color = color;
                                        //Console.WriteLine("Color of mesh vertex: {0}", color.ToString());
                                        mesh.Vertices.Add(mvert);
                                    }
                                }
                            }
                            else
                            {
                                for (int vert = end - 1; vert >= start; vert--)
                                {
                                    int row = gprim.GetVert(vert);
                                    reader.set_column("vertex");
                                    reader.set_row(row);
                                    CoordinateF pos = reader.get_data_3f();

                                    pos = pos + gdata.transform._quat.Rotate(gdata.transform._pos);
                                    pos = pos.ComponentMultiply(gdata.transform._scale);
                                    pos = pos.ComponentMultiply(new CoordinateF(16, 16, 16));
                                    float temp = -pos.X;
                                    pos.X = pos.Y;
                                    pos.Y = temp;

                                    TexcoordF texcoord = new TexcoordF(0, 0);
                                    if (data._format.has_column("texcoord"))
                                    {
                                        reader.set_column("texcoord");
                                        reader.set_row(row);
                                        texcoord = reader.get_data_2f();
                                    }

                                    CoordinateF normal = new CoordinateF(0, 1, 0);
                                    if (data._format.has_column("normal"))
                                    {
                                        reader.set_column("normal");
                                        reader.set_row(row);
                                        normal = reader.get_data_3f();
                                    }

                                    //Color4 color = Color4.White;
                                    //if (data._format.has_column("color"))
                                    //{
                                    //    reader.set_column("color");
                                    //    reader.set_row(row);
                                    //    CoordinateF co_color = reader.get_data_3f();
                                    //    color = new Color4(co_color.X * 255, co_color.Y * 255, co_color.Z * 255, 255);
                                    //}

                                    var mvert = new DataStructures.Models.MeshVertex(pos, normal, mdl.Bones[0], texcoord._x, 1 - texcoord._y);
                                    //mvert.Color = color;
                                    //Console.WriteLine("Color of mesh vertex: {0}", color.ToString());

                                    mesh.Vertices.Add(mvert);
                                }
                            }
                        }
                        
                    }
                    mdl.AddMesh(gn._name, 0, mesh);
                }
            }

            for (int i = 0; i < root._children.Count; i++)
            {
                PandaNode child = root._children[i];
                TraverseData ctdata = tdata.Compose(child._transform, child._state);
                r_traverse_below(ref mdl, ref child, ctdata);
            }
        }

        private TypedWritable r_construct_obj(TypeHandle type)
        {
            TypedWritable obj = (TypedWritable)System.Reflection.Assembly.GetExecutingAssembly().CreateInstance("Sledge.Providers.Model." + type.name);
            if (obj != null)
            {
                return obj;
            }

            if (_typehandle_record[type].Count > 0)
            {
                return r_construct_obj(_typehandle_record[type][0]);
            }

            return null;
        }

        private TypedWritable try_construct_obj(TypeHandle type)
        {
            TypedWritable obj = r_construct_obj(type);
            if (obj == null)
            {
                Console.WriteLine("Could not construct object of type {0} or any of its parents", type.name);
            }

            return obj;
        }

        private TypeHandle find_type(string name)
        {
            foreach (TypeHandle type in _typehandle_record.Keys)
            {
                if (type.name == name)
                {
                    return type;
                }
            }

            return null;
        }

        private TypeHandle read_handle(BinaryReader br)
        {
            int id = br.ReadUInt16();
            if (id == 0)
            {
                // Index number 0 is always, by convention, null.
                return null;
            }

            if (_typehandle_map.ContainsKey(id))
            {
                // This index number already has been encountered.
                // Return the stored TypeHandle.
                return _typehandle_map[id];
            }

            // We haven't encountered this type handle.
            string name = br.ReadPandaString();
            bool new_type = false;
            TypeHandle type = find_type(name);
            if (type == null)
            {
                new_type = true;
                type = new TypeHandle();
                type.name = name;
                type.id = id;
                type.num_parent_classes = 0;
                _typehandle_record.Add(type, new List<TypeHandle>());
                _typehandle_map.Add(id, type);
            }

            int num_parents = br.ReadChar();
            type.num_parent_classes = num_parents;
            for (int i = 0; i < num_parents; i++)
            {
                TypeHandle parent_type = read_handle(br);
                if (new_type)
                {
                    _typehandle_record[type].Add(parent_type);
                }

            }

            return type;
        }

        private TypedWritable read_object(BinaryReader br)
        {
            int start_level = _nesting_level;
            int object_id = p_read_object(br);
            while (_num_extra_objects > 0)
            {
                p_read_object(br);
                _num_extra_objects--;
            }

            while (_nesting_level > start_level)
            {
                p_read_object(br);
            }

            if (object_id == 0)
            {
                return null;
            }

            if (!_created_objs.ContainsKey(object_id))
            {
                Console.WriteLine("Undefined object encountered!");
                return null;
            }

            TypedWritable created_obj = _created_objs[object_id];
            return created_obj;
        }

        private int read_object_id(BinaryReader scan)
        {
            return scan.ReadUInt16();
        }

        private int p_read_object(BinaryReader br)
        {
            BinaryReader scan = get_datagram(br);

            BamObjectCode boc = BamObjectCode.BOC_adjunct;
            if (_file_minor >= 21)
            {
                boc = (BamObjectCode)scan.ReadChar();
            }
            switch (boc)
            {
                case BamObjectCode.BOC_push:
                    _nesting_level++;
                    break;

                case BamObjectCode.BOC_pop:
                    _nesting_level--;
                    return 0;

                case BamObjectCode.BOC_adjunct:
                    break;

                case BamObjectCode.BOC_remove:
                    Console.WriteLine("Encountered BOC_remove in bam file.");
                    break;

                default:
                    Console.WriteLine("Encountered invalid BamObjectCode in bam file {0}", boc);
                    break;
            }

            TypeHandle type = read_handle(scan);
            int object_id = read_object_id(scan);

            if (type != null)
            {
                
                TypedWritable obj = try_construct_obj(type);
                if (obj == null)
                {
                    Console.WriteLine("Tried to create object of type {0}, does not exist.", type.name);
                }
                else
                {
                    ToFillIn tfi = new ToFillIn();
                    tfi.obj = obj;
                    tfi.scan = scan;
                    tfi.finished = false;
                    _tofillin.Add(tfi);
                }
                _created_objs[object_id] = obj;

            }

            return object_id;
        }
    }

    /// <summary>
    /// Loader for Panda3D BAM files.
    /// While BAM files can store a multitude of objects,
    /// the only things we will care about are objects related to geometry
    /// such as GeomNodes, GeomVertexDatas, GeomPrimitives, etc.
    /// </summary>
    public class BamProvider : ModelProvider
    {
        public static int version_minor = 44;
        public static int version_major = 6;
        public static int first_minor_ver = 14;
        public static string magic_number = "pbj";

        protected override bool IsValidForFile(IFile file)
        {
            return file.Extension.ToLowerInvariant() == "bam";
        }

        protected override DataStructures.Models.Model LoadFromFile(IFile file)
        {
            using (var fs = new MemoryStream(file.ReadAll()))
            {
                using (var br = new BinaryReader(fs))
                {
                    try
                    {
                        BamReader reader = new BamReader();
                        DataStructures.Models.Model mdl = reader.ReadModel(br, file);
                        return mdl;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Exception occured when reading bam file:");
                        Console.WriteLine(e.ToString());
                        return null;
                    }
                    
                }
            }
        }
        
    }
}
