using System;
using System.Collections.Generic;

namespace Wizdom.Plugin.Model
{
    public class WizdomSearch
    {
        public string id { get; set; }
        public string versioname { get; set; }
        public int? score { get; set; }
    }
    public class WizdomFreeSearch
    {
        public string title { get; set; }
        public string title_en { get; set; }
        public string imdb { get; set; }
    }
}