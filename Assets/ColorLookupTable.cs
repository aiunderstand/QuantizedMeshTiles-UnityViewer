using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets
{
    static class ColorLookupTable
    {
        public static readonly Dictionary<int, Color> colors = new Dictionary<int, Color> {
            { 0, new Color(0,0,0,1)},
            { 1, new Color(0.2f,0,0,1)},
            { 2, new Color(0.2f,0,0.2f,1)},
            { 3, new Color(0.2f,0,0.4f,1)},
            { 4, new Color(0.2f,0,0.6f,1)},
            { 5, new Color(0.2f,0,0.8f,1)},
            { 6, new Color(0.2f,0,1.0f,1)},
            { 7, new Color(0.2f,0.2f,0,1)},
            { 8, new Color(0.2f,0.2f,0.2f,1)},
            { 9, new Color(0.2f,0.2f,0.4f,1)},
            { 10, new Color(0.2f,0.2f,0.6f,1)},
            { 11, new Color(0.2f,0.2f,0.8f,1)},
            { 12, new Color(0.2f,0.2f,1.0f,1)},
            { 13, new Color(0.2f,0.4f,0,1)},
            { 14, new Color(0.2f,0.4f,0.2f,1)},
            { 15, new Color(0.2f,0.4f,0.4f,1)},
            { 16, new Color(0.2f,0.4f,0.6f,1)},
            { 17, new Color(0.2f,0.4f,0.8f,1)},
            { 18, new Color(0.2f,0.4f,1.0f,1)},
            { 19, new Color(0.2f,0.6f,0,1)},
            { 20, new Color(0.2f,0.6f,0.2f,1)},
            { 21, new Color(0.2f,0.6f,0.4f,1)},
            { 22, new Color(0.2f,0.6f,0.6f,1)},
            { 23, new Color(0.2f,0.6f,0.8f,1)},
            { 24, new Color(0.2f,0.6f,1.0f,1)},
            { 25, new Color(0.2f,0.8f,0,1)},
            { 26, new Color(0.2f,0.8f,0.2f,1)},
            { 27, new Color(0.2f,0.8f,0.4f,1)},
            { 28, new Color(0.2f,0.8f,0.6f,1)},
            { 29, new Color(0.2f,0.8f,0.8f,1)},
            { 30, new Color(0.2f,0.8f,1.0f,1)}
        };
    }
}
