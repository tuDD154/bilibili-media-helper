using System;
using System.Drawing;
using System.Windows.Forms;

namespace BilibiliMediaApp
{
    public sealed class SettingsDialog : Form
    {
        private readonly Func<
            HotkeyBinding, HotkeyBinding, HotkeyBinding, HotkeyApplyResult> _checkHotkeys;
        private readonly Func<
            HotkeyBinding, HotkeyBinding, HotkeyBinding, HotkeyApplyResult> _applyHotkeys;

        private NumericUpDown _step;
        private CheckBox _topMost;
        private HotkeyCaptureBox _toggleBox;
        private HotkeyCaptureBox _backBox;
        private HotkeyCaptureBox _forwardBox;
        private Label _checkStatus;

        public int SeekSeconds { get; private set; }
        public bool TopMostWindow { get; private set; }
        public HotkeyBinding ToggleBinding { get; private set; }
        public HotkeyBinding BackBinding { get; private set; }
        public HotkeyBinding ForwardBinding { get; private set; }

        public SettingsDialog(
            AppSettings settings,
            HotkeyBinding toggle,
            HotkeyBinding back,
            HotkeyBinding forward,
            Func<HotkeyBinding, HotkeyBinding, HotkeyBinding, HotkeyApplyResult> checkHotkeys,
            Func<HotkeyBinding, HotkeyBinding, HotkeyBinding, HotkeyApplyResult> applyHotkeys)
        {
            _checkHotkeys = checkHotkeys;
            _applyHotkeys = applyHotkeys;
            SeekSeconds = settings.SeekSeconds;
            TopMostWindow = settings.TopMost;
            ToggleBinding = toggle;
            BackBinding = back;
            ForwardBinding = forward;

            this.Text = "设置";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.ClientSize = new Size(540, 535);
            this.BackColor = Color.FromArgb(24, 27, 34);
            this.ForeColor = Color.FromArgb(232, 234, 240);
            this.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular, GraphicsUnit.Point);

            var title = new Label();
            title.Text = "播放与快捷键设置";
            title.Font = new Font("Microsoft YaHei UI", 14f, FontStyle.Bold, GraphicsUnit.Point);
            title.ForeColor = Color.FromArgb(244, 246, 250);
            title.Location = new Point(22, 18);
            title.Size = new Size(400, 30);

            var subtitle = new Label();
            subtitle.Text = "调整步长、窗口置顶，并自定义全局快捷键。";
            subtitle.ForeColor = Color.FromArgb(150, 157, 172);
            subtitle.Location = new Point(24, 50);
            subtitle.Size = new Size(480, 24);

            var stepLabel = new Label();
            stepLabel.Text = "快进 / 快退步长";
            stepLabel.ForeColor = Color.FromArgb(220, 224, 235);
            stepLabel.Location = new Point(24, 92);
            stepLabel.Size = new Size(180, 24);

            _step = new NumericUpDown();
            _step.Minimum = 1;
            _step.Maximum = 30;
            _step.Value = Math.Max(1, Math.Min(30, settings.SeekSeconds));
            _step.Width = 110;
            _step.Height = 30;
            _step.Location = new Point(296, 88);
            _step.BackColor = Color.FromArgb(38, 42, 53);
            _step.ForeColor = Color.FromArgb(240, 242, 247);
            _step.BorderStyle = BorderStyle.FixedSingle;
            _step.TextAlign = HorizontalAlignment.Center;
            _step.Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Regular, GraphicsUnit.Point);

            var secondLabel = new Label();
            secondLabel.Text = "秒";
            secondLabel.ForeColor = Color.FromArgb(150, 157, 172);
            secondLabel.Location = new Point(414, 94);
            secondLabel.Size = new Size(30, 22);

            _topMost = new CheckBox();
            _topMost.Text = "窗口置顶（适合放在副屏最上层）";
            _topMost.Checked = settings.TopMost;
            _topMost.ForeColor = Color.FromArgb(220, 224, 235);
            _topMost.Location = new Point(24, 126);
            _topMost.Size = new Size(430, 26);
            _topMost.FlatStyle = FlatStyle.Flat;

            var divider = new Panel();
            divider.Location = new Point(24, 160);
            divider.Size = new Size(492, 1);
            divider.BackColor = Color.FromArgb(52, 58, 72);

