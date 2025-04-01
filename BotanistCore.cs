using System;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Il2CppScheduleOne.Employees;
using Il2CppSteamworks;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(BotanistEnhanced.Core), "BotanistEnhanced", "1.0.0", "rbawe", null)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace BotanistEnhanced
{
    public class ModConfig
    {
        public int MaxAssignedPots = 20;
        public bool SyncConfig = true;  // Add sync config option

        // Simple JSON serialization
        public string ToJson()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"MaxAssignedPots\": {MaxAssignedPots},");
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
                string maxPotsPattern = "\"MaxAssignedPots\"\\s*:\\s*(\\d+)";
                string syncConfigPattern = "\"SyncConfig\"\\s*:\\s*(true|false)";
                
                var maxPotsMatch = System.Text.RegularExpressions.Regex.Match(json, maxPotsPattern);
                var syncConfigMatch = System.Text.RegularExpressions.Regex.Match(json, syncConfigPattern);
                
                if (maxPotsMatch.Success && maxPotsMatch.Groups.Count > 1)
                {
                    if (int.TryParse(maxPotsMatch.Groups[1].Value, out int result))
                    {
                        config.MaxAssignedPots = result;
                    }
                }
                
                if (syncConfigMatch.Success && syncConfigMatch.Groups.Count > 1)
                {
                    if (bool.TryParse(syncConfigMatch.Groups[1].Value, out bool result))
                    {
                        config.SyncConfig = result;
                    }
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
        private Il2CppSystem.Collections.Generic.Dictionary<int, bool> processedBotanists;
        private HarmonyLib.Harmony harmony;
        private static ModConfig config = new ModConfig();

        // Config file paths
        private static string CONFIG_DIRECTORY = Path.Combine("UserData", "BotanistEnhanced");
        private static string CONFIG_FILE = Path.Combine(CONFIG_DIRECTORY, "config.json");

        // MelonPrefs categories and entries
        private const string CATEGORY_GENERAL = "BotanistEnhanced";
        private const string SETTING_MAX_POTS = "MaxAssignedPots";
        private const string SETTING_SYNC_CONFIG = "SyncConfig";

        // Network-related fields
        private static CSteamID localSteamID;
        private static bool isHost = false;
        private static bool configSynced = false;
        private static bool isInitialized = false;
        private const string CONFIG_MESSAGE_PREFIX = "BOT_CONFIG:";
        private const int CONFIG_SYNC_INTERVAL = 10;
        private static float lastConfigSyncTime = 0f;

        // Host and client gameplay values
        private static int hostMaxPots = 20;
        private static int clientMaxPots = 20;

        // Steam callbacks for networking
        private static class SteamCallbacks
        {
            private static Callback<P2PSessionRequest_t> p2pSessionRequestCallback;
            private static Callback<P2PSessionConnectFail_t> p2pSessionConnectFailCallback;

            public static void RegisterCallbacks()
            {
                p2pSessionRequestCallback = Callback<P2PSessionRequest_t>.Create(new Action<P2PSessionRequest_t>(OnP2PSessionRequest));
                p2pSessionConnectFailCallback = Callback<P2PSessionConnectFail_t>.Create(new Action<P2PSessionConnectFail_t>(OnP2PSessionConnectFail));

                MelonCoroutines.Start(PollForMessages());
            }

            private static void OnP2PSessionRequest(P2PSessionRequest_t param)
            {
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
            LoggerInstance.Msg($"BotanistEnhanced Starting...");
            processedBotanists = new Il2CppSystem.Collections.Generic.Dictionary<int, bool>();
            harmony = new HarmonyLib.Harmony("com.rbawe.botanistenhanced");

            // Register MelonPrefs
            MelonPreferences.CreateCategory(CATEGORY_GENERAL, "Botanist Enhanced Settings");
            MelonPreferences.CreateEntry(CATEGORY_GENERAL, SETTING_MAX_POTS, config.MaxAssignedPots, "Max Assigned Pots");
            MelonPreferences.CreateEntry(CATEGORY_GENERAL, SETTING_SYNC_CONFIG, config.SyncConfig, "Sync Config (Host Only)");

            // Load config and initialize gameplay values
            LoadConfig();
            hostMaxPots = config.MaxAssignedPots;
            clientMaxPots = config.MaxAssignedPots;

            // Initialize Steam callbacks
            SteamCallbacks.RegisterCallbacks();

            LoggerInstance.Msg("If you need any help join https://discord.gg/PCawAVnhMH");
            LoggerInstance.Msg("Happy Selling!");
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
                CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(0);
                CSteamID ownerId = SteamMatchmaking.GetLobbyOwner(lobbyId);
                isHost = (ownerId.m_SteamID == localSteamID.m_SteamID);
                LoggerInstance.Msg($"Player is {(isHost ? "host" : "client")}");
            }
            catch (Exception ex)
            {
                LoggerInstance.Error($"Failed to determine host status: {ex.Message}");
                isHost = false;
            }
        }

        private static void ProcessReceivedConfigMessage(string message)
        {
            if (!message.StartsWith(CONFIG_MESSAGE_PREFIX)) return;

            try
            {
                string[] parts = message.Substring(CONFIG_MESSAGE_PREFIX.Length).Split('|');
                if (parts.Length > 0 && int.TryParse(parts[0], out int maxPots))
                {
                    // Only update the client-side gameplay value, never touch the config
                    clientMaxPots = maxPots;
                    MelonLogger.Msg($"Received host gameplay value: MaxPots = {clientMaxPots}");
                    configSynced = true;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to process config message: {ex.Message}");
                // On failure, fall back to local config value
                clientMaxPots = config.MaxAssignedPots;
            }
        }

        private void SyncConfigToClients()
        {
            if (!isHost || !config.SyncConfig) return;

            try
            {
                string configData = $"{CONFIG_MESSAGE_PREFIX}{hostMaxPots}";

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
                LoggerInstance.Msg("Pot values synced to all clients");
            }
            catch (Exception ex)
            {
                LoggerInstance.Error($"Failed to sync config: {ex.Message}");
            }
        }

        public override void OnUpdate()
        {
            if (!isInitialized) return;

            // Periodic config sync for host
            if (isHost && config.SyncConfig && Time.time - lastConfigSyncTime > CONFIG_SYNC_INTERVAL)
            {
                SyncConfigToClients();
                lastConfigSyncTime = Time.time;
            }
        }

        private void LoadConfig()
        {
            try
            {
                if (!Directory.Exists(CONFIG_DIRECTORY))
                {
                    Directory.CreateDirectory(CONFIG_DIRECTORY);
                }

                if (File.Exists(CONFIG_FILE))
                {
                    string json = File.ReadAllText(CONFIG_FILE);
                    config = ModConfig.FromJson(json);

                    MelonPreferences.SetEntryValue(CATEGORY_GENERAL, SETTING_MAX_POTS, config.MaxAssignedPots);
                    MelonPreferences.SetEntryValue(CATEGORY_GENERAL, SETTING_SYNC_CONFIG, config.SyncConfig);

                    LoggerInstance.Msg("Config loaded from file");
                }
                else
                {
                    config = new ModConfig();
                    SaveConfig();
                    LoggerInstance.Msg("Created new config file with default values");
                }
            }
            catch (Exception ex)
            {
                LoggerInstance.Error($"Error loading config: {ex.Message}");
                config = new ModConfig();
            }
        }

        private void SaveConfig()
        {
            try
            {
                config.MaxAssignedPots = MelonPreferences.GetEntryValue<int>(CATEGORY_GENERAL, SETTING_MAX_POTS);
                config.SyncConfig = MelonPreferences.GetEntryValue<bool>(CATEGORY_GENERAL, SETTING_SYNC_CONFIG);

                if (!Directory.Exists(CONFIG_DIRECTORY))
                {
                    Directory.CreateDirectory(CONFIG_DIRECTORY);
                }

                string json = config.ToJson();
                File.WriteAllText(CONFIG_FILE, json);

                MelonPreferences.Save();

                LoggerInstance.Msg("Config saved to file");
            }
            catch (Exception ex)
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

            try
            {
                var botanistType = typeof(Botanist);
                var methods = botanistType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.Static);

                var initializeMethod = methods.FirstOrDefault(m => m.Name.Contains("Initialize"));

                if (initializeMethod == null)
                {
                    MelonLogger.Error("Could not find Initialize method");
                    return;
                }

                var postfix = typeof(Core).GetMethod(nameof(BotanistInitializePostfix),
                    BindingFlags.NonPublic | BindingFlags.Static);

                harmony.Patch(initializeMethod,
                    postfix: new HarmonyMethod(postfix));

                MelonLogger.Msg("Successfully patched Botanist Initialize method");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to patch: {ex.Message}");
            }
        }

        private static void BotanistInitializePostfix(Botanist __instance)
        {
            try
            {
                if (__instance == null) return;

                // Use the appropriate value based on whether we're host or client
                __instance.MaxAssignedPots = isHost ? hostMaxPots : clientMaxPots;
                MelonLogger.Msg($"Enhanced Botanist: MaxAssignedPots set to {__instance.MaxAssignedPots}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in postfix: {ex.Message}");
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (sceneName != "Main") return;
            processedBotanists.Clear();
        }

        public override void OnPreferencesSaved()
        {
            int newMaxPots = MelonPreferences.GetEntryValue<int>(CATEGORY_GENERAL, SETTING_MAX_POTS);
            bool newSyncConfig = MelonPreferences.GetEntryValue<bool>(CATEGORY_GENERAL, SETTING_SYNC_CONFIG);

            if (newMaxPots != config.MaxAssignedPots || newSyncConfig != config.SyncConfig)
            {
                config.MaxAssignedPots = newMaxPots;
                config.SyncConfig = newSyncConfig;
                
                if (isHost)
                {
                    hostMaxPots = config.MaxAssignedPots;
                }
                
                SaveConfig();
                LoggerInstance.Msg($"Config updated - Max Assigned Pots: {config.MaxAssignedPots}, Sync Config: {config.SyncConfig}");
            }
        }

        public override void OnApplicationQuit()
        {
            SaveConfig();
        }
    }
}
