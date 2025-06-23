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

            // Regex to find commands like SendText "text", Wait Nms, KeyPress VK_CODE, KeyDown VK_CODE, KeyUp VK_CODE
            // Example: SendText "Hello World"{ENTER} Wait 500ms KeyPress LControlKey KeyPress C KeyUp C KeyUp LControlKey
            // This is a simplified parser. A more robust one would handle escaping quotes within SendText etc.
            var regex = new Regex(@"(SendText\s*""([^""]*)""|Wait\s*(\d+)\s*ms|KeyPress\s*([A-Za-z0-9_]+)|KeyDown\s*([A-Za-z0-9_]+)|KeyUp\s*([A-Za-z0-9_]+)|SendKey\s*([A-Za-z0-9_]+(?:{[A-Z]+})*))", RegexOptions.IgnoreCase);
            var matches = regex.Matches(sequence);

            foreach (Match match in matches)
            {
                if (match.Groups[1].Value.StartsWith("SendText", StringComparison.OrdinalIgnoreCase))
                {
                    string text = match.Groups[2].Value;
                    // Replace common placeholders like {ENTER}
                    text = text.Replace("{ENTER}", "\n").Replace("{TAB}", "\t");
                    actions.Add(new MacroAction { Type = MacroActionType.SendText, Argument = text });
                }
                else if (match.Groups[1].Value.StartsWith("Wait", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(match.Groups[3].Value, out int delay))
                    {
                        actions.Add(new MacroAction { Type = MacroActionType.Wait, Argument = delay });
                    }
                }
                else if (match.Groups[1].Value.StartsWith("KeyPress", StringComparison.OrdinalIgnoreCase))
                {
                    if (Enum.TryParse<VirtualKeyCode>(match.Groups[4].Value, true, out var vk))
                    {
                        actions.Add(new MacroAction { Type = MacroActionType.KeyPress, Argument = vk });
                    }
                }
                else if (match.Groups[1].Value.StartsWith("KeyDown", StringComparison.OrdinalIgnoreCase))
                {
                     if (Enum.TryParse<VirtualKeyCode>(match.Groups[5].Value, true, out var vk))
                    {
                        actions.Add(new MacroAction { Type = MacroActionType.KeyDown, Argument = vk });
                    }
                }
                else if (match.Groups[1].Value.StartsWith("KeyUp", StringComparison.OrdinalIgnoreCase))
                {
                     if (Enum.TryParse<VirtualKeyCode>(match.Groups[6].Value, true, out var vk))
                    {
                        actions.Add(new MacroAction { Type = MacroActionType.KeyUp, Argument = vk });
                    }
                }
                 else if (match.Groups[1].Value.StartsWith("SendKey", StringComparison.OrdinalIgnoreCase))
                {
                    // This is a more direct approach for simple single key presses or text with special keys
                    // Example: SendKey A, SendKey {ENTER}, SendKey Hello{TAB}World
                    // This will be handled by InputSimulator.Keyboard.TextEntry for sequences like "Hello"
                    // and specific key presses for things like {ENTER}
                    actions.Add(new MacroAction { Type = MacroActionType.SendKeySequence, Argument = match.Groups[7].Value });
                }
            }
            return actions;
        }
    }

    public enum MacroActionType
    {
        SendText,    // Argument is string
        Wait,        // Argument is int (milliseconds)
        KeyPress,    // Argument is VirtualKeyCode
        KeyDown,     // Argument is VirtualKeyCode
        KeyUp,       // Argument is VirtualKeyCode
        SendKeySequence // Argument is string (e.g. "A", "{ENTER}", "Ctrl+C") - for more complex input sim
    }

    public struct MacroAction
    {
        public MacroActionType Type { get; set; }
        public object Argument { get; set; }
    }
}
