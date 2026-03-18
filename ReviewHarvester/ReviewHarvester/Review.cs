using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReviewHarvester
{
    public class Review
    {
        public string User { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; }
        public string Source { get; set; }
    }
}
