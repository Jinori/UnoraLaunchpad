using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Input; // For Key and ModifierKeys
using InputSimulatorStandard.Native; // For VirtualKeyCode

namespace UnoraLaunchpad
{
    internal static class MacroParser
    {
        // Regex for parsing commands within {braces}
        // Simplified to focus on Wait, KeyPress, KeyDown, KeyUp.
        // SendText and SendKey are removed from here as the outer parser handles text-like input.
        private static readonly Regex BracedCommandRegex = new Regex(
            @"^\s*(Wait\s*(\d+)\s*ms|KeyPress\s*([A-Za-z0-9_]+)|KeyDown\s*([A-Za-z0-9_]+)|KeyUp\s*([A-Za-z0-9_]+))\s*$",
            RegexOptions.IgnoreCase);

        public static bool TryParseTriggerKey(string triggerKeyString, out uint modifiers, out uint vkCode)
        {
            modifiers = NativeMethods.MOD_NONE;
            vkCode = 0;

            if (string.IsNullOrWhiteSpace(triggerKeyString)) return false;

            var parts = triggerKeyString.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            if (!parts.Any()) return false;

            string keyPart = parts.Last().Trim();
            Key wpfKey;

            try
            {
                wpfKey = (Key)Enum.Parse(typeof(Key), keyPart, true);
            }
            catch (ArgumentException)
            {
                // Check for common names like PageDown, PageUp, etc.
                // WPF's Key enum is quite comprehensive.
                // This could be expanded if specific non-standard names are used.
                System.Diagnostics.Debug.WriteLine($"[MacroParser] Could not parse key: {keyPart}");
                return false;
            }

            vkCode = (uint)KeyInterop.VirtualKeyFromKey(wpfKey);
            if (vkCode == 0) return false;


            for (int i = 0; i < parts.Length - 1; i++)
            {
                switch (parts[i].Trim().ToUpperInvariant())
                {
                    case "CTRL":
                    case "CONTROL":
                        modifiers |= NativeMethods.MOD_CONTROL;
                        break;
                    case "SHIFT":
                        modifiers |= NativeMethods.MOD_SHIFT;
                        break;
                    case "ALT":
                        modifiers |= NativeMethods.MOD_ALT;
                        break;
                    case "WIN":
                    case "WINDOWS":
                        modifiers |= NativeMethods.MOD_WIN;
                        break;
                    // default: // Unknown modifier
                        // System.Diagnostics.Debug.WriteLine($"[MacroParser] Unknown modifier: {parts[i]}");
                        // return false;
                        // Allow unknown parts for now, maybe they are part of the key name if not split well.
                }
            }
            return true;
        }

