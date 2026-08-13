using TMPro;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 顶部击杀进度条：每帧从 WaveManager 读取本波次击杀数与所需击杀数，
    /// 驱动 PointBar 显示进度，并在 TMP 文本上显示 "kills/required"。
    /// 整备阶段同样显示 "0/下一波所需"（Prep 期间顶部条通常被整备界面遮挡）。
    /// </summary>
    public class WaveKillProgressBar : MonoBehaviour
    {
        [SerializeField] private WaveManager waveManager;
        [SerializeField] private PointBar killBar;
        [SerializeField] private TextMeshProUGUI killText;

        private int lastRequired = -1;

        private void Update()
        {
            if (waveManager == null || killBar == null) return;

            int kills = waveManager.KillsThisWave;
            int required = waveManager.KillsRequiredPerWave;

            // 仅在所需击杀数变化时重新 Setup（波次推进时 max 改变）
            if (required != lastRequired)
            {
                killBar.Setup(kills, 0, Mathf.Max(1, required), clamp: false);
                lastRequired = required;
            }
            else
            {
                killBar.SetPoints(kills);
            }

            if (killText != null)
                killText.text = kills + "/" + required;
        }
    }
}
