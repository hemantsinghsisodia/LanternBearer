using UnityEngine;
using UnityEngine.EventSystems;

namespace LanternKeeper
{
// Sits on the Slider object and tells its SliderRow to persist on pointer-up, submit or deselect.
public class SliderCommitRelay : MonoBehaviour, IPointerUpHandler, ISubmitHandler, IDeselectHandler
{
    [SerializeField] private SliderRow row;

    public void Configure(SliderRow target)
    {
        row = target;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Commit();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        Commit();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        Commit();
    }

    private void Commit()
    {
        if (row != null)
        {
            row.Commit();
        }
    }
}
}
