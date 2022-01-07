using Infrastructure;
using UnityEngine;

namespace LobbyRelaySample
{
    /// <summary>
    /// Main menu start button.
    /// </summary>
    public class StartOfflineGameButtonUI : MonoBehaviour
    {
        public void ToSoloGame()
        {
            Locator.Get.Messenger.OnReceiveMessage(MessageType.StartSoloGame, null);
        }
    }
}
