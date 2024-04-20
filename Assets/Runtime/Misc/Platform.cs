using UnityEngine;

namespace NetBuff.Misc
{
    public enum Platform
    {
        Desktop,
        Windows,
        Linux,
        MacOS, 
        
        Mobile,
        Android,
        IOS,
        
        Unknown
    }
    
    /// <summary>
    /// Used to determine the current platform
    /// </summary>
    public static class PlatformExtensions
    {
        /// <summary>
        /// Returns the current platform
        /// </summary>
        /// <returns></returns>
        public static Platform GetPlatform()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer:
                    return Platform.Windows;
                case RuntimePlatform.LinuxPlayer:
                    return Platform.Linux;
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXPlayer:
                    return Platform.MacOS;
                case RuntimePlatform.Android:
                    return Platform.Android;
                case RuntimePlatform.IPhonePlayer:
                    return Platform.IOS;
                default:
                    return Platform.Unknown;
            }
        }
    }
}