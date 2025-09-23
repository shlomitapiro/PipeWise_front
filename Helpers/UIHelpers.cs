using System;

namespace PipeWiseClient.Helpers
{
    internal static class UIHelpers
    {
        public static void Let<T>(this T? obj, Action<T> act) where T : class
        {
            if (obj is not null) act(obj);
        }
    }
}

