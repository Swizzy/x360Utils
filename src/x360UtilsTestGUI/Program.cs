namespace x360UtilsTestGUI {
    using System;
    using System.Reflection;
    using System.Windows.Forms;

    internal static class Program {
        /// <summary>
        ///     The main entry point for the application.
        /// </summary>
        [STAThread] private static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomainAssemblyResolve;
            Application.Run(new MainForm());
        }

        private static Assembly CurrentDomainAssemblyResolve(object sender, ResolveEventArgs args) {
            if(string.IsNullOrEmpty(args.Name))
                throw new Exception("DLL Read Failure (Nothing to load!)");
            var name = string.Format("{0}.dll", args.Name.Split(',')[0]);
            using(var stream = Assembly.GetAssembly(typeof(Program)).GetManifestResourceStream(string.Format("{0}.{1}", typeof(Program).Namespace, name))) {
                if(stream != null) {
                    var data = new byte[stream.Length];
                    stream.Read(data, 0, data.Length);
                    return Assembly.Load(data);
                }
                throw new Exception(string.Format("Can't find external nor internal {0}!", name));
            }
        }
    }
}