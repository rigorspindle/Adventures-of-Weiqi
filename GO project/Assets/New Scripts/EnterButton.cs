using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EnterButton : Singleton<EnterButton>
{
    public Button enterButton; // Assign the Enter button in the Inspector

    private UITrigger boundTrigger;

    public void BindTrigger(UITrigger trigger)
    {
        if (enterButton == null)
        {
            return;
        }

        if (boundTrigger == trigger)
        {
            return;
        }

        // Remove the old trigger listener before assigning the new one.
        if (boundTrigger != null)
        {
            enterButton.onClick.RemoveListener(boundTrigger.TriggerFromButton);
        }

        boundTrigger = trigger;

        if (boundTrigger != null)
        {
            enterButton.onClick.AddListener(boundTrigger.TriggerFromButton);
        }
    }

    public void UnbindTrigger()
    {
        if (enterButton == null)
        {
            boundTrigger = null;
            return;
        }

        if (boundTrigger != null)
        {
            enterButton.onClick.RemoveListener(boundTrigger.TriggerFromButton);
            boundTrigger = null;
        }
    }

    public void Show()
    {
        if (enterButton != null)
        {
            enterButton.gameObject.SetActive(true);
        }
    }

    public void Hide()
    {
        if (enterButton != null)
        {
            enterButton.gameObject.SetActive(false);
        }
    }

    public void Show(bool show)
    {
        if (show)
        {
            Show();
        }
        else
        {
            Hide();
        }
    }
}
