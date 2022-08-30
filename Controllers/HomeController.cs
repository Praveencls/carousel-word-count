using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using HtmlAgilityPack;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;

namespace CodeTest.Controllers
{
    public class ResponseData
    {
        public List<string> Slides { get; set; }
        public string Content { get; set; }
        public string WordcountReport { get; set; }
        public string Error { get; set; }
    }
    public class ImageDetail
    {
        public string Src { get; set; }
        public string Alt { get; set; }
        public string Width { get; set; }
        public string Height { get; set; }
        public string Class { get; set; }
    }
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public JsonResult AjaxMethod(string url, string noCountContent, string count)
        {
            ResponseData linkList = new ResponseData();
            if (string.IsNullOrEmpty(url.Trim()))
                linkList.Error = "Please, enter the url.";
            else
            {
                Uri address = new Uri(url);

                ServicePointManager.ServerCertificateValidationCallback += ValidateRemoteCertificate;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3;
                try
                {
                    using (WebClient webClient = new WebClient())
                    {
                        System.Net.ServicePointManager.ServerCertificateValidationCallback += (send, certificate, chain, sslPolicyErrors) => { return true; };
                        var stream = webClient.OpenRead(address);
                        using (StreamReader sr = new StreamReader(stream))
                        {
                            var page = sr.ReadToEnd();
                            HtmlDocument htmlDoc = new HtmlDocument();
                            htmlDoc.LoadHtml(page);
                            linkList = ParseHtml(htmlDoc, url, noCountContent, count);
                        }
                    }
                }
                catch (Exception ex)
                {
                    linkList.Error = ex.InnerException.ToString();
                }
            }
            return Json(linkList);
        }

        /// <summary>
        /// Certificate validation callback.
        /// </summary>
        private static bool ValidateRemoteCertificate(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors error)
        {
            // If the certificate is a valid, signed certificate, return true.
            if (error == System.Net.Security.SslPolicyErrors.None)
            {
                return true;
            }

            Console.WriteLine("X509Certificate [{0}] Policy Error: '{1}'",
                cert.Subject,
                error.ToString());

            return false;
        }

        private ResponseData ParseHtml(HtmlDocument htmlDoc, string url, string noCountContent, string count)
        {
            var images = htmlDoc.DocumentNode.SelectNodes("//img");
            List<string> imageNodes = new List<string>();
            ResponseData response = new ResponseData();

            int i = 0;
            if (images != null)
            {
                foreach (var node in images)
                {
                    if (node.Attributes["src"] == null)
                        continue;
                    var src = node.Attributes["src"].Value;
                    if (src.StartsWith("/"))
                        node.SetAttributeValue("src", url + src);

                    var newImage = new ImageDetail()
                    {
                        Alt = node.Attributes["alt"] != null ? node.Attributes["alt"].Value : "",
                        Src = node.Attributes["src"].Value,
                        Class = node.Attributes["class"] != null ? node.Attributes["class"].Value : "",
                        Height = node.Attributes["height"] != null ? node.Attributes["height"].Value : "",
                        Width = node.Attributes["width"] != null ? node.Attributes["width"].Value : ""
                    };

                    var activeClass = i == 0 ? "item active" : "item";
                    var slideItem = @"<div class='" + @activeClass + "'><div class='carousel-content'><div style = 'margin: 0 auto' ><p><img alt='" + @newImage.Alt + "' src='" + @newImage.Src + "' style='" + @newImage.Class + "' width='" + @newImage.Width + "' height='" + @newImage.Height + "' /></p></div></div></div>";

                    imageNodes.Add(slideItem);
                    i++;
                }
            }

            response.Slides = imageNodes;

            var paragraphs = htmlDoc.DocumentNode.SelectNodes("//div/p");
            if (paragraphs == null)
                paragraphs = htmlDoc.DocumentNode.SelectNodes("//p");

            StringBuilder paragraphNodes = new StringBuilder();
            if (paragraphs != null)
            {
                foreach (var node in paragraphs)
                {
                    if (!string.IsNullOrEmpty(node.InnerText))
                        paragraphNodes.Append(node.InnerText);
                }
            }
            response.Content = paragraphNodes.ToString();
            response.WordcountReport = GetWordCountReport(response.Content, count, noCountContent, url);
            return response;
        }


        private string GetWordCountReport(string textAreaContent, string count, string noCountContent, string url)
        {
            Dictionary<string, int> model = new Dictionary<string, int>();
            StringBuilder sb = new StringBuilder();
            if (!string.IsNullOrEmpty(textAreaContent))
            {
                char[] chars = { ' ', '.', ',', ';', ':', '?', '\n', '\r' };
                string[] words = textAreaContent.Split(chars);
                int minWordLength = 2;// to count words having more than 2 characters
                string[] noCountWords = noCountContent == null ? null : noCountContent.Split(',').Select(sValue => sValue.ToLower().Trim()).ToArray();
                // iterate over the word collection to count occurrences
                foreach (string word in words)
                {
                    if (word == "" || (noCountWords != null && noCountWords.Contains(word.ToLower().Trim())))
                        continue;

                    string w = word.Trim().ToLower();
                    if (w.Length > minWordLength)
                    {
                        if (!model.ContainsKey(w))
                        {
                            // add new word to collection
                            model.Add(w, 1);
                        }
                        else
                        {
                            // update word occurrence count
                            model[w] += 1;
                        }
                    }
                }

                int resultCount = !string.IsNullOrEmpty(count) ? Convert.ToInt32(count) : model.Count;
                // order the collection by word count
                model = model.Count > 0 ? model.OrderByDescending(x => x.Value).Take(resultCount).ToDictionary(x => x.Key, x => x.Value) : null;

                sb.Append(@"<div class='word-count-report'>
            <h2>Word Count Report</h2>
            <div>We found {0} words on the {1}</div>
            <table class='table-content'>
                <thead>
                    <tr>
                        <th class='word' style='min-width:200px;'><b>Word</b></th>
                        <th class='word-count'><b>Count</b></th>
                    </tr>
                </thead>").Replace("{0}", words.Length.ToString()).Replace("{1}", url);
                if (model != null && model.Count > 0)
                {
                    foreach (var pair in model)
                    {
                        sb.Append(@"<tr><td>" + pair.Key + "</td><td>" + pair.Value + "</td></tr>");

                    }
                }
                sb.Append(@"</table></div>");
            }

            return sb.ToString();
        }
    }
}
