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
// Partial : animation curves

namespace VAMMoanPlugin
{
    public partial class VAMMoan : MVRScript
    {
		// AnimationCurves
		private AnimationCurve ACBreathingCh;
		private AnimationCurve ACIntensity1Ch;
		private AnimationCurve ACIntensity2Ch;
		private AnimationCurve ACIntensity4Ch;
		private AnimationCurve ACOrgasmCh;
		
		private AnimationCurve ACBreathingSt;
		private AnimationCurve ACIntensity1St;

		
		// ***************************************
		// Everything related to morphs animation
		// ***************************************
		
		// A big function to create custom curves for my morphs animations
		private void initAnimationCurves() {
			
			// **************************************************
			// ********* CHEST
			// **************************************************
			
			// ******** BREATHING ***********************
			Keyframe[] ks = new Keyframe[3];
			ks[0] = new Keyframe(0f, 0f);
			ks[0].inTangent = 2f;
			ks[0].inWeight = 0.3333333f;
			ks[0].outTangent = 2f;
			ks[0].outWeight = 0.3333333f;
			ks[0].weightedMode = WeightedMode.None;

			ks[1] = new Keyframe(0.5f, 1f);
			ks[1].inTangent = 0f;
			ks[1].inWeight = 0.3333333f;
			ks[1].outTangent = 0f;
			ks[1].outWeight = 0.3333333f;
			ks[1].weightedMode = WeightedMode.None;

			ks[2] = new Keyframe(1f, 0f);
			ks[2].inTangent = -2f;
			ks[2].inWeight = 0.3333333f;
			ks[2].outTangent = -2f;
			ks[2].outWeight = 0f;
			ks[2].weightedMode = WeightedMode.None;
			
			ACBreathingCh = new AnimationCurve(ks);
			
			// ******** INTENSITY 4 ***********************
			ks = new Keyframe[3];
			ks[0] = new Keyframe(0f, 0f);
			ks[0].inTangent = 5f;
			ks[0].inWeight = 0.3333333f;
			ks[0].outTangent = 5f;
			ks[0].outWeight = 0.3333333f;
			ks[0].weightedMode = WeightedMode.None;

			ks[1] = new Keyframe(0.2f, 1f);
			ks[1].inTangent = 0f;
			ks[1].inWeight = 0.3333333f;
			ks[1].outTangent = 0f;
			ks[1].outWeight = 0.3333333f;
			ks[1].weightedMode = WeightedMode.None;

			ks[2] = new Keyframe(1f, 0f);
			ks[2].inTangent = -1.25f;
			ks[2].inWeight = 0.3333333f;
			ks[2].outTangent = -1.25f;
			ks[2].outWeight = 0f;
			ks[2].weightedMode = WeightedMode.None;

			ACIntensity4Ch = new AnimationCurve(ks);
			
			// ******** ORGASM ***********************
			ks = new Keyframe[9];
			ks[0] = new Keyframe(0f, 0f);
			ks[0].inTangent = 10f;
			ks[0].inWeight = 0f;
			ks[0].outTangent = 10f;
			ks[0].outWeight = 0.3333333f;
			ks[0].weightedMode = WeightedMode.None;

			ks[1] = new Keyframe(0.1f, 1f);
			ks[1].inTangent = 0f;
			ks[1].inWeight = 0.3333333f;
			ks[1].outTangent = 0f;
			ks[1].outWeight = 0.3333333f;
			ks[1].weightedMode = WeightedMode.None;

			ks[2] = new Keyframe(0.2f, 0f);
			ks[2].inTangent = 0f;
			ks[2].inWeight = 0.4426205f;
			ks[2].outTangent = 0f;
			ks[2].outWeight = 0.3333333f;
			ks[2].weightedMode = WeightedMode.None;

			ks[3] = new Keyframe(0.4f, 0.8f);
			ks[3].inTangent = 0.3623226f;
			ks[3].inWeight = 0.3333333f;
			ks[3].outTangent = 0.3623226f;
			ks[3].outWeight = 0.3333333f;
			ks[3].weightedMode = WeightedMode.None;

			ks[4] = new Keyframe(0.55f, 0f);
			ks[4].inTangent = -0.0202657f;
			ks[4].inWeight = 0.3333333f;
			ks[4].outTangent = -0.0202657f;
			ks[4].outWeight = 0.2525516f;
			ks[4].weightedMode = WeightedMode.None;

			ks[5] = new Keyframe(0.7f, 0.6f);
			ks[5].inTangent = -0.008669191f;
			ks[5].inWeight = 0.3333333f;
			ks[5].outTangent = -0.008669191f;
			ks[5].outWeight = 0.1256833f;
			ks[5].weightedMode = WeightedMode.None;

			ks[6] = new Keyframe(0.8f, 0f);
			ks[6].inTangent = -0.03181061f;
			ks[6].inWeight = 0.5136608f;
			ks[6].outTangent = -0.03181061f;
			ks[6].outWeight = 0.3333333f;
			ks[6].weightedMode = WeightedMode.None;

			ks[7] = new Keyframe(0.9f, 0.3f);
			ks[7].inTangent = 0.00762606f;
			ks[7].inWeight = 0.3333333f;
			ks[7].outTangent = 0.00762606f;
			ks[7].outWeight = 0.3333333f;
			ks[7].weightedMode = WeightedMode.None;

			ks[8] = new Keyframe(1f, 0f);
			ks[8].inTangent = -3f;
			ks[8].inWeight = 0.3333333f;
			ks[8].outTangent = -3f;
			ks[8].outWeight = 0f;
			ks[8].weightedMode = WeightedMode.None;

			ACOrgasmCh = new AnimationCurve(ks);
			
			
			// **************************************************
			// ********* STOMACH
			// **************************************************
			
			// ******** BREATHING & Intensity 0 ***********************
			ks = new Keyframe[3];
			ks[0] = new Keyframe(0f, 0f);
			ks[0].inTangent = 2.93292f;
			ks[0].inWeight = 0.3333333f;
			ks[0].outTangent = 2.93292f;
			ks[0].outWeight = 0.06066411f;
			ks[0].weightedMode = WeightedMode.None;

			ks[1] = new Keyframe(0.45f, 1f);
			ks[1].inTangent = 7.778403E-06f;
			ks[1].inWeight = 0.3333333f;
			ks[1].outTangent = 7.778403E-06f;
			ks[1].outWeight = 0.9195403f;
			ks[1].weightedMode = WeightedMode.None;

			ks[2] = new Keyframe(1f, 0f);
			ks[2].inTangent = -1.457108f;
			ks[2].inWeight = 0.08359454f;
			ks[2].outTangent = -1.457108f;
			ks[2].outWeight = 0f;
			ks[2].weightedMode = WeightedMode.None;

			ACBreathingSt = new AnimationCurve(ks);

			// ******** INTENSITY 1 and above ***********************
			ks = new Keyframe[5];
			ks[0] = new Keyframe(0f, 0f);
			ks[0].inTangent = 1.994234f;
			ks[0].inWeight = 0.3333333f;
			ks[0].outTangent = 1.994234f;
			ks[0].outWeight = 0.3333333f;
			ks[0].weightedMode = WeightedMode.None;

			ks[1] = new Keyframe(0.2005783f, 0.4f);
			ks[1].inTangent = 0.05928797f;
			ks[1].inWeight = 0.41177f;
			ks[1].outTangent = 0.05928797f;
			ks[1].outWeight = 0.1950696f;
			ks[1].weightedMode = WeightedMode.None;

			ks[2] = new Keyframe(0.5f, 1f);
			ks[2].inTangent = 7.778403E-06f;
			ks[2].inWeight = 0.3333333f;
			ks[2].outTangent = 7.778403E-06f;
			ks[2].outWeight = 0.9195403f;
			ks[2].weightedMode = WeightedMode.None;

			ks[3] = new Keyframe(0.7477173f, 0.4f);
			ks[3].inTangent = -0.2527227f;
			ks[3].inWeight = 0.1810344f;
			ks[3].outTangent = -0.2527227f;
			ks[3].outWeight = 0.2887932f;
			ks[3].weightedMode = WeightedMode.None;

			ks[4] = new Keyframe(1f, 0f);
			ks[4].inTangent = -1.585523f;
			ks[4].inWeight = 0.3333333f;
			ks[4].outTangent = -1.585523f;
			ks[4].outWeight = 0f;
			ks[4].weightedMode = WeightedMode.None;

			ACIntensity1St = new AnimationCurve(ks);
			
		}
	}
}