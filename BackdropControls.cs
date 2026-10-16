using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Backdrop;

internal static class BackdropPalette
{
    public static readonly Color Window = Color.FromArgb(24, 25, 28);
    public static readonly Color Surface = Color.FromArgb(32, 33, 37);
    public static readonly Color Field = Color.FromArgb(43, 44, 49);
    public static readonly Color Border = Color.FromArgb(57, 59, 65);
    public static readonly Color Text = Color.FromArgb(235, 236, 239);
    public static readonly Color Muted = Color.FromArgb(158, 160, 167);
    public static readonly Color Preview = Color.FromArgb(18, 19, 22);
    public static readonly Color Accent = Color.FromArgb(231, 232, 235);
    public static readonly Color AccentText = Color.FromArgb(25, 26, 29);
    public static readonly Color Selection = Color.FromArgb(68, 70, 77);
    public static readonly Color Focus = Color.FromArgb(190, 193, 201);
}

internal static class BackdropControlPaint
{
    public static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void DrawChevron(Graphics graphics, Rectangle bounds, bool up, Color color)
    {
        var centerX = bounds.Left + bounds.Width / 2f;
        var centerY = bounds.Top + bounds.Height / 2f;
        var direction = up ? 1 : -1;
        using var pen = new Pen(color, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        graphics.DrawLine(pen, centerX - 4, centerY + direction, centerX, centerY - direction * 3);
        graphics.DrawLine(pen, centerX, centerY - direction * 3, centerX + 4, centerY + direction);
    }

    public static void DrawHorizontalChevron(Graphics graphics, Rectangle bounds, bool right, Color color)
    {
        var centerX = bounds.Left + bounds.Width / 2f;
        var centerY = bounds.Top + bounds.Height / 2f;
        var scale = Math.Max(1f, graphics.DpiX / 96f);
        var span = Math.Min(9f * scale, Math.Min(bounds.Width - 2f * scale, bounds.Height - 2f * scale));
        var direction = right ? 1 : -1;
        using var pen = new Pen(color, 1.8f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        graphics.DrawLine(pen, centerX - direction * span / 2, centerY - span / 2,
            centerX + direction * span / 2, centerY);
        graphics.DrawLine(pen, centerX + direction * span / 2, centerY,
            centerX - direction * span / 2, centerY + span / 2);
    }
}

internal sealed class SmallBusySpinner : Control
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 75 };
    private int _angle;

    public SmallBusySpinner()
    {
        Size = new Size(16, 16);
        AccessibleName = "Preview updating";
        AccessibleDescription = "The composition preview is being updated.";
        AccessibleRole = AccessibleRole.ProgressBar;
        TabStop = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        _timer.Tick += (_, _) =>
        {
            _angle = (_angle + 30) % 360;
            Invalidate();
        };
    }

    public void StartSpinning()
    {
        Visible = true;
        if (!_timer.Enabled)
            _timer.Start();
    }

    public void StopSpinning()
    {
        _timer.Stop();
        Visible = false;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(BackdropPalette.Muted, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        e.Graphics.DrawArc(pen, RectangleF.Inflate(ClientRectangle, -3, -3), _angle, 245);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _timer.Dispose();
        base.Dispose(disposing);
    }
}

internal sealed class BackdropButton : Button
{
    private readonly bool _primary;
    private bool _hovered;
    private bool _pressed;

    public BackdropButton(string text, bool primary = false)
    {
        _primary = primary;
        Text = text;
        AutoSize = false;
        Height = 32;
        FlatStyle = FlatStyle.Flat;
        UseVisualStyleBackColor = false;
        AccessibleRole = AccessibleRole.PushButton;
        ForeColor = primary ? BackdropPalette.AccentText : BackdropPalette.Text;
        BackColor = primary ? BackdropPalette.Accent : BackdropPalette.Field;
        Cursor = Cursors.Hand;
        Font = BackdropTypography.Create(9.25F, primary ? FontStyle.Bold : FontStyle.Regular);
        FlatAppearance.BorderSize = 0;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Parent?.BackColor ?? BackdropPalette.Window);

        var bounds = RectangleF.Inflate(ClientRectangle, -1, -1);
        using var path = BackdropControlPaint.RoundedRectangle(bounds, DeviceDpi / 12f);
        using var fill = new SolidBrush(GetFillColor());
        graphics.FillPath(fill, path);

        if (!_primary)
        {
            using var border = new Pen(Enabled ? BackdropPalette.Border : BackdropPalette.Surface);
            graphics.DrawPath(border, path);
        }

