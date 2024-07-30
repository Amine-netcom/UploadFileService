using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UploadFileService.Model
{
    public class KestrelSettings
    {
        public CEndpoints Endpoints { get; set; }

        public class CEndpoints
        {
            public Http Http { get; set; }
        }

        public class Http
        {
            public string Url { get; set; }
            public int PortServer { get; set; }
        }
    }

}
