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