        var textColor = Enabled ? (_primary ? BackdropPalette.AccentText : BackdropPalette.Text) : BackdropPalette.Muted;
        var compactText = Text.Length == 1;
        var textBounds = Rectangle.Inflate(ClientRectangle, compactText ? -3 : -8, -3);
        var textFlags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
        if (compactText)
            textFlags |= TextFormatFlags.NoPadding;
        if (Text is "‹" or "›")
            BackdropControlPaint.DrawHorizontalChevron(graphics, textBounds, right: Text == "›", textColor);
        else
            TextRenderer.DrawText(graphics, Text, Font, textBounds, textColor, textFlags);

        if (Focused && ShowFocusCues)
        {
            using var focusPath = BackdropControlPaint.RoundedRectangle(RectangleF.Inflate(bounds, -2, -2), DeviceDpi / 20f);
            using var focusPen = new Pen(BackdropPalette.Focus, 1) { DashStyle = DashStyle.Dot };
            graphics.DrawPath(focusPen, focusPath);
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
            _pressed = true;
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            _pressed = true;
            Invalidate();
        }
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            _pressed = false;
            Invalidate();
        }
        base.OnKeyUp(e);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        Invalidate();
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnLostFocus(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }

    private Color GetFillColor()
    {
        if (!Enabled)
            return BackdropPalette.Surface;
        if (_pressed)
            return _primary ? Color.FromArgb(207, 208, 211) : Color.FromArgb(64, 65, 72);
        if (_hovered)
            return _primary ? Color.White : Color.FromArgb(54, 55, 61);
        return _primary ? BackdropPalette.Accent : BackdropPalette.Field;
    }
}

internal sealed class BackdropChevronButton : Button
{
    private readonly bool _right;
    private bool _hovered;
    private bool _pressed;

