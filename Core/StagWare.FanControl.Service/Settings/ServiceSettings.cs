﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;
using System.Globalization;
using System.Linq;
using System.Diagnostics;

namespace StagWare.Settings
{
    public sealed partial class ServiceSettings
    {
        #region Nested Types

        private class Properties
        {
            #region Private Fields

            internal static readonly ServiceSettings instance;

            #endregion

            #region Constructors

            private Properties()
            { }

            // Explicit static constructor to tell C# compiler
            // not to mark type as 'beforefieldinit'.
            static Properties()
            {
                instance = new ServiceSettings();

                if (ServiceSettings.SettingsFileExists)
                {
                    try
                    {
                        using (FileStream fs = new FileStream(SettingsFileName, FileMode.Open))
                        {
                            var serializer = new XmlSerializer(typeof(ServiceSettings));
                            Properties.instance = (ServiceSettings)serializer.Deserialize(fs);
                        }
                    }
                    catch (Exception e)
                    {
                        OnLoadSettingsFailed(e);
                        RestoreDefaults();
                    }
                }
            }

            #endregion
        }

        #endregion

        #region Constants

        private const string DefaultSettingsFolderName = "AppSettings";
        private const string DefaultSettingsFileName = "settings.xml";

        #endregion

        #region Private Fields

        Dictionary<string, object> storedValues;

        #endregion

        #region Events

        public static event EventHandler<LoadSettingsFailedEventArgs> LoadSettingsFailed;

        #endregion

        #region Properties

        // Singleton instance.
        public static ServiceSettings Default
        {
            get { return Properties.instance; }
        }

        public static string SettingsFileName { get; set; }

        public static bool SettingsFileExists
        {
            get
            {
                return File.Exists(SettingsFileName);
            }
        }

        #endregion

        #region Constructors

        static ServiceSettings()
        {
            string folderName = GetProductName();

            if (string.IsNullOrWhiteSpace(folderName))
            {
                folderName = DefaultSettingsFolderName;
            }

            SettingsFileName = Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData), folderName, DefaultSettingsFileName);

            foreach (char c in Path.GetInvalidPathChars())
            {
                SettingsFileName = SettingsFileName.Replace(c, '_');
            }
        }

        // Hide constructor (Singleton)
        private ServiceSettings()
        {
            this.storedValues = new Dictionary<string, object>();
            RestoreDefaults(this, true);
        }

        #endregion

        #region Public Methods

        public static void RestoreDefaults()
        {
            RestoreDefaults(ServiceSettings.Default, false);
        }

        public static void Save()
        {
            string dir = Path.GetDirectoryName(SettingsFileName);

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using (FileStream fs = new FileStream(SettingsFileName, FileMode.Create))
            {
                var serializer = new XmlSerializer(typeof(ServiceSettings));
                serializer.Serialize(fs, ServiceSettings.Default);
            }
        }

        public static void StoreCurrentSettings()
        {
            Properties.instance.storedValues.Clear();

            foreach (PropertyInfo info in GetNonStaticProperties(typeof(ServiceSettings)))
            {
                object value = info.GetValue(Default, null);
                ServiceSettings.Default.storedValues.Add(info.Name, value);
            }
        }

        public static void LoadStoredSettings()
        {
            foreach (PropertyInfo info in GetNonStaticProperties(typeof(ServiceSettings)))
            {
                if (ServiceSettings.Default.storedValues.ContainsKey(info.Name))
                {
                    object value = ServiceSettings.Default.storedValues[info.Name];
                    info.SetValue(Default, value, null);
                }
            }
        }

        public static void DeleteSettingsFile()
        {
            if (ServiceSettings.SettingsFileExists)
            {
                File.Delete(SettingsFileName);
            }

            string dir = Path.GetDirectoryName(SettingsFileName);

            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }

        #endregion

        #region Private Methods

        private static void RestoreDefaults(ServiceSettings settings, bool force)
        {
            foreach (PropertyInfo pInfo in GetNonStaticProperties(typeof(ServiceSettings)))
            {
                if (force || !HasRestoreDefaultsIgnoreAttribute(pInfo))
                {
                    var defaultAttrib = pInfo.GetCustomAttributes(typeof(DefaultValueAttribute), false)
                        .FirstOrDefault() as DefaultValueAttribute;

                    if (defaultAttrib != null)
                    {
                        pInfo.SetValue(settings, defaultAttrib.Value, null);
                    }
                }
            }
        }

        private static IEnumerable<PropertyInfo> GetNonStaticProperties(Type type)
        {
            foreach (PropertyInfo propInfo in type.GetProperties())
            {
                MethodInfo methInfo = propInfo.GetGetMethod(false);

                if ((methInfo != null) && (!methInfo.IsStatic))
                {
                    yield return propInfo;
                }
            }
        }

        private static bool HasRestoreDefaultsIgnoreAttribute(PropertyInfo info)
        {
            return info.GetCustomAttributes(typeof(RestoreDefaultsIgnoreAttribute), false).Length > 0;
        }

        private static string GetProductName()
        {
            var info = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);
            return info.ProductName;
        }

        private static void OnLoadSettingsFailed(Exception e)
        {
            if (ServiceSettings.LoadSettingsFailed != null)
            {
                ServiceSettings.LoadSettingsFailed(null, new LoadSettingsFailedEventArgs(e));
            }
        }

        #endregion
    }
}

