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
        iOS,
        
        Unknown
    }
    
    public static class PlatformExtensions
    {
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
                    return Platform.iOS;
                default:
                    return Platform.Unknown;
            }
        }
    }
}