namespace LAHEE.Data {
    public class PresenceHistory {
        public DateTime Time;
        public String Message;

        public PresenceHistory(DateTime time, string message) {
            Time = time;
            Message = message;
        }
    }
}