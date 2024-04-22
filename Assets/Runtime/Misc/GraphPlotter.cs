using System;
using System.Collections.Generic;
using UnityEngine;

namespace NetBuff.Misc
{
    public static class GraphPlotter
    {
        public class GraphPlotterData
        {
            private readonly List<float> _data = new List<float>();
            public IList<float> Data => _data;
            public int Limit { get; set; } = 1000;
            public float Max { get; set; } = 1f;
            
            public void AddData(float toAdd, bool changeMax = true)
            {
                _data.Add(toAdd);
                while (_data.Count > Limit)
                    _data.RemoveAt(0);
                
                if (changeMax && toAdd > Max)
                    Max = toAdd;
            }
            
            public void Clear()
            {
                _data.Clear();
            }
        }
        
        public static void DrawGraph(float x, float y, int barWidth, Color color, int maxWidth, IList<float> data, float max, float maxHeight)
        {
            var c = Mathf.Ceil(maxWidth * 1f / barWidth);
            var from = (int) Mathf.Max(0, data.Count - c);
            
            GUI.color = color;
            for (var i = from; i < data.Count; i++)
            {
                var pX = x + (i - from) * barWidth;
                var height = data[i] / max * maxHeight;
                GUI.DrawTexture(new Rect(pX, y - height, barWidth, height), Texture2D.whiteTexture);
            }
        }

        public static void AddDataSafe(IList<float> data, float toAdd, int limit)
        {
            data.Add(toAdd);
            while (data.Count > limit)
                data.RemoveAt(0);
        }
    }
}