        public static List<MacroAction> ParseActionSequence(string sequence)
        {
            var actions = new List<MacroAction>();
            if (string.IsNullOrWhiteSpace(sequence)) return actions;

            int currentIndex = 0;
            while (currentIndex < sequence.Length)
            {
                char currentChar = sequence[currentIndex];

                if (char.IsWhiteSpace(currentChar))
                {
                    currentIndex++;
                    continue; // Skip whitespace
                }

                if (currentChar == '{')
                {
                    int closingBraceIndex = sequence.IndexOf('}', currentIndex);
                    if (closingBraceIndex == -1)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MacroParser] Mismatched '{{' at index {currentIndex}. Treating rest of sequence as literal characters.");
                        // Add remaining characters as SendChar actions
                        for (int i = currentIndex; i < sequence.Length; i++)
                        {
                            if (!char.IsWhiteSpace(sequence[i])) // Avoid adding whitespace if it was part of the unterminated block start
                            {
                                actions.Add(new MacroAction { Type = MacroActionType.SendChar, Argument = sequence[i] });
                            }
                        }
                        break; // End parsing
                    }

                    string commandBlock = sequence.Substring(currentIndex + 1, closingBraceIndex - currentIndex - 1);
                    ParseAndAddBracedCommand(commandBlock, actions);
                    currentIndex = closingBraceIndex + 1;
                }
                else
                {
                    // Literal character to be pressed
                    actions.Add(new MacroAction { Type = MacroActionType.SendChar, Argument = currentChar });
                    currentIndex++;
                }
            }
            return actions;
        }

        private static void ParseAndAddBracedCommand(string commandBlock, List<MacroAction> actions)
        {
            Match match = BracedCommandRegex.Match(commandBlock);

            if (!match.Success)
            {
                System.Diagnostics.Debug.WriteLine($"[MacroParser] Unknown or malformed command in braces: {{{commandBlock}}}. Interpreting as literal text.");
                // Fallback: treat the content of unknown/malformed braces as literal characters
                foreach (char c in commandBlock)
                {
                    if (!char.IsWhiteSpace(c)) // Avoid adding whitespace from the command block itself
                    {
                        actions.Add(new MacroAction { Type = MacroActionType.SendChar, Argument = c });
                    }
                }
                return;
            }

            // Group 1 is the first part of the OR in regex, e.g., "Wait 500ms" or "KeyPress VK_A"
            // Group 2 is (\d+) for Wait
            // Group 3 is ([A-Za-z0-9_]+) for KeyPress
            // Group 4 is ([A-Za-z0-9_]+) for KeyDown
            // Group 5 is ([A-Za-z0-9_]+) for KeyUp

            string commandTypeStr = match.Groups[1].Value; // Full matched command like "Wait 500ms" or "KeyPress VK_A"

            if (commandTypeStr.StartsWith("Wait", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(match.Groups[2].Value, out int delay))
                {
                    actions.Add(new MacroAction { Type = MacroActionType.Wait, Argument = delay });
                }
            }
            else if (commandTypeStr.StartsWith("KeyPress", StringComparison.OrdinalIgnoreCase))
            {
                if (Enum.TryParse<VirtualKeyCode>(match.Groups[3].Value, true, out var vk))
                {
                    actions.Add(new MacroAction { Type = MacroActionType.KeyPress, Argument = vk });
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[MacroParser] Failed to parse VirtualKeyCode for KeyPress: {match.Groups[3].Value}");
                }
            }
            else if (commandTypeStr.StartsWith("KeyDown", StringComparison.OrdinalIgnoreCase))
            {
                 if (Enum.TryParse<VirtualKeyCode>(match.Groups[4].Value, true, out var vk))
                {
                    actions.Add(new MacroAction { Type = MacroActionType.KeyDown, Argument = vk });
                }
                 else
                 {
                     System.Diagnostics.Debug.WriteLine($"[MacroParser] Failed to parse VirtualKeyCode for KeyDown: {match.Groups[4].Value}");
                 }
            }
            else if (commandTypeStr.StartsWith("KeyUp", StringComparison.OrdinalIgnoreCase))
            {
                 if (Enum.TryParse<VirtualKeyCode>(match.Groups[5].Value, true, out var vk))
                {
                    actions.Add(new MacroAction { Type = MacroActionType.KeyUp, Argument = vk });
                }
                 else
                 {
                     System.Diagnostics.Debug.WriteLine($"[MacroParser] Failed to parse VirtualKeyCode for KeyUp: {match.Groups[5].Value}");
                 }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[MacroParser] Unhandled matched command in ParseAndAddBracedCommand: {commandTypeStr}");
            }
        }
    }

    public enum MacroActionType
    {
        SendText,        // Argument is string (Legacy, prefer SendChar for new parser output)
        Wait,            // Argument is int (milliseconds)
        KeyPress,        // Argument is VirtualKeyCode
        KeyDown,         // Argument is VirtualKeyCode
        KeyUp,           // Argument is VirtualKeyCode
        SendKeySequence, // Argument is string (Legacy, prefer SendChar/KeyPress for new parser output)
        SendChar         // Argument is char
    }

    public struct MacroAction
    {
        public MacroActionType Type { get; set; }
        public object Argument { get; set; }
    }
}
