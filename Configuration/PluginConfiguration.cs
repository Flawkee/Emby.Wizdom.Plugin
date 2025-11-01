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

        public override string EditorDescription => "Automatically downloads Hebrew subtitles from wizdom.xyz.\n\n";

        [DisplayName("Wizdom Request Timeout")]
        [Description("Request Timeout (in seconds): If not configured, the default is 2 seconds. This should be set and saved before setting up username and password. This setting is helpful if Wizdom.xyz is unavailable, ensuring that subtitle search doesn't wait the full 30 seconds (the Emby default) before returning a response.")]
        public int? requestTimeout { get; set; }

        protected override void Validate(ValidationContext context)
        {
            var explorer = WizdomExplorer.Instance;
            if (explorer == null)
            {
                context.AddValidationError("WizdomExplorer is not initialized.");
                return;
            }

            if (requestTimeout.HasValue && (requestTimeout.Value <= 0 || requestTimeout.Value >= 30))
            {
                context.AddValidationError("Request Timeout must be a greater than 0 and lower than 30 seconds.");
                return;
            }
            var accessStatus = explorer.WizdomAccessValidation();
            if (!accessStatus)
            {
                context.AddValidationError("Could not reach Wizdom.xyz with the request timeout configured. Wizdom.xyz might be unavailable, Please try again later.");
            }
        }
    }
}