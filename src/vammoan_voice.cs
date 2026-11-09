using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using SimpleJSON;
using System.Linq;
using System.IO;
using MeshVR;
using Request = MeshVR.AssetLoader.AssetBundleFromFileRequest;
using MVR;

// VAMMoan
//
// Partial : voice

namespace VAMMoanPlugin
{
    public partial class VAMMoan : MVRScript
    {
		public class Voice
		{
			public string rootPath;
			public string voicePath;
			public string name;
			
			public string infos;
			public string infosShared;
			
			public List<string> lastMoanList;
			public List<string> lastPerpOrgList;
			public List<string> lastKissList;
			public List<string> lastBjList;
			public List<string> lastPslList;
			public List<string> lastSqsList;
			public List<string> lastSqspList;
			
			public int moanRepetitionBuffer;
			
			public JSONClass voiceConfig;
			public JSONClass voiceSharedConfig;

			List<AudioClip> audioClips = new List<AudioClip>();
			List<AudioClip> sharedAudioClips = new List<AudioClip>();

			// 音频索引：文件名（不含扩展名）→ 完整路径
			private Dictionary<string, string> audioIndex = null;
			private Dictionary<string, string> sharedIndex = null;

			// 解码协程与代号：代号变了说明音色被切换/卸载，旧协程应当丢弃结果
			private Coroutine loadRoutine = null;
			private int generation = 0;

			private Voices voicesInstance;
		
			public Voice(string rootPath, string voicePath, string name, Voices voicesInst)
			{
				this.rootPath = rootPath;
				this.voicePath = voicePath;
				this.name = name;
				
				this.voicesInstance = voicesInst;
			}

			public void Load()
			{
				voiceConfig = LoadVoiceConfig();
				voiceSharedConfig = LoadVoiceSharedConfig();
				LoadAudio();
				
				lastMoanList = new List<string>();
				lastPerpOrgList = new List<string>();
				lastKissList = new List<string>();
				lastBjList = new List<string>();
				lastPslList = new List<string>();
				lastSqsList = new List<string>();
				lastSqspList = new List<string>();
				
				moanRepetitionBuffer = int.Parse( voiceConfig["config"]["settings"]["moanRepetitionBuffer"].Value );
			}

			JSONClass LoadVoiceConfig()
			{
				JSONClass json = null;
				try
				{
					json = JSON.Parse(SuperController.singleton.ReadFileIntoString(voicePath + "/voice.json")).AsObject;
				}
				catch(Exception e)
				{
					Debug.LogWarning(e);
				}
				
				infos = json["config"]["informations"].Value;
				
				return json;
			}

			JSONClass LoadVoiceSharedConfig()
			{
				JSONClass json = null;
				
				try
				{
					json = JSON.Parse(SuperController.singleton.ReadFileIntoString(rootPath + "/jsondb/voice-shared.json")).AsObject;
				}
				catch(Exception e)
				{
					Debug.LogWarning(e);
				}
				
				//SuperController.singleton.ClearMessages();
				infosShared = json["config"]["informations"].Value;
				return json;
			}
			
			/// <summary>
			/// 直接从磁盘读取 BASS 内核支持的音频并解码：wav / ogg / mp3 / mp2 / mp1 / aif / aiff / aifc
			/// （白名单见 vammoan_voices.cs 的 IndexAudioRecursive；flac / wma / aac 等需要附加 dll，不收）。
			/// 解码是协程，所以 audioClips 是渐进填充的：还没解出来的片段查询会返回 null，
			/// 调用方本来就都做了 null 判断，因此不会出错，只是开头一两秒可能没声音。
			/// </summary>
			void LoadAudio()
			{
				generation++;

				if (loadRoutine != null && voicesInstance.host != null)
				{
					voicesInstance.host.StopCoroutine(loadRoutine);
					loadRoutine = null;
				}

				if (voicesInstance.host == null) return;
				loadRoutine = voicesInstance.host.StartCoroutine(LoadAudioRoutine(generation));
			}

