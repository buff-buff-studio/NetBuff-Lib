using System;
using UnityEngine;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NetBuff.Editor
{
	#if UNITY_EDITOR
	[Serializable]
	public class EditorGUISplitView
	{
		[Serializable]
		public enum Direction 
		{
			Horizontal,
			Vertical
		}

		#region Internal Fields
		[SerializeField]
		private Direction _splitDirection;
		[SerializeField]
		private float _splitNormalizedPosition;
		[SerializeField]
		private bool _resize;
		[SerializeField]
		private Vector2 _scrollPosition;
		
		private Rect _availableRect;
		
		[NonSerialized]
		private Texture2D _dividerTexture;
		#endregion

		#region Public Fields
		public float minNormalizedPosition = 0.1f;
		public float maxNormalizedPosition = 0.9f;
		#endregion
		
		public EditorGUISplitView(Direction splitDirection) 
		{
			_splitNormalizedPosition = 0.5f;
			this._splitDirection = splitDirection;
		}

		public void BeginSplitView() 
		{
			var tempRect = _splitDirection == Direction.Horizontal ? EditorGUILayout.BeginHorizontal (GUILayout.ExpandWidth(true)) : EditorGUILayout.BeginVertical (GUILayout.ExpandHeight(true));
			
			if (tempRect.width > 0f)
				_availableRect = tempRect;

			_scrollPosition = GUILayout.BeginScrollView(_scrollPosition, _splitDirection == Direction.Horizontal ? GUILayout.Width(_availableRect.width * _splitNormalizedPosition) : GUILayout.Height(_availableRect.height * _splitNormalizedPosition));
		}

		public void Split() 
		{
			GUILayout.EndScrollView();
			_ResizeSplitFirstView();
		}

		public void EndSplitView() 
		{
			if(_splitDirection == Direction.Horizontal)
				EditorGUILayout.EndHorizontal();
			else 
				EditorGUILayout.EndVertical();
		}

		private Texture2D _CreateTexture()
		{
			var texture = new Texture2D(1, 1);
			texture.SetPixel(0, 0, EditorGUIUtility.isProSkin ? new Color(0.0785f, 0.0785f, 0.0785f) : new Color(0.25f, 0.25f, 0.25f));
			texture.Apply();
			return texture;
		}

		private void _ResizeSplitFirstView()
		{
			if(_dividerTexture == null)
				_dividerTexture = _CreateTexture();
			
			var resizeHandleRect = _splitDirection == Direction.Horizontal ? new Rect (_availableRect.width * _splitNormalizedPosition, _availableRect.y, 2f, _availableRect.height) : new Rect (_availableRect.x,_availableRect.height * _splitNormalizedPosition, _availableRect.width, 2f);
			GUI.DrawTexture(resizeHandleRect, _dividerTexture);

			EditorGUIUtility.AddCursorRect(resizeHandleRect,
				_splitDirection == Direction.Horizontal ? MouseCursor.ResizeHorizontal : MouseCursor.ResizeVertical);

			if(Event.current.type == EventType.MouseDown && resizeHandleRect.Contains(Event.current.mousePosition))
				_resize = true;

			if(_resize)
			{
				if(_splitDirection == Direction.Horizontal)
					_splitNormalizedPosition = Event.current.mousePosition.x / _availableRect.width;
				else
					_splitNormalizedPosition = Event.current.mousePosition.y / _availableRect.height;
				
				_splitNormalizedPosition = Mathf.Clamp(_splitNormalizedPosition, minNormalizedPosition, maxNormalizedPosition);
			}
			
			if(Event.current.type == EventType.MouseUp)
				_resize = false;        
		}
	}
	#endif
}