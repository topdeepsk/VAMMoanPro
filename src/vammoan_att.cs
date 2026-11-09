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
using AssetBundles;
using MVR;

// VAMMoan
//
// Partial : attenuation curves

namespace VAMMoanPlugin
{
    public partial class VAMMoan : MVRScript
    {
		// Audio attenuation
		// Based on MacGruber's Audio attenuation and a custom curve of mine
		// that fits how the voice attenuates at longer distances
		private AnimationCurve originalAttenuationCurve;
		private AnimationCurve customAttenuationCurve;
		
		// ***************************************
		// Everything related to morphs animation
		// ***************************************
		
		// Initializing audio attenuation by saving the original rolloff
		private void initAttenuation() {
			originalAttenuationCurve = headAudioSource.audioSource.GetCustomCurve(AudioSourceCurveType.CustomRolloff);
			enableCustomAttenuation();
		}
			
		// Enabling Custom attenuation
		private void enableCustomAttenuation() {
			// ******** INIT CUSTOM ATT CURVE ***********************
			Keyframe[] ks = new Keyframe[5];
			ks[0] = new Keyframe(0f, 1f);
			ks[0].inTangent = -0.7945501f;
			ks[0].inWeight = 0.3333333f;
			ks[0].outTangent = -0.7945501f;
			ks[0].outWeight = 0.3333333f;
			ks[0].weightedMode = WeightedMode.None;

			ks[1] = new Keyframe(0.1287323f, 0.8977157f);
			ks[1].inTangent = -1.167509f;
			ks[1].inWeight = 0.4196278f;
			ks[1].outTangent = -1.167509f;
			ks[1].outWeight = 0.4759657f;
			ks[1].weightedMode = WeightedMode.None;

			ks[2] = new Keyframe(0.3494293f, 0.302229f);
			ks[2].inTangent = -1.380502f;
			ks[2].inWeight = 0.3333333f;
			ks[2].outTangent = -1.380502f;
			ks[2].outWeight = 0.06884071f;
			ks[2].weightedMode = WeightedMode.None;

			ks[3] = new Keyframe(0.8f, 0.1f);
			ks[3].inTangent = -0.2623334f;
			ks[3].inWeight = 0.3333333f;
			ks[3].outTangent = -0.2623334f;
			ks[3].outWeight = 0.3333333f;
			ks[3].weightedMode = WeightedMode.None;

			ks[4] = new Keyframe(1f, 0f);
			ks[4].inTangent = -0.5000001f;
			ks[4].inWeight = 0.3333333f;
			ks[4].outTangent = -0.5000001f;
			ks[4].outWeight = 0f;
			ks[4].weightedMode = WeightedMode.None;

			customAttenuationCurve = new AnimationCurve(ks);
			customAttenuationCurve.preWrapMode = WrapMode.ClampForever;
			customAttenuationCurve.postWrapMode = WrapMode.ClampForever;
					
			headAudioSource.audioSource.rolloffMode = AudioRolloffMode.Custom;
			headAudioSource.audioSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, customAttenuationCurve);
		}
		
		// Disabling/restoring audio attenuation
		private void disableCustomAttenuation() {
			headAudioSource.audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
			headAudioSource.audioSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, originalAttenuationCurve);
		}
	}
}