			IEnumerator LoadAudioRoutine(int gen)
			{
				// 索引只建一次，切换回同一个音色时复用
				if (audioIndex == null)
				{
					audioIndex = voicesInstance.IndexAudio(Voices.AUDIO_PATH + "/voices/" + name.ToLowerInvariant(), 0);
				}
				if (sharedIndex == null)
				{
					// 共用音效按类别分了子目录（blowjob / breathing / kissing / pelvicslap / squishes）
					sharedIndex = voicesInstance.IndexAudio(Voices.AUDIO_PATH + "/voices-shared", 2);
				}

				List<string> audioPaths = GetAudioPaths();
				List<string> sharedAudioPaths = GetSharedAudioPaths();

				yield return LoadClips(audioPaths, audioIndex, audioClips, gen);

				if (gen != generation) yield break;

				yield return LoadClips(sharedAudioPaths, sharedIndex, sharedAudioClips, gen);

				loadRoutine = null;
			}

			IEnumerator LoadClips(List<string> paths, Dictionary<string, string> index, List<AudioClip> target, int gen)
			{
				int missing = 0;

				for (int i = 0; i < paths.Count; i++)
				{
					if (gen != generation) yield break;

					string key = PathExt.GetFileNameWithoutExtension(paths[i]);
					if (string.IsNullOrEmpty(key)) continue;

					string fullPath;
					if (index == null || !index.TryGetValue(key, out fullPath))
					{
						missing++;
						continue;
					}

					AudioClip clip = null;
					yield return voicesInstance.Decode(fullPath, delegate(AudioClip c) { clip = c; });

					if (clip == null)
					{
						missing++;
						continue;
					}

					// 查询是按 AudioClip.name 匹配的，必须用文件名（BassImporter 建出来的名字是空的）
					clip.name = key;

					if (gen == generation) target.Add(clip);
					else UnityEngine.Object.Destroy(clip);
				}

				if (missing > 0)
				{
					SuperController.LogMessage("VAMMoan : " + name + " - " + missing + " audio sample(s) missing or failed to decode.");
				}
			}
			
			public void Unload()
			{
				UnloadAudio();
			}

			void UnloadAudio()
			{
				generation++;

				if (loadRoutine != null && voicesInstance.host != null)
				{
					voicesInstance.host.StopCoroutine(loadRoutine);
					loadRoutine = null;
				}

				// 解码出来的 AudioClip 是运行时创建的，必须显式销毁，否则换音色会一直累积
				for (int i = 0; i < audioClips.Count; i++)
				{
					if (audioClips[i] != null) UnityEngine.Object.Destroy(audioClips[i]);
				}
				for (int i = 0; i < sharedAudioClips.Count; i++)
				{
					if (sharedAudioClips[i] != null) UnityEngine.Object.Destroy(sharedAudioClips[i]);
				}

				// Clearing lists
				audioClips.Clear();
				sharedAudioClips.Clear();
				if (lastMoanList != null) lastMoanList.Clear();
				if (lastPerpOrgList != null) lastPerpOrgList.Clear();
				if (lastKissList != null) lastKissList.Clear();
				if (lastBjList != null) lastBjList.Clear();
				if (lastSqsList != null) lastSqsList.Clear();
				if (lastSqspList != null) lastSqspList.Clear();
			}

			List<string> GetAudioPaths()
			{	
				List<string> audioPaths = new List<string>();
				
				// Moans
				JSONArray audioPathsMoansJSON = voiceConfig["moans"].AsArray;
				for(int i=0; i<audioPathsMoansJSON.Count; i++)
				{
					
					JSONClass entry = audioPathsMoansJSON[i].AsObject;
					audioPaths.Add(entry["audio"].Value);
				}
				
				// Orgasms
				JSONArray audioPathsOrgasmsJSON = voiceConfig["orgasms"].AsArray;
				for(int i=0; i<audioPathsOrgasmsJSON.Count; i++)
				{
					
					JSONClass entry = audioPathsOrgasmsJSON[i].AsObject;
					audioPaths.Add(entry["audio"].Value);
				}
				
				// Perpetual Orgasms
				JSONArray audioPathsPerpOrgJSON = voiceConfig["perpetual_orgasms"].AsArray;
				for(int i=0; i<audioPathsPerpOrgJSON.Count; i++)
				{
					
					JSONClass entry = audioPathsPerpOrgJSON[i].AsObject;
					audioPaths.Add(entry["audio"].Value);
				}
				
				return audioPaths;
			}
			
