using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.Rendering;

#if !UNITY_EDITOR
namespace Unity
{
    [Preserve]
    public static class SkipSplash
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void BeforeSplashScreen()
        {
#if UNITY_WEBGL
            // WebGL 上只能靠事件回调碰碰运气
            Application.focusChanged += OnFocusChanged;
#else
            // 非 WebGL 才能用线程方式
            System.Threading.Tasks.Task.Run(() =>
            {
                SplashScreen.Stop(SplashScreen.StopBehavior.StopImmediate);
            });
#endif
        }

#if UNITY_WEBGL
        private static void OnFocusChanged(bool hasFocus)
        {
            Application.focusChanged -= OnFocusChanged;
            SplashScreen.Stop(SplashScreen.StopBehavior.StopImmediate);
        }
#endif
    }
}
#endif
