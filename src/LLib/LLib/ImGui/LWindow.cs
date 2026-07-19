using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace LLib.ImGui;

public abstract class LWindow : Window
{
	private bool _initializedConfig;

	private bool _wasCollapsedLastFrame;

	protected bool ClickedHeaderLastFrame { get; private set; }

	protected bool ClickedHeaderCurrentFrame { get; private set; }

	protected bool UncollapseNextFrame { get; set; }

	public bool IsOpenAndUncollapsed
	{
		get
		{
			if (base.IsOpen)
			{
				return !_wasCollapsedLastFrame;
			}
			return false;
		}
		set
		{
			base.IsOpen = value;
			UncollapseNextFrame = value;
		}
	}

	protected int? Alpha
	{
		get
		{
			return base.BgAlpha is float alpha ? (int)(100000f * alpha) : null;
		}
		set
		{
			base.BgAlpha = value is int alpha ? alpha / 100000f : null;
		}
	}

	protected LWindow(string windowName, ImGuiWindowFlags flags = ImGuiWindowFlags.None, bool forceMainWindow = false)
		: base(windowName, flags, forceMainWindow)
	{
	}

	private void LoadWindowConfig()
	{
		if (!(this is IPersistableWindowConfig pwc))
		{
			return;
		}
		WindowConfig config = pwc.WindowConfig;
		if (config != null)
		{
			if (base.AllowPinning)
			{
				IsPinned = config.IsPinned;
			}
			if (base.AllowClickthrough)
			{
				IsClickthrough = config.IsClickthrough;
			}
			Alpha = config.Alpha;
		}
		_initializedConfig = true;
	}

	private void UpdateWindowConfig()
	{
		if (!(this is IPersistableWindowConfig pwc) || Dalamud.Bindings.ImGui.ImGui.IsAnyMouseDown())
		{
			return;
		}
		WindowConfig config = pwc.WindowConfig;
		if (config != null)
		{
			bool changed = false;
			if (base.AllowPinning && config.IsPinned != IsPinned)
			{
				config.IsPinned = IsPinned;
				changed = true;
			}
			if (base.AllowClickthrough && config.IsClickthrough != IsClickthrough)
			{
				config.IsClickthrough = IsClickthrough;
				changed = true;
			}
			if (config.Alpha != Alpha)
			{
				config.Alpha = Alpha;
				changed = true;
			}
			if (changed)
			{
				pwc.SaveWindowConfig();
			}
		}
	}

	public void ToggleOrUncollapse()
	{
		IsOpenAndUncollapsed = !IsOpenAndUncollapsed;
	}

	public override void OnOpen()
	{
		UncollapseNextFrame = true;
		base.OnOpen();
	}

	public override void Update()
	{
		_wasCollapsedLastFrame = true;
	}

	public override void PreDraw()
	{
		if (!_initializedConfig)
		{
			LoadWindowConfig();
		}
		if (UncollapseNextFrame)
		{
			Dalamud.Bindings.ImGui.ImGui.SetNextWindowCollapsed(collapsed: false);
			UncollapseNextFrame = false;
		}
		base.PreDraw();
		ClickedHeaderLastFrame = ClickedHeaderCurrentFrame;
		ClickedHeaderCurrentFrame = false;
	}

	public sealed override void Draw()
	{
		_wasCollapsedLastFrame = false;
		DrawContent();
	}

	public abstract void DrawContent();

	public override void PostDraw()
	{
		base.PostDraw();
		if (_initializedConfig)
		{
			UpdateWindowConfig();
		}
	}

}