			List<string> GetSharedAudioPaths()
			{	
				List<string> sharedAudioPaths = new List<string>();
				
				// Kisses
				JSONArray audioPathsKissesJSON = voiceSharedConfig[GetAudioGenre()]["kisses"].AsArray;
				for(int i=0; i<audioPathsKissesJSON.Count; i++)
				{
					
					JSONClass entry = audioPathsKissesJSON[i].AsObject;
					sharedAudioPaths.Add(entry["audio"].Value);
				}
				
				// Breathes
				JSONArray audioPathsBreathesJSON = voiceSharedConfig[GetAudioGenre()]["breathes"].AsArray;
				for(int i=0; i<audioPathsBreathesJSON.Count; i++)
				{
					
					JSONClass entry = audioPathsBreathesJSON[i].AsObject;
					sharedAudioPaths.Add(entry["audio"].Value);
				}
				
				// Blowjobs
				JSONArray audioPathsBlowjobsJSON = voiceSharedConfig[GetAudioGenre()]["blowjobs"].AsArray;
				for(int i=0; i<audioPathsBlowjobsJSON.Count; i++)
				{
					
					JSONClass entry = audioPathsBlowjobsJSON[i].AsObject;
					sharedAudioPaths.Add(entry["audio"].Value);
				}
				
				// Pelvic Slaps
				JSONArray audioPathsPelvicSlapsJSON = voiceSharedConfig["pelvicslaps"].AsArray;
				for(int i=0; i<audioPathsPelvicSlapsJSON.Count; i++)
				{
					
					JSONClass entry = audioPathsPelvicSlapsJSON[i].AsObject;
					sharedAudioPaths.Add(entry["audio"].Value);
				}
				
				// Squishes
				JSONArray audioPathsSquishesJSON = voiceSharedConfig["squishes"].AsArray;
				for(int i=0; i<audioPathsSquishesJSON.Count; i++)
				{
					
					JSONClass entry = audioPathsSquishesJSON[i].AsObject;
					sharedAudioPaths.Add(entry["audio"].Value);
				}
				
				// Squishes
				JSONArray audioPathsSquishespJSON = voiceSharedConfig["squishes_penis"].AsArray;
				for(int i=0; i<audioPathsSquishespJSON.Count; i++)
				{
					
					JSONClass entry = audioPathsSquishespJSON[i].AsObject;
					sharedAudioPaths.Add(entry["audio"].Value);
				}
				
				return sharedAudioPaths;
			}
			
			public string GetAudioGenre() {
				int genderId = int.Parse(voiceConfig["config"]["settings"]["gender"].Value);
				return genderId == 0 ? "female" : "male";
			}

			public AudioClip GetTriggeredAudioMoan(float arousal)
			{
				if (audioClips.Count == 0)
				{
					return null;
				}

				if (voiceConfig == null)
				{
					return GetRandomAudioClip();
				}
				else
				{
					JSONArray moans = voiceConfig["moans"].AsArray;
					List<JSONClass> validMoans = new List<JSONClass>();
					for(int i=0; i<moans.Count; i++)
					{
						JSONClass moan = moans[i].AsObject;
						float minArousal = moan["minArousal"].AsFloat;
						float maxArousal = moan["maxArousal"].AsFloat;
						string currentClipName = PathExt.GetFileNameWithoutExtension(moan["audio"].Value);
														
						float normalizedArousal = (float)Math.Floor(arousal);

						if ( !lastMoanList.Any(currentClipName.Contains) && normalizedArousal >= minArousal && normalizedArousal <= maxArousal)
						{
							validMoans.Add(moan);
						}
					}

					if (validMoans.Count == 0)
					{
						validMoans.Add(moans[0].AsObject);
					}
					
					int randomIndex = UnityEngine.Random.Range(0, validMoans.Count);
					JSONClass picked = validMoans[randomIndex].AsObject;
					string clipName = PathExt.GetFileNameWithoutExtension(picked["audio"].Value);
					
					lastMoanList.Add(clipName);
					if( lastMoanList.Count > moanRepetitionBuffer ) {
						lastMoanList.RemoveAt(0);
						lastMoanList.TrimExcess();
					}				
					
					return audioClips.Find((ac) =>
					{
						return ac.name == PathExt.GetFileNameWithoutExtension(clipName);
					});
				}
			}
			
