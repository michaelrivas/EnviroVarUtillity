using System;
using System.Collections;
using System.Collections.Generic;
using System.IO; // Required for File operations
using System.Security.Cryptography; // Required for DPAPI (ProtectedData)
using System.Text; // Required for Encoding
using System.Xml.Linq; // Required for parsing XML
using Microsoft.Win32; // Required for direct Registry access to list variables

namespace EnviroVarUtillity
{
    internal class Program
    {
        private static string currentEnvironmentVariablePrefix = string.Empty;
        private static string currentFileKeyPrefix = string.Empty;
        static void Main(string[] args)
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("--- Environment Variable Manager ---");
                Console.WriteLine($"Current Env Var Prefix: {(string.IsNullOrEmpty(currentEnvironmentVariablePrefix) ? "<Not Set>" : currentEnvironmentVariablePrefix)}");
                Console.WriteLine($"Current File Key Prefix: {(string.IsNullOrEmpty(currentFileKeyPrefix) ? "<Not Set>" : currentFileKeyPrefix)}");
                Console.WriteLine("------------------------------------");
                Console.WriteLine("1. Set/Change Default Prefixes");
                Console.WriteLine("2. Remove User environment variables (uses Env Var Prefix)");
                Console.WriteLine("3. Add new User environment variables (uses Env Var Prefix, values encrypted)");
                Console.WriteLine("4. List User environment variables (uses Env Var Prefix, values decrypted)");
                Console.WriteLine("5. Demo: Lookup App Setting (Env Var -> Config File)");
                Console.WriteLine("6. Encrypt all plain text settings in 'setting.config' (uses File Key Prefix)");
                Console.WriteLine("7. Exit");
                Console.Write("Select an option (1-7): ");

