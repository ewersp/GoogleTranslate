using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using UnityEngine;

/// <summary>
/// Translates text via google's web API.
/// Loosly based off existing implementation: https://www.codeproject.com/kb/ip/googletranslator.aspx
/// </summary>
public class GoogleTranslator {

	/// <summary>
	/// For matching {token} tokens.
	/// </summary>
	private const string kTokenRegex = @"\{([^}]+)}";

	/// <summary>
	/// Mapping of language names to their prefix abbreviations.
	/// </summary>
	private static Dictionary<string, string> kLanguagePrefixMap = new Dictionary<string, string> {
		{ "Afrikaans", "af" },
		{ "Albanian", "sq" },
		{ "Arabic", "ar" },
		{ "Armenian", "hy" },
		{ "Azerbaijani", "az" },
		{ "Basque", "eu" },
		{ "Belarusian", "be" },
		{ "Bengali", "bn" },
		{ "Bulgarian", "bg" },
		{ "Catalan", "ca" },
		{ "Chinese", "zh-CN" },
		{ "Croatian", "hr" },
		{ "Czech", "cs" },
		{ "Danish", "da" },
		{ "Dutch", "nl" },
		{ "English", "en" },
		{ "Esperanto", "eo" },
		{ "Estonian", "et" },
		{ "Filipino", "tl" },
		{ "Finnish", "fi" },
		{ "French", "fr" },
		{ "Galician", "gl" },
		{ "German", "de" },
		{ "Georgian", "ka" },
		{ "Greek", "el" },
		{ "Haitian Creole", "ht" },
		{ "Hebrew", "iw" },
		{ "Hindi", "hi" },
		{ "Hungarian", "hu" },
		{ "Icelandic", "is" },
		{ "Indonesian", "id" },
		{ "Irish", "ga" },
		{ "Italian", "it" },
		{ "Japanese", "ja" },
		{ "Korean", "ko" },
		{ "Lao", "lo" },
		{ "Latin", "la" },
		{ "Latvian", "lv" },
		{ "Lithuanian", "lt" },
		{ "Macedonian", "mk" },
		{ "Malay", "ms" },
		{ "Maltese", "mt" },
		{ "Norwegian", "no" },
		{ "Persian", "fa" },
		{ "Polish", "pl" },
		{ "Portuguese", "pt" },
		{ "Romanian", "ro" },
		{ "Russian", "ru" },
		{ "Serbian", "sr" },
		{ "Slovak", "sk" },
		{ "Slovenian", "sl" },
		{ "Spanish", "es" },
		{ "Swahili", "sw" },
		{ "Swedish", "sv" },
		{ "Tamil", "ta" },
		{ "Telugu", "te" },
		{ "Thai", "th" },
		{ "Turkish", "tr" },
		{ "Ukrainian", "uk" },
		{ "Urdu", "ur" },
		{ "Vietnamese", "vi" },
		{ "Welsh", "cy" },
		{ "Yiddish", "yi" },
	};

	/// <summary>
	/// The time it took for this request.
	/// </summary>
	public TimeSpan TranslationTime {
		get; private set;
	}

	/// <summary>
	/// The error, if one was generated.
	/// </summary>
	public Exception Error {
		get; private set;
	}

	/// <summary>
	/// Callback delegate definition.
	/// </summary>
	/// <param name="text">The translated text.</param>
	public delegate void TranslationComplete(string text);

	/// <summary>
	/// Allow valid certificates.
	/// http://stackoverflow.com/questions/4926676/mono-webrequest-fails-with-https
	/// </summary>
	public GoogleTranslator() {
		ServicePointManager.ServerCertificateValidationCallback += delegate(object s, X509Certificate c, X509Chain ch, SslPolicyErrors e) {
			return true;
		};
	}

