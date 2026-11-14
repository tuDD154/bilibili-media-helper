using System;
using System.Text;
using System.Windows.Forms;

namespace BilibiliMediaApp
{
    public sealed class HotkeyBinding
    {
        public const uint ModAlt = 0x0001;
        public const uint ModControl = 0x0002;
        public const uint ModShift = 0x0004;

        public Keys Key { get; private set; }
        public uint Modifiers { get; private set; }

        public HotkeyBinding(Keys key, uint modifiers)
        {
            Key = key;
            Modifiers = modifiers & (ModAlt | ModControl | ModShift);
        }

        public bool IsEmpty
        {
            get { return Key == Keys.None; }
        }

        public bool IsSingleKey
        {
            get { return Modifiers == 0; }
        }

        public static HotkeyBinding DefaultToggle
        {
            get { return new HotkeyBinding(Keys.F8, 0); }
        }

        public static HotkeyBinding DefaultBack
        {
            get { return new HotkeyBinding(Keys.F9, 0); }
        }

        public static HotkeyBinding DefaultForward
        {
            get { return new HotkeyBinding(Keys.F10, 0); }
        }

        public static HotkeyBinding FromStorage(string value, HotkeyBinding fallback)
        {
            HotkeyBinding parsed;
            if (TryParse(value, out parsed) && parsed.Validate() == null)
            {
                return parsed;
            }
            return fallback;
        }

        public string ToStorageString()
        {
            var builder = new StringBuilder();
            if ((Modifiers & ModControl) != 0) builder.Append("Ctrl+");
            if ((Modifiers & ModAlt) != 0) builder.Append("Alt+");
            if ((Modifiers & ModShift) != 0) builder.Append("Shift+");
            builder.Append(Key.ToString());
            return builder.ToString();
        }

        public string DisplayText
        {
            get
            {
                var builder = new StringBuilder();
                if ((Modifiers & ModControl) != 0) builder.Append("Ctrl+");
                if ((Modifiers & ModAlt) != 0) builder.Append("Alt+");
                if ((Modifiers & ModShift) != 0) builder.Append("Shift+");
                builder.Append(GetKeyDisplay(Key));
                return builder.ToString();
            }
        }

        public string Validate()
        {
            if (Key == Keys.None)
            {
                return "请设置一个按键";
            }
            if (IsModifierKey(Key))
            {
                return "只按 Ctrl/Alt/Shift 不合法，请再按一个主键";
            }

            if (IsSingleKey)
            {
                if (!IsSafeSingleKey(Key))
                {
                    return "单键只支持 F1–F24、字母、数字或小键盘数字；其他键请搭配 Ctrl/Alt/Shift";
                }
                return null;
            }

            if ((Modifiers & ModAlt) != 0 && Key == Keys.F4)
            {
                return "Alt+F4 是系统关闭窗口快捷键";
            }
            if ((Modifiers & ModAlt) != 0 && Key == Keys.Tab)
            {
                return "Alt+Tab 是系统切换窗口快捷键";
            }
            if ((Modifiers & ModControl) != 0 && Key == Keys.Escape)
            {
                return "Ctrl+Esc 是系统开始菜单快捷键";
            }
            if ((Modifiers & (ModControl | ModShift)) == (ModControl | ModShift) && Key == Keys.Escape)
            {
                return "Ctrl+Shift+Esc 是系统任务管理器快捷键";
            }
            if ((Modifiers & (ModControl | ModAlt)) == (ModControl | ModAlt) && Key == Keys.Delete)
            {
                return "Ctrl+Alt+Delete 是系统安全快捷键";
            }
            return null;
        }

        public string Warning
        {
            get
            {
                if (!IsSingleKey)
                {
                    return string.Empty;
                }
                if (IsLetterOrDigitKey(Key))
                {
                    return "单键字母/数字会被应用全局占用，游戏将收不到这个键，可能和游戏操作冲突。建议改用 F8–F10。";
                }
                return "单键 " + GetKeyDisplay(Key) + " 会被应用全局占用，请确认游戏没有使用这个键。";
            }
        }

        public bool SameKey(HotkeyBinding other)
        {
            return other != null && Key == other.Key && Modifiers == other.Modifiers;
        }

        public static bool TryParse(string value, out HotkeyBinding binding)
        {
            binding = null;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            string[] parts = value.Split('+');
            if (parts.Length == 0)
            {
                return false;
            }

            Keys key;
            if (!TryParseKey(parts[parts.Length - 1].Trim(), out key))
            {
                return false;
            }

            uint modifiers = 0;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                string token = parts[i].Trim().ToLowerInvariant();
                if (token == "ctrl" || token == "control")
                {
                    modifiers |= ModControl;
                }
                else if (token == "alt")
                {
                    modifiers |= ModAlt;
                }
                else if (token == "shift")
                {
                    modifiers |= ModShift;
                }
                else
                {
                    return false;
                }
            }