			public AudioClip GetTriggeredAudioOrgasm()
			{
				if (audioClips.Count == 0)
				{
					return null;
				}

				if (voiceConfig == null)
				{
					return GetRandomAudioClip();
				}
				else
				{
					JSONArray orgasms = voiceConfig["orgasms"].AsArray;
					List<JSONClass> validOrgasms = new List<JSONClass>();
					for(int i=0; i<orgasms.Count; i++)
					{
						JSONClass orgasm = orgasms[i].AsObject;
						string currentClipName = PathExt.GetFileName(orgasm["audio"].Value);
														
						validOrgasms.Add(orgasm);
					}

					if (validOrgasms.Count == 0)
					{
						validOrgasms.Add(orgasms[0].AsObject);
					}
					
					int randomIndex = UnityEngine.Random.Range(0, validOrgasms.Count);
					JSONClass picked = validOrgasms[randomIndex].AsObject;
					string clipName = PathExt.GetFileName(picked["audio"].Value);
					
					return audioClips.Find((ac) =>
					{
						return ac.name == PathExt.GetFileNameWithoutExtension(clipName);
					});
				}
			}
			
			public AudioClip GetTriggeredAudioPerpetualOrgasm()
			{
				if (audioClips.Count == 0)
				{
					return null;
				}

				if (voiceConfig == null)
				{
					return GetRandomAudioClip();
				}
				else
				{
					JSONArray perpetualorgasms = voiceConfig["perpetual_orgasms"].AsArray;
					List<JSONClass> validPerpOrg = new List<JSONClass>();
					for(int i=0; i<perpetualorgasms.Count; i++)
					{
						JSONClass perpetualorgasm = perpetualorgasms[i].AsObject;
						string currentClipName = PathExt.GetFileName(perpetualorgasm["audio"].Value);
														
						if ( !lastPerpOrgList.Any(currentClipName.Contains) )
						{
							validPerpOrg.Add(perpetualorgasm);
						}
					}

					if (validPerpOrg.Count == 0)
					{
						validPerpOrg.Add(perpetualorgasms[0].AsObject);
					}
					
					int randomIndex = UnityEngine.Random.Range(0, validPerpOrg.Count);
					JSONClass picked = validPerpOrg[randomIndex].AsObject;
					string clipName = PathExt.GetFileName(picked["audio"].Value);

					lastPerpOrgList.Add(clipName);
					if( lastPerpOrgList.Count > 8 ) { //
						lastPerpOrgList.RemoveAt(0);
						lastPerpOrgList.TrimExcess();
					}
					
					return audioClips.Find((ac) =>
					{
						return ac.name == PathExt.GetFileNameWithoutExtension(clipName);
					});
				}
			}
			
			public AudioClip GetTriggeredAudioKiss()
			{
				if (sharedAudioClips.Count == 0)
				{
					return null;
				}

				if (voiceSharedConfig == null)
				{
					//return GetRandomAudioClip();
					return null;
				}
				else
				{
					JSONArray kisses = voiceSharedConfig[GetAudioGenre()]["kisses"].AsArray;
					List<JSONClass> validKisses = new List<JSONClass>();
					for(int i=0; i<kisses.Count; i++)
					{
						JSONClass kiss = kisses[i].AsObject;
						string currentClipName = PathExt.GetFileNameWithoutExtension(kiss["audio"].Value);
						if ( !lastKissList.Any(currentClipName.Contains) )
						{
							validKisses.Add(kiss);
						}
					}

					if (validKisses.Count == 0)
					{
						validKisses.Add(kisses[0].AsObject);
					}
					
					int randomIndex = UnityEngine.Random.Range(0, validKisses.Count);
					JSONClass picked = validKisses[randomIndex].AsObject;
					string clipName = PathExt.GetFileNameWithoutExtension(picked["audio"].Value);
					
					lastKissList.Add(clipName);
					if( lastKissList.Count > 5 ) {
						lastKissList.RemoveAt(0);
						lastKissList.TrimExcess();
					}
					
					return sharedAudioClips.Find((ac) =>
					{
						return ac.name == PathExt.GetFileNameWithoutExtension(clipName);
					});
				}
			}
			
