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
            ItemDebug = Keys.NumPad5;
        }


        [Menu("Item Debug")]
        public HotkeyNode ItemDebug { get; set; }
    }
}