            binding = new HotkeyBinding(key, modifiers);
            return true;
        }

        private static bool TryParseKey(string token, out Keys key)
        {
            key = Keys.None;
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            if (token.Length == 1 && token[0] >= '0' && token[0] <= '9')
            {
                key = (Keys)((int)Keys.D0 + (token[0] - '0'));
                return true;
            }

            try
            {
                key = (Keys)Enum.Parse(typeof(Keys), token, true);
                return key != Keys.None && !IsModifierKey(key);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsSafeSingleKey(Keys key)
        {
            int code = (int)key;
            if (code >= (int)Keys.F1 && code <= (int)Keys.F24) return true;
            if (code >= (int)Keys.A && code <= (int)Keys.Z) return true;
            if (code >= (int)Keys.D0 && code <= (int)Keys.D9) return true;
            if (code >= (int)Keys.NumPad0 && code <= (int)Keys.NumPad9) return true;
            return false;
        }

        private static bool IsLetterOrDigitKey(Keys key)
        {
            int code = (int)key;
            if (code >= (int)Keys.A && code <= (int)Keys.Z) return true;
            if (code >= (int)Keys.D0 && code <= (int)Keys.D9) return true;
            if (code >= (int)Keys.NumPad0 && code <= (int)Keys.NumPad9) return true;
            return false;
        }

        private static bool IsModifierKey(Keys key)
        {
            return key == Keys.ControlKey || key == Keys.LControlKey || key == Keys.RControlKey ||
                   key == Keys.ShiftKey || key == Keys.LShiftKey || key == Keys.RShiftKey ||
                   key == Keys.Menu || key == Keys.LMenu || key == Keys.RMenu ||
                   key == Keys.LWin || key == Keys.RWin;
        }

        private static string GetKeyDisplay(Keys key)
        {
            int code = (int)key;
            if (code >= (int)Keys.D0 && code <= (int)Keys.D9)
            {
                return ((char)('0' + (code - (int)Keys.D0))).ToString();
            }
            if (code >= (int)Keys.NumPad0 && code <= (int)Keys.NumPad9)
            {
                return "Num " + (code - (int)Keys.NumPad0).ToString();
            }
            if (code >= (int)Keys.A && code <= (int)Keys.Z)
            {
                return ((char)('A' + (code - (int)Keys.A))).ToString();
            }
            if (code >= (int)Keys.F1 && code <= (int)Keys.F24)
            {
                return "F" + (code - (int)Keys.F1 + 1).ToString();
            }

            switch (key)
            {
                case Keys.Up: return "↑";
                case Keys.Down: return "↓";
                case Keys.Left: return "←";
                case Keys.Right: return "→";
                case Keys.Space: return "Space";
                case Keys.Enter: return "Enter";
                case Keys.Escape: return "Esc";
                case Keys.Tab: return "Tab";
                case Keys.Back: return "Backspace";
                case Keys.Delete: return "Delete";
                case Keys.Insert: return "Insert";
                case Keys.Home: return "Home";
                case Keys.End: return "End";
                case Keys.PageUp: return "PageUp";
                case Keys.PageDown: return "PageDown";
                case Keys.OemMinus: return "-";
                case Keys.Oemplus: return "=";
                case Keys.OemOpenBrackets: return "[";
                case Keys.OemCloseBrackets: return "]";
                case Keys.OemSemicolon: return ";";
                case Keys.OemQuotes: return "'";
                case Keys.Oemcomma: return ",";
                case Keys.OemPeriod: return ".";
                case Keys.OemQuestion: return "/";
                case Keys.OemPipe: return "\\";
                case Keys.Oemtilde: return "`";
                default: return key.ToString();
            }
        }
    }

    public sealed class HotkeyApplyResult
    {
        public bool Ok { get; private set; }
        public string Message { get; private set; }
        public string Details { get; private set; }

        public HotkeyApplyResult(bool ok, string message, string details)
        {
            Ok = ok;
            Message = message;
            Details = details;
        }

        public static HotkeyApplyResult Success(string message)
        {
            return new HotkeyApplyResult(true, message, string.Empty);
        }

        public static HotkeyApplyResult Success(string message, string details)
        {
            return new HotkeyApplyResult(true, message, details);
        }

        public static HotkeyApplyResult Failure(string message)
        {
            return new HotkeyApplyResult(false, message, string.Empty);
        }

        public static HotkeyApplyResult Failure(string message, string details)
        {
            return new HotkeyApplyResult(false, message, details);
        }
    }
}
