using UnityEngine;

namespace LanternKeeper
{
// Rare one-shot ambience on top of the bed. Island2 only: an owl call at a random point 15-30 m from the listener,
// every 12-30 s, never sooner than 8 s after load, and never while paused, dying or after the round is over.
public class AmbienceExtras : MonoBehaviour
{
    public const string OwlScene = "Island2";
    public const float MinDistance = 15f;
    public const float MaxDistance = 30f;

    [SerializeField] float firstDelay = 8f;
    [SerializeField] float minInterval = 12f;
    [SerializeField] float maxInterval = 30f;

    float countdown;
    AudioListener listener;

    // Test hook: shorter timings, restarts the countdown.
    public void Configure(float first, float min, float max)
    {
        firstDelay = first;
        minInterval = min;
        maxInterval = max;
        countdown = first;
    }

    void Start()
    {
        countdown = firstDelay;
    }

    void Update()
    {
        if (gameObject.scene.name != OwlScene)
        {
            return;
        }

        GameManager game = GameManager.Instance;
        if (game != null && (game.IsPaused || game.IsRoundOver || game.IsDying || game.IntroShowing))
        {
            return;
        }

        if (Time.timeScale <= 0f || AudioListener.pause)
        {
            return;
        }

        countdown -= Time.deltaTime;
        if (countdown > 0f)
        {
            return;
        }

        countdown = Random.Range(minInterval, maxInterval);
        PlayOwl();
    }

    void PlayOwl()
    {
        AudioManager audio = AudioManager.Instance;
        if (audio == null)
        {
            return;
        }

        if (listener == null)
        {
            listener = FindAnyObjectByType<AudioListener>();
        }

        Vector3 origin = listener != null ? listener.transform.position : Vector3.zero;
        float angle = Random.value * Mathf.PI * 2f;
        float distance = Random.Range(MinDistance, MaxDistance);
        Vector3 point = origin + new Vector3(Mathf.Cos(angle) * distance, Random.Range(2f, 10f), Mathf.Sin(angle) * distance);
        audio.PlayCue(SoundCues.AmbienceOwl, point);
    }
}
}
