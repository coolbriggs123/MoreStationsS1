
using System.Reflection;
using System.Text;
using HarmonyLib;
using Il2CppScheduleOne.Employees;
using Il2CppSteamworks;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(ChemistEnhanced.Core), "ChemistEnhanced", "1.0.0", "Coolbriggs", null)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace ChemistEnhanced
{
    public class ModConfig
    {
        public int MaxStations = 20;
        public bool SyncConfig = true;  // Add sync config option

        // Simple JSON serialization
        public string ToJson()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"MaxStations\": {MaxStations},");
            sb.AppendLine($"  \"SyncConfig\": {SyncConfig.ToString().ToLower()}");
            sb.AppendLine("}");
            return sb.ToString();
        }

        // Simple JSON deserialization
        public static ModConfig FromJson(string json)
        {
            ModConfig config = new ModConfig();
            try
            {
                var maxStationsMatch = System.Text.RegularExpressions.Regex.Match(json, "\"MaxStations\"\\s*:\\s*(\\d+)");
                var syncConfigMatch = System.Text.RegularExpressions.Regex.Match(json, "\"SyncConfig\"\\s*:\\s*(true|false)");

                if (maxStationsMatch.Success && maxStationsMatch.Groups.Count > 1)
                {
                    if (int.TryParse(maxStationsMatch.Groups[1].Value, out int result))
                    {
                        config.MaxStations = result;
                    }
                }

                if (syncConfigMatch.Success && syncConfigMatch.Groups.Count > 1)
                {
                    config.SyncConfig = bool.Parse(syncConfigMatch.Groups[1].Value);
                }
            }
            catch (System.Exception)
            {
                // If parsing fails, return default config
            }
            return config;
        }
    }

    public class Core : MelonMod
    {
        private Il2CppSystem.Collections.Generic.Dictionary<int, bool> processedChemists;
        private HarmonyLib.Harmony harmony;
        private static ModConfig config = new ModConfig();

        // Host and client gameplay values
        private static int hostMaxStations = 20;
        private static int clientMaxStations = 20;

        // Network-related fields
        private static CSteamID localSteamID;
        private static bool isHost = false;
        private static bool configSynced = false;
        private static bool isInitialized = false;
        private const string CONFIG_MESSAGE_PREFIX = "CHEM_CONFIG:";
        private const int CONFIG_SYNC_INTERVAL = 10;
        private static float lastConfigSyncTime = 0f;

        // Config file paths
        private static string CONFIG_DIRECTORY = Path.Combine("UserData", "ChemistEnhanced");
        private static string CONFIG_FILE = Path.Combine(CONFIG_DIRECTORY, "config.json");

        // MelonPrefs categories and entries
        private const string CATEGORY_GENERAL = "ChemistEnhanced";
        private const string SETTING_MAX_STATIONS = "MaxStations";
        private const string SETTING_SYNC_CONFIG = "SyncConfig";

        // Steam callbacks for networking
        private static class SteamCallbacks
        {
            private static Callback<P2PSessionRequest_t> p2pSessionRequestCallback;
            private static Callback<P2PSessionConnectFail_t> p2pSessionConnectFailCallback;

            public static void RegisterCallbacks()
            {
                // Use the static Create method with an Action delegate
                p2pSessionRequestCallback = Callback<P2PSessionRequest_t>.Create(new Action<P2PSessionRequest_t>(OnP2PSessionRequest));
                p2pSessionConnectFailCallback = Callback<P2PSessionConnectFail_t>.Create(new Action<P2PSessionConnectFail_t>(OnP2PSessionConnectFail));

                // Start polling for messages
                MelonCoroutines.Start(PollForMessages());
            }

            private static void OnP2PSessionRequest(P2PSessionRequest_t param)
            {
                // Accept all session requests
                SteamNetworking.AcceptP2PSessionWithUser(param.m_steamIDRemote);
            }

            private static void OnP2PSessionConnectFail(P2PSessionConnectFail_t param)
            {
                MelonLogger.Warning($"P2P connection failed: {param.m_eP2PSessionError}");
            }

            private static System.Collections.IEnumerator PollForMessages()
            {
                while (true)
                {
                    yield return new WaitForSeconds(0.5f);

                    uint msgSize;
                    while (SteamNetworking.IsP2PPacketAvailable(out msgSize))
                    {
                        byte[] data = new byte[msgSize];
                        CSteamID senderId;

                        if (SteamNetworking.ReadP2PPacket(data, msgSize, out msgSize, out senderId))
                        {
                            string message = Encoding.UTF8.GetString(data);

                            // Check if it's a config message
                            if (message.StartsWith(CONFIG_MESSAGE_PREFIX))
                            {
                                ProcessReceivedConfigMessage(message);
                            }
                        }
                    }
                }
            }
        }

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg($"ChemistEnhanced Starting...");
            processedChemists = new Il2CppSystem.Collections.Generic.Dictionary<int, bool>();
            harmony = new HarmonyLib.Harmony("com.coolbriggs.chemistenhanced");

            // Register MelonPrefs
            MelonPreferences.CreateCategory(CATEGORY_GENERAL, "Chemist Enhanced Settings");
            MelonPreferences.CreateEntry(CATEGORY_GENERAL, SETTING_MAX_STATIONS, config.MaxStations, "Max Stations");
            MelonPreferences.CreateEntry(CATEGORY_GENERAL, SETTING_SYNC_CONFIG, config.SyncConfig, "Sync Config (Host Only)");

            // Load config and initialize gameplay values
            LoadConfig();
            hostMaxStations = config.MaxStations;
            clientMaxStations = config.MaxStations;

            // Initialize Steam callbacks
            SteamCallbacks.RegisterCallbacks();

            LoggerInstance.Msg("If you need any help join https://discord.gg/PCawAVnhMH");
            LoggerInstance.Msg("Happy Selling!");
        }

        private void LoadConfig()
        {
            try
            {
                // Create directory if it doesn't exist
                if (!Directory.Exists(CONFIG_DIRECTORY))
                {
                    Directory.CreateDirectory(CONFIG_DIRECTORY);
                }

                // If config file exists, load it
                if (File.Exists(CONFIG_FILE))
                {
                    string json = File.ReadAllText(CONFIG_FILE);
                    config = ModConfig.FromJson(json);

                    // Update MelonPrefs to match loaded config
                    MelonPreferences.SetEntryValue(CATEGORY_GENERAL, SETTING_MAX_STATIONS, config.MaxStations);
                    MelonPreferences.SetEntryValue(CATEGORY_GENERAL, SETTING_SYNC_CONFIG, config.SyncConfig);

                    LoggerInstance.Msg("Config loaded from file");
                }
                else
                {
                    // If no config file exists, create one with default values
                    config = new ModConfig();
                    SaveConfig();
                    LoggerInstance.Msg("Created new config file with default values");
                }
            }
            catch (System.Exception ex)
            {
                LoggerInstance.Error($"Error loading config: {ex.Message}");
                config = new ModConfig();
            }
        }

        private void SaveConfig()
        {
            try
            {
                // Update config from MelonPrefs
                config.MaxStations = MelonPreferences.GetEntryValue<int>(CATEGORY_GENERAL, SETTING_MAX_STATIONS);
                config.SyncConfig = MelonPreferences.GetEntryValue<bool>(CATEGORY_GENERAL, SETTING_SYNC_CONFIG);

                // Create directory if it doesn't exist
                if (!Directory.Exists(CONFIG_DIRECTORY))
                {
                    Directory.CreateDirectory(CONFIG_DIRECTORY);
                }

                // Save config to file
                string json = config.ToJson();
                File.WriteAllText(CONFIG_FILE, json);

                MelonPreferences.Save();

                LoggerInstance.Msg("Config saved to file");
            }
            catch (System.Exception ex)
            {
                LoggerInstance.Error($"Error saving config: {ex.Message}");
            }
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (sceneName != "Main") return;

            isInitialized = false;
            configSynced = false;
            MelonCoroutines.Start(DelayedInit());

            // Set up Harmony patches
            try
            {
                var chemistType = typeof(Chemist);
                var initializeMethod = chemistType.GetMethod("Initialize",
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.Static);

                if (initializeMethod != null)
                {
                    var postfix = typeof(Core).GetMethod(nameof(ChemistInitializePostfix),
                        BindingFlags.NonPublic | BindingFlags.Static);

                    harmony.Patch(initializeMethod,
                        postfix: new HarmonyMethod(postfix));
                }
            }
            catch (System.Exception)
            {
                // Silent catch
            }
        }

        private System.Collections.IEnumerator DelayedInit()
        {
            yield return new WaitForSeconds(1.0f);
            var player = GameObject.Find("Player");
            if (player != null)
            {
                DetermineIfHost();
                isInitialized = true;
            }
        }

        private void DetermineIfHost()
        {
            try
            {
                localSteamID = SteamUser.GetSteamID();
                CSteamID lobbyOwner = SteamMatchmaking.GetLobbyOwner(SteamMatchmaking.GetLobbyByIndex(0));
                isHost = lobbyOwner.m_SteamID == localSteamID.m_SteamID;
                LoggerInstance.Msg($"You are {(isHost ? "the host" : "a client")}");
            }
            catch (System.Exception)
            {
                isHost = false;
            }
        }

        public override void OnUpdate()
        {
            if (!isInitialized || !isHost) return;

            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Main") return;

            if (isHost && config.SyncConfig && !configSynced && UnityEngine.Time.time - lastConfigSyncTime > CONFIG_SYNC_INTERVAL)
            {
                SyncConfigToClients();
                lastConfigSyncTime = UnityEngine.Time.time;
            }

            // Read incoming packets
            uint packetSize;
            while (SteamNetworking.IsP2PPacketAvailable(out packetSize))
            {
                byte[] buffer = new byte[packetSize];
                CSteamID senderId;
                if (SteamNetworking.ReadP2PPacket(buffer, packetSize, out _, out senderId))
                {
                    string message = Encoding.UTF8.GetString(buffer);
                    if (message.StartsWith(CONFIG_MESSAGE_PREFIX))
                    {
                        ProcessReceivedConfigMessage(message);
                    }
                }
            }
        }

        private void SyncConfigToClients()
        {
            if (!isHost || !config.SyncConfig) return;

            try
            {
                string configData = $"{CONFIG_MESSAGE_PREFIX}{hostMaxStations}";

                CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(0);
                int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobbyId);

                for (int i = 0; i < memberCount; i++)
                {
                    CSteamID memberId = SteamMatchmaking.GetLobbyMemberByIndex(lobbyId, i);
                    if (memberId.m_SteamID != localSteamID.m_SteamID)
                    {
                        byte[] data = Encoding.UTF8.GetBytes(configData);
                        SteamNetworking.SendP2PPacket(memberId, data, (uint)data.Length, EP2PSend.k_EP2PSendReliable);
                    }
                }

                configSynced = true;
                LoggerInstance.Msg("Station values synced to all clients");
            }
            catch (System.Exception ex)
            {
                LoggerInstance.Error($"Failed to sync config: {ex.Message}");
            }
        }

        private static void ProcessReceivedConfigMessage(string message)
        {
            if (!message.StartsWith(CONFIG_MESSAGE_PREFIX)) return;

            try
            {
                string configData = message.Substring(CONFIG_MESSAGE_PREFIX.Length);
                // Only update the client-side gameplay value, never touch the config
                clientMaxStations = int.Parse(configData);
                MelonLogger.Msg($"Received host gameplay value: maxStations={clientMaxStations}");
                configSynced = true;
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Failed to process station values: {ex.Message}");
                // On failure, fall back to local config value
                clientMaxStations = config.MaxStations;
            }
        }

        private static void ChemistInitializePostfix(Chemist __instance)
        {
            if (__instance == null || __instance._configuration_k__BackingField == null ||
                __instance._configuration_k__BackingField.Stations == null) return;

            __instance._configuration_k__BackingField.Stations.MaxItems = isHost ? hostMaxStations : clientMaxStations;
            MelonLogger.Msg($"Max Stations set to {__instance._configuration_k__BackingField.Stations.MaxItems}");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (sceneName != "Main") return;
            processedChemists.Clear();
        }

        // Handle config changes from the MelonPrefs menu
        public override void OnPreferencesSaved()
        {
            bool configChanged = false;

            int newMaxStations = MelonPreferences.GetEntryValue<int>(CATEGORY_GENERAL, SETTING_MAX_STATIONS);
            if (newMaxStations != config.MaxStations)
            {
                config.MaxStations = newMaxStations;
                hostMaxStations = newMaxStations; // Update host value only
                configChanged = true;
            }

            bool newSyncConfig = MelonPreferences.GetEntryValue<bool>(CATEGORY_GENERAL, SETTING_SYNC_CONFIG);
            if (newSyncConfig != config.SyncConfig)
            {
                config.SyncConfig = newSyncConfig;
                configChanged = true;
            }

            if (configChanged)
            {
                SaveConfig();

                if (isHost && config.SyncConfig)
                {
                    configSynced = false;
                    lastConfigSyncTime = 0f; // Force immediate sync
                    LoggerInstance.Msg("Config changed - will sync to clients");
                }
            }
        }

        public override void OnApplicationQuit()
        {
            SaveConfig();
        }
    }
}