	/// <summary>
	/// Primary method for translating text via google's web API.
	/// </summary>
	/// <param name="sourceText">The text to translate.</param>
	/// <param name="sourceLanguage">The language translating from.</param>
	/// <param name="targetLanguage">The language translating to.</param>
	/// <param name="callback">Callback when the translation finishes.</param>
	public void Translate(string sourceText, string sourceLanguage, string targetLanguage, TranslationComplete callback) {

		// Initialize
		Error = null;
		TranslationTime = TimeSpan.Zero;
		DateTime startTime = DateTime.Now;
		string translation = string.Empty;
		Dictionary<string, string> tokens = new Dictionary<string, string>();

		// Strip tokens before translating
		sourceText = StripTokensForGoogleTranslate(sourceText, out tokens);

		try {
			// Generate query url
			string url = string.Format("https://translate.googleapis.com/translate_a/single?client=gtx&sl={0}&tl={1}&dt=t&q={2}",
										GetLanguagePrefix(sourceLanguage),
										GetLanguagePrefix(targetLanguage),
										WWW.EscapeURL(sourceText));

			// Make request
			using (WebClient wc = new WebClient()) {
				wc.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2228.0 Safari/537.36");
				wc.Headers.Add(HttpRequestHeader.AcceptCharset, "UTF-8");
				wc.Encoding = System.Text.Encoding.UTF8;
				wc.DownloadStringCompleted += new DownloadStringCompletedEventHandler((object a, DownloadStringCompletedEventArgs b) => {
					
					// Process result and replace tokens after translating
					string result = ParseResult(sourceLanguage, b.Result);
					result = ReplaceTokensForGoogleTranslate(result, tokens);

					// Compute duration
					TranslationTime = DateTime.Now - startTime;

					// Inform caller
					if (callback != null) {
						callback(result);
					}
				});
				wc.DownloadStringAsync(new Uri(url));
			}
		} catch (Exception ex) {
			Error = ex;
		}
	}

	/// <summary>
	/// Converts long language name to shorter name.
	/// </summary>
	/// <param name="language">Long language name.</param>
	/// <returns>The language prefix.</returns>
	private static string GetLanguagePrefix(string language) {
		if (kLanguagePrefixMap.ContainsKey(language)) {
			return kLanguagePrefixMap[language];
		}
		return string.Empty;
	}

	/// <summary>
	/// Process raw result returned from google translation web API.
	/// </summary>
	/// <param name="sourceLanguage">The language we're translating from.</param>
	/// <param name="text">The raw string returned from translation API.</param>
	/// <returns>The translated text string.</returns>
	private string ParseResult(string sourceLanguage, string text) {
		string translation = "";

		// Get phrase collection
		int index = text.IndexOf(string.Format(",,\"{0}\"", GetLanguagePrefix(sourceLanguage)));
		if (index == -1) {
			// Translation of single word
			int startQuote = text.IndexOf('\"');
			if (startQuote != -1) {
				int endQuote = text.IndexOf('\"', startQuote + 1);
				if (endQuote != -1) {
					translation = text.Substring(startQuote + 1, endQuote - startQuote - 1);
				}
			}
		} else {
			// Translation of phrase
			text = text.Substring(0, index);
			text = text.Replace("],[", ",");
			text = text.Replace("]", "");
			text = text.Replace("[", "");
			text = text.Replace("\",\"", "\"");

			// Get translated phrases
			string[] phrases = text.Split(new[] { '\"' }, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; (i < phrases.Count()); i += 2) {
				string translatedPhrase = phrases[i];
				if (translatedPhrase.StartsWith(",,")) {
					i--;
					continue;
				}
				translation += translatedPhrase + "  ";
			}
		}

		return translation.Trim();
	}

	/// <summary>
	/// Strip tokens we don't want replaced before google translating.
	///	Example: "I have {count} of {total} [FF0000]Red[-] Apples." -> "I have {1} of {2} {3}Red{4} Apples."
	/// </summary>
	/// <param name="text">The text to strip the tokens from.</param>
	/// <param name="tokens">The output tokens that were stripped.</param>
	/// <returns>The input string with stripped tokens.</returns>
	private string StripTokensForGoogleTranslate(string text, out Dictionary<string, string> tokens) {
		tokens = new Dictionary<string, string>();

		if (text == null) {
			return string.Empty;
		}

		// Cannot use 'ref' or 'out' parameter inside anonymous functions, so use temp variable
		Dictionary<string, string> tempTokens = new Dictionary<string, string>();

		int index = 0;
		var rex = new Regex(kTokenRegex);
		string result = (rex.Replace(text, delegate (Match m) {
			string token = m.Groups[1].Value;
			tempTokens.Add((++index).ToString(), "{" + token + "}");
			return "{" + index.ToString() + "}";
		}));

		tokens = tempTokens;
		return result;
	}

	/// <summary>
	/// Replace tokens after translation completes.
	/// Example: "I have {1} of {2} {3}Red{4} Apples." -> "I have {count} of {total} [FF0000]Red[-] Apples."
	/// </summary>
	/// <param name="text">The text to replace tokens in.</param>
	/// <param name="extraTokens">The tokens we're replacing.</param>
	/// <returns>The input string with all tokens replaced.</returns>
	private string ReplaceTokensForGoogleTranslate(string text, Dictionary<string, string> extraTokens) {
		if (text == null) {
			return string.Empty;
		}

		var rex = new Regex(kTokenRegex);
		return (rex.Replace(text, delegate (Match m) {
			string token = m.Groups[1].Value;
			if (extraTokens.ContainsKey(token)) {
				return extraTokens[token];
			}
			return string.Empty;
		}));
	}
}
