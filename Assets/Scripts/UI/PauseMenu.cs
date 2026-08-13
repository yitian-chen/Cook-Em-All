using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] private Image pauseButton;
        [SerializeField] private Sprite pauseSprite, playSprite;
        [SerializeField] private GameObject pauseMenu;
        [SerializeField] private KeyCode pauseKey = KeyCode.Escape;
        private bool paused = false;
        private bool timeIsFrozen = false;

        public bool TimeIsFrozen { set => timeIsFrozen = value; }

        void Update()
        {
            if (Input.GetKeyDown(pauseKey))
            {
                PlayPause();
            }
        }

        public void PlayPause()
        {
            if (paused = !paused)
            {
                if (!timeIsFrozen)
                    Time.timeScale = 0;
                if (pauseButton != null) pauseButton.sprite = playSprite;
                if (pauseMenu != null) pauseMenu.SetActive(true);
            }
            else
            {
                if (!timeIsFrozen)
                    Time.timeScale = 1;
                if (pauseButton != null) pauseButton.sprite = pauseSprite;
                if (pauseMenu != null) pauseMenu.SetActive(false);
            }
        }
    }
}
