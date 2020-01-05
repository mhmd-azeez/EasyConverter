using Microsoft.AspNetCore.Routing;
using System;
using System.Text.RegularExpressions;

namespace EasyConverter.WebUI.Helpers
{
    public class SlugifyParameterTransformer : IOutboundParameterTransformer
    {
        public string TransformOutbound(object value)
        {
            // Slugify value https://github.com/aspnet/Routing/issues/861
            return value == null ? null : Regex.Replace(value.ToString(), "([a-z])([A-Z])", "$1-$2").ToLower();
        }
    }
}
