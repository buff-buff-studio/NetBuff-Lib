using System;
using System.Collections.Generic;
using NetBuff.Components;

namespace NetBuff.Misc
{
    /// <summary>
    ///     Main class for network actions. Should be used to register and invoke network actions
    /// </summary>
    public static class NetworkAction
    {
        /// <summary>
        ///     Called when a object is spawned
        /// </summary>
        public static NetworkActionListener<NetworkId, NetworkIdentity> OnObjectSpawn { get; } = new();

        /// <summary>
        ///     Called when a object is despawned
        /// </summary>
        public static NetworkActionListener<NetworkId, NetworkIdentity> OnObjectDespawn { get; } = new();

        /// <summary>
        ///     Called when a object owner is changed
        /// </summary>
        public static NetworkActionListener<NetworkId, NetworkIdentity> OnObjectChangeOwner { get; } = new();

        /// <summary>
        ///     Called when a object active state is changed
        /// </summary>
        public static NetworkActionListener<NetworkId, NetworkIdentity> OnObjectChangeActive { get; } = new();

        /// <summary>
        ///     Called when a object scene is changed
        /// </summary>
        public static NetworkActionListener<NetworkId, NetworkIdentity> OnObjectSceneChanged { get; } = new();

        /// <summary>
        ///     Called when a scene is loaded
        /// </summary>
        public static NetworkActionListener<string, int> OnSceneLoaded { get; } = new();

        /// <summary>
        ///     Called when a scene is unloaded
        /// </summary>
        public static NetworkActionListener<string, int> OnSceneUnloaded { get; } = new();

        /// <summary>
        ///     Clear all network actions
        /// </summary>
        public static void ClearAll()
        {
            OnObjectSpawn.Clear();
            OnObjectDespawn.Clear();
            OnObjectChangeOwner.Clear();
            OnObjectChangeActive.Clear();
            OnObjectSceneChanged.Clear();
        }
    }

    /// <summary>
    ///     Base class for network actions listeners
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TR"></typeparam>
    public class NetworkActionListener<T, TR>
    {
        private readonly Dictionary<T, Action<TR>[]> _then = new();

        /// <summary>
        ///     Register a network action
        /// </summary>
        /// <param name="key"></param>
        /// <param name="action"></param>
        /// <param name="temp"></param>
        public void Register(T key, NetworkAction<T, TR> action, bool temp)
        {
            if (!_then.TryGetValue(key, out var value))
            {
                value = new Action<TR>[2];
                _then.Add(key, value);
            }

            value[temp ? 0 : 1] += action.Invoke;
        }

        /// <summary>
        ///     Remove a network action
        /// </summary>
        /// <param name="key"></param>
        /// <param name="action"></param>
        public void Remove(T key, NetworkAction<T, TR> action)
        {
            if (_then.ContainsKey(key))
            {
                _then[key][0] -= action.Invoke;
                _then[key][1] -= action.Invoke;
            }
        }

        /// <summary>
        ///     Invoke all registered network action. Clears all temporary actions
        /// </summary>
        /// <param name="key"></param>
        /// <param name="result"></param>
        public void Invoke(T key, TR result)
        {
            if (_then.TryGetValue(key, out var h))
            {
                h[0]?.Invoke(result);
                h[1]?.Invoke(result);

                h[0] = null;
            }
        }

        /// <summary>
        ///     Clear all registred network actions
        /// </summary>
        public void Clear()
        {
            _then.Clear();
        }
    }

    /// <summary>
    ///     Used as callback for network actions
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TR"></typeparam>
    public class NetworkAction<T, TR>
    {
        private Action<TR> _then;

        public NetworkAction(T value)
        {
            Value = value;
        }

        /// <summary>
        ///     Holds the value of the network action
        /// </summary>
        public T Value { get; private set; }

        /// <summary>
        ///     Set the callback for the network action
        /// </summary>
        /// <param name="then"></param>
        public void Then(Action<TR> then)
        {
            _then = then;
        }

        /// <summary>
        ///     Invoke the network action
        /// </summary>
        /// <param name="result"></param>
        public void Invoke(TR result)
        {
            _then?.Invoke(result);
        }
    }
}