using Lumina.Text.ReadOnly;

namespace LLib;

public interface IQuestDialogueText
{
	ReadOnlySeString Key { get; }

	ReadOnlySeString Value { get; }
}
