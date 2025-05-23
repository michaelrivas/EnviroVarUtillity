using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnviroVarUtillity
{
    internal class Program
    {
        static void Main(string[] args)
        {
            while (true)
            {
                Console.Clear(); // Clear console for better menu visibility
                Console.WriteLine("--- Environment Variable Manager ---");
                Console.WriteLine("1. Remove User environment variables by prefix");
                Console.WriteLine("2. Add new User environment variables with a prefix");
                Console.WriteLine("3. List User environment variables by prefix");
                Console.WriteLine("4. Exit");
                Console.Write("Select an option (1-4): ");

                string choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        RemoveVariablesByPrefix();
                        break;
                    case "2":
                        AddNewVariablesWithPrefix();
                        break;
                    case "3":
                        ListVariablesByPrefix();
                        break;
                    case "4":
                        Console.WriteLine("Exiting application.");
                        return; // Exit the Main method, thus the application
                    default:
                        Console.WriteLine("Invalid choice. Press any key to try again.");
                        Console.ReadKey();
                        break;
                }
            }
        }

        static void ListVariablesByPrefix()
        {
            Console.Clear();
            Console.WriteLine("--- List User Environment Variables by Prefix ---");
            Console.Write("Enter the prefix of the User environment variables you want to list (e.g., MYAPP or MYAPP_): ");
            string userInputPrefix = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(userInputPrefix))
            {
                Console.WriteLine("Prefix cannot be empty.");
                PauseBeforeContinuing();
                return;
            }

            // Ensure prefix ends with an underscore for consistent searching
            string prefixToList = userInputPrefix;
            if (!userInputPrefix.EndsWith("_"))
            {
                prefixToList += "_";
            }
            Console.WriteLine($"\nListing User environment variables effectively starting with '{prefixToList}' (based on input '{userInputPrefix}')...");


            List<KeyValuePair<string, string>> varsFound = new List<KeyValuePair<string, string>>();
            try
            {
                // Reading directly from registry gives the current persistent state.
                using (RegistryKey envKey = Registry.CurrentUser.OpenSubKey("Environment", writable: false)) // writable: false is fine for reading
                {
                    if (envKey != null)
                    {
                        foreach (string valueName in envKey.GetValueNames())
                        {
                            if (valueName.StartsWith(prefixToList, StringComparison.OrdinalIgnoreCase))
                            {
                                // Get the value of the environment variable
                                string varValue = envKey.GetValue(valueName)?.ToString();
                                varsFound.Add(new KeyValuePair<string, string>(valueName, varValue ?? "<null or empty>"));
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Could not open the User Environment registry key.");
                        PauseBeforeContinuing();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accessing the registry: {ex.Message}");
                PauseBeforeContinuing();
                return;
            }

            if (varsFound.Count == 0)
            {
                Console.WriteLine($"\nNo User environment variables found effectively starting with '{prefixToList}'.");
            }
            else
            {
                Console.WriteLine($"\nFound {varsFound.Count} variable(s) effectively matching prefix '{prefixToList}':");
                foreach (var kvp in varsFound)
                {
                    Console.WriteLine($" - {kvp.Key} = {kvp.Value}");
                }
            }
            PauseBeforeContinuing();
        }


        static void AddNewVariablesWithPrefix()
        {
            Console.Clear();
            Console.WriteLine("--- Add New User Environment Variables with Prefix ---");

            Console.Write("Enter the prefix for the new environment variables (e.g., MYAPP or MYAPP_): ");
            string prefix = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(prefix))
            {
                Console.WriteLine("Prefix cannot be empty.");
                PauseBeforeContinuing();
                return;
            }

            // Ensure prefix ends with an underscore if it doesn't already, for consistent naming
            string formattedPrefix = prefix;
            if (!prefix.EndsWith("_"))
            {
                formattedPrefix += "_";
            }


            while (true)
            {
                Console.WriteLine($"\nCurrent prefix being used: '{formattedPrefix}' (Original input: '{prefix}')");
                Console.Write("Enter the name for the new environment variable (without prefix, or type 'done' to finish): ");
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

                // Check if variable already exists
                string currentValue = Environment.GetEnvironmentVariable(fullVarName, EnvironmentVariableTarget.User);
                if (currentValue != null)
                {
                    Console.WriteLine($"Notice: Variable '{fullVarName}' already exists with value: '{currentValue}'. It will be overwritten.");
                }

                Console.Write($"Enter the value for '{fullVarName}': ");
                string varValue = Console.ReadLine();

                try
                {
                    Environment.SetEnvironmentVariable(fullVarName, varValue, EnvironmentVariableTarget.User);
                    Console.WriteLine($"Successfully set User environment variable '{fullVarName}' to '{varValue}'.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error setting environment variable '{fullVarName}': {ex.Message}");
                }
                Console.WriteLine("---"); // Separator for adding next variable
            }

            NotifyUserAboutChanges();
            PauseBeforeContinuing();
        }


        static void RemoveVariablesByPrefix()
        {
            Console.Clear();
            Console.WriteLine("--- Remove User Environment Variables by Prefix ---");
            Console.Write("Enter the prefix of the User environment variables you want to remove (e.g., MYAPP or MYAPP_): ");
            string userInputPrefix = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(userInputPrefix))
            {
                Console.WriteLine("Prefix cannot be empty.");
                PauseBeforeContinuing();
                return;
            }

            // Ensure prefix ends with an underscore for consistent searching
            string prefixToRemove = userInputPrefix;
            if (!userInputPrefix.EndsWith("_"))
            {
                prefixToRemove += "_";
            }
            Console.WriteLine($"\nSearching for User environment variables effectively starting with '{prefixToRemove}' (based on input '{userInputPrefix}')...");


            List<string> varsFound = new List<string>();
            try
            {
                // Reading directly from registry gives the current persistent state.
                using (RegistryKey envKey = Registry.CurrentUser.OpenSubKey("Environment", writable: true))
                {
                    if (envKey != null)
                    {
                        foreach (string valueName in envKey.GetValueNames())
                        {
                            if (valueName.StartsWith(prefixToRemove, StringComparison.OrdinalIgnoreCase))
                            {
                                varsFound.Add(valueName);
                                Console.WriteLine($" - Found: {valueName}");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Could not open the User Environment registry key.");
                        PauseBeforeContinuing();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accessing the registry: {ex.Message}");
                PauseBeforeContinuing();
                return;
            }

            if (varsFound.Count == 0)
            {
                Console.WriteLine($"\nNo User environment variables found effectively starting with '{prefixToRemove}'.");
                PauseBeforeContinuing();
                return;
            }

            Console.WriteLine($"\nFound {varsFound.Count} variable(s) effectively matching prefix '{prefixToRemove}'.");
            Console.Write("Do you want to remove all listed variables? (yes/no): ");
            string confirmation = Console.ReadLine();

            if (confirmation.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                int successCount = 0;
                int failCount = 0;
                List<string> attemptedToRemove = new List<string>(varsFound);

                Console.WriteLine("\nAttempting to remove variables...");
                foreach (string varName in attemptedToRemove)
                {
                    try
                    {
                        Environment.SetEnvironmentVariable(varName, null, EnvironmentVariableTarget.User);
                        Console.WriteLine($" - Attempted removal of: {varName}");
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($" - FAILED initial attempt to remove '{varName}': {ex.Message}");
                        failCount++;
                    }
                }
                Console.WriteLine($"\nInitial removal attempt summary: {successCount} initiated, {failCount} failed outright.");

                Console.WriteLine("\nDouble-checking removal status...");
                int actuallyRemovedCount = 0;
                int blankedCount = 0;
                List<string> notFullyRemoved = new List<string>();

                foreach (string varName in attemptedToRemove)
                {
                    string currentValue = Environment.GetEnvironmentVariable(varName, EnvironmentVariableTarget.User);
                    if (currentValue == null)
                    {
                        actuallyRemovedCount++;
                    }
                    else
                    {
                        notFullyRemoved.Add(varName);
                        Console.WriteLine($" - Variable '{varName}' still exists (Value: '{currentValue}'). Attempting to blank its value.");
                        try
                        {
                            Environment.SetEnvironmentVariable(varName, "", EnvironmentVariableTarget.User);
                            string blankedValue = Environment.GetEnvironmentVariable(varName, EnvironmentVariableTarget.User);
                            if (string.IsNullOrEmpty(blankedValue))
                            {
                                Console.WriteLine($"   - Successfully blanked out '{varName}'.");
                                blankedCount++;
                            }
                            else
                            {
                                Console.WriteLine($"   - FAILED to blank out '{varName}'. It still has a value: '{blankedValue}'");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"   - FAILED to blank out '{varName}': {ex.Message}");
                        }
                    }
                }

                Console.WriteLine("\nFinal Removal Status:");
                Console.WriteLine($" - Variables confirmed fully removed: {actuallyRemovedCount}");
                Console.WriteLine($" - Variables blanked out (could not be fully removed): {blankedCount}");
                if (notFullyRemoved.Count - blankedCount > 0)
                {
                    Console.WriteLine($" - Variables that could not be removed or blanked: {notFullyRemoved.Count - blankedCount}");
                }

            }
            else
            {
                Console.WriteLine("\nOperation cancelled by the user. No variables were removed.");
            }
            NotifyUserAboutChanges();
            PauseBeforeContinuing();
        }

        static void NotifyUserAboutChanges()
        {
            Console.WriteLine("\nIMPORTANT: For changes to be fully reflected in all applications and open shells,");
            Console.WriteLine("you may need to log out and log back in, or restart your machine.");
        }

        static void PauseBeforeContinuing()
        {
            Console.WriteLine("\nPress any key to return to the menu...");
            Console.ReadKey();
        }



    }
}
