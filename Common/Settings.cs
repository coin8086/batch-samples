using System;
using System.Collections.Generic;

namespace Common
{
    public class Settings
    {
        public string BatchAccountName { get; protected set; }

        public string BatchAccountUrl { get; protected set; }

        public string BatchAccountKey { get; protected set; }

        public Settings()
        {
            var args = Environment.GetCommandLineArgs();
            try
            {
                GetCmdArgs(args);
                GetEnvArgs();
            }
            catch (Exception ex)
            {
                ShowHelp(ex);
                throw;
            }
        }

        protected virtual void ShowHelp(Exception error)
        {
            if (error != null)
            {
                Console.WriteLine($"Error:\n{error.Message}");
            }
        }

        protected virtual void GetCmdArgs(string[] args) { }

        protected virtual void GetEnvArgs()
        {
            var settingNames = new string[] {
                "AZURE_BATCH_ACCOUNT",
                "AZURE_BATCH_ENDPOINT",
                "AZURE_BATCH_ACCESS_KEY",
            };
            var settings = new Dictionary<string, string>();
            foreach (var name in settingNames)
            {
                var value = Environment.GetEnvironmentVariable(name);
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException($"Environment variable `{name}' is not set.");
                }
                if (name == "AZ_BATCH_ACCOUNT_URL")
                {
                    //NOTE: The trailing '/' will invalidate the BatchSharedKeyCredentials with the URL.
                    value = value.TrimEnd('/');
                }
                settings[name] = value;
            }
            BatchAccountName = settings["AZURE_BATCH_ACCOUNT"];
            BatchAccountUrl = settings["AZURE_BATCH_ENDPOINT"];
            BatchAccountKey = settings["AZURE_BATCH_ACCESS_KEY"];
        }
    }
}