    public BackdropChevronButton(bool right)
    {
        _right = right;
        Text = right ? "›" : "‹";
        AutoSize = false;
        FlatStyle = FlatStyle.Flat;
        UseVisualStyleBackColor = false;
        FlatAppearance.BorderSize = 0;
        AccessibleRole = AccessibleRole.PushButton;
        BackColor = BackdropPalette.Field;
        ForeColor = BackdropPalette.Text;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    internal bool PointsRight => _right;

    internal void ClipToRoundedParent(Control roundedParent)
    {
        var parentBounds = new RectangleF(1 - Left, 1 - Top,
            roundedParent.ClientSize.Width - 2, roundedParent.ClientSize.Height - 2);
        using var parentPath = BackdropControlPaint.RoundedRectangle(parentBounds,
            roundedParent.DeviceDpi / 12f);
        var previousRegion = Region;
        Region = new Region(parentPath);
        previousRegion?.Dispose();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Parent?.BackColor ?? BackdropPalette.Surface);
        var bounds = RectangleF.Inflate(ClientRectangle, -1, -1);
        using var path = BackdropControlPaint.RoundedRectangle(bounds, DeviceDpi / 12f);
        var fillColor = !Enabled ? BackdropPalette.Surface : _pressed ? BackdropPalette.Selection : _hovered ? Color.FromArgb(54, 55, 61) : BackdropPalette.Field;
        using var fill = new SolidBrush(fillColor);
        graphics.FillPath(fill, path);
        BackdropControlPaint.DrawHorizontalChevron(graphics, Rectangle.Round(bounds), _right,
            Enabled ? BackdropPalette.Text : BackdropPalette.Muted);

        if (Focused && ShowFocusCues)
        {
            using var focusPath = BackdropControlPaint.RoundedRectangle(RectangleF.Inflate(bounds, -2, -2), DeviceDpi / 20f);
            using var focusPen = new Pen(BackdropPalette.Focus, 1) { DashStyle = DashStyle.Dot };
            graphics.DrawPath(focusPen, focusPath);
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
            _pressed = true;
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        Invalidate();
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnLostFocus(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnEnter(EventArgs e)
    {
        Invalidate();
        base.OnEnter(e);
    }

    protected override void OnLeave(EventArgs e)
    {
        Invalidate();
        base.OnLeave(e);
    }
}

internal sealed class BackdropDropdown : UserControl
{
    private readonly ToolStripDropDown _popup = new()
    {
        AutoClose = true,
        AutoSize = false,
        BackColor = BackdropPalette.Border,
        Padding = new Padding(1),
        DropShadowEnabled = true
    };
    private readonly ToolStripControlHost _popupHost;
    private readonly BackdropDropdownList _popupList;
    private int _selectedIndex = -1;
    private bool _popupIsOpen;
    private bool _committingSelection;
    private bool _restoreFocusAfterClose;
    private bool _popupWasOpenAtMouseDown;
    private bool _popupClosedByOwnerMouseDown;

    public BackdropDropdown()
    {
        _popup.Renderer = new ToolStripProfessionalRenderer(new BackdropDropdownColorTable());
        _popupList = new BackdropDropdownList(this);
        Items = [];
        MinimumSize = new Size(0, 38);
        AccessibleRole = AccessibleRole.ComboBox;
        AccessibleDescription = "Choose an option. Press Enter, Space, or Alt+Down to open the list. Use the arrow keys, then Enter to select.";
        TabStop = true;
        BackColor = BackdropPalette.Field;
        ForeColor = BackdropPalette.Text;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        _popupList.AccessibleName = "Dropdown options";
        _popupHost = new ToolStripControlHost(_popupList)
        {
            AutoSize = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _popup.Items.Add(_popupHost);
        _popup.Closed += (_, _) =>
        {
            if ((Control.MouseButtons & MouseButtons.Left) != 0 &&
                ClientRectangle.Contains(PointToClient(Control.MousePosition)))
                _popupClosedByOwnerMouseDown = true;
            var restoreFocus = _restoreFocusAfterClose;
            _restoreFocusAfterClose = false;
            _popupIsOpen = false;
            if (!_committingSelection)
                _popupList.SelectedIndex = _selectedIndex;
            Invalidate();
            if (restoreFocus && !Disposing && !IsDisposed && CanFocus)
                Focus();
        };
    }

    internal List<string> Items { get; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (value < -1 || value >= Items.Count)
                throw new ArgumentOutOfRangeException(nameof(value), value, "The selected index must refer to an item or be -1.");
            if (_selectedIndex == value)
                return;
            _selectedIndex = value;
            _popupList.CommittedIndex = value;
            _popupList.Invalidate();
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    internal string? SelectedItem => SelectedIndex >= 0 ? Items[SelectedIndex] : null;
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool DroppedDown
    {
        get => _popupIsOpen;
        set
        {
            if (value)
                OpenPopup();
            else
                ClosePopup();
        }
    }
    internal Control PopupList => _popupList;
    internal Control PopupSurface => _popup;
    internal Rectangle PopupBounds => _popup.Bounds;
    internal bool HasRoundedPopupRegion => _popup.Region is not null;
    internal int PopupCheckmarkCount => SelectedIndex >= 0 ? 1 : 0;
    internal event EventHandler? SelectedIndexChanged;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (ClientSize.Width < 4 || ClientSize.Height < 4)
            return;

        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Parent?.BackColor ?? BackdropPalette.Surface);
        var bounds = RectangleF.Inflate(ClientRectangle, -1, -1);
        using var path = BackdropControlPaint.RoundedRectangle(bounds, DeviceDpi / 18f);
        using var fill = new SolidBrush(Enabled ? BackdropPalette.Field : BackdropPalette.Surface);
        using var border = new Pen((ContainsFocus || _popupIsOpen) && ShowFocusCues ? BackdropPalette.Focus : BackdropPalette.Border);
        graphics.FillPath(fill, path);
        graphics.DrawPath(border, path);

        var arrowWidth = Math.Min(Math.Max(DeviceDpi / 4, 24), ClientSize.Width / 3);
        var textBounds = Rectangle.Inflate(ClientRectangle, -8, -2);
        textBounds.Width = Math.Max(0, textBounds.Width - arrowWidth);
        var selectedText = SelectedItem ?? string.Empty;
        TextRenderer.DrawText(graphics, selectedText, Font, textBounds,
            Enabled ? BackdropPalette.Text : BackdropPalette.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

        var arrowBounds = new Rectangle(ClientSize.Width - arrowWidth, 0, arrowWidth, ClientSize.Height);
        BackdropControlPaint.DrawChevron(graphics, arrowBounds, up: _popupIsOpen,
            Enabled ? BackdropPalette.Text : BackdropPalette.Muted);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
            return;
        _popupWasOpenAtMouseDown = _popupIsOpen || _popupClosedByOwnerMouseDown;
        _popupClosedByOwnerMouseDown = false;
        Focus();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left)
            return;
        var wasOpenForClick = _popupWasOpenAtMouseDown || _popupIsOpen || _popupClosedByOwnerMouseDown;
        _popupWasOpenAtMouseDown = false;
        _popupClosedByOwnerMouseDown = false;
        if (!ClientRectangle.Contains(e.Location))
            return;
        if (wasOpenForClick)
            ClosePopup();
        else
            OpenPopup();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        if (_popupList is not null)
        {
            _popupList.Font = Font;
            Invalidate();
        }
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        if (_popupList is not null)
            _popupList.ItemHeight = Math.Max(32, (int)Math.Round(36 * DeviceDpi / 96f));
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (HandleKeyCommand(keyData))
            return true;

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (HandleKeyCommand(keyData))
            return true;

        return base.ProcessDialogKey(keyData);
    }

    internal bool HandleKeyCommand(Keys keyData)
    {
        if (Items.Count == 0)
            return false;

        if (_popupIsOpen)
        {
            if (keyData is Keys.Enter)
            {
                CommitPopupSelection();
                return true;
            }
            if (keyData is Keys.Escape)
            {
                ClosePopup(restoreFocus: true);
                return true;
            }
            if (keyData is Keys.Down or Keys.Up or Keys.Home or Keys.End)
            {
                _popupList.SelectedIndex = keyData switch
                {
                    Keys.Down => Math.Min(Items.Count - 1, Math.Max(0, _popupList.SelectedIndex + 1)),
                    Keys.Up => Math.Max(0, _popupList.SelectedIndex < 0 ? 0 : _popupList.SelectedIndex - 1),
                    Keys.Home => 0,
                    _ => Items.Count - 1
                };
                return true;
            }
            return false;
        }

        if (!_popupIsOpen)
        {
            if (keyData is Keys.Enter or Keys.Space or Keys.F4 || keyData == (Keys.Alt | Keys.Down))
            {
                OpenPopup();
                return true;
            }

            if (keyData is Keys.Down or Keys.Up or Keys.Home or Keys.End)
            {
                SelectedIndex = keyData switch
                {
                    Keys.Down => Math.Min(Items.Count - 1, Math.Max(0, SelectedIndex + 1)),
                    Keys.Up => Math.Max(0, SelectedIndex < 0 ? 0 : SelectedIndex - 1),
                    Keys.Home => 0,
                    _ => Items.Count - 1
                };
                return true;
            }
        }
        return false;
    }

    private void OpenPopup()
    {
        if (_popupIsOpen || !Enabled || Items.Count == 0)
            return;

        _popupList.BeginUpdate();
        _popupList.Items.Clear();
        _popupList.Items.AddRange(Items.ToArray());
        _popupList.CommittedIndex = SelectedIndex;
        _popupList.AccessibleName = string.IsNullOrWhiteSpace(AccessibleName)
            ? "Dropdown options"
            : $"{AccessibleName} options";
        _popupList.SelectedIndex = SelectedIndex;
        _popupList.EndUpdate();
        _popupList.Font = Font;
        _popupList.ItemHeight = Math.Max(32, (int)Math.Round(36 * DeviceDpi / 96f));
        var workArea = Screen.FromControl(this).WorkingArea;
        var widestItem = 0;
        foreach (var item in Items)
            widestItem = Math.Max(widestItem, TextRenderer.MeasureText(item, Font).Width);
        var textAndCheckPadding = Math.Max(68, (int)Math.Round(68f * DeviceDpi / 96f));
        var popupWidth = Math.Min(workArea.Width,
            Math.Max(Math.Max(100, Width), widestItem + textAndCheckPadding));
        _popupList.Width = popupWidth - 2;
        _popupList.Height = _popupList.ItemHeight * Math.Min(Items.Count, 8);
        _popupHost.Size = _popupList.Size;
        _popup.Size = new Size(popupWidth, _popupList.Height + 2);
        var previousRegion = _popup.Region;
        using (var popupPath = BackdropControlPaint.RoundedRectangle(
            new RectangleF(0, 0, _popup.Width, _popup.Height), 7 * DeviceDpi / 96f))
            _popup.Region = new Region(popupPath);
        previousRegion?.Dispose();
        _popupIsOpen = true;
        _committingSelection = false;
        _restoreFocusAfterClose = false;

        var popupGap = Math.Max(3, (int)Math.Round(4 * DeviceDpi / 96f));
        var location = PointToScreen(new Point(0, Height + popupGap));
        if (location.Y + _popup.Height > workArea.Bottom)
            location.Y = Math.Max(workArea.Top, PointToScreen(Point.Empty).Y - _popup.Height - popupGap);
        if (location.X + _popup.Width > workArea.Right)
            location.X = Math.Max(workArea.Left, workArea.Right - _popup.Width);
        _popup.Show(location);
        _popupList.Focus();
        _popupList.Invalidate();
        Invalidate();
    }

    private void CommitPopupSelection()
    {
        if (_popupList.SelectedIndex < 0)
            return;
        _committingSelection = true;
        SelectedIndex = _popupList.SelectedIndex;
        ClosePopup(restoreFocus: true);
    }

    private void ClosePopup(bool restoreFocus = false)
    {
        _restoreFocusAfterClose |= restoreFocus;
        if (_popup.Visible)
            _popup.Close();
        else
        {
            _popupIsOpen = false;
            Invalidate();
        }
    }

    private sealed class BackdropDropdownColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => BackdropPalette.Field;
        public override Color ToolStripBorder => BackdropPalette.Border;
        public override Color MenuBorder => BackdropPalette.Border;
        public override Color MenuItemSelected => BackdropPalette.Selection;
    }

    private sealed class BackdropDropdownList : ListBox
    {
        private readonly BackdropDropdown _owner;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal int CommittedIndex { get; set; } = -1;
        internal BackdropDropdownList(BackdropDropdown owner)
        {
            _owner = owner;
            AccessibleRole = AccessibleRole.List;
            SelectionMode = SelectionMode.One;
            DrawMode = DrawMode.OwnerDrawFixed;
            IntegralHeight = false;
            BorderStyle = BorderStyle.None;
            BackColor = BackdropPalette.Field;
            ForeColor = BackdropPalette.Text;
            ItemHeight = 36;
            TabStop = true;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count)
                return;
            var selected = (e.State & DrawItemState.Selected) != 0;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var background = new SolidBrush(BackdropPalette.Field))
                e.Graphics.FillRectangle(background, e.Bounds);
            if (selected)
            {
                var selectionBounds = RectangleF.Inflate(e.Bounds, -5 * DeviceDpi / 96f, -2 * DeviceDpi / 96f);
                using var selectionPath = BackdropControlPaint.RoundedRectangle(selectionBounds, 5 * DeviceDpi / 96f);
                using var selection = new SolidBrush(BackdropPalette.Selection);
                e.Graphics.FillPath(selection, selectionPath);
            }
            if (e.Index == CommittedIndex)
            {
                var x = e.Bounds.Left + 14;
                var y = e.Bounds.Top + e.Bounds.Height / 2;
                using var check = new Pen(BackdropPalette.Text, Math.Max(1.6f, DeviceDpi / 60f))
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };
                e.Graphics.DrawLine(check, x, y, x + 4, y + 4);
                e.Graphics.DrawLine(check, x + 4, y + 4, x + 11, y - 5);
            }
            var textBounds = Rectangle.Inflate(e.Bounds, -34, 0);
            TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, textBounds,
                BackdropPalette.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Left)
            {
                var index = IndexFromPoint(e.Location);
                if (index >= 0 && index < Items.Count)
                {
                    SelectedIndex = index;
                    _owner.CommitPopupSelection();
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (_owner.HandleKeyCommand(e.KeyCode))
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else
                base.OnKeyDown(e);
        }
    }
}

