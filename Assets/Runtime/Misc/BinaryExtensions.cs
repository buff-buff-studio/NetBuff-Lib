using System.IO;
using UnityEngine;

namespace NetBuff.Misc
{
    /// <summary>
    ///      Provides extension methods for writing and reading Unity types to and from a binary stream.
    ///      These methods are useful for serializing Unity types for network transmission or file storage.
    /// </summary>
    public static class BinaryWriterExtensions
    {
        public static void Write(this BinaryWriter writer, Vector2 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
        }

        public static void Write(this BinaryWriter writer, Vector3 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }

        public static void Write(this BinaryWriter writer, Vector4 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
            writer.Write(value.w);
        }

        public static void Write(this BinaryWriter writer, Quaternion value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
            writer.Write(value.w);
        }

        public static void Write(this BinaryWriter writer, Color value)
        {
            writer.Write(value.r);
            writer.Write(value.g);
            writer.Write(value.b);
            writer.Write(value.a);
        }

        public static void Write(this BinaryWriter writer, Color32 value)
        {
            writer.Write(value.r);
            writer.Write(value.g);
            writer.Write(value.b);
            writer.Write(value.a);
        }

        public static void Write(this BinaryWriter writer, Rect value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.width);
            writer.Write(value.height);
        }

        public static void Write(this BinaryWriter writer, Bounds value)
        {
            writer.Write(value.center);
            writer.Write(value.size);
        }

        public static void Write(this BinaryWriter writer, Matrix4x4 value)
        {
            for (int i = 0; i < 16; i++)
            {
                writer.Write(value[i / 4, i % 4]);
            }
        }

        public static void Write(this BinaryWriter writer, LayerMask value)
        {
            writer.Write(value.value);
        }

        public static void Write(this BinaryWriter writer, Vector2Int value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
        }

        public static void Write(this BinaryWriter writer, Vector3Int value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }

        public static void Write(this BinaryWriter writer, RectInt value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.width);
            writer.Write(value.height);
        }

        public static void Write(this BinaryWriter writer, BoundsInt value)
        {
            writer.Write(value.position);
            writer.Write(value.size);
        }

        public static void Write(this BinaryWriter writer, NetworkId value)
        {
            writer.Write(value.High);
            writer.Write(value.Low);
        }
    }

    public static class BinaryReaderExtensions
    {
        public static Vector2 ReadVector2(this BinaryReader reader)
        {
            return new Vector2(reader.ReadSingle(), reader.ReadSingle());
        }

        public static Vector3 ReadVector3(this BinaryReader reader)
        {
            return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        public static Vector4 ReadVector4(this BinaryReader reader)
        {
            return new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        public static Quaternion ReadQuaternion(this BinaryReader reader)
        {
            return new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        public static Color ReadColor(this BinaryReader reader)
        {
            return new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        public static Color32 ReadColor32(this BinaryReader reader)
        {
            return new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
        }

        public static Rect ReadRect(this BinaryReader reader)
        {
            return new Rect(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        public static Bounds ReadBounds(this BinaryReader reader)
        {
            return new Bounds(reader.ReadVector3(), reader.ReadVector3());
        }

        public static Matrix4x4 ReadMatrix4x4(this BinaryReader reader)
        {
            var matrix = new Matrix4x4();
            for (int i = 0; i < 16; i++)
            {
                matrix[i / 4, i % 4] = reader.ReadSingle();
            }
            return matrix;
        }

        public static LayerMask ReadLayerMask(this BinaryReader reader)
        {
            return new LayerMask { value = reader.ReadInt32() };
        }

        public static Vector2Int ReadVector2Int(this BinaryReader reader)
        {
            return new Vector2Int(reader.ReadInt32(), reader.ReadInt32());
        }

        public static Vector3Int ReadVector3Int(this BinaryReader reader)
        {
            return new Vector3Int(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
        }

        public static RectInt ReadRectInt(this BinaryReader reader)
        {
            return new RectInt(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
        }

        public static BoundsInt ReadBoundsInt(this BinaryReader reader)
        {
            return new BoundsInt(reader.ReadVector3Int(), reader.ReadVector3Int());
        }

        public static NetworkId ReadNetworkId(this BinaryReader reader)
        {
            return new NetworkId(reader.ReadInt32(), reader.ReadInt32());
        }
    }
}