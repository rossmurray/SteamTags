using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SteamTags
{
	class Program
	{
		static void Main(string[] args)
		{
			//if (args.Length < 2) return;
			var p = new Program();
			//var game = args.Skip(1).Aggregate((a, b) => a + " " + b);
			//var target = args[0];
			var target = @"D:\temp\ross.txt";
			var game = "sonic";
			var work = Task.Factory.StartNew(() =>
			{
				var info = p.GetGameInfo(game);
				if (info == null)
				{
					File.WriteAllText(target, "Could not find " + game);
				}
				else
				{
					var title = info.Item1;
					var tags = info.Item2;
					if (tags == null || !tags.Any())
					{
						File.WriteAllText(target, "Could not parse page for " + game);
						return;
					}
					var top8 = tags.OrderByDescending(x => x.count).Take(15).Select(x => x.name).Aggregate((a, b) => a + ", " + b);
					var output = title + ": " + top8;
					File.WriteAllText(target, output);
				}
			}).ContinueWith((t) => { p.Log(t.Exception.Flatten().ToString()); }, TaskContinuationOptions.OnlyOnFaulted);
			try
			{
				work.Wait((int)(TimeSpan.FromSeconds(8.0).TotalMilliseconds));
			}
			catch (Exception ex)
			{
				p.Log(ex.ToString());
			}
		}

		private void Log(string s)
		{
			var file = new FileInfo(Path.Combine(Path.GetDirectoryName(Assembly.GetAssembly(typeof(Program)).FullName), "err.txt")).FullName;
			File.AppendAllText(file, Environment.NewLine + s);
		}

		public string GetAppId(string searchTerm)
		{
			var url = @"http://store.steampowered.com/search/?term=";
			var page = GETString(url + searchTerm);
			var pattern = @"http://store.steampowered.com/app/([0-9]+)/";
			var match = Regex.Match(page, pattern);
			var appid = match.Groups[1].Value;
			return appid;
		}

		public Tuple<string, IEnumerable<Tag>> GetGameInfo(string gameSearch)
		{
			var id = GetAppId(gameSearch);
			if (string.IsNullOrWhiteSpace(id)) return null;
			Log(gameSearch + " - found id: [" + id + "]");
			return GetTitleAndTagsFromAppId(id);
		}

		public Tuple<string, IEnumerable<Tag>> GetTitleAndTagsFromAppId(string appId)
		{
			var url = @"http://store.steampowered.com/app/" + appId;
			var page = GETString(url);
			//var json = "[" + new string(page.ToCharArray().Reverse().SkipWhile(c => c != ',').Skip(1).TakeWhile(c => c != '[').Reverse().ToArray());
			var tagmatch = Regex.Match(page, @"InitAppTagModal\(\s*?(\d+),\s*?(\[.+?\])");
			if (!tagmatch.Success)
			{
				var ageMatch = Regex.Match(page, @"Please enter your birth date to continue");
				if(!ageMatch.Success)
				{
					return Tuple.Create("", Enumerable.Empty<Tag>());
				}
				else
				{
					var ageUrl = @"http://store.steampowered.com/agecheck/app/" + appId + "/";
					using(var wc = new WebClient())
					{
						wc.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
						wc.Encoding = Encoding.UTF8;
						//wc.Headers[HttpRequestHeader.Accept] = "application/json";
						var submitData = new NameValueCollection();
						//submitData.Add("snr", "1_agecheck_agecheck__age-gate");
						submitData.Add("ageDay", "1");
						submitData.Add("ageMonth", "May");
						submitData.Add("ageYear", "1980");
						var result = wc.UploadValues(ageUrl, "POST", submitData);
						Log("got2: " + Encoding.UTF8.GetString(result));
					}
					page = GETString(url);
					tagmatch = Regex.Match(page, @"InitAppTagModal\(\s*?(\d+),\s*?(\[.+?\])");
				}
			}
			var json = tagmatch.Groups[2].Value;
			var tags = Json.Deserialize<Tag[]>(json);
			var titlematch = Regex.Match(page, "<title>(.*?)</title>");
			var title = titlematch.Groups[1].Value;
			title = new string(title.ToCharArray().Reverse().Skip(9).Reverse().ToArray());
			title = title.Replace("&trade;", "™");
			title = title.Replace("&reg;", "®");
			return Tuple.Create(title, tags.AsEnumerable() ?? Enumerable.Empty<Tag>());
		}

		public static string GETString(string url)
		{
			using (var wc = new WebClient())
			{
				wc.Encoding = Encoding.UTF8;
				//wc.Headers[HttpRequestHeader.UserAgent] = "LINQPad/4.0 (Windows NT 6.1; WOW64; U; en) .NETCLR/WebClient Version/4.0";
				return wc.DownloadString(url);
			}
		}
	}

	public class Tag
	{
		public string tagid { get; set; }
		public int count { get; set; }
		public string name { get; set; }
	}

	public class Json
	{
		public static string Serialize(object o)
		{
			var json = JsonConvert.SerializeObject(o, GetSerializerSettings());
			return json;
		}

		public static string SerializePrettyPrint(object o)
		{
			return JsonConvert.SerializeObject(o, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include, DateFormatHandling = DateFormatHandling.IsoDateFormat, Formatting = Newtonsoft.Json.Formatting.Indented });
		}

		public static dynamic Deserialize(string json, Type type)
		{
			dynamic o = JsonConvert.DeserializeObject(json, type, GetSerializerSettings());
			return o;
		}

		public static T Deserialize<T>(string json)
		{
			return JsonConvert.DeserializeObject<T>(json, GetSerializerSettings());
		}

		public static dynamic DeserializeAnonymous(string json)
		{
			return JsonConvert.DeserializeObject(json);
		}

		private static JsonSerializerSettings GetSerializerSettings()
		{
			return new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, DateFormatHandling = DateFormatHandling.IsoDateFormat };
		}
	}
}