            var shortcutTitle = new Label();
            shortcutTitle.Text = "全局快捷键（支持单键）";
            shortcutTitle.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold, GraphicsUnit.Point);
            shortcutTitle.ForeColor = Color.FromArgb(244, 246, 250);
            shortcutTitle.Location = new Point(24, 172);
            shortcutTitle.Size = new Size(300, 24);

            var shortcutHint = new Label();
            shortcutHint.Text = "点击右侧按键框，然后按下想要的键；按 Backspace/Delete 清除。";
            shortcutHint.ForeColor = Color.FromArgb(140, 147, 162);
            shortcutHint.Location = new Point(24, 198);
            shortcutHint.Size = new Size(492, 20);

            _toggleBox = CreateHotkeyBox(toggle, 222);
            _backBox = CreateHotkeyBox(back, 260);
            _forwardBox = CreateHotkeyBox(forward, 298);

            var toggleLabel = CreateRowLabel("播放 / 暂停", 227);
            var backLabel = CreateRowLabel("快退", 265);
            var forwardLabel = CreateRowLabel("快进", 303);

            Button checkButton = CreateDialogButton("检测冲突", false);
            checkButton.Location = new Point(296, 342);
            checkButton.Width = 106;
            checkButton.Click += delegate { CheckForConflicts(); };

            Button resetButton = CreateDialogButton("恢复默认", false);
            resetButton.Location = new Point(410, 342);
            resetButton.Width = 106;
            resetButton.Click += delegate { ResetDefaults(); };

            var statusPanel = new Panel();
            statusPanel.Location = new Point(24, 384);
            statusPanel.Size = new Size(492, 52);
            statusPanel.BackColor = Color.FromArgb(31, 35, 45);

            _checkStatus = new Label();
            _checkStatus.Dock = DockStyle.Fill;
            _checkStatus.TextAlign = ContentAlignment.MiddleLeft;
            _checkStatus.Padding = new Padding(10, 0, 10, 0);
            _checkStatus.ForeColor = Color.FromArgb(150, 157, 172);
            _checkStatus.Text = "修改后点“检测冲突”，检查快捷键是否被占用。";
            _checkStatus.AutoEllipsis = true;
            statusPanel.Controls.Add(_checkStatus);

            var warning = new Label();
            warning.Text = "单键会被全局占用，游戏也收不到；原神默认已用 F1–F5、F7。\n" +
                           "建议用 F8–F10；若按下无反应，说明可能被其他程序占用。";
            warning.ForeColor = Color.FromArgb(230, 174, 92);
            warning.Location = new Point(24, 442);
            warning.Size = new Size(492, 40);

            Button save = CreateDialogButton("保存", true);
            save.Location = new Point(318, 486);
            save.Width = 88;
            save.Click += delegate { SaveSettings(); };

            Button cancel = CreateDialogButton("取消", false);
            cancel.Location = new Point(414, 486);
            cancel.Width = 88;
            cancel.DialogResult = DialogResult.Cancel;

            this.Controls.Add(title);
            this.Controls.Add(subtitle);
            this.Controls.Add(stepLabel);
            this.Controls.Add(_step);
            this.Controls.Add(secondLabel);
            this.Controls.Add(_topMost);
            this.Controls.Add(divider);
            this.Controls.Add(shortcutTitle);
            this.Controls.Add(shortcutHint);
            this.Controls.Add(toggleLabel);
            this.Controls.Add(backLabel);
            this.Controls.Add(forwardLabel);
            this.Controls.Add(_toggleBox);
            this.Controls.Add(_backBox);
            this.Controls.Add(_forwardBox);
            this.Controls.Add(checkButton);
            this.Controls.Add(resetButton);
            this.Controls.Add(statusPanel);
            this.Controls.Add(warning);
            this.Controls.Add(save);
            this.Controls.Add(cancel);

            this.AcceptButton = save;
            this.CancelButton = cancel;
            this.Shown += delegate { _step.Focus(); };
        }

        private HotkeyCaptureBox CreateHotkeyBox(HotkeyBinding binding, int top)
        {
            var box = new HotkeyCaptureBox();
            box.Binding = binding;
            box.Location = new Point(296, top);
            box.Width = 220;
            box.Height = 30;
            box.TabIndex = 10 + (top / 38);
            box.BindingChanged += delegate
            {
                _checkStatus.ForeColor = Color.FromArgb(150, 157, 172);
                _checkStatus.Text = "快捷键已修改，点“检测冲突”检查是否被占用。";
            };
            return box;
        }

        private static Label CreateRowLabel(string text, int top)
        {
            var label = new Label();
            label.Text = text;
            label.ForeColor = Color.FromArgb(220, 224, 235);
            label.Location = new Point(24, top);
            label.Size = new Size(240, 24);
            return label;
        }

        private void CheckForConflicts()
        {
            HotkeyBinding toggle;
            HotkeyBinding back;
            HotkeyBinding forward;
            if (!TryReadBindings(out toggle, out back, out forward))
            {
                return;
            }

            HotkeyApplyResult result = _checkHotkeys(toggle, back, forward);
            ShowCheckResult(result);
        }

        private void SaveSettings()
        {
            HotkeyBinding toggle;
            HotkeyBinding back;
            HotkeyBinding forward;
            if (!TryReadBindings(out toggle, out back, out forward))
            {
                return;
            }

            HotkeyApplyResult result = _applyHotkeys(toggle, back, forward);
            if (!result.Ok)
            {
                ShowCheckResult(result);
                return;
            }

            ToggleBinding = toggle;
            BackBinding = back;
            ForwardBinding = forward;
            SeekSeconds = (int)_step.Value;
            TopMostWindow = _topMost.Checked;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private bool TryReadBindings(
            out HotkeyBinding toggle,
            out HotkeyBinding back,
            out HotkeyBinding forward)
        {
            toggle = _toggleBox.Binding;
            back = _backBox.Binding;
            forward = _forwardBox.Binding;

            if (toggle == null || back == null || forward == null)
            {
                ShowCheckResult(HotkeyApplyResult.Failure("请为三个动作都设置快捷键"));
                return false;
            }

            string error = toggle.Validate();
            if (error != null)
            {
                ShowCheckResult(HotkeyApplyResult.Failure("播放/暂停：" + error));
                return false;
            }
            error = back.Validate();
            if (error != null)
            {
                ShowCheckResult(HotkeyApplyResult.Failure("快退：" + error));
                return false;
            }
            error = forward.Validate();
            if (error != null)
            {
                ShowCheckResult(HotkeyApplyResult.Failure("快进：" + error));
                return false;
            }

            if (toggle.SameKey(back) || toggle.SameKey(forward) || back.SameKey(forward))
            {
                ShowCheckResult(HotkeyApplyResult.Failure("三个动作不能使用同一个快捷键"));
                return false;
            }
            return true;
        }

        private void ShowCheckResult(HotkeyApplyResult result)
        {
            _checkStatus.ForeColor = result.Ok
                ? Color.FromArgb(120, 214, 160)
                : Color.FromArgb(255, 130, 130);
            _checkStatus.Text = result.Message +
                (string.IsNullOrEmpty(result.Details) ? string.Empty : "\n" + result.Details);
        }

        private void ResetDefaults()
        {
            _toggleBox.Binding = HotkeyBinding.DefaultToggle;
            _backBox.Binding = HotkeyBinding.DefaultBack;
            _forwardBox.Binding = HotkeyBinding.DefaultForward;
            _checkStatus.ForeColor = Color.FromArgb(150, 157, 172);
            _checkStatus.Text = "已恢复默认单键：F8 / F9 / F10，点“检测冲突”或“保存”。";
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int enabled = 1;
                NativeMethods.DwmSetWindowAttribute(
                    this.Handle,
                    NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE,
                    ref enabled,
                    sizeof(int));
                int caption = ColorTranslator.ToWin32(Color.FromArgb(24, 27, 34));
                NativeMethods.DwmSetWindowAttribute(
                    this.Handle,
                    NativeMethods.DWMWA_CAPTION_COLOR,
                    ref caption,
                    sizeof(int));
                int text = ColorTranslator.ToWin32(Color.FromArgb(240, 242, 247));
                NativeMethods.DwmSetWindowAttribute(
                    this.Handle,
                    NativeMethods.DWMWA_TEXT_COLOR,
                    ref text,
                    sizeof(int));
            }
            catch
            {
            }
        }

        private static Button CreateDialogButton(string text, bool primary)
        {
            var button = new Button();
            button.Text = text;
            button.Width = 88;
            button.Height = 32;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = primary
                ? Color.FromArgb(251, 114, 153)
                : Color.FromArgb(48, 53, 66);
            button.ForeColor = Color.White;
            button.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold, GraphicsUnit.Point);
            button.Cursor = Cursors.Hand;
            button.FlatAppearance.MouseOverBackColor = primary
                ? Color.FromArgb(255, 135, 168)
                : Color.FromArgb(64, 70, 86);
            return button;
        }
    }
}
