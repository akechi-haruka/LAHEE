using LAHEE.Data;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Reflection;

namespace LAHEE {

    internal class Program {

        public static readonly String NAME;

        static Program() {

            string gitHash = Assembly.Load(typeof(Program).Assembly.FullName)
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(attr => attr.Key == "GitHash")?.Value;

            AssemblyName assemblyInfo = Assembly.GetExecutingAssembly().GetName();
            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly().Location);
            NAME = assemblyInfo.Name + "/" + assemblyInfo.Version + "-" + gitHash + " - " + versionInfo.LegalCopyright;
        }

        static void Main(string[] args) {

            Console.Title = NAME;

            try {
                Configuration.Initialize();
            }catch(Exception ex) {
                Console.WriteLine("An error ocurred during loading the configuration:\n"+ex.Message);
#if DEBUG
                Console.WriteLine(ex);
#endif
                Console.ReadLine();
                return;
            }

            Log.Initialize();
            UserManager.Initialize();
            StaticDataManager.Initialize();
            Network.Initialize();

            Thread.Sleep(Int32.MaxValue);
        }

    }

}
