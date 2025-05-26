using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnviroVarUtillity
{
    public static class UserEnvironmentVariableOperations
    {
        public static void ListByPrefix(string prefixToList)
        {
            Console.Clear();
            Console.WriteLine("--- List User Environment Variables by Prefix (values will be decrypted) ---");
            if (string.IsNullOrEmpty(prefixToList))
            {
                Console.WriteLine("Environment Variable Prefix is not set or is empty. Cannot list variables.");
                return;
            }
            Console.WriteLine($"Using Environment Variable Prefix: '{prefixToList}'");

            List<KeyValuePair<string, string>> varsFoundDisplay = new List<KeyValuePair<string, string>>();
            try
            {
                using (RegistryKey envKey = Registry.CurrentUser.OpenSubKey("Environment", writable: false))
                {
                    if (envKey != null)
                    {
                        foreach (string valueName in envKey.GetValueNames())
                        {
                            if (valueName.StartsWith(prefixToList, StringComparison.OrdinalIgnoreCase))
                            {
                                string rawValue = envKey.GetValue(valueName)?.ToString();
                                SecretManagerLogic.TryDecryptValue(rawValue, out string displayValue, out bool wasEncrypted);
                                string statusTag = "";
                                if (wasEncrypted)
                                {
                                    statusTag = displayValue.Contains("DECRYPTION FAILED") ? " (encrypted, decryption FAILED)" : " (decrypted)";
                                }
                                varsFoundDisplay.Add(new KeyValuePair<string, string>(valueName, $"{displayValue}{statusTag}"));
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Could not open the User Environment registry key.");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accessing the registry: {ex.Message}");
                return;
            }

            if (varsFoundDisplay.Count == 0)
            {
                Console.WriteLine($"\nNo User environment variables found starting with prefix '{prefixToList}'.");
            }
            else
            {
                Console.WriteLine($"\nFound {varsFoundDisplay.Count} variable(s) starting with prefix '{prefixToList}':");
                foreach (var kvp in varsFoundDisplay)
                {
                    Console.WriteLine($" - {kvp.Key} = {kvp.Value}");
                }
            }
        }
        public static void AddNewWithPrefix(string formattedPrefix)
        {
            Console.Clear();
            Console.WriteLine("--- Add New User Environment Variables with Prefix (values will be encrypted) ---");
            if (string.IsNullOrEmpty(formattedPrefix))
            {
                Console.WriteLine("Environment Variable Prefix is not set or is empty. Cannot add variables.");
                return;
            }
            Console.WriteLine($"Using Environment Variable Prefix: '{formattedPrefix}'");


            while (true)
            {
                Console.Write("\nEnter the name for the new environment variable (without prefix, or type 'done' to finish): ");
                string varNameWithoutPrefix = Console.ReadLine();

                if (varNameWithoutPrefix.Equals("done", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(varNameWithoutPrefix))
                {
                    Console.WriteLine("Variable name cannot be empty. Please try again.");
                    continue;
                }

                string fullVarName = formattedPrefix + varNameWithoutPrefix;

                string rawCurrentValue = Environment.GetEnvironmentVariable(fullVarName, EnvironmentVariableTarget.User);
                if (rawCurrentValue != null)
                {
                    SecretManagerLogic.TryDecryptValue(rawCurrentValue, out string displayCurrentValue, out _);
                    Console.WriteLine($"Notice: Variable '{fullVarName}' already exists with value: '{displayCurrentValue}'. It will be overwritten.");
                }

                Console.Write($"Enter the plain text value for '{fullVarName}': ");
                string plainTextVarValue = Console.ReadLine();
                string valueToStore = SecretManagerLogic.EncryptValue(plainTextVarValue);

                if (!valueToStore.StartsWith(SecretManagerLogic.ENCRYPTED_PREFIX) && !string.IsNullOrEmpty(plainTextVarValue))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Failed to encrypt value for '{fullVarName}'. Variable not set.");
                    Console.ResetColor();
                }
                else
                {
                    try
                    {
                        Environment.SetEnvironmentVariable(fullVarName, valueToStore, EnvironmentVariableTarget.User);
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"Successfully set and encrypted User environment variable '{fullVarName}'.");
                        Console.ResetColor();
                        Console.WriteLine($"Stored as: {valueToStore.Substring(0, Math.Min(valueToStore.Length, 40))}...");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error setting environment variable '{fullVarName}': {ex.Message}");
                    }
                }
                Console.WriteLine("---");
            }
        }
        public static void RemoveByPrefix(string prefixToRemove)
        {
            Console.Clear();
            Console.WriteLine("--- Remove User Environment Variables by Prefix ---");
            if (string.IsNullOrEmpty(prefixToRemove))
            {
                Console.WriteLine("Environment Variable Prefix is not set or is empty. Cannot remove variables.");
                return;
            }
            Console.WriteLine($"Using Environment Variable Prefix: '{prefixToRemove}' for removal.");


            List<string> varsFoundNames = new List<string>();
            try
            {
                using (RegistryKey envKey = Registry.CurrentUser.OpenSubKey("Environment", writable: true))
                {
                    if (envKey != null)
                    {
                        foreach (string valueName in envKey.GetValueNames())
                        {
                            if (valueName.StartsWith(prefixToRemove, StringComparison.OrdinalIgnoreCase))
                            {
                                varsFoundNames.Add(valueName);
                                string rawValue = envKey.GetValue(valueName)?.ToString();
                                SecretManagerLogic.TryDecryptValue(rawValue, out string displayValue, out bool wasEncrypted);
                                Console.Write($" - Found: {valueName} = {displayValue}");
                                string statusTag = "";
                                if (wasEncrypted)
                                {
                                    statusTag = displayValue.Contains("DECRYPTION FAILED") ? " (encrypted, decryption FAILED)" : " (decrypted)";
                                }
                                Console.WriteLine(statusTag);
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Could not open the User Environment registry key.");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accessing the registry: {ex.Message}");
                return;
            }

            if (varsFoundNames.Count == 0)
            {
                Console.WriteLine($"\nNo User environment variables found starting with prefix '{prefixToRemove}'.");
                return;
            }

            Console.WriteLine($"\nFound {varsFoundNames.Count} variable(s) starting with prefix '{prefixToRemove}'.");
            Console.Write("Do you want to remove all listed variables? (yes/no): ");
            string confirmation = Console.ReadLine();

            if (confirmation.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                int initialAttemptSuccessCount = 0;
                int initialAttemptFailCount = 0;

                Console.WriteLine("\nAttempting to remove variables...");
                foreach (string varName in varsFoundNames)
                {
                    try
                    {
                        Environment.SetEnvironmentVariable(varName, null, EnvironmentVariableTarget.User);
                        Console.WriteLine($" - Attempted removal of: {varName}");
                        initialAttemptSuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($" - FAILED initial attempt to remove '{varName}': {ex.Message}");
                        initialAttemptFailCount++;
                    }
                }
                Console.WriteLine($"\nInitial removal attempt summary: {initialAttemptSuccessCount} initiated, {initialAttemptFailCount} failed outright.");

                Console.WriteLine("\nDouble-checking removal status...");
                int actuallyRemovedCount = 0;
                int blankedSuccessfullyCount = 0;
                List<string> persistedDespiteBlanking = new List<string>();


                foreach (string varName in varsFoundNames)
                {
                    string currentValue = Environment.GetEnvironmentVariable(varName, EnvironmentVariableTarget.User);
                    if (currentValue == null)
                    {
                        if (varsFoundNames.Contains(varName))
                            actuallyRemovedCount++;
                    }
                    else
                    {
                        SecretManagerLogic.TryDecryptValue(currentValue, out string displayValue, out _);
                        Console.WriteLine($" - Variable '{varName}' still exists (Value: '{displayValue}'). Attempting to blank its value.");
                        try
                        {
                            Environment.SetEnvironmentVariable(varName, "", EnvironmentVariableTarget.User); // Blank it out
                            string valueAfterBlanking = Environment.GetEnvironmentVariable(varName, EnvironmentVariableTarget.User);
                            if (string.IsNullOrEmpty(valueAfterBlanking))
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine($"   - Successfully blanked out '{varName}'. It is now empty.");
                                Console.ResetColor();
                                blankedSuccessfullyCount++;
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                SecretManagerLogic.TryDecryptValue(valueAfterBlanking, out displayValue, out _);
                                Console.WriteLine($"   - FAILED to blank out '{varName}'. It still has value: '{displayValue}'.");
                                Console.ResetColor();
                                persistedDespiteBlanking.Add(varName);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"   - ERROR attempting to blank out '{varName}': {ex.Message}");
                            Console.ResetColor();
                            persistedDespiteBlanking.Add(varName);
                        }
                    }
                }

                int finalActuallyRemovedCount = 0;
                foreach (string varName in varsFoundNames)
                {
                    if (Environment.GetEnvironmentVariable(varName, EnvironmentVariableTarget.User) == null)
                    {
                        finalActuallyRemovedCount++;
                    }
                }

                Console.WriteLine("\nFinal Removal Status:");
                Console.WriteLine($" - Variables confirmed fully removed: {finalActuallyRemovedCount}");
                Console.WriteLine($" - Variables blanked out (after failing initial removal): {blankedSuccessfullyCount}");
                if (persistedDespiteBlanking.Count > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($" - Variables that could NOT be removed or blanked out: {persistedDespiteBlanking.Count}");
                    foreach (var name in persistedDespiteBlanking) Console.WriteLine($"   - {name}");
                    Console.ResetColor();
                }
            }
            else
            {
                Console.WriteLine("\nOperation cancelled by the user. No variables were removed.");
            }
        }
    }
}
