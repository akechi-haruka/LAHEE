using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LAHEE.Data {
    
    public class UserAchievementData {
        public int AchievementID;
        public StatusFlag Status;
        public long AchieveDate;
        public long AchieveDateSoftcore;

        public enum StatusFlag {
            Locked = 0,
            SoftcoreUnlock = 1,
            HardcoreUnlock = 2
        }

        public override string ToString() {
            return AchievementID.ToString();
        }
    }
}
