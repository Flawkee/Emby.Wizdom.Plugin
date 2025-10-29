using Emby.Web.GenericEdit;
using Emby.Web.GenericEdit.Validation;
using Wizdom.Plugin.Helpers;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Attributes;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Serialization;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Wizdom.Plugin.Configuration
{
    public class PluginConfiguration : EditableOptionsBase
    {

        public override string EditorTitle => "Wizdom Plugin Configuration";

        public override string EditorDescription => "Automatically downloads Hebrew subtitles from wizdom.xyz.\n\n"
                                                    + "Fow now, no additional configuration is required, you can use the plugin right away.";

    }
}