namespace LAHEE.Data {
    public class AchievementData {
        public int ID;
        public String MemAddr;
        public String Title;
        public String Description;
        public int Points;
        public String Author;
        public long Modified;
        public long Created;
        public String BadgeName;
        public int Flags;
        public String Type;
        public float Rarity;
        public float RarityHardcore;
        public String BadgeURL;
        public String BadgeLockedURL;

        internal static string ConvertType(string type) {
            switch (type) {
                case "1": return "missable";
                case "2": return "progression";
                case "3": return "win_condition";
                default: return "";
            }
        }

        public override string ToString() {
            return Title + " (" + ID + ")";
        }
    }
}