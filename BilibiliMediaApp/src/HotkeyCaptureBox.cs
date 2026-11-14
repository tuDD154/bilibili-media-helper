using System;
using System.Drawing;
using System.Windows.Forms;

namespace BilibiliMediaApp
{
    public sealed class HotkeyCaptureBox : TextBox
    {
        private bool _capturing;
        private HotkeyBinding _binding;

        public event EventHandler BindingChanged;

        public HotkeyCaptureBox()
        {
            ReadOnly = true;
            ShortcutsEnabled = false;
            TextAlign = HorizontalAlignment.Center;
            BorderStyle = BorderStyle.FixedSingle;
            BackColor = Color.FromArgb(38, 42, 53);
            ForeColor = Color.FromArgb(240, 242, 247);
            Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold, GraphicsUnit.Point);
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        public HotkeyBinding Binding
        {
            get { return _binding; }
            set
            {
                _binding = value;
                UpdateDisplay();
            }
        }

        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            _capturing = true;
            BackColor = Color.FromArgb(55, 62, 80);
            Text = "按下按键…";
        }

        protected override void OnLeave(EventArgs e)
        {
            if (_capturing)
            {
                _capturing = false;
                UpdateDisplay();
            }
            BackColor = Color.FromArgb(38, 42, 53);
            base.OnLeave(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (_capturing)
            {
                return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_capturing)
            {
                if (keyData == Keys.Escape)
                {
                    _capturing = false;
                    UpdateDisplay();
                    return true;
                }
                if (keyData == Keys.Enter || keyData == (Keys.Alt | Keys.F4))
                {
                    Text = "不可用：" + (keyData == Keys.Enter ? "Enter" : "Alt+F4");
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!_capturing)
            {
                base.OnKeyDown(e);
                return;
            }

            e.Handled = true;
            e.SuppressKeyPress = true;

            if (e.KeyCode == Keys.Tab &&
                (e.Modifiers == Keys.None || e.Modifiers == Keys.Shift))
            {
                _capturing = false;
                UpdateDisplay();
                bool forward = e.Modifiers != Keys.Shift;
                if (this.Parent != null)
                {
                    this.Parent.SelectNextControl(this, forward, true, true, true);
                }
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                _capturing = false;
                UpdateDisplay();
                return;
            }

            if (e.KeyCode == Keys.Back || e.KeyCode == Keys.Delete)
            {
                _binding = null;
                _capturing = false;
                UpdateDisplay();
                RaiseBindingChanged();
                return;
            }

            if (IsModifierKey(e.KeyCode))
            {
                Text = "请再按一个主键…";
                return;
            }

            uint modifiers = 0;
            if ((e.Modifiers & Keys.Control) != 0) modifiers |= HotkeyBinding.ModControl;
            if ((e.Modifiers & Keys.Alt) != 0) modifiers |= HotkeyBinding.ModAlt;
            if ((e.Modifiers & Keys.Shift) != 0) modifiers |= HotkeyBinding.ModShift;

            var binding = new HotkeyBinding(e.KeyCode, modifiers);
            string error = binding.Validate();
            if (error != null)
            {
                Text = "不可用：" + binding.DisplayText;
                return;
            }

            _binding = binding;
            _capturing = false;
            UpdateDisplay();
            RaiseBindingChanged();
        }

        private void UpdateDisplay()
        {
            Text = _binding == null || _binding.IsEmpty ? "未设置" : _binding.DisplayText;
        }

        private void RaiseBindingChanged()
        {
            EventHandler handler = BindingChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private static bool IsModifierKey(Keys key)
        {
            return key == Keys.ControlKey || key == Keys.LControlKey || key == Keys.RControlKey ||
                   key == Keys.ShiftKey || key == Keys.LShiftKey || key == Keys.RShiftKey ||
                   key == Keys.Menu || key == Keys.LMenu || key == Keys.RMenu ||
                   key == Keys.LWin || key == Keys.RWin;
        }
    }
}
