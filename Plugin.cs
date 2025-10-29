using Wizdom.Plugin.Configuration;
using Wizdom.Plugin.Helpers;
using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using System;
using System.IO;
using MediaBrowser.Model.IO;

namespace Wizdom.Plugin
{
    public class Plugin : BasePluginSimpleUI<PluginConfiguration>, IHasThumbImage
    {
        public Plugin(IApplicationHost applicationHost, IJsonSerializer jsonSerializer, ILogger logger , IHttpClient httpClient, IZipClient zipClient) : base(applicationHost)
        {
            Instance = this;
            WizdomExplorer.Initialize(jsonSerializer, logger, httpClient, zipClient);
            ImdbExplorer.Initialize(jsonSerializer, logger, httpClient);
        }


        public override sealed string Name => PluginName;

        public static string PluginName = "Wizdom";
        public override Guid Id => new Guid("1c7b36cb-db0a-4a57-ab22-b8fedccde0a5");
        public override string Description => "Downloads Hebrew subtitles from wizdom.xyz";
        public static Plugin Instance { get; private set; }
        public PluginConfiguration Options => this.GetOptions();

        public Stream GetThumbImage()
        {
            var type = GetType();
            return type.Assembly.GetManifestResourceStream(type.Namespace + ".thumb.png");
        }

        public ImageFormat ThumbImageFormat
        {
            get
            {
                return ImageFormat.Png;
            }
        }
    }
}