using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vampire
{
    public class MainMenu : MonoBehaviour
    {
        [SerializeField] private CharacterBlueprint defaultCharacter;

        /// <summary>开始按钮调用：用默认角色直接进入 Level 1。</summary>
        public void StartDefaultGame()
        {
            CrossSceneData.CharacterBlueprint = defaultCharacter;
            SceneManager.LoadScene(1);
        }
    }
}
