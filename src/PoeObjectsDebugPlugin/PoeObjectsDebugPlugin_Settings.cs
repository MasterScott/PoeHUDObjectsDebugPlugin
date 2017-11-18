using PoeHUD.Hud.Settings;
using PoeHUD.Plugins;
using System.Windows.Forms;

namespace PoeObjectsDebugPlugin
{
    public class PoeObjectsDebugPlugin_Settings : SettingsBase
    {
        public PoeObjectsDebugPlugin_Settings()
        {
            Enable = false;
            TranslateModsFromFiles = false;
            ItemDebug = Keys.NumPad5;
            ObjectDebug = Keys.NumPad6;
            ObjectDebugList = new ListNode();
            DetectDist = new RangeNode<float>(3, 1, 30);
        }


        [Menu("Item Debug")]
        public HotkeyNode ItemDebug { get; set; }

        [Menu("Object Debug")]
        public HotkeyNode ObjectDebug { get; set; }
        [Menu("Object Debug List")]

        public ListNode ObjectDebugList { get; set; }

        [Menu("Translate Item Mods From Game Files")]
        public ToggleNode TranslateModsFromFiles { get; set; }

        [Menu("Detect dist")]
        public RangeNode<float> DetectDist { get; set; }
    }
}
