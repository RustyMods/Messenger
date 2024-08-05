using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using ServerSync;
using UnityEngine;

namespace Messenger
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class MessengerPlugin : BaseUnityPlugin
    {
        internal const string ModName = "Messenger";
        internal const string ModVersion = "1.0.0";
        internal const string Author = "RustyMods";
        private const string ModGUID = Author + "." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static string ConnectionError = "";
        private readonly Harmony _harmony = new(ModGUID);
        public static readonly ManualLogSource MessengerLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);
        private static readonly ConfigSync ConfigSync = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

        public enum Toggle { On = 1, Off = 0 }
        public enum DateFormat
        {
            Numbered, Worded, TimeOnly
        }
        public static GameObject m_root = null!;
        
        private static ConfigEntry<Toggle> _serverConfigLocked = null!;

        public static ConfigEntry<Toggle> _useNotifications = null!;
        public static ConfigEntry<Toggle> _showIcon = null!;
        public static ConfigEntry<int> _iconSize = null!;
        public static ConfigEntry<Vector2> _notificationPos = null!;
        public static ConfigEntry<int> _spacing = null!;
        public static ConfigEntry<int> _msgSpacing = null!;
        public static ConfigEntry<int> _amount = null!;
        public static ConfigEntry<Toggle> _crossFade = null!;
        public static ConfigEntry<float> _crossFadeDuration = null!;

        public static ConfigEntry<string> _chatLogTitle = null!;
        public static ConfigEntry<int> _maxChatLog = null!;
        public static ConfigEntry<Toggle> _showTimestamp = null!;
        public static ConfigEntry<Toggle> _useChatLog = null!;
        public static ConfigEntry<DateFormat> _chatLogDateFormat = null!;
        public static ConfigEntry<Color> _dateColor = null!;

        public static ConfigEntry<Vector2> _removeButtonPos = null!;
        public static ConfigEntry<string> _removeText = null!;

        public static ConfigEntry<string> _damageLogTitle = null!;
        public static ConfigEntry<Toggle> _useDamageLog = null!;

        public static ConfigEntry<string> _unknown = null!;
        public static ConfigEntry<string> _hasInflicted = null!;
        public static ConfigEntry<string> _hasTaken = null!;
        public static ConfigEntry<string> _dps = null!;
        public static ConfigEntry<string> _attacks = null!;

        private void LoadConfigs()
        {
            _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On,
                "If on, the configuration is locked and can be changed by server admins only.");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);

            _useNotifications = config("2 - Notifications", "Enabled", Toggle.On, "If on, plugin will override vanilla top left messaging system");
            _showIcon = config("2 - Notifications", "Show Icon", Toggle.On, "If on, icon will appear with message");
            _iconSize = config("2 - Notifications", "Icon Size", 32, "Set message icon size");
            _notificationPos = config("2 - Notifications", "Position", new Vector2(350, 900), "Set position");
            _spacing = config("2 - Notifications", "Spacing", 0, "Set space between messages");
            _msgSpacing = config("2 - Notifications", "Message Spacing", -100, "Set the space between icon and text");
            _amount = config("2 - Notifications", "Amount", 5, "Set amount seen at the same time");
            _crossFade = config("2 - Notifications", "Crossfade", Toggle.On, "If on, messages will fade");
            _crossFadeDuration = config("2 - Notifications", "Crossfade Duration", 5f, "Set the duration of message");

            _chatLogTitle = config("3 - Chat Log", "Title", "Chat log", "Set title in compendium for chat log");
            _maxChatLog = config("3 - Chat Log", "Max", 50, "Set max amount of recorded messages");
            _useChatLog = config("3 - Chat Log", "Enabled", Toggle.On, "If on, chat log will be added to compendium");
            _showTimestamp = config("3 - Chat Log", "Show DateTime", Toggle.On, "If on, timestamp will be shown in chat log");
            _chatLogDateFormat = config("3 - Chat Log", "DateTime Format", DateFormat.Worded, "Set visual of date time");
            _dateColor = config("3 - Chat Log", "DateTime Color", new Color(1f, 0.4f, 0.5f, 1f), "Set color of date time in chat log");

            _removeButtonPos = config("4 - Compendium", "Remove Button Position", new Vector2(520, 100), "Set the position of the remove button");
            _removeText = config("4 - Compendium", "Remove Text", "Remove", "Set what the remove button text should display");

            _damageLogTitle = config("5 - Damage Log", "Title", "Damage log", "Set the title in compendium for damage log");
            _useDamageLog = config("5 - Damage Log", "Enabled", Toggle.On, "If on, damage log will be added to compendium");
            
            _hasTaken = config("6 - Localization", "Has taken", "has taken", "set this text to your language of choice");
            _hasInflicted = config("6 - Localization", "Has inflicted", "has inflicted", "set this text to your language of choice");
            _unknown = config("6 - Localization", "Unknown", "Unknown", "set this text to your language of choice");
            _dps = config("6 - Localization", "DPS", "DPS", "set this text to your language of choice");
            _attacks = config("6 - Localization", "Attacks", "attacks", "set this text to your language of choice");
        }
        public void Awake()
        {
            LoadConfigs();
            m_root = new GameObject("root");
            DontDestroyOnLoad(m_root);
            m_root.SetActive(false);
            
            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                MessengerLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                MessengerLogger.LogError($"There was an issue loading your {ConfigFileName}");
                MessengerLogger.LogError("Please check your config entries for spelling and format!");
            }
        }


        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription =
                new(
                    description.Description +
                    (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                    description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
            //var configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        private class ConfigurationManagerAttributes
        {
            [UsedImplicitly] public int? Order;
            [UsedImplicitly] public bool? Browsable;
            [UsedImplicitly] public string? Category;
            [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer;
        }

    }
}