			public AudioClip GetTriggeredAudioBlowjob( float intensity )
			{
				if (sharedAudioClips.Count == 0)
				{
					return null;
				}

				if (voiceSharedConfig == null)
				{
					return null;
				}
				else
				{
					JSONArray blowjobs = voiceSharedConfig[GetAudioGenre()]["blowjobs"].AsArray;
					List<JSONClass> validBjs = new List<JSONClass>();
					for(int i=0; i<blowjobs.Count; i++)
					{
						JSONClass blowjob = blowjobs[i].AsObject;
						float bjIntensity = blowjob["intensity"].AsFloat;
						string currentClipName = PathExt.GetFileNameWithoutExtension(blowjob["audio"].Value);

						if ( !lastBjList.Any(currentClipName.Contains) && intensity == bjIntensity)
						{
							validBjs.Add(blowjob);
						}
					}

					if (validBjs.Count == 0)
					{
						validBjs.Add(blowjobs[0].AsObject);
					}
					
					int randomIndex = UnityEngine.Random.Range(0, validBjs.Count);
					JSONClass picked = validBjs[randomIndex].AsObject;
					string clipName = PathExt.GetFileNameWithoutExtension(picked["audio"].Value);
					
					lastBjList.Add(clipName);
					if( lastBjList.Count > 25 ) {
						lastBjList.RemoveAt(0);
						lastBjList.TrimExcess();
					}				
					
					return sharedAudioClips.Find((ac) =>
					{
						return ac.name == PathExt.GetFileNameWithoutExtension(clipName);
					});
				}
			}
			
			public AudioClip GetTriggeredAudioBreath()
			{
				if (sharedAudioClips.Count == 0)
				{
					return null;
				}

				if (voiceSharedConfig == null)
				{
					return null;
				}
				else
				{
					JSONArray breathes = voiceSharedConfig[GetAudioGenre()]["breathes"].AsArray;
					List<JSONClass> validBreathes = new List<JSONClass>();
					for(int i=0; i<breathes.Count; i++)
					{
						JSONClass breath = breathes[i].AsObject;
						string currentClipName = PathExt.GetFileName(breath["audio"].Value);
														
						validBreathes.Add(breath);
					}

					if (validBreathes.Count == 0)
					{
						validBreathes.Add(breathes[0].AsObject);
					}
					
					int randomIndex = UnityEngine.Random.Range(0, validBreathes.Count);
					JSONClass picked = validBreathes[randomIndex].AsObject;
					string clipName = PathExt.GetFileNameWithoutExtension(picked["audio"].Value);
					
					return sharedAudioClips.Find((ac) =>
					{
						return ac.name == PathExt.GetFileNameWithoutExtension(clipName);
					});
				}
			}

			public AudioClip GetTriggeredAudioPelvicSlap( float intensity )
			{
				if (sharedAudioClips.Count == 0)
				{
					return null;
				}

				if (voiceSharedConfig == null)
				{
					return null;
				}
				else
				{
					intensity = Mathf.Round( intensity );
					JSONArray pelvicslaps = voiceSharedConfig["pelvicslaps"].AsArray;
					List<JSONClass> validPsl = new List<JSONClass>();
					for(int i=0; i<pelvicslaps.Count; i++)
					{
						JSONClass pelvicslap = pelvicslaps[i].AsObject;
						float pslIntensity = pelvicslap["intensity"].AsFloat;
						string currentClipName = PathExt.GetFileNameWithoutExtension(pelvicslap["audio"].Value);
						if ( !lastPslList.Any(currentClipName.Contains) && intensity == pslIntensity)
						{
							validPsl.Add(pelvicslap);
						}
					}

					if (validPsl.Count == 0)
					{
						validPsl.Add(pelvicslaps[0].AsObject);
					}
					
					int randomIndex = UnityEngine.Random.Range(0, validPsl.Count);
					JSONClass picked = validPsl[randomIndex].AsObject;
					string clipName = PathExt.GetFileNameWithoutExtension(picked["audio"].Value);
					
					lastPslList.Add(clipName);
					if( lastPslList.Count > 8 ) {
						lastPslList.RemoveAt(0);
						lastPslList.TrimExcess();
					}				
					
					return sharedAudioClips.Find((ac) =>
					{
						return ac.name == PathExt.GetFileNameWithoutExtension(clipName);
					});
				}
			}
			