internal sealed class BackdropTextInputHost : UserControl
{
    private readonly TextBox _editor;

    public BackdropTextInputHost(TextBox editor)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        MinimumSize = new Size(0, 36);
        Height = 36;
        BackColor = BackdropPalette.Surface;
        ForeColor = BackdropPalette.Text;
        Cursor = Cursors.IBeam;
        TabStop = false;
        Margin = Padding.Empty;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        _editor.AutoSize = false;
        _editor.BorderStyle = BorderStyle.None;
        _editor.BackColor = BackdropPalette.Field;
        _editor.ForeColor = BackdropPalette.Text;
        _editor.Dock = DockStyle.None;
        _editor.GotFocus += (_, _) => Invalidate();
        _editor.LostFocus += (_, _) => Invalidate();
        _editor.FontChanged += (_, _) => LayoutEditor();
        Controls.Add(_editor);
        SizeChanged += (_, _) => LayoutEditor();
        FontChanged += (_, _) => LayoutEditor();
        LayoutEditor();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        LayoutEditor();
    }

    protected override void OnPaintBackground(PaintEventArgs e) =>
        e.Graphics.Clear(Parent?.BackColor ?? BackdropPalette.Surface);

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = RectangleF.Inflate(ClientRectangle, -0.5f, -0.5f);
        using var path = BackdropControlPaint.RoundedRectangle(bounds, DeviceDpi / 12f);
        using var fill = new SolidBrush(BackdropPalette.Field);
        using var border = new Pen(ContainsFocus ? BackdropPalette.Focus : BackdropPalette.Border);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
            _editor.Focus();
    }

    private void LayoutEditor()
    {
        if (_editor.IsDisposed || ClientSize.Width <= 0 || ClientSize.Height <= 0)
            return;

        var horizontalInset = Math.Max(8, (int)Math.Round(8f * DeviceDpi / 96f));
        var editorHeight = Math.Min(_editor.PreferredHeight, ClientSize.Height);
        var editorTop = (ClientSize.Height - editorHeight) / 2;
        _editor.Bounds = new Rectangle(horizontalInset, editorTop,
            Math.Max(0, ClientSize.Width - horizontalInset * 2), editorHeight);
    }
}

