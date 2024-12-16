using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tmdl_utility
{
    internal class UtilityInitInfo
    {
        public enum ExportType
        {
            Single = 0,
            Batch = 1,
        }

        public string Source, Dest;
        public ExportType Type;

        public UtilityInitInfo(string[] args)
        {
            Enum.TryParse(args[0], out Type);
            Source = args[1];
            Dest = args[2];
        }

    }
}
