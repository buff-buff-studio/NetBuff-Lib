using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using UnityEngine;
using Random = System.Random;

namespace NetBuff.Misc
{
    [Serializable]
    public class NetworkId : IComparable
    {
        private static Random _random = new Random();

        public static NetworkId Empty => new NetworkId
        {
            high = 0,
            low = 0
        };
        
        
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
        
        public static NetworkId New()
        {
            return new NetworkId()
            {
                low = _random.Next(-2147483648, 2147483647),
                high = _random.Next(-2147483648, 2147483647)
            };
        }
        
        public static NetworkId Read(BinaryReader reader)
        {
            return new NetworkId
            {
                low = reader.ReadInt32(),
                high = reader.ReadInt32()
            };
        }
        
        public bool IsEmpty => low == 0 && high == 0;
        
        [SerializeField]
        private int high;
        [SerializeField]
        private int low;
        
        public int High => high;
        public int Low => low;

        public int CompareTo ( object obj )
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
        
        public override bool Equals ( object obj )
        {
            return obj switch
            {
                null => false,
                NetworkId networkId => networkId.high == high && networkId.low == low,
                _ => false
            };
        }
        
        [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode")]
        public override int GetHashCode()
        {
            return low ^ high;
        }
        
        public NetworkId Serialize(BinaryWriter writer)
        {
            writer.Write(low);
            writer.Write(high);
            return this;
        }
        
        public NetworkId Deserialize(BinaryReader reader)
        {
            low = reader.ReadInt32();
            high = reader.ReadInt32();
            return this;
        }

        public override string ToString()
        {
            var str = new System.Text.StringBuilder();
            str.Append(high.ToString("x8"));
            str.Append(low.ToString("x8"));
            return str.ToString();
        }
        
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

        public static bool operator ==(NetworkId a, NetworkId b)
        {
            if (a is null || b is null)
                return a is null && b is null;
            return a.high == b.high && a.low == b.low;
        }

        public static bool operator !=(NetworkId a, NetworkId b)
        {
            if (a is null || b is null)
            {
                return a is not null || b is not null;
            }
            return a.high != b.high || a.low != b.low;
        }
    }
    
    
#if UNITY_EDITOR
    [UnityEditor.CustomPropertyDrawer(typeof(NetworkId))]
    public class NetworkIdDrawer : UnityEditor.PropertyDrawer
    {
        public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
        {
            position.width -= 20;
            var a = property.FindPropertyRelative("high").intValue;
            var b = property.FindPropertyRelative("low").intValue;
            var str = new System.Text.StringBuilder();
            str.Append(a.ToString("x8"));
            str.Append(b.ToString("x8"));
            var old = str.ToString();
            var changed = UnityEditor.EditorGUI.TextField(position, label, old);
        
            if (changed != old)
            {
                if (changed.Length == 16)
                {
                    property.FindPropertyRelative("high").intValue = int.Parse(changed[..8], NumberStyles.HexNumber);
                    property.FindPropertyRelative("low").intValue = int.Parse(changed.Substring(8, 8), NumberStyles.HexNumber);
                }
            }
            GUI.enabled = true;
        
            //Create button to generate new UUID
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


