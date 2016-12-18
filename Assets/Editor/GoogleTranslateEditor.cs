using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System;

/// <summary>
/// Unity editor for performing google translations.
/// </summary>
public class GoogleTranslateEditor : EditorWindow {

	/// <summary>
	/// Reference to editor window.
	/// </summary>
	private static GoogleTranslateEditor m_window;

	/// <summary>
	/// Language to translate from.
	/// </summary>
	private string m_languageFrom = string.Empty;

	/// <summary>
	/// Language to translate to.
	/// </summary>
	private string m_languageTo = string.Empty;

	/// <summary>
	/// CSV (comma separated value) file of strings to translate.
	/// </summary>
	private TextAsset m_csv = null;

	/// <summary>
	/// The output list of translated strings.
	/// </summary>
	private List<string> m_output = new List<string>();

	/// <summary>
	/// Create editor window.
	/// </summary>
	[MenuItem("Tools/Google Translate")]
	static void Init() {
		m_window = (GoogleTranslateEditor)GetWindow(typeof(GoogleTranslateEditor));
		m_window.titleContent = new GUIContent("Tools");
	}

	/// <summary>
	/// Special unity gui function.
	/// </summary>
	public void OnGUI() {

		// Public fields
		m_csv = (TextAsset)EditorGUILayout.ObjectField("CSV File", m_csv, typeof(TextAsset), true);
		m_languageFrom = EditorGUILayout.TextField("Language From", m_languageFrom);
		m_languageTo = EditorGUILayout.TextField("Language To", m_languageTo);

		// Start button
		if (GUILayout.Button("Start Translation")) {
			Translate();
		}
	}

	/// <summary>
	/// Perform async google translation.
	/// </summary>
	private void Translate() {
		m_output.Clear();

		// Compute full asset path
		string dataPath = Application.dataPath.Replace("Assets", string.Empty);
		string assetPath = AssetDatabase.GetAssetPath(m_csv);
		string fullPath = dataPath + assetPath;

		// Parse input text file
		string[] lines = File.ReadAllLines(fullPath);
		int totalLines = lines.Length;
		int linesComplete = 0;
		double totalSeconds = 0.0;
		DateTime startTime = DateTime.Now;
		Debug.Log("Translating file: " + fullPath + " | totalLines: " + totalLines);

		// For each line of text in the csv
		foreach (var line in lines) {
			
			// Validate CSV row
			string[] tokens = line.Split(new char[] { ',' }, 2);
			if (tokens.Length == 2) {
				string key = tokens[0];
				string value = tokens[1];

				// Add new output entry
				int index = m_output.Count;
				m_output.Add(string.Empty);

				// Strip quotes before translating
				value = value.Replace("\"", string.Empty);

				// Begin translation
				GoogleTranslator t = new GoogleTranslator();
				t.Translate(value, m_languageFrom, m_languageTo, (text) => {
					linesComplete++;

					// Store translation (wrap the result in quotes)
					m_output[index] = key + ",\"" + text + "\"";

					// Print progress
					float percentComplete = 100.0f * ((float)linesComplete / totalLines);
					Debug.Log(percentComplete.ToString("0.0") + "%" + " | " + m_output[index]);
					totalSeconds += t.TranslationTime.TotalSeconds;

					// If this was the last translation, finish
					if (linesComplete >= m_output.Count) {
						string outputFilename = AddSuffixToFileName(fullPath, "-" + m_languageTo);
						File.WriteAllLines(outputFilename, m_output.ToArray());

						double time = (DateTime.Now - startTime).TotalSeconds;
						Debug.Log("Done! Finished in: " + time.ToString("0.0") + " seconds. Output file: " + outputFilename);
					}
				});
			}
		}
	}

	/// <summary>
	/// Addes a suffix to a given filename.
	/// </summary>
	/// <param name="filename">The filename to add a suffix to.</param>
	/// <param name="suffix">The suffix to add.</param>
	/// <returns>The filename with proper suffix.</returns>
	private string AddSuffixToFileName(string filename, string suffix) {
		return string.Format("{0}{1}{2}{3}",
			Path.GetDirectoryName(filename) + Path.AltDirectorySeparatorChar, 
			Path.GetFileNameWithoutExtension(filename), suffix, 
			Path.GetExtension(filename)
		);
	}
}
