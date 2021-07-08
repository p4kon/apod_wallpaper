﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace apod_wallpaper
{
    public static class MyExtensions
    {
        public static string SubstringRange(this string str, int startIndex, int endIndex)
        {
            if (startIndex > str.Length - 1)
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            if (endIndex > str.Length - 1)
                throw new ArgumentOutOfRangeException(nameof(endIndex));

            return str.Substring(startIndex, endIndex - startIndex + 1);
        }
    }
}