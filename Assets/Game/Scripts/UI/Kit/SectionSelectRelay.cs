using UnityEngine;
using UnityEngine.EventSystems;

namespace LanternKeeper
{
// On a section tab: selecting it (keyboard, gamepad or mouse) shows its section, so the focus cue and the
// current section always agree.
public class SectionSelectRelay : MonoBehaviour, ISelectHandler
{
    [SerializeField] private SectionList list;
    [SerializeField] private int index;

    public void Configure(SectionList sectionList, int sectionIndex)
    {
        list = sectionList;
        index = sectionIndex;
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (list != null)
        {
            list.Show(index);
        }
    }
}
}
