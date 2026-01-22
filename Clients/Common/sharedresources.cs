using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace Viking.Common
{
    public static class SharedResources
    {
        public static HttpClient HttpClient = new();
    }
}