internal sealed class BackdropNumberSelector : UserControl
{
    private readonly BackdropChevronButton _decreaseButton;
    private readonly TextBox _valueEditor;
    private readonly BackdropChevronButton _increaseButton;
    private decimal _minimum;
    private decimal _maximum = 100;
    private decimal _increment = 1;
    private decimal _value;
    private Font? _scaledEditorFont;
    private bool _thousandsSeparator;

    public BackdropNumberSelector()
    {
        MinimumSize = new Size(0, 38);
        AccessibleRole = AccessibleRole.SpinButton;
        TabStop = false;
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);

        _decreaseButton = new BackdropChevronButton(right: false) { AccessibleName = "Decrease value", TabIndex = 0, Margin = Padding.Empty };
        _valueEditor = new TextBox
        {
            AutoSize = false,
            BorderStyle = BorderStyle.None,
            TextAlign = HorizontalAlignment.Center,
            AccessibleName = "Numeric value",
            TabIndex = 1,
            BackColor = BackdropPalette.Field,
            ForeColor = BackdropPalette.Text
        };
        _increaseButton = new BackdropChevronButton(right: true) { AccessibleName = "Increase value", TabIndex = 2, Margin = Padding.Empty };
        foreach (Control focusable in new Control[] { _decreaseButton, _valueEditor, _increaseButton })
        {
            focusable.GotFocus += (_, _) => Invalidate();
            focusable.LostFocus += (_, _) => Invalidate();
        }
        Controls.Add(_decreaseButton);
        Controls.Add(_valueEditor);
        Controls.Add(_increaseButton);

