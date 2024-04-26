using System.Collections.Generic;
using UnityEngine;

namespace NetBuff.Misc
{
    /// <summary>
    ///     Utility class for drawing graphs in Unity.
    ///     Used by NetworkManagerGUI to draw graphs of network statistics.
    /// </summary>
    public static class GraphPlotter
    {
        /// <summary>
        ///     Draws a graph to display the given data.
        ///     The position represents the bottom-left corner of the graph.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="barWidth"></param>
        /// <param name="color"></param>
        /// <param name="maxWidth"></param>
        /// <param name="data"></param>
        /// <param name="max"></param>
        /// <param name="maxHeight"></param>
        public static void DrawGraph(float x, float y, int barWidth, Color color, int maxWidth, IList<float> data,
            float max, float maxHeight)
        {
            var c = Mathf.Ceil(maxWidth * 1f / barWidth);
            var from = (int)Mathf.Max(0, data.Count - c);

            GUI.color = color;
            for (var i = from; i < data.Count; i++)
            {
                var pX = x + (i - from) * barWidth;
                var height = data[i] / max * maxHeight;
                GUI.DrawTexture(new Rect(pX, y - height, barWidth, height), Texture2D.whiteTexture);
            }
        }

        /// <summary>
        ///     Holds the data to be plotted on the graph.
        /// </summary>
        public class GraphPlotterData
        {
            #region Private Fields
            private readonly List<float> _data = new();
            #endregion

            /// <summary>
            ///     Adds a data point to the graph.
            ///     If the number of data points exceeds the limit, the oldest data point is removed.
            ///     If changeMax is true, the maximum value of the data is updated if the new data point is greater than the current
            ///     maximum.
            /// </summary>
            /// <param name="toAdd"></param>
            /// <param name="changeMax"></param>
            public void AddData(float toAdd, bool changeMax = true)
            {
                _data.Add(toAdd);
                while (_data.Count > Limit)
                    _data.RemoveAt(0);

                if (changeMax && toAdd > Max)
                    Max = toAdd;
            }

            /// <summary>
            ///     Clears all data points.
            /// </summary>
            public void Clear()
            {
                _data.Clear();
            }

            #region Helper Properties
            /// <summary>
            ///     The data to be plotted.
            /// </summary>
            public IList<float> Data => _data;

            /// <summary>
            ///     The maximum number of data points to be stored.
            /// </summary>
            public int Limit { get; set; } = 1000;

            /// <summary>
            ///     The maximum value of the data. Used to scale the graph.
            /// </summary>
            public float Max { get; set; } = 1f;
            #endregion
        }
    }
}