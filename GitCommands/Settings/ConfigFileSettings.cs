﻿using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using GitUIPluginInterfaces;

namespace GitCommands.Settings
{
    [DebuggerDisplay("{" + nameof(SettingLevel) + "}: {" + nameof(SettingsCache) + "} << {" + nameof(LowerPriority) + "}")]
    public sealed class ConfigFileSettings : SettingsContainer<ConfigFileSettings, ConfigFileSettingsCache>, IConfigFileSettings, IConfigValueStore
    {
        public ConfigFileSettings(ConfigFileSettings? lowerPriority, ConfigFileSettingsCache settingsCache, SettingLevel settingLevel)
            : base(lowerPriority, settingsCache)
        {
            SettingLevel = settingLevel;
        }

        public static ConfigFileSettings CreateEffective(GitModule module)
        {
            return CreateLocal(module,
                CreateGlobal(CreateSystemWide()),
                SettingLevel.Effective);
        }

        public static ConfigFileSettings CreateLocal(GitModule module, bool useSharedCache = true)
        {
            return CreateLocal(module, lowerPriority: null, SettingLevel.Local, useSharedCache);
        }

        private static ConfigFileSettings CreateLocal(GitModule module, ConfigFileSettings? lowerPriority, SettingLevel settingLevel, bool useSharedCache = true)
        {
            return new ConfigFileSettings(lowerPriority,
                ConfigFileSettingsCache.Create(Path.Combine(module.GitCommonDirectory, "config"), useSharedCache),
                settingLevel);
        }

        public static ConfigFileSettings CreateGlobal(bool useSharedCache = true)
        {
            return CreateGlobal(lowerPriority: null, useSharedCache);
        }

        public static ConfigFileSettings CreateGlobal(ConfigFileSettings? lowerPriority, bool useSharedCache = true)
        {
            string configPath = Path.Combine(EnvironmentConfiguration.GetHomeDir(), ".config", "git", "config");
            if (!File.Exists(configPath))
            {
                configPath = Path.Combine(EnvironmentConfiguration.GetHomeDir(), ".gitconfig");
            }

            return new ConfigFileSettings(lowerPriority,
                ConfigFileSettingsCache.Create(configPath, useSharedCache),
                SettingLevel.Global);
        }

        public static ConfigFileSettings? CreateSystemWide(bool useSharedCache = true)
        {
            // Git 2.xx
            string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Git", "config");
            if (!File.Exists(configPath))
            {
                // Git 1.xx
                configPath = Path.Combine(AppSettings.GitCommand, "..", "..", "etc", "gitconfig");
                if (!File.Exists(configPath))
                {
                    return null;
                }
            }

            return new ConfigFileSettings(lowerPriority: null,
                ConfigFileSettingsCache.Create(configPath, useSharedCache),
                SettingLevel.SystemWide);
        }

        public new string GetValue(string setting)
        {
            return GetString(setting, string.Empty);
        }

        public IReadOnlyList<string> GetValues(string setting)
        {
            return SettingsCache.GetValues(setting);
        }

        public new void SetValue(string setting, string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                // to remove setting
                value = null;
            }

            SetString(setting, value);
        }

        public void SetPathValue(string setting, string? value)
        {
            // for using unc paths -> these need to be backward slashes
            if (!string.IsNullOrWhiteSpace(value) && !value.StartsWith("\\\\"))
            {
                value = value.ToPosixPath();
            }

            SetValue(setting, value);
        }

        public IReadOnlyList<IConfigSection> GetConfigSections()
        {
            return SettingsCache.GetConfigSections();
        }

        /// <summary>
        /// Adds the specific configuration section to the .git/config file.
        /// </summary>
        /// <param name="configSection">The configuration section.</param>
        public void AddConfigSection(IConfigSection configSection)
        {
            SettingsCache.AddConfigSection(configSection);
        }

        /// <summary>
        /// Removes the specific configuration section from the .git/config file.
        /// </summary>
        /// <param name="configSectionName">The name of the configuration section.</param>
        /// <param name="performSave">If <see langword="true"/> the configuration changes will be saved immediately.</param>
        public void RemoveConfigSection(string configSectionName, bool performSave = false)
        {
            SettingsCache.RemoveConfigSection(configSectionName, performSave);
        }

        [MaybeNull]
        public Encoding FilesEncoding
        {
            get => GetEncoding("i18n.filesEncoding");
            set => SetEncoding("i18n.filesEncoding", value);
        }

        public Encoding? CommitEncoding => GetEncoding("i18n.commitEncoding");

        public Encoding? LogOutputEncoding => GetEncoding("i18n.logoutputencoding");

        private Encoding? GetEncoding(string settingName)
        {
            string? encodingName = GetValue(settingName);

            if (string.IsNullOrEmpty(encodingName))
            {
                return null;
            }

            // The keys in AppSettings.AvailableEncodings are lower-case, and the actual
            // configuration is case-insensitive, and can be configured uppercase on the
            // command line. If configured as UTF-8, then if we don't use the predefined
            // encoding, it will use the default, which adds BOM.
            // Convert it to lowercase, to ensure matching.
            encodingName = encodingName.ToLowerInvariant();

            if (AppSettings.AvailableEncodings.TryGetValue(encodingName, out var result))
            {
                return result;
            }

            try
            {
                return Encoding.GetEncoding(encodingName);
            }
            catch (ArgumentException)
            {
                Debug.WriteLine(
                    "Unsupported encoding set in git config file: {0}\n" +
                    "Please check the setting {1} in config file.", encodingName, settingName);
                return null;
            }
        }

        private void SetEncoding(string settingName, Encoding? encoding)
        {
            SetValue(settingName, encoding?.WebName);
        }
    }
}
