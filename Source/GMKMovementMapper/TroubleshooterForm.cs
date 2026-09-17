using GMKMovementMapper.Mapping;

namespace GMKMovementMapper;

public sealed class TroubleshooterForm : Form
{
    public TroubleshooterForm(bool hidHide, bool vigem, bool connected, MovementOutputMode mode, bool dark)
    {
        Text = "GMK Mapper Troubleshooting Assistant"; StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(690, 570); MinimumSize = new Size(620, 500); Font = new Font("Segoe UI", 9.5F);
        var surface = dark ? Color.FromArgb(22,30,43) : Color.White;
        var text = dark ? Color.FromArgb(235,241,249) : Color.FromArgb(28,37,51);
        var muted = dark ? Color.FromArgb(156,169,188) : Color.FromArgb(98,108,125);
        BackColor = dark ? Color.FromArgb(11,17,27) : Color.FromArgb(242,245,249);
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(26), BackColor = surface };
        flow.Controls.Add(new Label { Text="Troubleshooting assistant", AutoSize=true, ForeColor=text, Font=new Font("Segoe UI Semibold",17F,FontStyle.Bold), Margin=new Padding(0,0,0,10) });
        Add(flow, "GMK is not detected or says Waiting", "Close the original GMK Driver and AntiMicro. Connect the GMK directly to the PC, then click Start movement. Only one program can read its USB interface.", connected, text, muted);
        Add(flow, "Mouse stops working correctly in Fortnite", "Use Keyboard movement. The original GMK software and Controller mode create a virtual controller that Fortnite may detect. Stop Controller movement to remove it immediately.", mode == MovementOutputMode.Keyboard, text, muted);
        Add(flow, "Game reacts to both controller and keyboard", "Set up HidHide once and keep the original GMK software closed. HidHide itself does not need to remain open, and setup normally works without reconnecting.", hidHide, text, muted);
        Add(flow, "Controller mode will not start", "Install or repair ViGEmBus, restart Windows, and retry. Keyboard mode does not require ViGEmBus.", vigem, text, muted);
        Add(flow, "Movement opens inventory or changes weapons", "The app's movement keys must exactly match the four Fortnite movement binds. Check the active profile and remap forward, left, backward and right.", null, text, muted);
        Add(flow, "Right or another direction is blocked", "Calibrate the stick, verify the matching game bind, and check that no two movement directions use the same key.", null, text, muted);
        Add(flow, "Movement feels delayed or uneven", "Check Diagnostics for USB rate and processing latency. Connect directly instead of through a hub, calibrate, and avoid running the original GMK software at the same time.", null, text, muted);
        Controls.Add(flow);
    }

    private static void Add(FlowLayoutPanel flow, string issue, string fix, bool? passed, Color text, Color muted)
    {
        var status = passed is true ? "✓ " : passed is false ? "! " : "• ";
        var color = passed is true ? Color.FromArgb(22,163,74) : passed is false ? Color.FromArgb(217,119,6) : text;
        flow.Controls.Add(new Label { Text=status+issue, AutoSize=true, ForeColor=color, Font=new Font("Segoe UI Semibold",10F,FontStyle.Bold), Margin=new Padding(0,8,0,2) });
        flow.Controls.Add(new Label { Text=fix, AutoSize=true, MaximumSize=new Size(610,0), ForeColor=muted, Margin=new Padding(0,0,0,8) });
    }
}
