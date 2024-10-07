using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LAHEE {
    internal class Util {

        private static Random random = new Random();

        public static long CurrentUnixSeconds { get { return (long)(DateTime.Now.ToUniversalTime() - DateTime.UnixEpoch).TotalSeconds; } }

        public static string RandomString(int length) {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

    }
}
