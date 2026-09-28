using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper
{
public class HUD : MonoBehaviour
{
    [SerializeField] Image fuelFill;
    [SerializeField] Text beaconText;
    [SerializeField] Text promptText;
    [SerializeField] Text timerText;
    [SerializeField] GameObject winPanel;
    [SerializeField] GameObject losePanel;
    [SerializeField] Text winDetailText;
    [SerializeField] Text loseDetailText;

    Lantern lantern;

    void OnEnable()
    {
        lantern = FindAnyObjectByType<Lantern>();
        if (lantern != null)
        {
            lantern.FuelChanged += OnFuelChanged;
            OnFuelChanged(lantern.FuelNormalized);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.BeaconsChanged += OnBeaconsChanged;
            GameManager.Instance.PromptChanged += OnPromptChanged;
            GameManager.Instance.WonGame += OnWin;
            GameManager.Instance.LostGame += OnLose;
            GameManager.Instance.TimeChanged += OnTimeChanged;
            OnBeaconsChanged(GameManager.Instance.LitCount, GameManager.Instance.BeaconsToWin);
            OnPromptChanged();
            OnTimeChanged(GameManager.Instance.Elapsed);
        }

        if (winPanel != null)
        {
            winPanel.SetActive(false);
        }

        if (losePanel != null)
        {
            losePanel.SetActive(false);
        }
    }

    void OnDisable()
    {
        if (lantern != null)
        {
            lantern.FuelChanged -= OnFuelChanged;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.BeaconsChanged -= OnBeaconsChanged;
            GameManager.Instance.PromptChanged -= OnPromptChanged;
            GameManager.Instance.WonGame -= OnWin;
            GameManager.Instance.LostGame -= OnLose;
            GameManager.Instance.TimeChanged -= OnTimeChanged;
        }
    }

    void Update()
    {
        if (lantern == null)
        {
            lantern = FindAnyObjectByType<Lantern>();
        }

        if (lantern != null && fuelFill != null)
        {
            fuelFill.fillAmount = lantern.FuelNormalized;
        }

        GameManager manager = GameManager.Instance;
        if (manager == null)
        {
            return;
        }

        if (beaconText != null)
        {
            beaconText.text = "Beacons " + manager.LitCount + "/" + manager.BeaconsToWin;
        }

        if (timerText != null)
        {
            timerText.text = GameManager.FormatTime(manager.Elapsed);
        }

        if (promptText != null)
        {
            bool show = manager.ShowInteractPrompt && !manager.IsRoundOver;
            if (promptText.gameObject.activeSelf != show)
            {
                promptText.gameObject.SetActive(show);
                promptText.text = "Press E";
            }
        }

        if (manager.Won)
        {
            if (winPanel != null && !winPanel.activeSelf)
            {
                OnWin();
            }
        }
        else if (manager.IsRoundOver && losePanel != null && !losePanel.activeSelf)
        {
            OnLose();
        }
    }

    void OnFuelChanged(float normalized)
    {
        if (fuelFill != null)
        {
            fuelFill.fillAmount = normalized;
        }
    }

    void OnBeaconsChanged(int lit, int target)
    {
        if (beaconText != null)
        {
            beaconText.text = "Beacons " + lit + "/" + target;
        }
    }

    void OnPromptChanged()
    {
        bool show = GameManager.Instance != null && GameManager.Instance.ShowInteractPrompt;
        if (promptText != null)
        {
            promptText.gameObject.SetActive(show);
            promptText.text = "Press E";
        }
    }

    void OnTimeChanged(float seconds)
    {
        if (timerText != null)
        {
            timerText.text = GameManager.FormatTime(seconds);
        }
    }

    void OnWin()
    {
        if (winPanel != null)
        {
            winPanel.SetActive(true);
        }

        if (promptText != null)
        {
            promptText.gameObject.SetActive(false);
        }

        if (winDetailText != null && GameManager.Instance != null)
        {
            float best = GameManager.Instance.BestTime;
            string bestLabel = best < 0f ? "--:--" : GameManager.FormatTime(best);
            winDetailText.text = "Time " + GameManager.FormatTime(GameManager.Instance.Elapsed)
                + "\nBest " + bestLabel
                + "\nPress R to play again";
        }
    }

    void OnLose()
    {
        if (losePanel != null)
        {
            losePanel.SetActive(true);
        }

        if (promptText != null)
        {
            promptText.gameObject.SetActive(false);
        }

        if (loseDetailText != null)
        {
            loseDetailText.text = "The lantern went out.\nPress R to try again";
        }
    }
}
}
