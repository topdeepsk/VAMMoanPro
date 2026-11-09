using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using SimpleJSON;
using System.Linq;
using MeshVR;
using MVR.FileManagementSecure;

// VAMMoan
//
// Partial : voices
//
// 音频不再使用 AssetBundle，改为直接读取磁盘上的音频文件：
//   audio/jsondb/<音色>/voice.json            ← 音色配置
//   audio/jsondb/voice-shared.json            ← 共用配置
//   audio/voicebundle/voices/<音色>/*           ← 该音色的呻吟
//   audio/voicebundle/voices-shared/*/*         ← 共用音效（blowjob / breathing / kissing / pelvicslap / squishes）
// 支持 BASS 内核原生解码的格式：WAV / AIFF / MP3 / MP2 / MP1 / OGG
// （FLAC 等需要 bassflac 等附加 dll，VaM 未自带，故不支持）。
// 解码用 VaM 自带的 HelloMeow.BassImporter（内部走 BASS）。

namespace VAMMoanPlugin
{
    public partial class VAMMoan : MVRScript
    {
		public class Voices
		{
			public static string VOICES_PATH;    // <plugin>/audio
			public static string JSONDB_PATH;    // <plugin>/audio/jsondb
			public static string AUDIO_PATH;     // <plugin>/audio/voicebundle

			public string VoicesInformations = "";

			public List<string> voicesNames;
			Dictionary<string, Voice> nameToVoice = new Dictionary<string, Voice>();

			// 没有异步加载流程了，永远为 false（保留字段，外部代码还在判断它）
			public bool isLoading = false;

			// 协程宿主（插件本体）与解码器
			public MonoBehaviour host;
			private HelloMeow.BassImporter decoder;
			private GameObject decoderGO;

			public Voices(MonoBehaviour host)
			{
				try
				{
					this.host = host;

					VOICES_PATH = ASSETS_PATH;
					JSONDB_PATH = VOICES_PATH + "/jsondb";
					AUDIO_PATH = VOICES_PATH + "/voicebundle";

					voicesNames = new List<string>();
					nameToVoice.Clear();

					List<string> dirs = ListDirs(JSONDB_PATH);
					dirs.Sort(StringComparer.OrdinalIgnoreCase);

					for (int i = 0; i < dirs.Count; i++)
					{
						string name = PathExt.GetFileName(dirs[i]);
						if (string.IsNullOrEmpty(name)) continue;

						voicesNames.Add(name);
						nameToVoice[name] = new Voice(VOICES_PATH, JSONDB_PATH + "/" + name, name, this);
					}

					if (voicesNames.Count == 0)
					{
						SuperController.LogError("VAMMoan : no voice found in " + JSONDB_PATH);
					}
				}
				catch (Exception e)
				{
					Debug.LogWarning(e);
				}
			}

			public Voice GetVoice(string name)
			{
				if (nameToVoice.ContainsKey(name))
				{
					return nameToVoice[name];
				}
				else
				{
					return null;
				}
			}

			public Voice GetRandomVoice()
			{
				int randomIndex = Mathf.Clamp(UnityEngine.Random.Range(0, voices.Count), 0, voices.Count - 1);
				return voices[randomIndex];
			}

			// ------------------------------------------------------------------
			// 目录 / 文件索引
			// ------------------------------------------------------------------

			/// <summary>列出目录下的子目录（返回完整路径）。目录不存在时返回空列表。</summary>
			public List<string> ListDirs(string dir)
			{
				List<string> result = new List<string>();
				if (string.IsNullOrEmpty(dir)) return result;

				try
				{
					string[] dirs = SuperController.singleton.GetDirectoriesAtPath(dir, null);
					if (dirs != null) result.AddRange(dirs);
				}
				catch (Exception)
				{
					// 目录不存在，忽略
				}
				return result;
			}

			/// <summary>
			/// 递归建立「文件名（不含扩展名）→ 完整路径」索引。
			/// depth 为继续向下展开的层数，0 表示只看当前目录。
			/// 同一名字出现多次时保留第一条（音色目录是平的，共用目录按类别分开，重名不影响）。
			/// </summary>
			public Dictionary<string, string> IndexAudio(string dir, int depth)
			{
				Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
				IndexAudioRecursive(dir, depth, map);
				return map;
			}

			private void IndexAudioRecursive(string dir, int depth, Dictionary<string, string> map)
			{
				if (string.IsNullOrEmpty(dir)) return;

				string[] files = null;
				try
				{
					files = SuperController.singleton.GetFilesAtPath(dir, null);
				}
				catch (Exception)
				{
					files = null;
				}

				if (files != null)
				{
					for (int i = 0; i < files.Length; i++)
					{
						string ext = PathExt.GetExtension(files[i]);
						if (string.IsNullOrEmpty(ext)) continue;
						ext = ext.ToLowerInvariant();
						// BASS 内核原生支持的格式（不需要额外插件）：
						// WAV / AIFF / MP3 / MP2 / MP1 / OGG。FLAC 等需要附加 dll，VaM 没带，故不收。
						if (ext != ".wav" && ext != ".ogg"
							&& ext != ".mp3" && ext != ".mp2" && ext != ".mp1"
							&& ext != ".aif" && ext != ".aiff" && ext != ".aifc") continue;

						string key = PathExt.GetFileNameWithoutExtension(files[i]);
						if (string.IsNullOrEmpty(key)) continue;
						if (!map.ContainsKey(key)) map[key] = files[i];
					}
				}

				if (depth <= 0) return;

				List<string> dirs = ListDirs(dir);
				for (int i = 0; i < dirs.Count; i++)
				{
					IndexAudioRecursive(dirs[i], depth - 1, map);
				}
			}

			// ------------------------------------------------------------------
			// 解码
			// ------------------------------------------------------------------

			private bool EnsureDecoder()
			{
				if (decoder != null) return true;
				if (host == null) return false;

				try
				{
					decoderGO = new GameObject("VAMMoan_Decoder");
					decoderGO.transform.SetParent(host.transform, false);
					decoder = decoderGO.AddComponent<HelloMeow.BassImporter>();
					return decoder != null;
				}
				catch (Exception e)
				{
					SuperController.LogError("VAMMoan : cannot create audio decoder - " + e.Message);
					return false;
				}
			}

			/// <summary>
			/// 读取并解码一个音频文件。失败时回调 null（不抛异常）。
			/// </summary>
			public IEnumerator Decode(string path, Action<AudioClip> done)
			{
				if (done == null) yield break;

				if (!EnsureDecoder())
				{
					done(null);
					yield break;
				}

				byte[] bytes = null;
				try
				{
					bytes = FileManagerSecure.ReadAllBytes(path);
				}
				catch (Exception e)
				{
					Debug.LogWarning(e);
				}

				if (bytes == null || bytes.Length == 0)
				{
					done(null);
					yield break;
				}

				AudioClip decoded = null;
				Action<AudioClip> onLoaded = delegate(AudioClip c) { decoded = c; };

				decoder.Loaded += onLoaded;
				yield return host.StartCoroutine(decoder.SetData(bytes));
				decoder.Loaded -= onLoaded;

				done(decoded);
			}

			public void UnloadAudio()
			{
				if (decoderGO != null)
				{
					UnityEngine.Object.Destroy(decoderGO);
					decoderGO = null;
				}
				decoder = null;
			}

			public List<Voice> voices
			{
				get
				{
					return nameToVoice.Values.ToList();
				}
			}
		}
	}
}