			public AudioClip GetTriggeredAudioSquish( float intensity )
			{
				if (sharedAudioClips.Count == 0)
				{
					return null;
				}

				if (voiceSharedConfig == null)
				{
					return null;
				}
				else
				{
					intensity = Mathf.Round( intensity );
					JSONArray squishes = voiceSharedConfig["squishes"].AsArray;
					List<JSONClass> validSqs = new List<JSONClass>();
					for(int i=0; i<squishes.Count; i++)
					{
						JSONClass squish = squishes[i].AsObject;
						float sqsIntensity = squish["intensity"].AsFloat;
						string currentClipName = PathExt.GetFileNameWithoutExtension(squish["audio"].Value);
						if ( !lastSqsList.Any(currentClipName.Contains) && intensity == sqsIntensity)
						{
							validSqs.Add(squish);
						}
					}

					if (validSqs.Count == 0)
					{
						validSqs.Add(squishes[0].AsObject);
					}
					
					int randomIndex = UnityEngine.Random.Range(0, validSqs.Count);
					JSONClass picked = validSqs[randomIndex].AsObject;
					string clipName = PathExt.GetFileNameWithoutExtension(picked["audio"].Value);
					
					lastSqsList.Add(clipName);
					if( lastSqsList.Count > 14 ) {
						lastSqsList.RemoveAt(0);
						lastSqsList.TrimExcess();
					}				
					
					return sharedAudioClips.Find((ac) =>
					{
						return ac.name == PathExt.GetFileNameWithoutExtension(clipName);
					});
				}
			}
			
			public AudioClip GetTriggeredAudioSquishPenis( float intensity )
			{
				if (sharedAudioClips.Count == 0)
				{
					return null;
				}

				if (voiceSharedConfig == null)
				{
					return null;
				}
				else
				{
					intensity = Mathf.Round( intensity );
					JSONArray squishesp = voiceSharedConfig["squishes_penis"].AsArray;
					List<JSONClass> validSqsp = new List<JSONClass>();
					for(int i=0; i<squishesp.Count; i++)
					{
						JSONClass squishp = squishesp[i].AsObject;
						float sqsIntensity = squishp["intensity"].AsFloat;
						string currentClipName = PathExt.GetFileNameWithoutExtension(squishp["audio"].Value);
						if ( !lastSqspList.Any(currentClipName.Contains) && intensity == sqsIntensity)
						{
							validSqsp.Add(squishp);
						}
					}

					if (validSqsp.Count == 0)
					{
						validSqsp.Add(squishesp[0].AsObject);
					}
					
					int randomIndex = UnityEngine.Random.Range(0, validSqsp.Count);
					JSONClass picked = validSqsp[randomIndex].AsObject;
					string clipName = PathExt.GetFileNameWithoutExtension(picked["audio"].Value);
					
					lastSqspList.Add(clipName);
					if( lastSqspList.Count > 24 ) {
						lastSqspList.RemoveAt(0);
						lastSqspList.TrimExcess();
					}				
					
					return sharedAudioClips.Find((ac) =>
					{
						return ac.name == PathExt.GetFileNameWithoutExtension(clipName);
					});
				}
			}
			
			AudioClip GetRandomAudioClip()
			{
				if (audioClips.Count == 0)
				{
					return null;
				}

				int index = Mathf.Clamp(UnityEngine.Random.Range(0, audioClips.Count), 0, audioClips.Count - 1);
				return audioClips[index];
			}
			
			public void OnDestroy()
			{
				UnloadAudio();
			}
		}
	}
}