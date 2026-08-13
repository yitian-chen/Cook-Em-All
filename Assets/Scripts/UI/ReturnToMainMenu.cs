using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vampire
{
    /// <summary>挂载在"退出到主菜单"按钮上。点击加载主菜单场景（build index 0）。
    /// 退出前重置 Time.timeScale，避免整备阶段的冻结状态带到主菜单。</summary>
    public class ReturnToMainMenu : MonoBehaviour
    {
        public void Return()
        {
            Time.timeScale = 1;
            SceneManager.LoadScene(0);
        }
    }
}
