using HNControl.Web.Services;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace HNControl.Web.TagHelpers;

[HtmlTargetElement("a")]
[HtmlTargetElement(Attributes = "hn-module")]
[HtmlTargetElement(Attributes = "hn-menu-relocated")]
public sealed class ModuleVisibilityTagHelper(ModuleAvailability features) : TagHelper
{
    public override int Order => 1000; // After AnchorTagHelper generates the destination.
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var module = context.AllAttributes["hn-module"]?.Value?.ToString();
        var href = output.Attributes["href"]?.Value?.ToString();
        var hidden = context.AllAttributes.ContainsName("hn-menu-relocated")
            || (module != null && !features.IsEnabled(module));
        if (href?.StartsWith('/') == true && !href.StartsWith("//")) hidden |= !features.IsPathEnabled(href);
        output.Attributes.RemoveAll("hn-module");
        output.Attributes.RemoveAll("hn-menu-relocated");
        if (hidden) output.SuppressOutput();
    }
}
