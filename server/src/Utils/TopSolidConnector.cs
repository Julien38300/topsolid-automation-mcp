using System;
using System.IO;
using TopSolid.Kernel.Automating;
using TopSolid.Cad.Design.Automating;
using TopSolid.Cad.Drafting.Automating;

namespace TopSolidMcpServer.Utils
{
    /// <summary>
    /// Gère la connexion à TopSolid via l'API Automation.
    /// Utilise la connexion par défaut (Named Pipes).
    /// </summary>
    public class TopSolidConnector
    {
        private bool _isConnected;
        private readonly int _port;
        private DateTime _lastConnectAttempt = DateTime.MinValue;
        private static readonly TimeSpan ReconnectCooldown = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Fired when connection status changes. Used by TrayIcon to update UI.
        /// </summary>
        public event Action<bool> ConnectionChanged;

        /// <summary>
        /// Creates a connector targeting a specific TopSolid instance via TCP port.
        /// </summary>
        /// <param name="port">TCP port (default 8090). Set in TopSolid: Tools > Options > General > Automation.</param>
        public TopSolidConnector(int port = 8090)
        {
            _port = port;
        }

        /// <summary>
        /// Indique si la connexion à TopSolid est active.
        /// </summary>
        public bool IsConnected => _isConnected && CheckConnection();

        /// <summary>
        /// Établit la connexion avec une instance de TopSolid en cours d'exécution.
        /// Uses DefineConnection to target the specific TCP port, allowing multiple
        /// TopSolid instances to coexist on the same machine.
        /// </summary>
        /// <returns>True si la connexion est établie, sinon False.</returns>
        public bool Connect()
        {
            _lastConnectAttempt = DateTime.UtcNow;
            bool wasConnected = _isConnected;

            try
            {
                // Target the specific TopSolid instance by TCP port.
                // Without this, Connect() picks the first available named pipe,
                // which fails when multiple TopSolid versions are running.
                TopSolidHost.DefineConnection("localhost", _port, null, 0);

                // Connect() returns false even when connection succeeds (TopSolid 7.20 bug).
                // We ignore the return value and verify with an actual API call.
                TopSolidHost.Connect(false, 10000);

                // Verify connection with a real API call
                var version = TopSolidHost.Version;
                _isConnected = version > 0;

                if (_isConnected)
                {
                    TopSolidDesignHost.Connect();
                    TopSolidDraftingHost.Connect();
                    Console.Error.WriteLine("[TopSolidConnector] Connected to TopSolid v" + version + " on port " + _port + ".");
                }
                else
                {
                    Console.Error.WriteLine("[TopSolidConnector] TopSolid not available.");
                }

                // Notify listeners if status changed
                if (wasConnected != _isConnected)
                    ConnectionChanged?.Invoke(_isConnected);

                return _isConnected;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[TopSolidConnector] Connection error: " + ex.Message);
                _isConnected = false;
                if (wasConnected)
                    ConnectionChanged?.Invoke(false);
                return false;
            }
        }

        /// <summary>
        /// Ensures a live connection to TopSolid, auto-reconnecting if needed.
        /// Call this before every operation that requires TopSolid.
        /// Respects a cooldown to avoid hammering the connection.
        /// </summary>
        /// <returns>True if connected, false if TopSolid is unreachable.</returns>
        public bool EnsureConnected()
        {
            if (_isConnected && CheckConnection())
                return true;

            // Cooldown: don't retry too fast
            if (DateTime.UtcNow - _lastConnectAttempt < ReconnectCooldown)
                return false;

            Console.Error.WriteLine("[TopSolidConnector] Connection lost — attempting reconnect...");
            return Connect();
        }

        /// <summary>
        /// Ferme la connexion avec TopSolid.
        /// </summary>
        public void Disconnect()
        {
            try
            {
                if (_isConnected)
                {
                    TopSolidDraftingHost.Disconnect();
                    TopSolidDesignHost.Disconnect();
                    TopSolidHost.Disconnect();
                    _isConnected = false;
                    Console.Error.WriteLine("[TopSolidConnector] Disconnected.");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[TopSolidConnector] Disconnection error: {ex.Message}");
            }
        }

        /// <summary>
        /// Récupère l'état actuel de TopSolid (document actif, projet).
        /// </summary>
        /// <returns>Une chaîne décrivant l'état ou un message d'erreur.</returns>
        public string GetState()
        {
            if (!EnsureConnected())
            {
                return "Not connected to TopSolid. Please check that TopSolid is running with Automation enabled (port " + _port + ").\nThe tools find_path and explore_paths are still available.";
            }

            try
            {
                var version = TopSolidHost.Version;
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Version : " + version);

                var docId = TopSolidHost.Documents.EditedDocument;
                if (docId.IsEmpty)
                {
                    sb.AppendLine("État : Connecté (aucun document en cours d'édition)");
                    return sb.ToString();
                }

                string docName = TopSolidHost.Documents.GetName(docId);
                string docExtension = Path.GetExtension(docName);
                string projectName = "Inconnu";

                var pdmObj = TopSolidHost.Documents.GetPdmObject(docId);
                if (!pdmObj.IsEmpty)
                {
                    var projectId = TopSolidHost.Pdm.GetProject(pdmObj);
                    if (!projectId.IsEmpty)
                    {
                        projectName = TopSolidHost.Pdm.GetName(projectId);
                    }
                }

                sb.AppendLine("Document en cours : " + Path.GetFileNameWithoutExtension(docName));
                sb.AppendLine("Type : " + docExtension);
                sb.AppendLine("Projet : " + projectName);

                return sb.ToString();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[TopSolidConnector] Error getting state: {ex.Message}");
                return "Erreur lors de la récupération de l'état de TopSolid.";
            }
        }

        /// <summary>
        /// Vérifie si la connexion est toujours valide en tentant un appel léger.
        /// </summary>
        private bool CheckConnection()
        {
            try
            {
                // Un simple appel pour vérifier que le pipe répond
                var app = TopSolidHost.Application;
                return app != null;
            }
            catch
            {
                bool wasConnected = _isConnected;
                _isConnected = false;
                if (wasConnected)
                {
                    Console.Error.WriteLine("[TopSolidConnector] Connection lost to TopSolid.");
                    ConnectionChanged?.Invoke(false);
                }
                return false;
            }
        }
    }
}