                string choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        SetDefaultPrefixes();
                        break;
                    case "2":
                        if (string.IsNullOrEmpty(currentEnvironmentVariablePrefix)) { NotifyMissingPrefix("Environment Variable"); break; }
                        UserEnvironmentVariableOperations.RemoveByPrefix(currentEnvironmentVariablePrefix);
                        NotifyUserAboutChanges();
                        PauseBeforeContinuing();
                        break;
                    case "3":
                        if (string.IsNullOrEmpty(currentEnvironmentVariablePrefix)) { NotifyMissingPrefix("Environment Variable"); break; }
                        UserEnvironmentVariableOperations.AddNewWithPrefix(currentEnvironmentVariablePrefix);
                        NotifyUserAboutChanges();
                        PauseBeforeContinuing();
                        break;
                    case "4":
                        if (string.IsNullOrEmpty(currentEnvironmentVariablePrefix)) { NotifyMissingPrefix("Environment Variable"); break; }
                        UserEnvironmentVariableOperations.ListByPrefix(currentEnvironmentVariablePrefix);
                        PauseBeforeContinuing();
                        break;
                    case "5":
                        InteractiveDemoAppSettingLookup();
                        break;
                    case "6":
                        CallEncryptAllPlainTextSettingsInConfig();
                        break;
                    case "7":
                        Console.WriteLine("Exiting application.");
                        return;
                    default:
                        Console.WriteLine("Invalid choice. Press any key to try again.");
                        Console.ReadKey();
                        break;
                }
            }
        }

        static void NotifyMissingPrefix(string prefixType)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nOperation requires the '{prefixType} Prefix' to be set.");
            Console.WriteLine("Please use Option 1 from the main menu to set it.");
            Console.ResetColor();
            PauseBeforeContinuing();
        }
        static void SetDefaultPrefixes()
        {
            Console.Clear();
            Console.WriteLine("--- Set/Change Default Prefixes ---");
            Console.WriteLine($"Current Environment Variable Prefix: {(string.IsNullOrEmpty(currentEnvironmentVariablePrefix) ? "<Not Set>" : currentEnvironmentVariablePrefix)}");
            Console.Write("Enter new Environment Variable Prefix (e.g., MYAPP_DEV_ ) or leave blank to clear: ");
            currentEnvironmentVariablePrefix = FormatKeyPrefix(Console.ReadLine());
            if (string.IsNullOrEmpty(currentEnvironmentVariablePrefix)) Console.WriteLine("Environment Variable Prefix cleared.");
            else Console.WriteLine($"Environment Variable Prefix set to: {currentEnvironmentVariablePrefix}");


            Console.WriteLine($"\nCurrent File Key Prefix: {(string.IsNullOrEmpty(currentFileKeyPrefix) ? "<Not Set>" : currentFileKeyPrefix)}");
            Console.Write("Enter new File Key Prefix (e.g., CONFIG_ ) or leave blank to clear: ");
            currentFileKeyPrefix = FormatKeyPrefix(Console.ReadLine());
            if (string.IsNullOrEmpty(currentFileKeyPrefix)) Console.WriteLine("File Key Prefix cleared.");
            else Console.WriteLine($"File Key Prefix set to: {currentFileKeyPrefix}");

            PauseBeforeContinuing();
        }
        private static string FormatKeyPrefix(string prefixInput)
        {
            if (string.IsNullOrWhiteSpace(prefixInput))
                return string.Empty;

            string formatted = prefixInput.Trim();
            if (!formatted.EndsWith("_") && !string.IsNullOrEmpty(formatted))
            {
                formatted += "_";
            }
            return formatted;
        }
        static void InteractiveDemoAppSettingLookup()
        {
            Console.Clear();
            Console.WriteLine("--- Demo: App Setting Lookup (Env Var -> Config File w/ per-value encryption) ---");

            if (string.IsNullOrEmpty(currentEnvironmentVariablePrefix))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Notice: Default Environment Variable Prefix is not set. Lookup will use base name for Env Vars.");
                Console.ResetColor();
            }
            if (string.IsNullOrEmpty(currentFileKeyPrefix))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Notice: Default File Key Prefix is not set. Lookup will use base name for File Keys.");
                Console.ResetColor();
            }

            Console.Write("\nEnter the base name of the setting you want to look up: ");
            string baseSettingName = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(baseSettingName))
            {
                Console.WriteLine("Base setting name cannot be empty.");
                PauseBeforeContinuing();
                return;
            }

            string settingNameToSearchInEnv = currentEnvironmentVariablePrefix + baseSettingName;
            string settingNameToSearchInFile = currentFileKeyPrefix + baseSettingName;


            Console.WriteLine($"\nLooking for setting '{settingNameToSearchInEnv}' (Env) / '{settingNameToSearchInFile}' (File)...");

            string configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "setting.config");
            bool wasPlainTextInFile;
            XElement targetElementInConfigFile;
            XDocument configFileDoc;
            bool configFileExists, configFileParsed, appSettingsNodeExists;
            bool envVarDecryptedSuccessfully, fileValueDecryptedSuccessfully;
            bool foundInEnv;

            string retrievedValue = SecretManagerLogic.GetSettingValue(
                settingNameToSearchInEnv,
                settingNameToSearchInFile,
                configFilePath,
                out wasPlainTextInFile, out targetElementInConfigFile, out configFileDoc,
                out configFileExists, out configFileParsed, out appSettingsNodeExists,
                out envVarDecryptedSuccessfully, out fileValueDecryptedSuccessfully,
                out foundInEnv
            );

            if (foundInEnv)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"SUCCESS: Found setting '{settingNameToSearchInEnv}' in User Environment Variables.");
                if (envVarDecryptedSuccessfully) Console.WriteLine("(Value was encrypted, successfully decrypted)");
                else if (retrievedValue.Contains("DECRYPTION FAILED")) Console.WriteLine("(Value was encrypted, DECRYPTION FAILED)");
                Console.ResetColor();
                Console.WriteLine($"Value: '{retrievedValue}'");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"INFO: Setting '{settingNameToSearchInEnv}' not found in User Environment Variables.");
                Console.ResetColor();
                Console.WriteLine($"Attempting to fall back to 'setting.config' looking for key '{settingNameToSearchInFile}'...");
                DisplayConfigFileGuidance(baseSettingName, currentFileKeyPrefix);


                if (!configFileExists)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"FAIL: Fallback file '{configFilePath}' not found.");
                    Console.ResetColor();
                }
                else if (!configFileParsed)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"FAIL: Could not parse '{configFilePath}' as XML. Check its content and format.");
                    Console.ResetColor();
                }
                else if (!appSettingsNodeExists)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"FAIL: File '{configFilePath}' does not contain a root <appSettings> element.");
                    Console.ResetColor();
                }
                else if (targetElementInConfigFile == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"FAIL: Setting key '{settingNameToSearchInFile}' not found within '{configFilePath}'.");
                    Console.ResetColor();
                }
                else if (wasPlainTextInFile)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"INFO: Found plain text setting for key '{settingNameToSearchInFile}' in '{configFilePath}'.");
                    Console.ResetColor();
                    Console.WriteLine($"Value: '{retrievedValue}'");

                    Console.Write($"\nDo you want to encrypt this plain text value for key '{settingNameToSearchInFile}' and update '{configFilePath}'? (yes/no): ");
                    string encryptConfirmation = Console.ReadLine();
                    if (encryptConfirmation.Equals("yes", StringComparison.OrdinalIgnoreCase))
                    {
                        SecretManagerLogic.TryEncryptAndUpdateConfigFile(configFilePath, targetElementInConfigFile, configFileDoc, retrievedValue, settingNameToSearchInFile);
                    }
                    else
                    {
                        Console.WriteLine("INFO: Plain text value was not encrypted or updated.");
                    }
                }
                else if (fileValueDecryptedSuccessfully)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"SUCCESS: Successfully retrieved and decrypted setting for key '{settingNameToSearchInFile}' from '{configFilePath}'.");
                    Console.ResetColor();
                    Console.WriteLine($"Value: '{retrievedValue}'");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"FAIL: Setting key '{settingNameToSearchInFile}' in '{configFilePath}' is marked as encrypted, but decryption failed.");
                    Console.WriteLine($"Value retrieved: {retrievedValue}");
                    Console.ResetColor();
                }
            }
            PauseBeforeContinuing();
        }
        static void DisplayConfigFileGuidance(string baseSettingName, string formattedFileKeyPrefix)
        {
            string exampleKeyInFile = formattedFileKeyPrefix + baseSettingName;
            string anotherExampleKeyInFile = formattedFileKeyPrefix + "Another" + baseSettingName;

            Console.WriteLine("\n--- How to prepare 'setting.config' for this demo (with per-value encryption) ---");
            Console.WriteLine("1. Create a plain text XML file named 'setting.config' in the same directory as this .exe:");
            Console.WriteLine("   <appSettings>");
            Console.WriteLine($"     <add key=\"{exampleKeyInFile}\" value=\"YourPlainTextValue\"/>");
            Console.WriteLine($"     <add key=\"{anotherExampleKeyInFile}\" value=\"{SecretManagerLogic.ENCRYPTED_PREFIX}YOUR_BASE64_ENCODED_DPAPI_ENCRYPTED_STRING_HERE\"/>");
            Console.WriteLine("     ");
            Console.WriteLine("   </appSettings>");
            Console.WriteLine($"2. To generate the '{SecretManagerLogic.ENCRYPTED_PREFIX}...' value for a secret:");
            Console.WriteLine("   a. Take your secret string (e.g., \"my_actual_api_key\").");
            Console.WriteLine("   b. Convert it to bytes: Encoding.UTF8.GetBytes(\"my_actual_api_key\").");
            Console.WriteLine("   c. Encrypt these bytes using DPAPI: ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser).");
            Console.WriteLine("   d. Convert the resulting encrypted byte array to a Base64 string: Convert.ToBase64String(encryptedBytes).");
            Console.WriteLine($"   e. Prepend \"{SecretManagerLogic.ENCRYPTED_PREFIX}\" to this Base64 string. This is the value you put in setting.config.");
            Console.WriteLine("-----------------------------------------------------------------------------------\n");
        }
        static void CallEncryptAllPlainTextSettingsInConfig()
        {
            Console.Clear();
            Console.WriteLine("--- Encrypt All Plain Text Settings in setting.config (using File Key Prefix) ---");
            Console.WriteLine($"Current File Key Prefix: {(string.IsNullOrEmpty(currentFileKeyPrefix) ? "<Will target ALL plain text settings>" : currentFileKeyPrefix)}");
            if (string.IsNullOrEmpty(currentFileKeyPrefix))
                Console.WriteLine("To target specific prefixed keys, please set a File Key Prefix using Option 1.");
            else
                Console.WriteLine($"Only unencrypted values whose keys start with '{currentFileKeyPrefix}' will be targeted.");

            string configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "setting.config");
            SecretManagerLogic.EncryptAllPlainTextSettingsInConfigFile(configFilePath, currentFileKeyPrefix);
        }
        public static void NotifyUserAboutChanges()
        {
            Console.WriteLine("\nIMPORTANT: For changes to be fully reflected in all applications and open shells,");
            Console.WriteLine("you may need to log out and log back in, or restart your machine.");
        }
        public static void PauseBeforeContinuing()
        {
            Console.WriteLine("\nPress any key to return to the menu...");
            Console.ReadKey();
        }

    }

}
