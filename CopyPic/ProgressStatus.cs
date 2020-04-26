using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CopyPic
{
    public struct ProgressStatus
    {
        public StatusCodeEnum StatusCode { get; set; }
        public int FileCount { get; set; }
        public string Status { get; set; }
    }
}
