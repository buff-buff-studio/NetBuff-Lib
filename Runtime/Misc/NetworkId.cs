using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Random = System.Random;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NetBuff.Misc
{
    /// <summary>
    /// A unique identifier for a network object.
    /// Represented as a 16-character hexadecimal string.
    /// Internally, it is stored as two 32-bit integers.
    /// There are 18,446,744,073,709,551,616 unique network IDs.
    /// </summary>
    [Serializable]
    public class NetworkId : IComparable
    {
        #region Internal Fields
        private static Random _random = new();

        [SerializeField]
        private int high;

        [SerializeField]
        private int low;
        #endregion

        #region Helper Properties
        /// <summary>
        /// A network ID with all bits set to 0.
        /// </summary>
        public static NetworkId Empty => new()
        {
            high = 0,
            low = 0
        };
        
        /// <summary>
        /// Checks if the network ID is empty.
        /// </summary>
        public bool IsEmpty => low == 0 && high == 0;

        /// <summary>
        /// Returns the high 32 bits of the network ID.
        /// </summary>
        public int High => high;
    
        /// <summary>
        /// Returns the low 32 bits of the network ID.
        /// </summary>
        public int Low => low;
        #endregion

        private NetworkId()
        {
            low = _random.Next(-2147483648, 2147483647);
            high = _random.Next(-2147483648, 2147483647);
        }
        
        public NetworkId(int high, int low)
        {
            this.high = high;
            this.low = low;
        }
        
        /// <summary>
        /// Compares two network IDs.
        /// If they are equal, returns 0.
        /// If this network ID is less than the other, returns -1.
        /// If this network ID is greater than the other, returns 1.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public int CompareTo(object obj)
        {
            switch (obj)
            {
                case null:
                    return -1;
                case NetworkId networkId:
                    var cmp = high.CompareTo(networkId.high);
                    return cmp == 0 ? low.CompareTo(networkId.low) : cmp;
            }

            return -1;
        }

        /// <summary>
        /// Creates a new random network ID.
        /// </summary>
        /// <returns></returns>
        public static NetworkId New()
        {
            return new NetworkId
            {
                low = _random.Next(-2147483648, 2147483647),
                high = _random.Next(-2147483648, 2147483647)
            };
        }
        
        /// <summary>
        /// Reads a network ID from a binary reader.
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        public static NetworkId Read(BinaryReader reader)
        {
            return new NetworkId
            {
                low = reader.ReadInt32(),
                high = reader.ReadInt32()
            };
        }

        /// <summary>
        /// Compares two network IDs.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            return obj switch
            {
                null => false,
                NetworkId networkId => networkId.high == high && networkId.low == low,
                _ => false
            };
        }

        /// <summary>
        /// Gets the hash code of the network ID.
        /// </summary>
        /// <returns></returns>
        [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode")]
        public override int GetHashCode()
        {
            return low ^ high;
        }
        
        /// <summary>
        /// Serializes the network ID to a binary writer.
        /// </summary>
        /// <param name="writer"></param>
        /// <returns></returns>
        public NetworkId Serialize(BinaryWriter writer)
        {
            writer.Write(low);
            writer.Write(high);
            return this;
        }

        /// <summary>
        /// Deserializes the network ID from a binary reader.
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        public NetworkId Deserialize(BinaryReader reader)
        {
            low = reader.ReadInt32();
            high = reader.ReadInt32();
            return this;
        }

        /// <summary>
        /// Converts the network ID to a 16-character hexadecimal string.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            var str = new StringBuilder();
            str.Append(high.ToString("x8"));
            str.Append(low.ToString("x8"));
            return str.ToString();
        }

        /// <summary>
        /// Try to parse a network ID from a string.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public static bool TryParse(string input, out object result)
        {
            try
            {
                result = new NetworkId
                {
                    high = int.Parse(input.Substring(0, 8), NumberStyles.HexNumber),
                    low = int.Parse(input.Substring(8, 8), NumberStyles.HexNumber)
                };
                return true;
            }
            catch
            {
                result = null;
                return false;
            }
        }

        /// <summary>
        /// Compares the equality of two network IDs.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool operator ==(NetworkId a, NetworkId b)
        {
            if (a is null || b is null)
                return a is null && b is null;
            return a.high == b.high && a.low == b.low;
        }

        /// <summary>
        /// Compares the inequality of two network IDs.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool operator !=(NetworkId a, NetworkId b)
        {
            if (a is null || b is null) return a is not null || b is not null;
            return a.high != b.high || a.low != b.low;
        }
    }
    
    #if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(NetworkId))]
    public class NetworkIdDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            position.width -= 20;
            var a = property.FindPropertyRelative("high").intValue;
            var b = property.FindPropertyRelative("low").intValue;
            var str = new StringBuilder();
            str.Append(a.ToString("x8"));
            str.Append(b.ToString("x8"));
            var old = str.ToString();
            var changed = EditorGUI.TextField(position, label, old);

            if (changed != old)
                if (changed.Length == 16)
                {
                    property.FindPropertyRelative("high").intValue = int.Parse(changed[..8], NumberStyles.HexNumber);
                    property.FindPropertyRelative("low").intValue =
                        int.Parse(changed.Substring(8, 8), NumberStyles.HexNumber);
                }

            GUI.enabled = true;

            position.x += position.width;
            position.width = 20;
            GUI.enabled = !Application.isPlaying;
            if (GUI.Button(position, "N"))
            {
                property.FindPropertyRelative("high").intValue = new Random().Next(-2147483648, 2147483647);
                property.FindPropertyRelative("low").intValue = new Random().Next(-2147483648, 2147483647);
                property.serializedObject.ApplyModifiedProperties();
            }

            GUI.enabled = true;
        }
    }
    #endif
}