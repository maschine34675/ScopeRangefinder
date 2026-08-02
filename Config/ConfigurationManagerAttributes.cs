namespace ScopeRangefinder
{
    internal sealed class ConfigurationManagerAttributes
    {
        public string DispName;
        public int? Order;
        public bool? IsAdvanced;
        public bool? Browsable;
        public bool? HideDefaultButton;
        public System.Action<BepInEx.Configuration.ConfigEntryBase> CustomDrawer;
    }
}
