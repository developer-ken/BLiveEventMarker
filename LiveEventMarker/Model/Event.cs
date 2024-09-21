using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiveEventMarker.Model
{
    internal class Event
    {
        public DateTime Time;
        public long Uid;
        public required string Name;
        public string? Text;
    }
}
