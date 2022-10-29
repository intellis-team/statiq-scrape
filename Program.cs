// See https://aka.ms/new-console-template for more information
using System.Globalization;
using System.Xml.Linq;
using HtmlAgilityPack;

if(args.Length == 0 || args[0] == "-h" || args[0] == "--help")
{
    // output the contents of readme.md
    Console.WriteLine(File.ReadAllText("readme.md"));
    return;
}

var url = args[0];

// parse args for options
var options = new Dictionary<string, string>();
for(var i = 1; i < args.Length; i++)
{
    var arg = args[i];
    if(arg.StartsWith("--"))
    {
        var parts = arg.Split("=");
        if(parts.Length == 2)
        {
            options[parts[0].Substring(2)] = parts[1];
        }
    }
}

options["output"] =  "output";
options["class"] ??= "main";
options["trim-title"] ??= "";

//trim trailing slash
if(url.EndsWith("/"))
{
    url = url.Substring(0, url.Length - 1);
}

string sitemap;
//try to download the sitemap
try {
  sitemap = await DownloadSitemap(url);
}
catch (Exception ex) {
  Console.WriteLine($"Unable to download the sitemap of the website at {url}': {ex.Message}");
  return;
}

var urls = ParseSitemap(sitemap);
if(urls.Count == 0) {
  Console.WriteLine($"No urls found in the sitemap of the website at {url}");
  return;
}

Console.WriteLine($"Found {urls.Count} urls in the sitemap of the website at {url}");


//create the output folder if it doesn't exist
if (!Directory.Exists(options["output"])) {
  Directory.CreateDirectory(options["output"]);
}

//download and convert the urls in parallel
await Task.WhenAll(urls.Select(async pageUrl => await DownloadAndSavePage(url, pageUrl, options)));

// function to download the sitemap of a website
async Task<string> DownloadSitemap(string url)
{
    // append sitemap.xml to the url
    var sitemapUrl = url + "/sitemap.xml";
    var client = new HttpClient();
    var response = await client.GetAsync(sitemapUrl);
    var content = await response.Content.ReadAsStringAsync();
    return content;
}

// function to parse the sitemap
List<string> ParseSitemap(string sitemap)
{
    var urls = new List<string>();
    var xml = XDocument.Parse(sitemap);
    if(xml.Root == null) return urls;

    var ns = xml.Root.GetDefaultNamespace();
    var locs = xml.Descendants(ns + "loc");
    foreach (var loc in locs)
    {
        urls.Add(loc.Value);
    }
    return urls;
}

// function to download a url
async Task<string> DownloadPage(string url)
{
    var client = new HttpClient();
    var response = await client.GetAsync(url);
    var content = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"Downloaded {url}");
    return content;
}



// function to parse the html of a page
string ParsePage(string html, string contentClass){
    var doc = new HtmlDocument();
    doc.LoadHtml(html);
    var content = doc.DocumentNode.SelectSingleNode($"//div[@class='{contentClass}']");
    return content?.InnerHtml;
}

// function to convert html to markdown using ReverseMarkdown
string HtmlToMarkdown(string html){
  var config = new ReverseMarkdown.Config
  {
      UnknownTags = ReverseMarkdown.Config.UnknownTagsOption.Bypass,
      GithubFlavored = true,
      RemoveComments = true,
      SmartHrefHandling = true
  };
    var converter = new ReverseMarkdown.Converter(config);
    return converter.Convert(html);
}

// function to extract front matter from the html of a page
Dictionary<string, string> ExtractFrontMatter(string html, Dictionary<string, string> options){

    var frontMatter = new Dictionary<string, string>();

    var trimFromTitle = options["trim-title"];

    //remove surrounding quotes from trimFromTitle
    if(trimFromTitle.StartsWith("\"") && trimFromTitle.EndsWith("\""))
    {
        trimFromTitle = trimFromTitle.Substring(1, trimFromTitle.Length - 2);
    }
    
    var doc = new HtmlDocument();
    doc.LoadHtml(html);
    
    var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText;
    if(!string.IsNullOrEmpty(trimFromTitle))
    {
        title = title.Replace(trimFromTitle, "");
    }
    

    var description = doc.DocumentNode.SelectSingleNode("//meta[@name='description']")?.Attributes["content"]?.Value;
    var keywords = doc.DocumentNode.SelectSingleNode("//meta[@name='keywords']")?.Attributes["content"]?.Value;
    var heading = doc.DocumentNode.SelectSingleNode("//div[@class='page_title']/h1")?.InnerText;
    var heroImage = doc.DocumentNode.SelectSingleNode("//div[@class='hero-banner-inner']/img")?.Attributes["src"]?.Value;
    var publicationDate = doc.DocumentNode.SelectSingleNode("//div[@class='page_title']/p/i")?.InnerText;
    if(!string.IsNullOrEmpty(publicationDate))
    {
        DateTime date;
        if(DateTime.TryParseExact(publicationDate, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            publicationDate = date.ToString("yyyy-MMM-dd");
        }
    }
    var authorFirstName = doc.DocumentNode.SelectSingleNode("//div[@class='author']/p/i")?.InnerText;
    
    if(!string.IsNullOrEmpty(title)) frontMatter["Title"] = title;
    if(!string.IsNullOrEmpty(description)) frontMatter["Description"] = description;
    if(!string.IsNullOrEmpty(keywords)) frontMatter["Keywords"] = keywords;
    if(!string.IsNullOrEmpty(heading)) frontMatter["Heading"] = heading;
    if(!string.IsNullOrEmpty(heroImage)) frontMatter["HeroImage"] = heroImage;
    if(!string.IsNullOrEmpty(publicationDate)) frontMatter["PublicationDate"] = publicationDate;
    if(!string.IsNullOrEmpty(authorFirstName)) frontMatter["AuthorFirstName"] = authorFirstName;

    return frontMatter;
}

// function to save the markdown to a file
void SaveMarkdown(string markdown, string filename){
    File.WriteAllText(filename, markdown);
}

// download a page and save it as markdown
async Task DownloadAndSavePage(string baseUrl, string url, Dictionary<string,string> options){
    if(!url.StartsWith(baseUrl)) {
      Console.WriteLine($"Skipping {url} because it doesn't start with {baseUrl}");
      return;
    }

    var html = await DownloadPage(url);
    var content = ParsePage(html, options["class"]);
    if(content == null){
      Console.WriteLine($"Skipping {url} because it doesn't contain a div with class {options["class"]}");
      return;
    }

    var markdown = HtmlToMarkdown(content);

    var frontMatter = ExtractFrontMatter(html, options);

    //format frontMatter as yaml
    var yaml = new YamlDotNet.Serialization.SerializerBuilder()
        //.WithNamingConvention(new CamelCaseNamingConvention())
        .Build()
        .Serialize(frontMatter);

    //add front matter to the markdown
    markdown = "---" + Environment.NewLine + yaml + "---" + Environment.NewLine + markdown;

    var filename = url.Replace(baseUrl + "/", "") + ".md";
    var path = Path.Combine(options["output"], filename);

    //create the folder if it doesn't exist
    var folder = Path.GetDirectoryName(path);
    if (!Directory.Exists(folder)) {
      Directory.CreateDirectory(folder);
    }

    SaveMarkdown(markdown, path);
}