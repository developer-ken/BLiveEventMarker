using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiveEventMarker.Model
{
    internal class LiveSegment
    {
        public DateTime startTime = DateTime.Now;
        public bool DataPending = true;
        public List<Event> Superchat = new List<Event>();
        public List<Event> Guardbuy = new List<Event>();
    }
}
