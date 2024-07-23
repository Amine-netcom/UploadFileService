using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UpladFileService.Model
{
    public class Config
    {
        public int maximum_file_size_in_MB { get; set; }
        public string extension_fallback { get; set; }
        public string tmp_path { get; set; }
        public int validity_period { get; set; }
        public string[] fhft_extension_black_list { get; set; }
    }
}
