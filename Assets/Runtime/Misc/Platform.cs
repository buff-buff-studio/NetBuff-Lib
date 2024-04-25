using UnityEngine;

namespace NetBuff.Misc
{
    /// <summary>
    /// Represents the platform the application is running on.
    /// </summary>
    public enum Platform
    {
        /// <summary>
        /// A generic desktop platform.
        /// </summary>
        Desktop,
        
        /// <summary>
        /// The Windows platform (editor or player).
        /// </summary>
        Windows,
        
        /// <summary>
        /// The Linux platform.
        /// </summary>
        Linux,
        
        /// <summary>
        /// The MacOS platform (editor or player).
        /// </summary>
        MacOS,
        
        /// <summary>
        /// A generic mobile platform.
        /// </summary>
        Mobile,
        
        /// <summary>
        /// Android platform.
        /// </summary>
        Android,
        
        /// <summary>
        /// iOS platform.
        /// </summary>
        IOS,

        /// <summary>
        /// Unknown platform.
        /// </summary>
        Unknown
    }
    
    /// <summary>
    /// Utility class for platform related operations.
    /// </summary>
    public static class PlatformExtensions
    {
        /// <summary>
        /// Returns the platform the application is running on.
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