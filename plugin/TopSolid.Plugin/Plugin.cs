using System;
using System.Runtime.InteropServices;
using TopSolid.Kernel.TX.AddIns;
using TopSolid.Kernel.SX;
// aliases
using TK = TopSolid.Kernel;

namespace EPFL.RhinoInsideTopSolid.AddIn
{
    [Guid("62b69852-5ad7-4ae1-9470-b7cac5cef940")]
    public class AddIn : TopSolid.Kernel.TX.AddIns.AddIn
    {
        private HttpServer _server;

        public override string Name => "EPFL.RhinoInsideTopSolid.AddIn"; // Impersonation
        public override string[] Description => new[] { "TopSolid Expert AI Plugin (Zone D)" };
        public override string Manufacturer => "EPFL"; // Impersonation
        public override Guid[] RequiredAddIns => new Guid[0];

        public override void InitializeSession()
        {
            // Init
        }

        public override void StartSession()
        {
            try
            {
                // Port can be configured, using 5050 for now (less likely to conflict than 8080)
                _server = new HttpServer(5050); 
                _server.Start();
                // Notify via debug console
                System.Diagnostics.Debug.WriteLine("TopSolidExpertAI Plugin Started on port 5050");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to start server: {ex}");
            }
        }

        public override void EndSession()
        {
            _server?.Stop();
        }

        public override string GetRegistrationCertificate()
        {
            // Use standard .NET resource reading
            // Resource name will now be EPFL.RhinoInsideTopSolid.AddIn.TopSolidAddInCertificate.xml
            string resourceName = "EPFL.RhinoInsideTopSolid.AddIn.TopSolidAddInCertificate.xml";
            using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null) return string.Empty;
                using (var reader = new System.IO.StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}
