using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LAHEE.Data {
    public class LeaderboardData {
        public int ID;
        public String Mem;
        public String Format;
        public int LowerIsBetter;
        public String Title;
        public String Description;
        public bool Hidden;

        public override string ToString() {
            return Title + " (" + ID + ")";
        }
    }
}
