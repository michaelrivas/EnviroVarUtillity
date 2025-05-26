using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace EnviroVarUtillity
{
    public static class SecretManagerLogic
    {
        public const string ENCRYPTED_PREFIX = "enc:";

        public static string EncryptValue(string plainText)
        {
            if (plainText == null) return null;
            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
                string base64EncryptedString = Convert.ToBase64String(encryptedBytes);
                return ENCRYPTED_PREFIX + base64EncryptedString;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"[Encryption Error]: {ex.Message}");
                Console.ResetColor();
                return plainText;
            }
        }

        public static bool TryDecryptValue(string potentiallyEncryptedValue, out string processedValue, out bool wasEncryptedAttempted)
        {
            processedValue = potentiallyEncryptedValue;
            wasEncryptedAttempted = false;

            if (string.IsNullOrEmpty(potentiallyEncryptedValue))
            {
                return true;
            }

            if (potentiallyEncryptedValue.StartsWith(ENCRYPTED_PREFIX, StringComparison.OrdinalIgnoreCase))
            {
                wasEncryptedAttempted = true;
                string base64EncryptedData = potentiallyEncryptedValue.Substring(ENCRYPTED_PREFIX.Length);
                try
                {
                    byte[] encryptedBytes = Convert.FromBase64String(base64EncryptedData);
                    byte[] decryptedBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                    processedValue = Encoding.UTF8.GetString(decryptedBytes);
                    return true;
                }
                catch (FormatException)
                {
                    processedValue = "[DECRYPTION FAILED: Invalid Base64 Format]";
                    return false;
                }
                catch (CryptographicException)
                {
                    processedValue = "[DECRYPTION FAILED: Cryptographic Error (ensure current user encrypted it)]";
                    return false;
                }
                catch (Exception ex)
                {
                    processedValue = $"[DECRYPTION FAILED: {ex.Message}]";
                    return false;
                }
            }
            return true;
        }


        public static string GetSettingValue(string settingNameForEnv, string settingNameForFile, string configFilePath,
                                            out bool wasPlainTextInFile, out XElement targetElementInDoc, out XDocument document,
                                            out bool configFileExists, out bool configFileParsed, out bool appSettingsNodeExists,
                                            out bool envVarDecryptedSuccessfully, out bool fileValueWasEncryptedAndDecryptedSuccessfully,
                                            out bool foundInEnv)
        {
            wasPlainTextInFile = false;
            targetElementInDoc = null;
            document = null;
            configFileExists = false;
            configFileParsed = false;
            appSettingsNodeExists = false;
            envVarDecryptedSuccessfully = false;
            fileValueWasEncryptedAndDecryptedSuccessfully = false;
            foundInEnv = false;


            string rawEnvValue = Environment.GetEnvironmentVariable(settingNameForEnv, EnvironmentVariableTarget.User);
            if (rawEnvValue != null)
            {
                foundInEnv = true;
                bool decryptionSuccess = TryDecryptValue(rawEnvValue, out string processedEnvValue, out bool wasEnvEncryptedAttempted);
                if (wasEnvEncryptedAttempted)
                {
                    envVarDecryptedSuccessfully = decryptionSuccess;
                }
                return processedEnvValue;
            }

            configFileExists = File.Exists(configFilePath);
            if (!configFileExists)
            {
                return null;
            }

            try
            {
                string xmlContent = File.ReadAllText(configFilePath);
                document = XDocument.Parse(xmlContent);
                configFileParsed = true;
                XElement appSettingsNode = document.Element("appSettings");

                if (appSettingsNode == null)
                {
                    return null;
                }
                appSettingsNodeExists = true;

                string rawValueFromConfig = null;

                foreach (XElement addElement in appSettingsNode.Elements("add"))
                {
                    if (addElement.Attribute("key")?.Value.Equals(settingNameForFile, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        rawValueFromConfig = addElement.Attribute("value")?.Value;
                        targetElementInDoc = addElement;
                        break;
                    }
                }

                if (rawValueFromConfig != null)
                {
                    bool decryptionSuccess = TryDecryptValue(rawValueFromConfig, out string processedFileValue, out bool wasFileEncryptedAttempted);
                    if (wasFileEncryptedAttempted)
                    {
                        fileValueWasEncryptedAndDecryptedSuccessfully = decryptionSuccess;
                        return processedFileValue;
                    }
                    else
                    {
                        wasPlainTextInFile = true;
                        return rawValueFromConfig;
                    }
                }
                return null;
            }
            catch (System.Xml.XmlException)
            {
                configFileParsed = false;
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static bool TryEncryptAndUpdateConfigFile(string configFilePath, XElement targetElement, XDocument doc, string plainTextValue, string settingName)
        {
            if (targetElement == null || doc == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ERROR: Cannot update config file. Internal error (target element or document is null).");
                Console.ResetColor();
                return false;
            }
            try
            {
                string encryptedValueForConfig = EncryptValue(plainTextValue);
                if (encryptedValueForConfig.Equals(plainTextValue) && !string.IsNullOrEmpty(plainTextValue))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"FAIL: Encryption of '{settingName}' failed. File not updated.");
                    Console.ResetColor();
                    return false;
                }


                targetElement.SetAttributeValue("value", encryptedValueForConfig);
                doc.Save(configFilePath);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"SUCCESS: Value for '{settingName}' has been encrypted and '{configFilePath}' updated.");
                Console.ResetColor();
                Console.WriteLine($"New raw value in config: '{encryptedValueForConfig}'");
                return true;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"FAIL: Could not encrypt and update the setting in '{configFilePath}'.");
                Console.ResetColor();
                Console.WriteLine($"Error: {ex.Message}");
                return false;
            }
        }
        public static void EncryptAllPlainTextSettingsInConfigFile(string configFilePath, string targetPrefix)
        {
            if (!File.Exists(configFilePath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"FAIL: Configuration file '{configFilePath}' not found.");
                Console.ResetColor();
                Program.PauseBeforeContinuing();
                return;
            }

            XDocument doc;
            try
            {
                doc = XDocument.Load(configFilePath);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"FAIL: Could not load or parse '{configFilePath}'.");
                Console.ResetColor();
                Console.WriteLine($"Error: {ex.Message}");
                Program.PauseBeforeContinuing();
                return;
            }

            XElement appSettingsNode = doc.Element("appSettings");
            if (appSettingsNode == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"FAIL: File '{configFilePath}' does not contain a root <appSettings> element.");
                Console.ResetColor();
                Program.PauseBeforeContinuing();
                return;
            }

            bool fileModified = false;
            int encryptedCount = 0;
            List<string> settingsToEncryptKeys = new List<string>();

            foreach (XElement addElement in appSettingsNode.Elements("add"))
            {
                string key = addElement.Attribute("key")?.Value;
                string currentValue = addElement.Attribute("value")?.Value;

                if (!string.IsNullOrEmpty(key) && currentValue != null && !currentValue.StartsWith(ENCRYPTED_PREFIX, StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(targetPrefix) || key.StartsWith(targetPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        settingsToEncryptKeys.Add(key);
                    }
                }
            }

            if (settingsToEncryptKeys.Count == 0)
            {
                if (!string.IsNullOrEmpty(targetPrefix))
                    Console.WriteLine($"INFO: No plain text settings found in 'setting.config' matching prefix '{targetPrefix}' that need encryption.");
                else
                    Console.WriteLine("INFO: No plain text settings found in 'setting.config' that need encryption.");
                Program.PauseBeforeContinuing();
                return;
            }

            Console.WriteLine("\nThe following plain text settings (matching criteria) will be encrypted:");
            foreach (string key in settingsToEncryptKeys)
            {
                Console.WriteLine($" - {key}");
            }
            Console.Write("\nDo you want to proceed with encrypting these values? (yes/no): ");
            string confirmation = Console.ReadLine();

            if (!confirmation.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Operation cancelled. No settings were encrypted.");
                Program.PauseBeforeContinuing();
                return;
            }

            Console.WriteLine("\nEncrypting settings...");
            foreach (XElement addElement in appSettingsNode.Elements("add"))
            {
                string key = addElement.Attribute("key")?.Value;
                string currentValue = addElement.Attribute("value")?.Value;

                if (settingsToEncryptKeys.Contains(key) && currentValue != null && !currentValue.StartsWith(ENCRYPTED_PREFIX, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"Encrypting setting '{key}' (Value: '{currentValue.Substring(0, Math.Min(currentValue.Length, 10))}...')...");
                    string encryptedValueForConfig = EncryptValue(currentValue);

                    if (!encryptedValueForConfig.StartsWith(ENCRYPTED_PREFIX) && !string.IsNullOrEmpty(currentValue))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($" - FAIL: Could not encrypt setting '{key}'. It remains plain text. (Encryption returned plain value)");
                        Console.ResetColor();
                        continue;
                    }

                    addElement.SetAttributeValue("value", encryptedValueForConfig);
                    fileModified = true;
                    encryptedCount++;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($" - SUCCESS: '{key}' encrypted.");
                    Console.ResetColor();
                }
            }

            if (fileModified)
            {
                try
                {
                    doc.Save(configFilePath);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"\nSUCCESS: '{configFilePath}' has been updated. {encryptedCount} setting(s) encrypted.");
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\nFAIL: Could not save changes to '{configFilePath}'.");
                    Console.ResetColor();
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
            else if (encryptedCount == 0 && settingsToEncryptKeys.Count > 0)
            {
                Console.WriteLine("\nINFO: No settings were successfully encrypted and saved, though some were identified and attempted.");
            }
            Program.PauseBeforeContinuing();
        }
    }
}