        _decreaseButton.Click += (_, _) => Step(-1);
        _increaseButton.Click += (_, _) => Step(1);
        _valueEditor.Leave += (_, _) => CommitEdit();
        _valueEditor.KeyDown += OnEditorKeyDown;
        UpdateAccessibleNames();
        UpdateText();
        LayoutChildren();
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal decimal Minimum
    {
        get => _minimum;
        set
        {
            if (_minimum == value)
                return;
            _minimum = value;
            if (_maximum < value)
                _maximum = value;
            if (_value < value)
                Value = value;
            UpdateAccessibleNames();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal decimal Maximum
    {
        get => _maximum;
        set
        {
            if (_maximum == value)
                return;
            _maximum = value;
            if (_minimum > value)
                _minimum = value;
            if (_value > value)
                Value = value;
            UpdateAccessibleNames();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal decimal Increment
    {
        get => _increment;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "The increment must be greater than zero.");
            _increment = value;
            UpdateAccessibleNames();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal decimal Value
    {
        get => _value;
        set
        {
            if (value < _minimum || value > _maximum)
                throw new ArgumentOutOfRangeException(nameof(value), value, $"The value must be between {_minimum} and {_maximum}.");
            if (_value == value)
                return;
            _value = value;
            UpdateText();
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool ThousandsSeparator
    {
        get => _thousandsSeparator;
        set
        {
            if (_thousandsSeparator == value)
                return;
            _thousandsSeparator = value;
            UpdateText();
        }
    }

    internal event EventHandler? ValueChanged;

    internal void RefreshAccessibility() => UpdateAccessibleNames();

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        if (_valueEditor is not null)
        {
            _valueEditor.ForeColor = Enabled ? BackdropPalette.Text : BackdropPalette.Muted;
            Invalidate();
        }
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        if (_valueEditor is null)
            return;
        UpdateEditorFont();
        _decreaseButton.Font = Font;
        _increaseButton.Font = Font;
        LayoutChildren();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        LayoutChildren();
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        UpdateEditorFont();
        LayoutChildren();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        UpdateEditorFont();
        LayoutChildren();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _scaledEditorFont?.Dispose();
            _scaledEditorFont = null;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? BackdropPalette.Surface);
        var bounds = RectangleF.Inflate(ClientRectangle, -1, -1);
        using var path = BackdropControlPaint.RoundedRectangle(bounds, DeviceDpi / 12f);
        using var fill = new SolidBrush(BackdropPalette.Field);
        using var border = new Pen(ContainsFocus && ShowFocusCues ? BackdropPalette.Focus : BackdropPalette.Border);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (!HandleEditorKey(e.KeyCode))
            return;

        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    internal bool HandleEditorKey(Keys key)
    {
        if (key is not (Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.Enter or Keys.Escape))
            return false;

        if (key == Keys.Escape)
            UpdateText();
        else
        {
            CommitEdit();
            if (key == Keys.Up)
                Step(1);
            else if (key == Keys.Down)
                Step(-1);
            else if (key == Keys.Home)
                Value = Minimum;
            else if (key == Keys.End)
                Value = Maximum;
        }
        return true;
    }

    private void Step(int direction)
    {
        CommitEdit();
        try
        {
            Value = Math.Clamp(_value + _increment * direction, _minimum, _maximum);
        }
        catch (OverflowException)
        {
            Value = direction > 0 ? _maximum : _minimum;
        }
    }

    private void CommitEdit()
    {
        if (decimal.TryParse(_valueEditor.Text, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.CurrentCulture, out var value) &&
            decimal.Truncate(value) == value &&
            value >= _minimum && value <= _maximum)
            Value = value;
        else
            UpdateText();
    }

    private void UpdateText() =>
        _valueEditor.Text = _value.ToString(
            _thousandsSeparator ? "#,##0.############################" : "0.############################",
            System.Globalization.CultureInfo.CurrentCulture);

    private void UpdateAccessibleNames()
    {
        var name = string.IsNullOrWhiteSpace(AccessibleName) ? "value" : AccessibleName;
        _decreaseButton.AccessibleName = $"Decrease {name}";
        _increaseButton.AccessibleName = $"Increase {name}";
        _decreaseButton.AccessibleDescription = $"Decrease by {_increment}.";
        _increaseButton.AccessibleDescription = $"Increase by {_increment}.";
        _valueEditor.AccessibleName = name;
        _valueEditor.AccessibleDescription = string.IsNullOrWhiteSpace(AccessibleDescription)
            ? "Enter a number. Use Up and Down Arrow to change the value. Home selects the minimum; End selects the maximum."
            : $"{AccessibleDescription} Use Up and Down Arrow to change the value. Home selects the minimum; End selects the maximum.";
    }

    private void LayoutChildren()
    {
        if (_valueEditor is null)
            return;

        var edgeInset = Math.Max(3, (int)Math.Ceiling(3f * DeviceDpi / 96f));
        var verticalInset = Math.Max(2, DeviceDpi / 48);
        var buttonGap = Math.Max(2, (int)Math.Round(3f * DeviceDpi / 96f));
        var centerMinimumWidth = Math.Min(44, Math.Max(0,
            ClientSize.Width - edgeInset * 2 - buttonGap * 2));
        var availableButtonWidth = Math.Max(0,
            (ClientSize.Width - edgeInset * 2 - buttonGap * 2 - centerMinimumWidth) / 2);
        var preferredButtonWidth = Math.Max(24, (int)Math.Round(28f * DeviceDpi / 96f));
        var buttonWidth = Math.Min(preferredButtonWidth, availableButtonWidth);
        var buttonHeight = Math.Max(0, ClientSize.Height - verticalInset * 2);
        _decreaseButton.Bounds = new Rectangle(edgeInset, verticalInset, buttonWidth, buttonHeight);
        _increaseButton.Bounds = new Rectangle(ClientSize.Width - edgeInset - buttonWidth,
            verticalInset, buttonWidth, buttonHeight);
        _decreaseButton.ClipToRoundedParent(this);
        _increaseButton.ClipToRoundedParent(this);

        var editorLeft = _decreaseButton.Right + buttonGap;
        var editorRight = _increaseButton.Left - buttonGap;
        var editorHeight = Math.Min(_valueEditor.PreferredHeight, buttonHeight);
        var editorTop = (ClientSize.Height - editorHeight) / 2;
        _valueEditor.Bounds = new Rectangle(editorLeft, editorTop, Math.Max(0, editorRight - editorLeft), editorHeight);
    }

    private void UpdateEditorFont()
    {
        if (_valueEditor is null)
            return;

        var dpiScale = DeviceDpi / 96f;
        if (Math.Abs(dpiScale - 1f) < 0.001f)
        {
            if (!ReferenceEquals(_valueEditor.Font, Font))
                _valueEditor.Font = Font;
            _scaledEditorFont?.Dispose();
            _scaledEditorFont = null;
            return;
        }

        var scaledSize = Font.SizeInPoints * dpiScale;
        if (_scaledEditorFont is null || _scaledEditorFont.SizeInPoints != scaledSize ||
            !_scaledEditorFont.FontFamily.Equals(Font.FontFamily) || _scaledEditorFont.Style != Font.Style)
        {
            var nextFont = new Font(Font.FontFamily, scaledSize, Font.Style, GraphicsUnit.Point);
            _valueEditor.Font = nextFont;
            _scaledEditorFont?.Dispose();
            _scaledEditorFont = nextFont;
        }
        else if (!ReferenceEquals(_valueEditor.Font, _scaledEditorFont))
        {
            _valueEditor.Font = _scaledEditorFont;
        }
    }

}
internal sealed class SegmentedChoiceControl : UserControl
{
    private readonly IReadOnlyList<string> _items;
    private readonly SegmentedRadioButton[] _options;

    public event EventHandler? SelectedIndexChanged;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<string> Items => _items;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => Array.FindIndex(_options, option => option.Checked);
        set
        {
            if (value < 0 || value >= _options.Length)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (_options[value].Checked)
                return;
            _options[value].Checked = true;
        }
    }

    public SegmentedChoiceControl(params string[] items)
    {
        if (items.Length < 2 || items.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("A choice group needs at least two named options.", nameof(items));

        _items = Array.AsReadOnly(items.ToArray());
        _options = new SegmentedRadioButton[_items.Count];
        MinimumSize = new Size(0, 38);
        Height = 38;
        AccessibleRole = AccessibleRole.Grouping;
        TabStop = false;
        BackColor = BackdropPalette.Surface;
        ForeColor = BackdropPalette.Text;
        Margin = Padding.Empty;

        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = _items.Count,
            RowCount = 1,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
            BackColor = BackdropPalette.Surface
        };
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        for (var index = 0; index < _items.Count; index++)
        {
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / _items.Count));
            var option = new SegmentedRadioButton(_items[index])
            {
                Dock = DockStyle.Fill,
                TabIndex = index,
                TabStop = index == 0,
                Margin = new Padding(index == 0 ? 0 : 1, 0, index == _items.Count - 1 ? 0 : 1, 0)
            };
            option.CheckedChanged += (_, _) =>
            {
                if (option.Checked)
                    SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            };
            _options[index] = option;
            row.Controls.Add(option, index, 0);
        }
        Controls.Add(row);
        _options[0].Checked = true;
    }
}

internal sealed class SegmentedRadioButton : RadioButton
{
    private const float CornerRadius = 5;
    private bool _hovered;

    public SegmentedRadioButton(string text)
    {
        Text = text;
        Font = BackdropTypography.Create(8.5F);
        Appearance = Appearance.Button;
        FlatStyle = FlatStyle.Flat;
        UseVisualStyleBackColor = false;
        AutoSize = false;
        TextAlign = ContentAlignment.MiddleCenter;
        AccessibleRole = AccessibleRole.RadioButton;
        AccessibleName = text;
        ForeColor = BackdropPalette.Text;
        BackColor = BackdropPalette.Field;
        FlatAppearance.BorderSize = 0;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Parent?.BackColor ?? BackdropPalette.Field);
        var bounds = RectangleF.Inflate(ClientRectangle, -1, -1);
        using var path = BackdropControlPaint.RoundedRectangle(bounds, CornerRadius * DeviceDpi / 96f);
        var fillColor = !Enabled
            ? BackdropPalette.Surface
            : Checked
                ? BackdropPalette.Selection
                : _hovered
                    ? Color.FromArgb(54, 55, 61)
                    : BackdropPalette.Field;
        using var fill = new SolidBrush(fillColor);
        graphics.FillPath(fill, path);
        using var border = new Pen(Checked || Focused ? BackdropPalette.Focus : BackdropPalette.Border);
        graphics.DrawPath(border, path);
        TextRenderer.DrawText(graphics, Text, Font, Rectangle.Inflate(ClientRectangle, -1, -2),
            Enabled ? BackdropPalette.Text : BackdropPalette.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        Invalidate();
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        Invalidate();
        base.OnLostFocus(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }
}
