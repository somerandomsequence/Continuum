﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContinuumNS
{
    public partial class MERRA_Node_table
    {
        public int Id { get; set; }
        public double latitude { get; set; }
        public double longitude { get; set; }
        public byte[] merraData { get; set; }
    }
}
