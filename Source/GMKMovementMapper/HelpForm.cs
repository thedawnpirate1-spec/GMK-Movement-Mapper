namespace GMKMovementMapper;

public sealed class HelpForm : Form
{
    public HelpForm(bool dark)
    {
        Text = "GMK Mapper Help & FAQ"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(680, 570);
        MinimumSize = new Size(620, 520); AutoScaleMode = AutoScaleMode.Dpi; Font = new Font("Segoe UI", 9.5F);
        var background = dark ? Color.FromArgb(11,17,27) : Color.FromArgb(242,245,249);
        var surface = dark ? Color.FromArgb(22,30,43) : Color.White;
        var text = dark ? Color.FromArgb(235,241,249) : Color.FromArgb(28,37,51);
        var muted = dark ? Color.FromArgb(156,169,188) : Color.FromArgb(98,108,125);
        BackColor = background;
        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(16,6) };
        tabs.TabPages.Add(Page("Quick start", new[] {
            ("1. Close conflicting apps", "Close the original GMK software and AntiMicro so this app can read the GMK directly."),
            ("2. Connect and set up", "Connect the GMK and click Set up HidHide once. The change takes effect immediately; reconnecting the GMK or reopening this app is normally unnecessary."),
            ("3. Choose a mode", "Keyboard movement avoids Fortnite's mixed-input controller issue. Controller movement provides analog output through a temporary virtual Xbox controller."),
            ("4. Select a profile", "Choose a preset or your saved profile, check its keys or analog settings, then click Start movement."),
            ("5. Stop safely", "Click Stop movement before changing modes. In Controller mode this immediately removes the virtual Xbox controller.") }, surface,text,muted));
        tabs.TabPages.Add(Page("FAQ", new[] {
            ("Why does it say Waiting for GMK?", "Connect the GMK directly, close the original driver and AntiMicro, then try again. Only one program can own the USB device."),
            ("Do I keep HidHide open?", "No. HidHide runs as a Windows driver. Its configuration app does not need to stay open."),
            ("Why does keyboard movement type letters?", "That is expected. The selected movement keys are normal keyboard output and must match your Fortnite movement binds."),
            ("Why use Controller mode?", "It restores analog movement and custom diagonal locking. Use Keyboard mode if Fortnite is switching input methods or affecting the mouse."),
            ("What does diagonal lock range mean?", "It is the size of the two purple forward zones. Input entering either zone snaps to your chosen forward diagonal angle. Backward movement is never locked."),
            ("Why is virtual controller unavailable?", "Install or repair ViGEmBus, restart Windows, and check the Diagnostics tab."),
            ("Does automatic startup remember my setup?", "Yes. It loads the last selected profile. Enable both startup options if you also want movement to begin automatically."),
            ("Can I control it from the tray?", "Yes. Right-click the tray icon to start or stop movement, switch profiles, reopen the window or exit."),
            ("What does performance mode change?", "It reduces visual and diagnostic refresh frequency. USB input and movement output continue at full speed."),
            ("What happens if the GMK is unplugged?", "Output returns to centre and the mapper attempts to reconnect automatically. Movement resumes with the same profile after the device returns.") }, surface,text,muted));
        Controls.Add(tabs);
    }

    private static TabPage Page(string title, IEnumerable<(string Q,string A)> items, Color surface, Color text, Color muted)
    {
        var page = new TabPage(title) { BackColor = surface, Padding = new Padding(22) };
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = surface };
        foreach (var (q,a) in items) { flow.Controls.Add(new Label { Text=q, AutoSize=true, ForeColor=text, Font=new Font("Segoe UI Semibold",10F,FontStyle.Bold), Margin=new Padding(0,6,0,2) }); flow.Controls.Add(new Label { Text=a, AutoSize=true, MaximumSize=new Size(590,0), ForeColor=muted, Margin=new Padding(0,0,0,9) }); }
        page.Controls.Add(flow); return page;
    }
}
