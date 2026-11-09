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

// VAMMoan v1.21
//
// Extensively based on Dollmaster plugin and on MacGruber's and AcidBubble's work
// Thank you for all your great plugins guyz !

namespace VAMMoanPlugin
{
    public partial class VAMMoan : MVRScript
    {	
        public static string PLUGIN_PATH;
        public static string ASSETS_PATH;
        public static string LOAD_PATH;
					
		private Voice voice;
		private Voices voices;
		private JSONStorableStringChooser voiceChoice;
		
		private JSONStorableString voiceInfos;
		private JSONStorableString stateInfos;
		public JSONStorableStringChooser ArousalMode;
		
		private JSONStorableFloat VAMMVolume;
		private float _voiceVolumeScale = 1f;   // 超出 1.0 的部分作为 PlayOneShot 倍数
		private JSONStorableFloat VAMMPitch;
		private JSONStorableFloat VAMMPause;
		private JSONStorableBool VAMMAutoJaw;
		private JSONStorableBool VAMMReverbEnabled;
		private JSONStorableBool VAMMBreathingEnabled;
		private JSONStorableFloat VAMMBreathingScale;
		
		private JSONStorableFloat VAMMAudioMinDistance;
		private JSONStorableFloat VAMMAudioMaxDistance;
		
		private JSONStorableFloat VAMMKissingSpeed;
		
		private JSONStorableFloat maxTriggerCount;
		private JSONStorableFloat arousalRate;
		private JSONStorableFloat TurnOffSpeed;
		private JSONStorableBool bumpUpArousalAfterInactivity;
		private JSONStorableBool allowOrgasm;
		
		private JSONStorableBool enableRandMoanTrigger;
		private JSONStorableFloat randMoanTriggerOccurenceMin;
		private JSONStorableFloat randMoanTriggerOccurenceMax;
		private JSONStorableFloat randMoanTriggerChance;
		private JSONStorableStringChooser randMoanTriggerTypeChoice;
		private JSONStorableString randMoanTriggerHelp;
		
		/** PELVIC SLAP START **/
		private JSONStorableBool enablePelvicSlap;
		private JSONStorableFloat PSLVolume;
		private JSONStorableFloat PSLGain;
		private JSONStorableFloat PSLReverbMix;
		private JSONStorableFloat PSLMinDelay;
		private JSONStorableBool PSLEditAdvOptions;
		private JSONStorableFloat PSLMinIntensity;
		private JSONStorableFloat PSLMaxIntensity;
		
		private JSONStorableFloat PSLTrigger01YOffset;
		private JSONStorableFloat PSLTrigger01ZOffset;
		private JSONStorableFloat PSLTrigger02YOffset;
		private JSONStorableFloat PSLTrigger02ZOffset;
		
		UIDynamic[] PSLAdvOptionsElements;
		// Values used during gameplay to avoid changing the default settings
		private bool _PSLPaused;
		private float _PSLVolume;
		private float _PSLGain;
		private float _PSLReverbMix;
		private float _PSLMinDelay;
		private float _PSLMinIntensity;
		private float _PSLMaxIntensity;
		
		private bool _PSLAllowed = true;
		private float _PSLLastVelocity = 0f;
		private AudioSource pelvicSlapAudioSource;
		private AudioLowPassFilter pelvicSlapLPF;
		private GameObject _PSLTrigger01;
		private GameObject _PSLTrigger02;
		private Vector3 _PSLTrigger01DefaultPos = new Vector3(0f,-0.09f,0.065f);
		private Vector3 _PSLTrigger01DefaultScale = new Vector3(0.02f, 0.08f, 0.02f);
		private Vector3 _PSLTrigger02DefaultPos = new Vector3(0f,-0.16f,-0.08f);
		private Vector3 _PSLTrigger02DefaultScale = new Vector3(0.02f, 0.08f, 0.02f);	
		/** PELVIC SLAP END **/
		
		/** SQUISHES START **/
		private JSONStorableBool enableSquishes;
		private JSONStorableFloat SQSVolume;
		private JSONStorableBool SQSEditAdvOptions;
		private JSONStorableFloat SQSMinDelay;
		private JSONStorableFloat SQSReverbMix;

		UIDynamic[] SQSAdvOptionsElements;
		// Values used during gameplay to avoid changing the default settings
		private bool _SQSPaused = false;
		private float _SQSVolume;
		private float _SQSMinDelay;
		private float _SQSReverbMix;
		
		private bool _SQSAllowed = true;
		private bool _SQSPAllowed = true;
		private AudioSource squishesAudioSource;
		private AudioSource squishesAudioSourcePen;
		private AudioLowPassFilter squishesLPF;
		private AudioLowPassFilter squishesLPFPen;
		/** SQUISHES END **/
		
		/** MOUTH/BJ START **/
		private JSONStorableBool enableBlowjob;
		private JSONStorableFloat BJVolume;
		private JSONStorableBool BJEditAdvOptions;
		private JSONStorableFloat BJMinDelay;
		private JSONStorableFloat BJReverbMix;
		private JSONStorableFloat BJMoanFallbackDelay;

		UIDynamic[] BJAdvOptionsElements;
		// Values used during gameplay to avoid changing the default settings
		private bool _BJPaused = false;
		private float _BJVolume;
		private float _BJMinDelay;
		private float _BJReverbMix;
		
		private float _BJEndTimeLastSample; // Used to know when the last sample played is gonna end
		private float _BJMoanFallbackDelay; // How much are we gonna wait before falling back to the moan loop
		
		private bool _BJAllowed = true;
		private AudioSource mouthAudioSource;
		private AudioLowPassFilter mouthLPF;
		/** MOUTH/BJ END **/
		
		private string defaultVoice = "Marta";
		private string selectedVoice;
		private string defaultMode = "Manual";
		private bool voiceInitialized = false;
		
		private float nextMoan = 0f;
		private float nextTurnoffDecrease = 0f;
		private float nextState = 0f;
		private float nextSeed = 0f;
		private float nextOrgasmCheck = 0f;
		private int moanLoopCount = 0;
		private int moanRandOccurence = 0;

		private float nextTriggerAllowed = 0f; // Used while the orgasm sound is playing, preventing any additionnal increase in arousal or sound played				
		private float lastCollisionTime = -1.0f;
		private float lastInteractionTime = -1.0f;
		private float minLastCollisionTime = 0.8f; // Minimum time in interactive mode to swap currentArousal and play a new intensity instead of breathing
		private float maleCollisionTimeout = 0.135f; // We're on a collision (not trigger) system, the amount of collision event has to be controlled

		private float vammTimer = 0f;
				
		private float breathPeriod;
		private float breathStart;
		private float breathEnd;

		private float audioLoudness = 0;
		private float highestLoudness = -10.0f;
		
		private float _currentArousal = -1.0f;
		public float currentArousal
		{
			get
			{
				return _currentArousal;				
			} 
			set
			{
				_currentArousal = value;
				VAMMValue_CurrentArousal.val = value;
			}
		}
		private float? _nextArousalAfterOrgasm = null; // Used to loop after orgasm
		
		private float _sexTriggerCount = 0.0f;
		public float sexTriggerCount
		{
			get
			{
				return _sexTriggerCount;				
			} 
			set
			{
				float newCount = allowOrgasm.val == false && value >= maxTriggerCount.val ? (maxTriggerCount.val - 1) : Mathf.Clamp(value,0,maxTriggerCount.val);
				_sexTriggerCount = newCount;
				VAMMValue_TriggerCount.val = newCount;
			}
		}
		public float sexTriggerDecayIncrement = 10.0f;
		
		public float defaultArousalIncrement = 0.15f;
		
		private AudioClip currentClipPlaying;
		private AudioClip currentCollisionClipPlaying;
			
		public AudioSourceControl headAudioSource;
		private AdjustJoints JawControlAJ;
		private JSONStorable AutoJawMouthMorphsJS;
		
		string lastAtomName = "";
		
		protected Rigidbody vaTrig;
		protected SexTriggerCollide vaTrigSTC;
		protected Rigidbody penTrig01;
		protected SexTriggerCollide penTrig01STC;
		protected Rigidbody penTrig02;
		protected SexTriggerCollide penTrig02STC;
		protected Rigidbody anaTrig01;
		protected SexTriggerCollide anaTrig01STC;
		protected Rigidbody labTrig;
		protected SexTriggerCollide labTrigSTC;
		protected Rigidbody mouTrig;
		
		protected Rigidbody pslRigidbody01;
		
		private bool intensityBoost = false;
				
		// Some buttons and UI stuffs I need globally to swap their states
		private UIDynamicButton showHelpBtn;
		private UIDynamicTextField helpTextfield;
		private UIDynamicButton previewKissingBtn;
		private UIDynamicButton previewBlowjobBtn;
		private UIDynamicButton previewBlowjobIntenseBtn;
		
		// Morphs ( thank you MacGruber ! )
		DAZCharacterSelector myGeometry;
		DAZCharacterSelector.Gender myGender = DAZCharacterSelector.Gender.None;
		private bool isMyGenderAltFuta = false;
		private DAZMorph lifeChestMorph;
		private DAZMorph lifeStomachMorph;
		private DAZMorph lifeNoseOutMorph;
		
		// Actions/triggers
		// ** All starting actions ** ( one shot trigger )
		private EventTrigger startDisabledTrigger;
		private EventTrigger startBreathingTrigger;
		private EventTrigger startKissingTrigger;
		private EventTrigger startBlowjobTrigger;
		private EventTrigger startIntensity0Trigger;
		private EventTrigger startIntensity1Trigger;
		private EventTrigger startIntensity2Trigger;
		private EventTrigger startIntensity3Trigger;
		private EventTrigger startIntensity4Trigger;
		
		private EventTrigger intensityLoweredTrigger;
		private EventTrigger intensityIncreasedTrigger;
		private EventTrigger reachOrgasmTrigger;
		private EventTrigger endOrgasmTrigger;
		
		private List<EventTrigger> allTriggers;
		
		private string editTriggerListDefault = "Select a trigger to edit";
		private List<string> editTriggersList;
		private JSONStorableStringChooser editTriggerChoice;
		
		// ** All transition actions ( updated every frame )
		private EventTrigger breathingTrigger;
		
		// All accessible Storable for scripting purposes
		public JSONStorableFloat VAMMValue_CurrentArousal;
		public JSONStorableFloat VAMMValue_TriggerCount;
		public JSONStorableFloat VAMMValue_MaxTriggerCount;
		public JSONStorableFloat VAMMValue_IntensitiesCount;
		
		public JSONStorableString A_UpdateArousalValue;
		
		// External calls
		public JSONStorableFloat VAMM_GlobalOcclusion_lpf;
		
		public override void Init()
        {			 
            try
            {
                if (containingAtom.type != "Person")
                {
                    SuperController.LogError("Please add VAMMoan to a Person atom");
                    return;
                }
							
                lastAtomName = containingAtom.uid;
                
				PLUGIN_PATH = GetPluginPath(this);
				ASSETS_PATH = PLUGIN_PATH + "/audio";
				               
                LOAD_PATH = SuperController.singleton.currentLoadDir;
				
				JawControlAJ = containingAtom.GetStorableByID("JawControl") as AdjustJoints;
				AutoJawMouthMorphsJS = containingAtom.GetStorableByID("AutoJawMouthMorph") as JSONStorable;
				
				headAudioSource = containingAtom.GetStorableByID("HeadAudioSource") as AudioSourceControl;
				headAudioSource.SetFloatParamValue("delayBetweenQueuedClips", 0.0f);
							
				selectedVoice = defaultVoice;
				voices = new Voices(this);
				
				/* ********************************* */
				/* **** LEFT UI                 **** */
				/* ********************************* */
				voiceChoice = new JSONStorableStringChooser("voice", voices.voicesNames, defaultVoice, "Voice", (string choice)=>
				{
					selectedVoice = choice;
					ChangeVoice();
				});

				RegisterStringChooser(voiceChoice);
				UIDynamicPopup voicePopup = CreateScrollablePopup(voiceChoice);
				voicePopup.popup.topButton.image.color = new Color(0.35f, 0.60f, 0.65f);
				voicePopup.popup.selectColor = new Color(0.35f, 0.60f, 0.65f);

				voiceInfos = new JSONStorableString("VoiceInfos", "");
				UIDynamic voiceinfosTextfield = CreateTextField(voiceInfos, false);
				voiceinfosTextfield.height = 200.0f;
				
				stateInfos = new JSONStorableString("Stateinfos", "");
				UIDynamic stateinfosTextfield = CreateTextField(stateInfos, false);
				stateinfosTextfield.height = 135.0f;

				List<string> ModesList = new List<string>();
				ModesList.Add("Manual");
				ModesList.Add("Interactive");
				
				ArousalMode = new JSONStorableStringChooser("arousalMode", ModesList, defaultMode, "Mode", switchArousalModeCallback);
				RegisterStringChooser(ArousalMode);
				UIDynamicPopup arousalModePopup = CreatePopup(ArousalMode);
				arousalModePopup.popup.topButton.image.color = new Color(0.35f, 0.60f, 0.65f);
				arousalModePopup.popup.selectColor = new Color(0.35f, 0.60f, 0.65f);
				
				JSONStorableString helpText = new JSONStorableString("Help",
					"<color=#000><size=35><b>VAMMoan help</b></size></color>\n\n" + 
					"<color=#333>" +
					"<b>Voice:</b> The currently selected girl voice.\n" +
					"<b>State:</b> Shows the current state of arousal selected.\n" +
					"<b>Mode:</b> The current mode of voice playback.\n" +
					"<size=25>- Manual : you have to drive the intensity with actions and triggers\n" +
					"- Interactive : playing with the character (male and female) will automatically arouse it</size>\n\n" +
					"<color=#000><size=35><b>Global Options</b></size></color>\n" + 
					"<i><size=25>Options for both manual and interactive mode</size></i>\n\n" +
					"<b>Voice volume :</b> changes the head audio source volume.\n<i>Warning:</i> reducing the volume will lower the jaw motion if it is enabled.\n" +
					"<b>Voice pitch :</b> changes the head audio pitch (makes a lower or higher voice).\n\n" +
					"<b>Enable Reverb (VAMAtmosphere) :</b> enables the proper configuration of the head audio to allow reverb using VAMAtmosphere. Check this if you want the voice to be affected by the Reverb Zones. <b>You need VAMAtmosphere plugin to add Reverb Zone</b>.\n\n" +
					"<b>Enable breathing animation :</b> enables morph animations to simulate breathing. <b>You need to have MacGruber's Life installed. Compatible versions are 10 to 13</b>.\n\n" +
					"<b>Enable auto-jaw animation :</b> enables the automatic animated jaw based on the voice audio. Check it to enable it, and click once on <b>Set optimal settings</b> to use predefined settings for this voice. You can tweak them aftewards if you want in the <b>Jaw Physics & triggers</b> tab.\n\n" +
					"<b>Pelvic slap :</b> enables a slap sound when a collision happens around the pelvice area. You can check <i>Edit advanced options</i> to tweak the triggers and several other options. <b>Warning:</b> It is not a magical solution that will work just by enabling it. If you have a complex animation or pose, it is recommended to tweak the triggers depending on your characters, and control the delay and pause during the animation to ensure you obtain a good feeling. Please refer to the documentation on the hub for more information.\n<b>Note:</b> the slap should be enabled on a single character for a scene with 2 characters. Most of the time, it works better when the slap is enabled on the receiving character of the slap, not the one doing the slap.\n\n" +
					"<color=#000><size=35><b>Manual Options</b></size></color>\n" + 
					"<i><size=25>Options for manual mode only</size></i>\n\n" +
					"<b>Kissing speed :</b> it changes the delay between the kisses / smooches sounds. The lower the value, the faster they get.\n" +
					"<color=#000><size=35><b>Interactive Options</b></size></color>\n" + 
					"<i><size=25>Options for interactive mode only</size></i>\n\n" +
					"<b>Raise arousal when resuming: </b> If you stop interacting with the girl for a few seconds, and you resume playing with her. If this checkbox is enabled, there is 25% chance that she will be super excited and get to the next level of intensity immediately.\n" +
					"<b>Allow orgasm: </b> If checked, the girl can have an orgasm. If not, she will stay at intensity 4 if you keep playing with her.\n" +
					"<b>Hard to please: </b> If the girl is hard to please or not. A low value is a lewd girl reaching orgasm pretty fast. A higher value is a frigid girl that demands of bit of trust to please.\n" +
					"<b>Arousal rate: </b> The amount of arousal added during interaction. It can be used to simulate situations where the girl is more or less sensitive when you interact with her.\n" +
					"<b>Turn Off Speed: </b> How much the girl is turned of when you don't interact with her. If the value is low, she will stay excited longer. If the value is high, she will be turned off pretty quick.\n" +
					"</color>"
				);
				helpTextfield = CreateTextField(helpText, false);
				helpTextfield.height = 650.0f;
				
				CreateSpacer();

				// TITLE TESTS
				createStaticDescriptionText("Tests states","<color=#000><size=35><b>TEST STATES</b></size></color>\n<size=24>Use these buttons below to test the different state of the character. Only works in manual mode.</size>",false,130);
				
				
				UIDynamicButton previewDisabledBtn = CreateButton("Disabled", false);
				if (previewDisabledBtn != null) {
					previewDisabledBtn.button.onClick.AddListener( () => { setArousal( -10.0f ); } );
				}

				UIDynamicButton previewBreathingBtn = CreateButton("Breathing", false);
				if (previewBreathingBtn != null) {
					previewBreathingBtn.button.onClick.AddListener( () => { setArousal( -1.0f ); } );
				}
				
				previewKissingBtn = CreateButton("Kissing", false);
				if (previewKissingBtn != null) {
					previewKissingBtn.button.onClick.AddListener( () => { setArousal( 100.0f ); } );
				}
											
				UIDynamicButton previewNeutralBtn = CreateButton("Intensity 0", false);
				if (previewNeutralBtn != null) {
					previewNeutralBtn.button.onClick.AddListener( () => { setArousal( 0.0f ); } );
				}
				
				UIDynamicButton previewIntensity1Btn = CreateButton("Intensity 1", false);
				if (previewIntensity1Btn != null) {
					previewIntensity1Btn.button.onClick.AddListener( () => { setArousal( 1.0f ); } );
				}
				
				UIDynamicButton previewIntensity2Btn = CreateButton("Intensity 2", false);
				if (previewIntensity2Btn != null) {
					previewIntensity2Btn.button.onClick.AddListener( () => { setArousal( 2.0f ); } );
				}
				
				UIDynamicButton previewIntensity3Btn = CreateButton("Intensity 3", false);
				if (previewIntensity3Btn != null) {
					previewIntensity3Btn.button.onClick.AddListener( () => { setArousal( 3.0f ); } );
				}
				
				UIDynamicButton previewIntensity4Btn = CreateButton("Intensity 4", false);
				if (previewIntensity4Btn != null) {
					previewIntensity4Btn.button.onClick.AddListener( () => { setArousal( 4.0f ); } );
				}
				
				UIDynamicButton previewIntensity5Btn = CreateButton("Orgasm", false);
				if (previewIntensity5Btn != null) {
					previewIntensity5Btn.button.onClick.AddListener( () => { setArousal( 5.0f ); } );
				}
				
				UIDynamicButton previewIntensity6Btn = CreateButton("Perpetual Orgasm", false);
				if (previewIntensity6Btn != null) {
					previewIntensity6Btn.button.onClick.AddListener( () => { setArousal( 6.0f ); } );
				}
				
				/* ********************************* */
				/* **** RIGHT UI                **** */
				/* ********************************* */

				// TITLE GLOBAL
				createStaticDescriptionText("Global options","<color=#000><size=35><b>GLOBAL OPTIONS</b></size></color>",true,40);
				
				
				VAMMVolume = new JSONStorableFloat("Voice volume", 1f, VAMMVolumeCallback, 0f, 10f);
				RegisterFloat(VAMMVolume);
				CreateSlider(VAMMVolume, true);
				// 仅在滑块仍为默认值时从音源同步，避免覆盖场景里已保存的 >1 值
				if (VAMMVolume.val == 1f) {
					VAMMVolume.val = headAudioSource.GetFloatParamValue("volume");
				}
				
				VAMMPitch = new JSONStorableFloat("Voice pitch", 1f, VAMMPitchCallback, 0.1f, 3f);
				RegisterFloat(VAMMPitch);
				CreateSlider(VAMMPitch, true);
				VAMMPitch.val = headAudioSource.GetFloatParamValue("pitch");
				
				VAMMReverbEnabled = new JSONStorableBool("Enable Reverb (VAMAtmosphere)", false, VAMMReverbEnabledCallback);
				VAMMReverbEnabled.storeType = JSONStorableParam.StoreType.Full;
				CreateToggle(VAMMReverbEnabled, true);
				RegisterBool(VAMMReverbEnabled);
				VAMMReverbEnabled.val = !headAudioSource.GetBoolParamValue("spatialize");
				
				VAMMBreathingEnabled = new JSONStorableBool("Enable breathing animation", true, VAMMBreathingEnabledCallback);
				VAMMBreathingEnabled.storeType = JSONStorableParam.StoreType.Full;
				CreateToggle(VAMMBreathingEnabled, true);
				RegisterBool(VAMMBreathingEnabled);
				
				VAMMAutoJaw = new JSONStorableBool("Enable auto-jaw animation", false, VAMMAutoJawCallback);
				VAMMAutoJaw.storeType = JSONStorableParam.StoreType.Full;
				CreateToggle(VAMMAutoJaw, true);
				RegisterBool(VAMMAutoJaw);
				VAMMAutoJaw.val = JawControlAJ.GetBoolParamValue("driveXRotationFromAudioSource"); // Reading the parameters from the normal tabs
				AutoJawMouthMorphsJS.SetBoolParamValue("enabled", VAMMAutoJaw.val); // Also settings the morphs to the same value
				
				UIDynamicButton setOptimalJawParams = CreateButton("Set optimal auto-jaw animation parameters", true);
				if (setOptimalJawParams != null) {
					setOptimalJawParams.button.onClick.AddListener( setOptimalJawParamsCallback );
				}
								
				CreateSpacer(true);
								
				createStaticDescriptionText("Global options pelvic slap","<color=#000><size=28><b>PELVIC SLAP</b></size></color>\n<size=24>Belly and ass slappy sound.</size>",true,80);

				enablePelvicSlap = new JSONStorableBool("Enable pelvic slap", false, enablePelvicSlapCallback);
				enablePelvicSlap.storeType = JSONStorableParam.StoreType.Full;
				CreateToggle(enablePelvicSlap, true);
				RegisterBool(enablePelvicSlap);
				
				PSLVolume = new JSONStorableFloat("Slap sound volume", 1f, PSLVolumeCallback, 0f, 10f);
				RegisterFloat(PSLVolume);
				CreateSlider(PSLVolume, true);
											
				PSLMinDelay = new JSONStorableFloat("Sound min delay", 0.45f, PSLMinDelayCallback, 0.20f, 1f);
				RegisterFloat(PSLMinDelay);
				CreateSlider(PSLMinDelay, true);
				
				PSLGain = new JSONStorableFloat("Slap sound gain", 0.2f, PSLGainCallback, 0f, 0.5f);
				RegisterFloat(PSLGain);
				CreateSlider(PSLGain, true);
							
				PSLEditAdvOptions = new JSONStorableBool("Edit advanced options", false, PSLEditAdvOptionsCallback);
				CreateToggle(PSLEditAdvOptions, true);
				
				PSLAdvOptionsElements = new UIDynamic[7];
				
				PSLReverbMix = new JSONStorableFloat("Reverb mix", 1f, PSLReverbMixCallback, 0f, 1f);
				RegisterFloat(PSLReverbMix);
				PSLAdvOptionsElements[0] = CreateSlider(PSLReverbMix, true);
				
				PSLMinIntensity = new JSONStorableFloat("Sound min intensity", 1f, (val) => { PSLMinIntensity.valNoCallback = Mathf.Round(val); PSLMinIntensityCallback( PSLMinIntensity.valNoCallback ); PSLMaxIntensity.SetVal( Mathf.Max(PSLMaxIntensity.val, val) ); }, 1f, 3f);
				RegisterFloat(PSLMinIntensity);
				PSLAdvOptionsElements[1] = CreateSlider(PSLMinIntensity, true);
				
				PSLMaxIntensity = new JSONStorableFloat("Sound max intensity", 3f, (val) => { PSLMaxIntensity.valNoCallback = Mathf.Round(val); PSLMaxIntensityCallback( PSLMaxIntensity.valNoCallback ); PSLMinIntensity.SetVal( Mathf.Min(PSLMinIntensity.val, val) ); }, 1f, 3f);
				RegisterFloat(PSLMaxIntensity);
				PSLAdvOptionsElements[2] = CreateSlider(PSLMaxIntensity, true);
								
				PSLTrigger01YOffset = new JSONStorableFloat("Trigger 01 Y Offset", 0f, PSLTrigger01OffsetCallback, -0.55f, 0.55f);
				RegisterFloat(PSLTrigger01YOffset);
				PSLAdvOptionsElements[3] = CreateSlider(PSLTrigger01YOffset, true);
				
				PSLTrigger01ZOffset = new JSONStorableFloat("Trigger 01 Z Offset", 0f, PSLTrigger01OffsetCallback, -0.55f, 0.55f);
				RegisterFloat(PSLTrigger01ZOffset);
				PSLAdvOptionsElements[4] = CreateSlider(PSLTrigger01ZOffset, true);

				PSLTrigger02YOffset = new JSONStorableFloat("Trigger 02 Y Offset", 0f, PSLTrigger02OffsetCallback, -0.55f, 0.55f);
				RegisterFloat(PSLTrigger02YOffset);
				PSLAdvOptionsElements[5] = CreateSlider(PSLTrigger02YOffset, true);
				
				PSLTrigger02ZOffset = new JSONStorableFloat("Trigger 02 Z Offset", 0f, PSLTrigger02OffsetCallback, -0.55f, 0.55f);
				RegisterFloat(PSLTrigger02ZOffset);
				PSLAdvOptionsElements[6] = CreateSlider(PSLTrigger02ZOffset, true);
				
				PSLEditAdvOptionsCallback(false);
				
				CreateSpacer(true);
								
				createStaticDescriptionText("Global options squishes","<color=#000><size=28><b>SQUISHES</b></size></color>\n<size=24>Labia and penis wet sounds.</size>",true,80);
				
				enableSquishes = new JSONStorableBool("Enable squishes sounds", false, enableSquishesCallback);
				enableSquishes.storeType = JSONStorableParam.StoreType.Full;
				CreateToggle(enableSquishes, true);
				RegisterBool(enableSquishes);
				
				SQSVolume = new JSONStorableFloat("Squishes sounds volume", 1f, SQSVolumeCallback, 0f, 10f);
				RegisterFloat(SQSVolume);
				CreateSlider(SQSVolume, true);

				SQSEditAdvOptions = new JSONStorableBool("Edit advanced options", false, SQSEditAdvOptionsCallback);
				CreateToggle(SQSEditAdvOptions, true);
				
				SQSAdvOptionsElements = new UIDynamic[2];
				
				SQSMinDelay = new JSONStorableFloat("Sound min delay", 0.15f, SQSMinDelayCallback, 0.1f, 1f);
				RegisterFloat(SQSMinDelay);
				SQSAdvOptionsElements[0] = CreateSlider(SQSMinDelay, true);
				
				SQSReverbMix = new JSONStorableFloat("Reverb mix", 1f, SQSReverbMixCallback, 0f, 1f);
				RegisterFloat(SQSReverbMix);
				SQSAdvOptionsElements[1] = CreateSlider(SQSReverbMix, true);

				SQSEditAdvOptionsCallback(false);
				
				CreateSpacer(true);
								
				createStaticDescriptionText("Global options blowjob","<color=#000><size=28><b>BLOWJOB</b></size></color>\n<size=24>Mouth wet sounds.</size>",true,80);

				enableBlowjob = new JSONStorableBool("Enable blowjob sounds", false, enableBlowjobCallback);
				enableBlowjob.storeType = JSONStorableParam.StoreType.Full;
				CreateToggle(enableBlowjob, true);
				RegisterBool(enableBlowjob);
				
				BJVolume = new JSONStorableFloat("Blowjob sounds volume", 1f, BJVolumeCallback, 0f, 10f);
				RegisterFloat(BJVolume);
				CreateSlider(BJVolume, true);

				BJEditAdvOptions = new JSONStorableBool("Edit advanced options", false, BJEditAdvOptionsCallback);
				CreateToggle(BJEditAdvOptions, true);
				
				BJAdvOptionsElements = new UIDynamic[3];
				
				BJMinDelay = new JSONStorableFloat("Sound min delay", 0.50f, BJMinDelayCallback, 0.1f, 1f);
				RegisterFloat(BJMinDelay);
				BJAdvOptionsElements[0] = CreateSlider(BJMinDelay, true);
				
				BJReverbMix = new JSONStorableFloat("Reverb mix", 1f, BJReverbMixCallback, 0f, 1f);
				RegisterFloat(BJReverbMix);
				BJAdvOptionsElements[1] = CreateSlider(BJReverbMix, true);

				BJMoanFallbackDelay = new JSONStorableFloat("Moan fallback delay", 2f, BJMoanFallbackDelayCallback, 1f, 5f);
				RegisterFloat(BJMoanFallbackDelay);
				BJAdvOptionsElements[2] = CreateSlider(BJMoanFallbackDelay, true);
				
				
				BJEditAdvOptionsCallback(false);

				CreateSpacer(true);
				
				// TITLE MANUAL
				createStaticDescriptionText("Manual mode options","<color=#000><size=35><b>MANUAL MODE</b></size></color>",true,40);

				
				VAMMKissingSpeed = new JSONStorableFloat("Kissing Speed", 0.50f, 0f, 1f);
				RegisterFloat(VAMMKissingSpeed);
				CreateSlider(VAMMKissingSpeed, true);
		
				CreateSpacer(true);
				
				// TITLE AUTO
				createStaticDescriptionText("Auto mode options","<color=#000><size=35><b>INTERACTIVE MODE</b></size></color>",true,40);

				
				bumpUpArousalAfterInactivity = new JSONStorableBool("Raise arousal when resuming", true);
				bumpUpArousalAfterInactivity.storeType = JSONStorableParam.StoreType.Full;
				CreateToggle(bumpUpArousalAfterInactivity, true);
				RegisterBool(bumpUpArousalAfterInactivity);
				
				allowOrgasm = new JSONStorableBool("Allow orgasm", true);
				allowOrgasm.storeType = JSONStorableParam.StoreType.Full;
				CreateToggle(allowOrgasm, true);
				RegisterBool(allowOrgasm);
				
				// Default at 700 is arbitrary. It feels like a good middle ground for a quick interactive experience
				maxTriggerCount = new JSONStorableFloat("Hard to please", 700.0f, (val) => { maxTriggerCount.valNoCallback = Mathf.Round(val); maxTriggerCountCallback( maxTriggerCount.val ); }, 500.0f, 3000.0f, true, true);
				maxTriggerCount.storeType = JSONStorableParam.StoreType.Full;
				CreateSlider(maxTriggerCount, true);
				RegisterFloat(maxTriggerCount);
				
				arousalRate = new JSONStorableFloat("Arousal rate", 1.0f, arousalRateCallback, 0.1f, 3.0f, true, true);
				CreateSlider(arousalRate, true);
				RegisterFloat(arousalRate);

                // 当前版本没有被任何代码读取，实际无效
                /*TurnOffSpeed = new JSONStorableFloat("Turn Off Speed", 1.5f, 1f, 2.5f, true, true);
				TurnOffSpeed.storeType = JSONStorableParam.StoreType.Full;
				CreateSlider(TurnOffSpeed, true);
				RegisterFloat(TurnOffSpeed);*/


                CreateSpacer(true);
				
				// Creating my triggers
				initTriggers();
				
				// TITLE ADVANCED
				createStaticDescriptionText("Advanced options","<color=#000><size=35><b>ADVANCED OPTIONS</b></size></color>",true,40);

				// Breathing scale
				VAMMBreathingScale = new JSONStorableFloat("Breathing scale", 1f, VAMMBreathingScaleCallback, 0.1f, 1f);
				RegisterFloat(VAMMBreathingScale);
				CreateSlider(VAMMBreathingScale, true);

				createStaticDescriptionText("Breathing scale description","<color=#000><size=26><i><b>Changes how much the breathing animations scales.</b> 1 means it will be animated to it's maximum depth.</i></size></color>",true,95);
				
				// Pause between samples
				VAMMPause = new JSONStorableFloat("Pause between moans", 0f, VAMMPauseCallback, -0.3f, 2f);
				CreateSlider(VAMMPause, true);
				
				createStaticDescriptionText("Pause description","<color=#000><size=26><i><b>Adds a tiny additional pause between moans.</b> 0 means no pause, 1 is a one second pause. It can be used to slow down the pace of the moans during an animation.</i></size></color>",true,125);
				
				CreateSpacer(true);
				
				// Random Moans trigger
				enableRandMoanTrigger = new JSONStorableBool("Enable randomized moan playback", false);
				enableRandMoanTrigger.storeType = JSONStorableParam.StoreType.Full;
				CreateToggle(enableRandMoanTrigger, true);
				RegisterBool(enableRandMoanTrigger);
				
				randMoanTriggerOccurenceMin = new JSONStorableFloat("Occurence Min", 3.0f, (val) => { randMoanTriggerOccurenceMin.valNoCallback = Mathf.Round(val); randMoanTriggerOccurenceMinCallback(val); randMoanTriggerOccurenceMax.SetVal( Mathf.Max(randMoanTriggerOccurenceMax.val, val) ); }, 1.0f, 20.0f);
				RegisterFloat(randMoanTriggerOccurenceMin);
				CreateSlider(randMoanTriggerOccurenceMin, true);
				
				randMoanTriggerOccurenceMax = new JSONStorableFloat("Occurence Max", 8.0f, (val) => { randMoanTriggerOccurenceMax.valNoCallback = Mathf.Round(val); randMoanTriggerOccurenceMaxCallback(val); randMoanTriggerOccurenceMin.SetVal( Mathf.Min(randMoanTriggerOccurenceMin.val, val) ); }, 1.0f, 20.0f);
				RegisterFloat(randMoanTriggerOccurenceMax);
				CreateSlider(randMoanTriggerOccurenceMax, true);
				
				randMoanTriggerChance = new JSONStorableFloat("Playback chance", 0.85f, randMoanTriggerChanceCallback, 0.1f, 1f);
				RegisterFloat(randMoanTriggerChance);
				CreateSlider(randMoanTriggerChance, true);

				List<string> rndMoanTriggerList = new List<string>();
				rndMoanTriggerList.Add("Breathing");
				rndMoanTriggerList.Add("Intensity 0");
				rndMoanTriggerList.Add("Intensity 1");
				rndMoanTriggerList.Add("Intensity 2");
				rndMoanTriggerList.Add("Intensity 3");
				rndMoanTriggerList.Add("Intensity 4");
				rndMoanTriggerList.Add("Current Intensity -1");
				rndMoanTriggerList.Add("Current Intensity -2");
				rndMoanTriggerList.Add("Current Intensity +1");
				rndMoanTriggerList.Add("Current Intensity +2");
				rndMoanTriggerList.Add("Current Intensity +/-1");
				rndMoanTriggerList.Add("Current Intensity +/-2");
				rndMoanTriggerList.Add("Current Intensity +/-X");
				randMoanTriggerTypeChoice = new JSONStorableStringChooser("Playback Type", rndMoanTriggerList, "Current Intensity -1", "Playback Type");
				RegisterStringChooser(randMoanTriggerTypeChoice);
				UIDynamicPopup randMoanTriggerPopup = CreateScrollablePopup(randMoanTriggerTypeChoice,true);
				randMoanTriggerPopup.popup.topButton.image.color = new Color(0.35f, 0.60f, 0.65f);
				randMoanTriggerPopup.popup.selectColor = new Color(0.35f, 0.60f, 0.65f);
				
				randMoanTriggerHelp = createStaticDescriptionText("Rand trigger description","",true,195);
				
				CreateSpacer(true);
				
				createStaticDescriptionText("Triggers","<color=#000><size=35><b>TRIGGERS</b></size></color>",true,40);
				createStaticDescriptionText("Triggers description","<color=#000><size=26><i><b>Create triggers based on VAMMoan events.</b> By selecting triggers in the dropdown below, you can add actions that will trigger depending on VAMM intensities or events.</i></size></color>",true,165);
				
				editTriggersList = new List<string>();
				editTriggersList.Add("Select a trigger to edit");
				editTriggersList.Add("Start disabled");
				editTriggersList.Add("Start breathing");
				editTriggersList.Add("Start kissing");
				editTriggersList.Add("Start blowjob");
				editTriggersList.Add("Start intensity 0");
				editTriggersList.Add("Start intensity 1");
				editTriggersList.Add("Start intensity 2");
				editTriggersList.Add("Start intensity 3");
				editTriggersList.Add("Start intensity 4");
				editTriggersList.Add("When intensity lowered");
				editTriggersList.Add("When intensity increased");
				editTriggersList.Add("Start orgasm");
				editTriggersList.Add("End orgasm");
				editTriggersList.Add("While breathing");
				
				editTriggerChoice = new JSONStorableStringChooser("triggers", editTriggersList, editTriggerListDefault, "Triggers", (string choice)=>
				{
					switch( choice ) {
						case "Start disabled":
							startDisabledTrigger.OpenPanelActionStart();
							break;
						case "Start breathing":
							startBreathingTrigger.OpenPanelActionStart();
							break;
						case "Start kissing":
							startKissingTrigger.OpenPanelActionStart();
							break;
						case "Start blowjob":
							startBlowjobTrigger.OpenPanelActionStart();
							break;
						case "Start intensity 0":
							startIntensity0Trigger.OpenPanelActionStart();
							break;
						case "Start intensity 1":
							startIntensity1Trigger.OpenPanelActionStart();
							break;
						case "Start intensity 2":
							startIntensity2Trigger.OpenPanelActionStart();
							break;
						case "Start intensity 3":
							startIntensity3Trigger.OpenPanelActionStart();
							break;
						case "Start intensity 4":
							startIntensity4Trigger.OpenPanelActionStart();
							break;
						case "When intensity lowered":
							intensityLoweredTrigger.OpenPanelActionStart();
							break;
						case "When intensity increased":
							intensityIncreasedTrigger.OpenPanelActionStart();
							break;
						case "Start orgasm":
							reachOrgasmTrigger.OpenPanelActionStart();
							break;
						case "End orgasm":
							endOrgasmTrigger.OpenPanelActionStart();
							break;
						case "While breathing":
							breathingTrigger.OpenPanelTransition();
							break;
					}
					editTriggerChoice.val = editTriggerListDefault;
				}){ isStorable = false };
				RegisterStringChooser(editTriggerChoice);
				UIDynamicPopup editTriggerPopup = CreateScrollablePopup(editTriggerChoice,true);
				editTriggerPopup.popup.topButton.image.color = new Color(0.35f, 0.60f, 0.65f);
				editTriggerPopup.popup.selectColor = new Color(0.35f, 0.60f, 0.65f);
				
				CreateSpacer(true);
				
				// TITLE SPATIALIZATION OPTIONS
				createStaticDescriptionText("Spatialization options","<color=#000><size=35><b>SPATIALIZATION OPTIONS</b></size></color>",true,40);
				createStaticDescriptionText("Spatialization options description","<color=#000><size=26><i><b>Only use if you understand what it is used for.</b> Otherwise, leave defaults.</i></size></color>",true,95);

				VAMMAudioMinDistance = new JSONStorableFloat("Audio Min Distance", 1f, VAMMAudioMinDistanceCallback, 0f, 10f);
				RegisterFloat(VAMMAudioMinDistance);
				CreateSlider(VAMMAudioMinDistance, true);
				headAudioSource.SetFloatParamValue("minDistance", VAMMAudioMinDistance.val );
				
				VAMMAudioMaxDistance = new JSONStorableFloat("Audio Max Distance", 10f, VAMMAudioMaxDistanceCallback, 1f, 500f);
				RegisterFloat(VAMMAudioMaxDistance);
				CreateSlider(VAMMAudioMaxDistance, true);
				headAudioSource.SetFloatParamValue("maxDistance", VAMMAudioMaxDistance.val );


				CreateSpacer(true);
				
				// ***************************************************************
				// ******* ACTIONS ***********************************************
				// ***************************************************************
				JSONStorableAction setVoiceDisabled = new JSONStorableAction("Voice disabled", () =>
				{
					setArousal( -10.0f );
				});
				
				JSONStorableAction setVoiceBreathing = new JSONStorableAction("Voice breathing", () =>
				{
					setArousal( -1.0f );
				});

				JSONStorableAction setVoiceKissing = new JSONStorableAction("Voice kissing", () =>
				{
					setArousal( 100.0f );
				});
				
				JSONStorableAction setVoiceI0 = new JSONStorableAction("Voice intensity 0", () =>
				{
					setArousal( 0.0f );
				});
				
				JSONStorableAction setVoiceI1 = new JSONStorableAction("Voice intensity 1", () =>
				{
					setArousal( 1.0f );
				});
				
				JSONStorableAction setVoiceI2 = new JSONStorableAction("Voice intensity 2", () =>
				{
					setArousal( 2.0f );
				});
				
				JSONStorableAction setVoiceI3 = new JSONStorableAction("Voice intensity 3", () =>
				{
					setArousal( 3.0f );
				});
				
				JSONStorableAction setVoiceI4 = new JSONStorableAction("Voice intensity 4", () =>
				{
					setArousal( 4.0f );
				});
				
				JSONStorableAction setVoiceOrg = new JSONStorableAction("Voice orgasm", () =>
				{
					setArousal( 5.0f );
				});
				
				JSONStorableAction setVoicePerpOrg = new JSONStorableAction("Voice perpetual orgasm", () =>
				{
					setArousal( 6.0f );
				});
				
				JSONStorableAction enableIntensityBoost = new JSONStorableAction("Enable Intensity Boost", () =>
				{
					intensityBoost = true;
				});
				
				JSONStorableAction disableIntensityBoost = new JSONStorableAction("Disable Intensity Boost", () =>
				{
					intensityBoost = false;
				});
				
				JSONStorableAction triggerSetOptimalJawParams = new JSONStorableAction("Set optimal jaw params", () =>
				{
					setOptimalJawParamsCallback();
				});
				
				JSONStorableAction pausePSLTrigger = new JSONStorableAction("Pause Pelvic slap trigger", () =>
				{
					_PSLPaused = true;
				});
				
				JSONStorableAction unpausePSLTrigger = new JSONStorableAction("Unpause Pelvic slap trigger", () =>
				{
					_PSLPaused = false;
				});
				
				JSONStorableAction pauseSQSTrigger = new JSONStorableAction("Pause squishes trigger", () =>
				{
					_SQSPaused = true;
				});
				
				JSONStorableAction unpauseSQSTrigger = new JSONStorableAction("Unpause squishes trigger", () =>
				{
					_SQSPaused = false;
				});
				
				JSONStorableAction pauseBJTrigger = new JSONStorableAction("Pause blowjob trigger", () =>
				{
					_BJPaused = true;
				});
				
				JSONStorableAction unpauseBJTrigger = new JSONStorableAction("Unpause blowjob trigger", () =>
				{
					_BJPaused = false;
				});
				
				JSONStorableAction A_TriggerArousalInteraction = new JSONStorableAction("Trigger arousal interaction", () =>
				{
					DoTriggerArousalInteraction();
				});
				
				JSONStorableAction fakeFuncUseBelow = new JSONStorableAction("- - - - Use these functions below ↓ - - - - -", () => {});

				// **********************************************************************************************************************************
				// "ACTIONS FLOAT/STRING" these are float values that are used for scripting, they are not saved and allow to change runtime values
				// without changing the default parameters set in the plugin used at "scene start"
				// **********************************************************************************************************************************				
				// Interactive Mode
				A_UpdateArousalValue = new JSONStorableString("Update arousal value", "", A_UpdateArousalValueCallback){ isStorable = false, isRestorable=false };
				
				
				// Pelvic slap
				JSONStorableFloat PSLChangeVolume = new JSONStorableFloat("Pelvic slap change volume", PSLVolume.val, (val) => {
					PSLVolumeCallback(val);
				}, 0f, 10f){isStorable=false,isRestorable=false};
				
				JSONStorableFloat PSLChangeMinDelay = new JSONStorableFloat("Pelvic slap change min delay", PSLMinDelay.val, (val) => {
					PSLMinDelayCallback(val);
				}, 0.20f, 1f){isStorable=false,isRestorable=false};
				
				JSONStorableFloat PSLChangeGain = new JSONStorableFloat("Pelvic slap change gain", PSLGain.val, (val) => {
					PSLGainCallback(val);
				}, 0f, 0.5f){isStorable=false,isRestorable=false};
				
				JSONStorableFloat PSLChangeReverbMix = new JSONStorableFloat("Pelvic slap change reverb mix", PSLReverbMix.val, (val) => {
					PSLReverbMixCallback(val);
				}, 0f, 1f){isStorable=false,isRestorable=false};
				
				JSONStorableFloat PSLChangeMinIntensity = new JSONStorableFloat("Pelvic slap change sound min intensity", 1f, (val) => {
					PSLMinIntensityCallback( Mathf.Round(val) );
					PSLMaxIntensityCallback( Mathf.Max(_PSLMaxIntensity, Mathf.Round(val)) );
				}, 1f, 3f){isStorable=false,isRestorable=false};
				
				JSONStorableFloat PSLChangeMaxIntensity = new JSONStorableFloat("Pelvic slap change sound max intensity", 1f, (val) => {
					PSLMaxIntensityCallback( Mathf.Round(val) );
					PSLMinIntensityCallback( Mathf.Min(_PSLMinIntensity, Mathf.Round(val)) );
				}, 1f, 3f){isStorable=false,isRestorable=false};
				
				
				// Squishes
				JSONStorableFloat SQSChangeVolume = new JSONStorableFloat("Squishes change volume", SQSVolume.val, (val) => {
					SQSVolumeCallback(val);
				}, 0f, 10f){isStorable=false,isRestorable=false};
				
				JSONStorableFloat SQSChangeMinDelay = new JSONStorableFloat("Squishes change min delay", SQSMinDelay.val, (val) => {
					SQSMinDelayCallback(val);
				}, 0.1f, 1f){isStorable=false,isRestorable=false};
				
				JSONStorableFloat SQSChangeReverbMix = new JSONStorableFloat("Squishes change reverb mix", SQSReverbMix.val, (val) => {
					SQSReverbMixCallback(val);
				}, 0f, 1f){isStorable=false,isRestorable=false};
				
				
				// Blowjob
				JSONStorableFloat BJChangeVolume = new JSONStorableFloat("Blowjob change volume", BJVolume.val, (val) => {
					BJVolumeCallback(val);
				}, 0f, 10f){isStorable=false,isRestorable=false};
				
				JSONStorableFloat BJChangeMinDelay = new JSONStorableFloat("Blowjob change min delay", BJMinDelay.val, (val) => {
					BJMinDelayCallback(val);
				}, 0.1f, 1f){isStorable=false,isRestorable=false};
				
				JSONStorableFloat BJChangeReverbMix = new JSONStorableFloat("Blowjob change reverb mix", BJReverbMix.val, (val) => {
					BJReverbMixCallback(val);
				}, 0f, 1f){isStorable=false,isRestorable=false};
				
				JSONStorableFloat BJChangeMoanFallbackDelay = new JSONStorableFloat("Blowjob change moan fallback delay", BJMoanFallbackDelay.val, (val) => {
					BJMoanFallbackDelayCallback(val);
				}, 1f, 5f){isStorable=false,isRestorable=false};
				
				// **********************************************************************************************************************************
				// Registering variables, a bit strange to disconnect them from the initial creation, but allows me to order them in the action list
				// **********************************************************************************************************************************
				// Generic triggers
				RegisterAction(fakeFuncUseBelow);
				RegisterAction(setVoiceDisabled);
				RegisterAction(setVoiceBreathing);
				RegisterAction(setVoiceKissing);
				RegisterAction(setVoiceI0);
				RegisterAction(setVoiceI1);
				RegisterAction(setVoiceI2);
				RegisterAction(setVoiceI3);
				RegisterAction(setVoiceI4);
				RegisterAction(setVoiceOrg);			
				RegisterAction(setVoicePerpOrg);			
				RegisterAction(enableIntensityBoost);
				RegisterAction(disableIntensityBoost);
				RegisterString(A_UpdateArousalValue);
				RegisterAction(A_TriggerArousalInteraction);
				
				// Pelvic slap triggers
				RegisterAction(pausePSLTrigger);
				RegisterAction(unpausePSLTrigger);
				RegisterFloat(PSLChangeVolume);
				RegisterFloat(PSLChangeMinDelay);
				RegisterFloat(PSLChangeGain);
				RegisterFloat(PSLChangeReverbMix);
				RegisterFloat(PSLChangeMinIntensity);
				RegisterFloat(PSLChangeMaxIntensity);

				// Squishes triggers
				RegisterAction(pauseSQSTrigger);
				RegisterAction(unpauseSQSTrigger);				
				RegisterFloat(SQSChangeVolume);
				RegisterFloat(SQSChangeMinDelay);
				RegisterFloat(SQSChangeReverbMix);
				
				// Blowjob triggers
				RegisterAction(pauseBJTrigger);
				RegisterAction(unpauseBJTrigger);
				RegisterFloat(BJChangeVolume);
				RegisterFloat(BJChangeMinDelay);
				RegisterFloat(BJChangeReverbMix);
				RegisterFloat(BJChangeMoanFallbackDelay);
				
				RegisterAction(triggerSetOptimalJawParams);
				
				RegisterFloat(VAMMPause);
				
				// ***************************************************************
				// ******* VALUES ************************************************
				// ***************************************************************
				
				JSONStorableAction fakeFuncUseBelow2 = new JSONStorableAction("- - - - Vars below are meant to be read by external plugins ↓ - - - - -", () => {});
				VAMMValue_CurrentArousal = new JSONStorableFloat("VAMM CurrentArousal", -1f, -1f, 4f){ isStorable = false };
				VAMMValue_TriggerCount = new JSONStorableFloat("VAMM TriggerCount", 0f, 0f, 5000f){ isStorable = false };
				VAMMValue_MaxTriggerCount = new JSONStorableFloat("VAMM MaxTriggerCount", 700f, 500f, 1500f){ isStorable = false };
				VAMMValue_IntensitiesCount = new JSONStorableFloat("VAMM IntensitiesCount", 0f, 0f, 5f){ isStorable = false };
				
				RegisterAction(fakeFuncUseBelow2);
				RegisterFloat(VAMMValue_CurrentArousal);
				RegisterFloat(VAMMValue_TriggerCount);
				RegisterFloat(VAMMValue_MaxTriggerCount);
				RegisterFloat(VAMMValue_IntensitiesCount);
				
				// ***************************************************************
				// ******* STUFFS FOR EXTERNAL CALLS *****************************
				// ***************************************************************
				VAMM_GlobalOcclusion_lpf = new JSONStorableFloat("VAMM Global Occlusion LPF", 22000f, 0f, 22000f){ isStorable = false };
				VAMM_GlobalOcclusion_lpf.setCallbackFunction += (val) => { onChangeGlobalOcclusionLPF(); };
				
				RegisterFloat(VAMM_GlobalOcclusion_lpf);
				
				// Initializing my custom animation curves
				initAnimationCurves();
				
				// Other inits
				initAttenuation();
				moanRandOccurence = (int)UnityEngine.Random.Range(randMoanTriggerOccurenceMin.val, randMoanTriggerOccurenceMax.val);
				updateRandomizedMoanHelp();
            }
            catch(Exception e)
            {
                SuperController.LogError("VAMMOAN - Exception caught: " + e);
            }
		}
		
		void Start()
        {				
			setArousal(currentArousal, true);
        }
		
		protected void FixedUpdate(){		
			// **********************************
			// Initialization stuffs start
			// **********************************
			
			// We don't apply the moan logic while the scene is loading OR when we have freeze enabled
			if ( SuperController.singleton.isLoading || SuperController.singleton.freezeAnimation || voices.isLoading == true ) {
				return;
			}
			
			// If the voice isn't initialized, we do that when the scene has finished loading
			if ( !SuperController.singleton.isLoading && voiceInitialized == false ) {
				ChangeVoice();
										
				myGeometry = containingAtom.GetStorableByID("geometry") as DAZCharacterSelector;
				myGender = myGeometry != null ? myGeometry.gender : DAZCharacterSelector.Gender.None;
				
				// Alt futa handling
				foreach (string stId in containingAtom.GetStorableIDs())
				{
					if (stId.Contains("plugin#") && stId.Contains("Stopper.AlternativeFuta"))
					{
						isMyGenderAltFuta = true;
						break;
					}
				}
				
				GenerateDAZMorphsControlUI morphControl = myGeometry?.morphsControlUI;
				if (morphControl != null) {
					// Looking for Life from version 14
					int lifeVersion = 14;
					while( lifeChestMorph == null ) {
						
						string morphDirFemale = "MacGruber.Life." + lifeVersion + ":/Custom/Atom/Person/Morphs/female/MacGruber/Pose/Breathing/";
						string morphDirMale = "MacGruber.Life." + lifeVersion + ":/Custom/Atom/Person/Morphs/male/MacGruber/Pose/Breathing/";

						string morphDir = myGender == DAZCharacterSelector.Gender.Male ? morphDirMale : morphDirFemale;

						lifeChestMorph = morphControl.GetMorphByUid(morphDir + "Breathing_Chest.vmi");
						lifeStomachMorph = morphControl.GetMorphByUid(morphDir + "Breathing_Stomach.vmi");
						lifeNoseOutMorph = morphControl.GetMorphByUid(morphDir + "Breathing_NoseOut.vmi");

						lifeVersion--;
						if( lifeVersion < 10 ) break;
					}
				}
				
				// Arousal control in interactive mode
				initMainArousalHandler();
					
				// Labia / Penis (squishes sounds)
				initSquishesHandler();
				
				// Blowjob (mouth slurpy sounds)
				initBlowjobHandler();
				
				// Initializing the pelvic slap handling
				initPelvicSlapHandler();
			}
				
			// **********************************
			// Initialization stuffs end
			// **********************************
			
			// Internal timer
			vammTimer += Time.fixedDeltaTime;
			
			// Updating all the morphs
			updateMorphs();
			
			// Interactive arousal
			float intAr = getInteractiveArousal();
			
			// Changing the seed to improve randomness every 10sec
			if( vammTimer >= nextSeed ) {
				UnityEngine.Random.seed = (int)UnityEngine.Random.Range(1, 15000);
				nextSeed = vammTimer + 10f;
			}
			
			// Controlling state infos and override that are not really important to update every frame
			if( vammTimer >= nextState )
            {
				// Pitch, if it has changed in the headAudioSource, I'm overriding it here
				if( headAudioSource.GetFloatParamValue("pitch") != VAMMPitch.val ) {
					VAMMPitchCallback( VAMMPitch.val );
				}
				
				// State infos
				nextState = vammTimer + 0.7f;
				stateInfos.val = getStateInfos();
			}
			
			 // Voice disabled, we don't do nothin'
			if( currentArousal == -10.0f ) return;
			
			// Checking the orgasm state
			if( canPlayOrgasm() ) {
				nextOrgasmCheck = vammTimer + 0.2f;
				if( intAr >= 5.0f || currentArousal == 5.0f ) {
					AudioClip oClip = currentClipPlaying = voice.GetTriggeredAudioOrgasm();
					
					if( oClip != null ) {
						// Executing triggers configured for the orgasm
						executeTriggers(5f);
						
						headAudioSource.CallAction("ClearQueue");
						headAudioSource.Stop();
						nextMoan = nextTriggerAllowed = vammTimer + ( oClip.length / Math.Abs(VAMMPitch.val) );
						sexTriggerCount = 0;
						currentArousal = 10000.0f; // Value that will be used to reset at the next cycle
						
						PlayAudio(oClip);
						
						breathStart = vammTimer;
						breathEnd = nextMoan;
					}
				}
			}
	
			// Controlling the sound : moans and shared (kisses, blowjob)
			if( canPlayMoan() )
            {
				// Reseting to 0 if we were waiting for the orgasm to finish
				if( currentArousal == 10000.0f ) {
					// Manually, and not through setArousal function 'coz we need to override the behavior of the function
					// If we have planned another intensity
					if( _nextArousalAfterOrgasm != null ) {
						currentArousal = (float)_nextArousalAfterOrgasm;
						_nextArousalAfterOrgasm = null;
					// Or default, fallback to base intensity
					} else {
						currentArousal = 0f; 
					}
					endOrgasmTrigger.Trigger(); // Manually, this is a specific case, the orgasm just ended
					executeTriggers(0f); // The the other cases
				}
				
				// I'm gonna drive the interactive mode with the "manual" mode
				// The reason behind that is that, if the sounds are triggered immediately from a collision
				// it is really hard to find a good pace or a nice solution to have a pretty seamless and credible voice
				// So since the samples are pretty short, it only delays the audio reaction based on the
				// interaction by 0.2sec to 1sec... tested in VR, it almost un-noticable especially if you don't focus on that
				if( ArousalMode.val == "Interactive" ) {
					float normalizedArousal = (float)Math.Floor( getInteractiveArousal() );
					float lastCollisionDiff = vammTimer - lastCollisionTime;
					float lastInteractionDiff = vammTimer - lastInteractionTime;
									
					// We either get the intensity we want based on the amount of interaction we did
					if( lastCollisionDiff <= minLastCollisionTime ) {
						// Let's imagine we've started to play with the girl, and we stopped a few moments...
						// We're gonna bump up the next moan to the next intensity one time like if she was more excited because you did not touch her enough
						if( lastInteractionTime != -1.0f ) { // First of all, only if we already interacted with her
							if( normalizedArousal >= 1.0f && normalizedArousal < 4.0f && lastInteractionDiff > 9.0f ) { // Now, we only bump up the arousal if we're under the maximum and if we let the girl rest for 9secs
								// We have a 25% chance to go to the next level of intensity
								int randBumpUp = UnityEngine.Random.Range(1, 5);
								if( bumpUpArousalAfterInactivity.val && randBumpUp == 4 ) {
									normalizedArousal += 1.0f;
									sexTriggerCount = Mathf.Round( normalizedArousal * ( maxTriggerCount.val / 4 ) );
								}
							}
						}
						
						currentArousal = normalizedArousal;
						lastInteractionTime = vammTimer;
					// Or we fall back to intensity 0
					} else {
						setArousal(0f);
					}
				}
				
				
				// Intensity boost (used for triggers for instance)
				float triggeredArousal = currentArousal;
				if( ArousalMode.val == "Manual" ) {
					if( currentArousal >= 0.0f && currentArousal < 4.0f ) {
						if( intensityBoost == true ) {
							triggeredArousal += 1.0f;
						}
					}
				}
				
				// Miscs temp vars
				AudioClip clip = null;

				// Randomized trigger calculation
				float randTriggerChance = -1f;
				bool canUseRandomTrigger = triggeredArousal >= 0.0f && triggeredArousal <= 4.0f;
				
				// We can only execute the random trigger if it is enabled and that we are in a situation where
				// it is a moan that is triggered, not breathing, kissing or blowjob
				if (enableRandMoanTrigger.val == true && canUseRandomTrigger == true)
				{
					moanLoopCount++;
					// We have reach the occurence where we wanna play a breathing sound instead of a moan
					if (moanLoopCount > moanRandOccurence)
					{
						// Calculating if we breath or moan
						randTriggerChance = UnityEngine.Random.Range(0f, 1f);
						// Resetting for the next occurence
						moanRandOccurence = (int) UnityEngine.Random.Range(randMoanTriggerOccurenceMin.val, randMoanTriggerOccurenceMax.val);
						moanLoopCount = 0;
					}
				}

				// Randomized trigger playback
				if (enableRandMoanTrigger.val == true && randTriggerChance >= 0f && randTriggerChance <= randMoanTriggerChance.val)
				{
					if (randMoanTriggerTypeChoice.val == "Breathing")
					{
						clip = currentClipPlaying = voice.GetTriggeredAudioBreath();
					}
					else if (randMoanTriggerTypeChoice.val == "Intensity 0" || randMoanTriggerTypeChoice.val == "Intensity 1" || randMoanTriggerTypeChoice.val == "Intensity 2" || randMoanTriggerTypeChoice.val == "Intensity 3" || randMoanTriggerTypeChoice.val == "Intensity 4")
					{
						string typeChoiceStr = randMoanTriggerTypeChoice.val;
						char[] separators = new char[] { ' ' };
						string[] typeChoiceArray = typeChoiceStr.Split(separators);
						float targetIntensity = float.Parse(typeChoiceArray[1]);
						clip = currentClipPlaying = voice.GetTriggeredAudioMoan(targetIntensity);
					}
					else if(randMoanTriggerTypeChoice.val == "Current Intensity -1")
					{
						float targetIntensity = UnityEngine.Mathf.Clamp(triggeredArousal - 1f, 0f, 3f);
						clip = currentClipPlaying = voice.GetTriggeredAudioMoan(targetIntensity);
					}
					else if(randMoanTriggerTypeChoice.val == "Current Intensity -2")
					{
						float targetIntensity = UnityEngine.Mathf.Clamp(triggeredArousal - 2f, 0f, 3f);
						clip = currentClipPlaying = voice.GetTriggeredAudioMoan(targetIntensity);
					}
					else if(randMoanTriggerTypeChoice.val == "Current Intensity +1")
					{
						float targetIntensity = UnityEngine.Mathf.Clamp(triggeredArousal + 1f, 1f, 4f);
						clip = currentClipPlaying = voice.GetTriggeredAudioMoan(targetIntensity);
					}
					else if(randMoanTriggerTypeChoice.val == "Current Intensity +2")
					{
						float targetIntensity = UnityEngine.Mathf.Clamp(triggeredArousal + 2f, 1f, 4f);
						clip = currentClipPlaying = voice.GetTriggeredAudioMoan(targetIntensity);
					}
					else if(randMoanTriggerTypeChoice.val == "Current Intensity +/-1")
					{
						float randIntensityMoan = UnityEngine.Random.Range(0, 2) * 2 - 1;
						float targetIntensity = UnityEngine.Mathf.Clamp(triggeredArousal + randIntensityMoan, 0f, 4f);
						clip = currentClipPlaying = voice.GetTriggeredAudioMoan(targetIntensity);
					}
					else if(randMoanTriggerTypeChoice.val == "Current Intensity +/-2")
					{
						float randIntensityMoan = ( UnityEngine.Random.Range(0, 2) * 2 - 1 ) * 2;
						float targetIntensity = UnityEngine.Mathf.Clamp(triggeredArousal + randIntensityMoan, 0f, 4f);
						clip = currentClipPlaying = voice.GetTriggeredAudioMoan(targetIntensity);
					}
					else if(randMoanTriggerTypeChoice.val == "Current Intensity +/-X")
					{
						float randIntensityMoan = ( UnityEngine.Random.Range(0, 2) * 2 - 1 ) * UnityEngine.Random.Range(1, 3);
						float targetIntensity = UnityEngine.Mathf.Clamp(triggeredArousal + randIntensityMoan, 0f, 4f);
						clip = currentClipPlaying = voice.GetTriggeredAudioMoan(targetIntensity);
					}
					


					if (clip != null)
					{
						float delayNextMoan = (clip.length / Math.Abs(VAMMPitch.val)) - 0.025f; // Overlapping the previous clip 				
						nextMoan = vammTimer + delayNextMoan;
						PlayAudio(clip);

						breathStart = vammTimer;
						breathEnd = nextMoan;
					}
				}
				// Or normal playback
				else
				{
					// Moans
					if (triggeredArousal >= 0.0f && triggeredArousal <= 4.0f)
					{
						clip = currentClipPlaying = voice.GetTriggeredAudioMoan(triggeredArousal);
						if (clip != null)
						{
							
							float delayBreathingEnd = (clip.length / Math.Abs(VAMMPitch.val)) + float.Parse(voice.voiceConfig["config"]["settings"]["overlapLengths"][""+(int) currentArousal].Value); // Overlapping the previous clip - this one is used for the breathing animation to be synced with the sample
							float delayClampMin = (clip.length / Math.Abs(VAMMPitch.val)) * 25 / 100;
							float delayNextMoan = Mathf.Clamp(delayBreathingEnd + VAMMPause.val, delayClampMin, 5f); // Overlapping the previous clip + pause between clips
							
							delayBreathingEnd = vammTimer + delayBreathingEnd;
							nextMoan = vammTimer + delayNextMoan;
							
							PlayAudio(clip);

							breathStart = vammTimer;
							breathEnd = delayBreathingEnd;
						}
					// Perpetual orgasm (handled like basic moans)
					}
					else if (triggeredArousal == 6.0f)
					{
						clip = currentClipPlaying = voice.GetTriggeredAudioPerpetualOrgasm();
						if (clip != null)
						{
							float delayBreathingEnd = (clip.length / Math.Abs(VAMMPitch.val)) + float.Parse(voice.voiceConfig["config"]["settings"]["overlapLengths"]["6"].Value);
							float delayClampMin = (clip.length / Math.Abs(VAMMPitch.val)) * 25 / 100;
							float delayNextMoan = Mathf.Clamp(delayBreathingEnd + VAMMPause.val, delayClampMin, 5f); // Overlapping the previous clip + pause between clips
							
							delayBreathingEnd = vammTimer + delayBreathingEnd;
							nextMoan = vammTimer + delayNextMoan;
							PlayAudio(clip);

							breathStart = vammTimer;
							breathEnd = delayBreathingEnd;
						}
					// Breathing
					}
					else if (currentArousal == -1.0f)
					{
						clip = currentClipPlaying = voice.GetTriggeredAudioBreath();
						if (clip != null)
						{
							float delayNextMoan =
								(clip.length / Math.Abs(VAMMPitch.val)) - 0.025f; // Overlapping the previous clip 				
							nextMoan = vammTimer + delayNextMoan;
							PlayAudio(clip);

							breathStart = vammTimer;
							breathEnd = nextMoan;
						}
					// Kissing
					}
					else if (currentArousal == 100.0f)
					{
						clip = currentClipPlaying = voice.GetTriggeredAudioKiss();
						if (clip != null)
						{
							float delayNextMoan =
								(clip.length / Math.Abs(VAMMPitch.val)) + VAMMKissingSpeed.val -
								0.025f; // Overlapping the previous clip 				
							nextMoan = vammTimer + delayNextMoan;
							PlayAudio(clip);
						}
					}
				}
            }
			
			// Controlling turn off while in interactive mode
			if( ArousalMode.val == "Interactive" ) {
				// We're only starting to turn off the girl after 10 seconds of non-interaction
				if( vammTimer >= nextTurnoffDecrease )
				{
					float lastCollisionDiff = vammTimer - lastCollisionTime;
					if( lastCollisionDiff > 10.0f ) {
						nextTurnoffDecrease = vammTimer + 1.0f;
						decreaseArousal();
					}
				}
			}
		}
		
		protected void setArousal( float arousal, bool ignoreTriggers=false ) {	
			// We don't change the arousal state if the character is in orgasm state
			// We simply plan it to change after the sample is done
			if( currentArousal == 10000.0f ) {
				_nextArousalAfterOrgasm = arousal;
			} else {
				// Executing triggers if we don't ignore them
				if( ignoreTriggers != true ) {
					executeTriggers( arousal );
				}
				
				// Changing current arousal
				currentArousal = arousal;
			}
        }
		
		protected float getInteractiveArousal(float valueToEvaluate = 0f) {
			float triggerCount = valueToEvaluate != null && valueToEvaluate > 0f ? valueToEvaluate : sexTriggerCount;
			float interactiveArousal = ( 4 * triggerCount / maxTriggerCount.val ) + 1;
			return interactiveArousal;
		}
		
		protected void decreaseArousal() {
			if( ArousalMode.val == "Interactive" ) {			
				sexTriggerCount = Mathf.Clamp( sexTriggerCount - sexTriggerDecayIncrement, 0.0f, maxTriggerCount.val );
			}
		}
		
		public string getArousalStateText() {
			// Bypassing the check between manual/interactive, because it is the normal arousal that drives the lock for orgasm
			if( currentArousal == 10000.0f ) {
				if( _nextArousalAfterOrgasm == null ) {
					return "Orgasming (locked until finished)\nNext Intensity: " + getStateTextFromId(0);
				} else {
					int nextStateArousal = (int)(_nextArousalAfterOrgasm);
					return "Orgasming (locked until finished)\nNext Intensity: " + getStateTextFromId(nextStateArousal);
				}
			}
			
			// Now checking depending on the mode
			int stateArousal = (int)(currentArousal);
			
			string stateText = getStateTextFromId(stateArousal);
			
			return stateText;
		}
		
		public string getStateTextFromId(int stateId) {			
			string stateText = "unknown";
			switch ( stateId ) {
				case -10:
					stateText = "Voice disabled";
					break;
				case -1:
					stateText = "Breathing";
					break;
				case 100:
					stateText = "Kissing";
					break;
				case 0:
					stateText = "Intensity 0";
					break;
				case 1:
					stateText = "Intensity 1";
					break;
				case 2:
					stateText = "Intensity 2";
					break;
				case 3:
					stateText = "Intensity 3";
					break;
				case 4:
					stateText = "Intensity 4";
					break;
				case 5:
					stateText = "Orgasm";
					break;
				case 6:
					stateText = "Perpetual Orgasm";
					break;
			}
			
			return stateText;
		}
				 
		public void ChangeVoice()
        {
			// We don't load anything if the scene is still loading
			if ( SuperController.singleton.isLoading ) {
				return;
			}
			
			// If we already have a voice in memory, unloading it
            if (voice != null)
            {
                voice.Unload();
            }
			
            voice = voices.GetVoice(selectedVoice);
            // Preventing loading a voice that does not exists (or is still not loaded... the plugin preset system triggers something odd...)
            if( voice != null ) {
				voice.Load();
				
				voiceInfos.val = "<color=#000><size=30><b>" + voice.name + ":</b></size></color>\n<color=#333><size=25>" + voice.infos + "</size></color>";

				VAMMValue_IntensitiesCount.val = float.Parse(voice.voiceConfig["config"]["settings"]["intensitiesCount"].Value);
				
				voiceInitialized = true;
			}
        }
		
		public void PlayAudio(AudioClip clip)
		{
			if (clip != null)
			{
				headAudioSource.audioSource.PlayOneShot(clip, _voiceVolumeScale);
			}
		}
		
		public void PlayAudioPelvicSlap(AudioClip clip, float pitchMin = 1f, float pitchMax = 1f, float gain = 1f)
		{
			if (clip != null)
			{
				if( pitchMin < 1f || pitchMax > 1f ) {
					pelvicSlapAudioSource.pitch = UnityEngine.Random.Range(pitchMin,pitchMax);
				}
				pelvicSlapAudioSource.PlayOneShot(clip, _PSLVolume * gain);
			}
		}
		
		public void PlayAudioSquishes(AudioClip clip, float pitchMin = 1f, float pitchMax = 1f)
		{
			if (clip != null)
			{
				if( pitchMin < 1f || pitchMax > 1f ) {
					squishesAudioSource.pitch = UnityEngine.Random.Range(pitchMin,pitchMax);
				}
				squishesAudioSource.PlayOneShot(clip, _SQSVolume);
			}
		}
		
		public void PlayAudioSquishesPen(AudioClip clip, float pitchMin = 1f, float pitchMax = 1f)
		{
			if (clip != null)
			{
				if( pitchMin < 1f || pitchMax > 1f ) {
					squishesAudioSourcePen.pitch = UnityEngine.Random.Range(pitchMin,pitchMax);
				}
				squishesAudioSourcePen.PlayOneShot(clip, _SQSVolume);
			}
		}
		
		public void PlayAudioBlowjob(AudioClip clip, float pitchMin = 1f, float pitchMax = 1f)
		{
			if (clip != null)
			{
				float clipPitch = UnityEngine.Random.Range(pitchMin,pitchMax);
				if( pitchMin < 1f || pitchMax > 1f ) {
					mouthAudioSource.pitch = clipPitch;
				}
				mouthAudioSource.PlayOneShot(clip, _BJVolume);
				_BJEndTimeLastSample = vammTimer + ( clip.length / Math.Abs(clipPitch) );
			}
		}
	
		private string getStateInfos() {
			string stateVal = "<color=#000><size=30><b>State:</b></size></color>\n<color=#333><size=25>Character: <b>" + getArousalStateText() + "</b>\n";
			
			if( ArousalMode.val == "Interactive" ) {
				stateVal += "Arousal value: <b>" + (Mathf.Round(sexTriggerCount * 100f) / 100f) + "/" + maxTriggerCount.val + "</b>\n";
			}
			
			stateVal += "</size></color>";
			return stateVal;
		}

		private void updateRandomizedMoanHelp()
		{
			string moanCountStr = "";
			if (randMoanTriggerOccurenceMin.val != randMoanTriggerOccurenceMax.val)
			{
				moanCountStr = randMoanTriggerOccurenceMin.val + " to " + randMoanTriggerOccurenceMax.val;
			}
			else
			{
				moanCountStr = "" + randMoanTriggerOccurenceMin.val;
			}

			string helpText = "<color=#000><size=26><i><b>Enables randomized moan.</b>\nEvery " + moanCountStr + " moan the character will moan or play another type of sound instead. Playback chance is a percentage :\n " + (Mathf.Round(randMoanTriggerChance.val * 100f) / 100f) + " = " + Mathf.Round(randMoanTriggerChance.val*100) + "% chance to play another sample instead of moaning everytime it happens.</i></size></color>";
			randMoanTriggerHelp.val = helpText;
		}

		private void executeTriggers( float? targetArousal = null ) {
			
			// No triggers if we're not initialized
			if( !voiceInitialized ) return;
			
			// Checking triggers when we're switching arousal state
			if( targetArousal != null ) {
				// Trigger when disabling the voice
				if( targetArousal == -10f ) {
					startDisabledTrigger.Trigger();
					return; // We don't want to trigger anything else when disabling
				}
				
				// Triggers when lowering / increasing intensity, just between 0 and 4
				if( targetArousal >= 0f && targetArousal <= 4f && currentArousal >= 0f && currentArousal <= 4f ) {
					if( targetArousal < currentArousal ) {
						intensityLoweredTrigger.Trigger();
					} else if( targetArousal > currentArousal ) {
						intensityIncreasedTrigger.Trigger();
					}
				}
				
				// Checking our potential different cases
				if( targetArousal == -1.0f ) {
					startBreathingTrigger.Trigger();
				} else if( targetArousal == 0f ) {
					startIntensity0Trigger.Trigger();
				} else if( targetArousal == 1f ) {
					startIntensity1Trigger.Trigger();
				} else if( targetArousal == 2f ) {
					startIntensity2Trigger.Trigger();
				} else if( targetArousal == 3f ) {
					startIntensity3Trigger.Trigger();
				} else if( targetArousal == 4f ) {
					startIntensity4Trigger.Trigger();
				} else if( targetArousal == 5f ) {
					reachOrgasmTrigger.Trigger();
				} else if( targetArousal == 100.0f ) {
					startKissingTrigger.Trigger();
				}
			}
		}
		
		private bool canPlayMoan() {
			if( 
				vammTimer >= nextMoan && // when the previous moan sample is done
				vammTimer > _BJEndTimeLastSample + _BJMoanFallbackDelay // when the previous BJ sample is done
			) {
				return true;
			}
			
			return false;
		}
		
		// Checking the orgasm state every 200ms
		// because i'm gonna reset a few variables and delay all the sound that are going to play
		private bool canPlayOrgasm() {
			if( 
				vammTimer >= nextOrgasmCheck && // only every 200ms
				currentArousal != 10000.0f && // if we're not already playing the orgasm sound
				vammTimer > _BJEndTimeLastSample + _BJMoanFallbackDelay && // when the previous BJ sample is done
				(
					ArousalMode.val == "Manual" || // if in  manual mode
					( ArousalMode.val == "Interactive" && allowOrgasm.val == true ) // or in interactive mode only if it is authorized
				)
			) {
				return true;
			}
			
			return false;
		}
		
		// ***************************************
		// Everything related to triggers
		// ***************************************	
		private void initTriggers() {
			// Creating all my triggers and adding them to a global list (to easily control them and save them)
			allTriggers = new List<EventTrigger>();
			
			startDisabledTrigger = new EventTrigger(this, "StartDisabledActions");
			allTriggers.Add(startDisabledTrigger);
			
			startBreathingTrigger = new EventTrigger(this, "StartBreathingActions");
			allTriggers.Add(startBreathingTrigger);
			
			startKissingTrigger = new EventTrigger(this, "StartKissingActions");
			allTriggers.Add(startKissingTrigger);
			
			startBlowjobTrigger = new EventTrigger(this, "StartBlowjobActions");
			allTriggers.Add(startBlowjobTrigger);
			
			startIntensity0Trigger = new EventTrigger(this, "StartIntensity0Actions");
			allTriggers.Add(startIntensity0Trigger);
			
			startIntensity1Trigger = new EventTrigger(this, "StartIntensity1Actions");
			allTriggers.Add(startIntensity1Trigger);
			
			startIntensity2Trigger = new EventTrigger(this, "StartIntensity2Actions");
			allTriggers.Add(startIntensity2Trigger);
			
			startIntensity3Trigger = new EventTrigger(this, "StartIntensity3Actions");
			allTriggers.Add(startIntensity3Trigger);
			
			startIntensity4Trigger = new EventTrigger(this, "StartIntensity4Actions");
			allTriggers.Add(startIntensity4Trigger);
			
			intensityLoweredTrigger = new EventTrigger(this, "IntensityLoweredActions");
			allTriggers.Add(intensityLoweredTrigger);
			
			intensityIncreasedTrigger = new EventTrigger(this, "IntensityIncreasedActions");
			allTriggers.Add(intensityIncreasedTrigger);
			
			reachOrgasmTrigger = new EventTrigger(this, "ReachOrgasmActions");
			allTriggers.Add(reachOrgasmTrigger);
			
			endOrgasmTrigger = new EventTrigger(this, "EndOrgasmActions");
			allTriggers.Add(endOrgasmTrigger);
			
			breathingTrigger = new EventTrigger(this, "BreathingActions");
			allTriggers.Add(breathingTrigger);
		
			// Loading triggers assets
			SimpleTriggerHandler.LoadAssets();
			
			// Adding the rename listener
			SuperController.singleton.onAtomUIDRenameHandlers += OnAtomRename;
		}
		
		private void OnAtomRename(string oldid, string newid)
		{
			foreach( EventTrigger et in allTriggers ) {
				et.SyncAtomNames();
			}
		}
		
		// ***************************************
		// Everything related to morphs animation
		// ***************************************		
		private void updateMorphs() {
			if( VAMMBreathingEnabled.val == false ) return;
			
			if( breathStart != null && breathEnd != null ) {
				if( ( currentArousal >= -1.0f && currentArousal <= 4.0f ) || currentArousal == 5.0f || currentArousal == 6.0f || currentArousal == 10000.0f ) {
					if( lifeChestMorph != null && currentClipPlaying != null ) {
						
						// Selecting my morphs max values
						float chestMorphStartValue;
						float chestMorphEndValue;
						float stomachMorphStartValue;
						float stomachMorphEndValue;
						float noseMorphStartValue;
						float noseMorphEndValue;
						
						float breathChestStartValue = 0.0f;
						float breathChestTargetValue = -0.45f;
						float breathStomachStartValue = 0.0f;
						float breathStomachTargetValue = -0.30f;
						float noseStartValue = 0.40f;
						float noseTargetValue = 0f;
						
						if( currentArousal == 3.0f ) {
							breathChestTargetValue = -0.35f;
							breathStomachTargetValue = -0.25f;
						} else if(currentArousal == 4.0f ) {
							breathChestTargetValue = -0.25f;
							breathStomachTargetValue = -0.20f;
						}
						
						// Selecting curves
						AnimationCurve currentCurveCh = ACBreathingCh;
						AnimationCurve currentCurveSt = ACBreathingSt;

						// Chest
						if( currentArousal == 4.0f ) {
							currentCurveCh = ACIntensity4Ch;
						} else if( currentArousal == 5.0f || currentArousal == 10000.0f ) {
							currentCurveCh = ACOrgasmCh;
						}
						
						// Stomach
						if( currentArousal >= 1.0f ) {
							currentCurveSt = ACIntensity1St;
						}
						
						// Settings my final vars modified by the breathing scale and doing the interpolation
						chestMorphStartValue = breathChestStartValue * VAMMBreathingScale.val;
						chestMorphEndValue = breathChestTargetValue * VAMMBreathingScale.val;
						stomachMorphStartValue = breathStomachStartValue * VAMMBreathingScale.val;
						stomachMorphEndValue = breathStomachTargetValue * VAMMBreathingScale.val;
						noseMorphStartValue = noseStartValue * VAMMBreathingScale.val;
						noseMorphEndValue = noseTargetValue * VAMMBreathingScale.val;

						breathPeriod = (vammTimer-breathStart)/(breathEnd-breathStart);
						
						lifeChestMorph.morphValue = Mathf.Lerp(chestMorphStartValue, chestMorphEndValue, currentCurveCh.Evaluate(breathPeriod) );
						lifeStomachMorph.morphValue = Mathf.Lerp(stomachMorphStartValue, stomachMorphEndValue, currentCurveSt.Evaluate(breathPeriod) );
						lifeNoseOutMorph.morphValue = Mathf.Lerp(noseMorphStartValue, noseMorphEndValue, currentCurveSt.Evaluate(breathPeriod) );
						breathingTrigger.active = true;
						breathingTrigger.TriggerTransition( currentCurveCh.Evaluate(breathPeriod) );
					}
				}
			}
		}
		
		private void resetMorphs() {
			if( lifeChestMorph != null ) {
				lifeChestMorph.morphValue = 0.0f;
				lifeStomachMorph.morphValue = 0.0f;
				lifeNoseOutMorph.morphValue = 0.0f;
			}
		}


		// **************************
		// Pelvic slap
		// **************************
		protected void initPelvicSlapHandler() {
			// If it is disable or the audiosource already exists (handler initialized)
			if( enablePelvicSlap.val == false || pelvicSlapAudioSource != null ) return;
			
			// Initializing gameplay values
			_PSLPaused = false;
			_PSLVolume = PSLVolume.val;
			_PSLGain = PSLGain.val;
			_PSLReverbMix = PSLReverbMix.val;
			_PSLMinDelay = PSLMinDelay.val;
			_PSLMinIntensity = PSLMinIntensity.val;
			_PSLMaxIntensity = PSLMaxIntensity.val;
			
			// Getting the rigidbody around the penis to have an accurate audiosource position
			pslRigidbody01 = containingAtom.rigidbodies.First(rb => rb.name == "pelvis");
			if( pslRigidbody01 != null ) {
				pelvicSlapAudioSource = pslRigidbody01.gameObject.AddComponent<AudioSource>();
				// Setting the default parameters
				pelvicSlapAudioSource.spatialBlend = 1f;
				pelvicSlapAudioSource.dopplerLevel = 0f;
				pelvicSlapAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
				pelvicSlapAudioSource.minDistance = 1f;
				pelvicSlapAudioSource.maxDistance = 1.5f;
				pelvicSlapAudioSource.reverbZoneMix = _PSLReverbMix;
				
				pelvicSlapLPF = pslRigidbody01.gameObject.AddComponent<AudioLowPassFilter>();
				pelvicSlapLPF.cutoffFrequency = 22000.0f;
			}

			// Initializing first trigger
			_PSLTrigger01 = GameObject.CreatePrimitive(PrimitiveType.Capsule);
			_PSLTrigger01.name = "vamm_psl_trigger01";

			_PSLTrigger01.GetComponent<CapsuleCollider>().isTrigger = true;
			_PSLTrigger01.GetComponent<Renderer>().material = new Material(Shader.Find("Battlehub/RTGizmos/Handles")){ color = new Color(1f, 0.1f, 0.1f, 0.3f) };
			_PSLTrigger01.GetComponent<Renderer>().enabled = false;
			_PSLTrigger01.transform.SetParent(pslRigidbody01.gameObject.transform,false);
			PSLTrigger01UpdateTransform();
			_PSLTrigger01.transform.localRotation = Quaternion.Euler(0, 0, 90f);
			
			// Initializing second trigger
			_PSLTrigger02 = GameObject.CreatePrimitive(PrimitiveType.Capsule);
			_PSLTrigger02.name = "vamm_psl_trigger02";

			_PSLTrigger02.GetComponent<CapsuleCollider>().isTrigger = true;
			_PSLTrigger02.GetComponent<Renderer>().material = new Material(Shader.Find("Battlehub/RTGizmos/Handles")){ color = new Color(0.6f, 0.3f, 0.3f, 0.3f) };
			_PSLTrigger02.GetComponent<Renderer>().enabled = false;
			_PSLTrigger02.transform.SetParent(pslRigidbody01.gameObject.transform,false);
			PSLTrigger02UpdateTransform();
			_PSLTrigger02.transform.localRotation = Quaternion.Euler(45f, 0, 90f);
		
			SexTriggerCollide collideComp;
			collideComp = pslRigidbody01.gameObject.AddComponent<SexTriggerCollide>();
			collideComp.OnTrigger += PslCollisionObserver;
			
			PSLShowTriggers(false);
		}
		
		protected void clearPelvicSlapHandler() {
			if( pslRigidbody01 == null ) return; 
			// Disabling observers and removing components
			SexTriggerCollide tmp;
			
			pslRigidbody01.gameObject.GetComponent<SexTriggerCollide>().OnTrigger -= PslCollisionObserver;
			tmp = pslRigidbody01.GetComponent<SexTriggerCollide>();
			if( tmp != null ) Destroy(tmp);
						
			if( pelvicSlapAudioSource != null ) Destroy( pelvicSlapAudioSource );
			if( pelvicSlapLPF != null ) Destroy( pelvicSlapLPF );
			pelvicSlapAudioSource = null;
			
			if( _PSLTrigger01 != null ) Destroy( _PSLTrigger01 );
			if( _PSLTrigger02 != null ) Destroy( _PSLTrigger02 );
			_PSLTrigger01 = _PSLTrigger02 = null;
		}
		
		protected void PSLTrigger01UpdateTransform() {
			if( _PSLTrigger01 == null ) return;
			_PSLTrigger01.transform.localPosition = _PSLTrigger01DefaultPos + new Vector3(0,PSLTrigger01YOffset.val,PSLTrigger01ZOffset.val);
			_PSLTrigger01.transform.localScale = _PSLTrigger01DefaultScale;
		}
		
		protected void PSLTrigger02UpdateTransform() {
			if( _PSLTrigger02 == null ) return;
			_PSLTrigger02.transform.localPosition = _PSLTrigger02DefaultPos + new Vector3(0,PSLTrigger02YOffset.val,PSLTrigger02ZOffset.val);
			_PSLTrigger02.transform.localScale = _PSLTrigger02DefaultScale;
		}

		protected void PSLShowTriggers( bool show ) {
			if( _PSLTrigger01 == null ) return;
			_PSLTrigger01.GetComponent<Renderer>().enabled = show;
			_PSLTrigger02.GetComponent<Renderer>().enabled = show;
		}
		
		// **************************
		// Main arousal handling
		// **************************
		protected void initMainArousalHandler() {
			if( myGender == null ) return;
			if( myGender == DAZCharacterSelector.Gender.Female ) {
				vaTrig = containingAtom.rigidbodies.First(rb => rb.name == "VaginaTrigger");
				vaTrigSTC = vaTrig.gameObject.AddComponent<SexTriggerCollide>();
				vaTrigSTC.OnTrigger += FemaleArousalObserver;
			}
			
			if( myGender == DAZCharacterSelector.Gender.Male || isMyGenderAltFuta ) {
				penTrig01 = containingAtom.rigidbodies.First(rb => rb.name == "Gen1");
				penTrig01STC = penTrig01.gameObject.AddComponent<SexTriggerCollide>();
				penTrig01STC.OnCollide += MaleArousalObserver;
				
				penTrig02 = containingAtom.rigidbodies.First(rb => rb.name == "Gen2");
				penTrig02STC = penTrig02.gameObject.AddComponent<SexTriggerCollide>();
				penTrig02STC.OnCollide += MaleArousalObserver;
				
				anaTrig01 = containingAtom.rigidbodies.First(rb => rb.name == "_JointAlMale");
				anaTrig01STC = anaTrig01.gameObject.AddComponent<SexTriggerCollide>();
				anaTrig01STC.OnCollide += MaleArousalObserver;
			}		
		}
		
		protected void clearMainArousalHandler() {
			if( myGender == null || vaTrig == null ) return; 
			// Disabling observers and removing components
			SexTriggerCollide tmp;
			
			if( myGender == DAZCharacterSelector.Gender.Female ) {
				if (vaTrigSTC != null)
				{
					vaTrigSTC.OnTrigger -= FemaleArousalObserver;
					Destroy( vaTrigSTC );
				}
			}
			
			if( myGender == DAZCharacterSelector.Gender.Male || isMyGenderAltFuta ) {
				
				if (penTrig01STC != null)
				{
					penTrig01STC.OnCollide -= MaleArousalObserver;
					Destroy( penTrig01STC );
				}
				
				if (penTrig02 != null)
				{
					penTrig02STC.OnCollide -= MaleArousalObserver;
					Destroy( penTrig02 );
				}
				
				if (anaTrig01STC != null)
				{
					anaTrig01STC.OnCollide -= MaleArousalObserver;
					Destroy( anaTrig01STC );
				}
			}
		}
		
		// **************************
		// Squishes
		// **************************
		protected void initSquishesHandler() {			
			// If it is disable or the audiosource already exists (handler initialized)
			if( enableSquishes.val == false || squishesAudioSource != null || squishesAudioSourcePen != null ) return;

			// Initializing gameplay values
			_SQSPaused = false;
			_SQSVolume = SQSVolume.val;
			_SQSReverbMix = SQSReverbMix.val;
			_SQSMinDelay = SQSMinDelay.val;
			
			// Female / Labia (squishes sounds)
			if( myGender != null && myGender == DAZCharacterSelector.Gender.Female ) {
				labTrig = containingAtom.rigidbodies.First(rb => rb.name == "LabiaTrigger");
				if( labTrig != null ) {
					squishesAudioSource = labTrig.gameObject.AddComponent<AudioSource>();
					
					squishesLPF = labTrig.gameObject.AddComponent<AudioLowPassFilter>();
					squishesLPF.cutoffFrequency = 22000.0f;
				}
				
				labTrigSTC = labTrig.gameObject.AddComponent<SexTriggerCollide>();
				labTrigSTC.OnTrigger += LabTrigObserver;
			
			}
			
			// Male / Penis (squishes sounds)
			if ( myGender != null && ( myGender == DAZCharacterSelector.Gender.Male || isMyGenderAltFuta ) )
			{
				penTrig01STC.OnCollide += PenTrigObserver;
				penTrig02STC.OnCollide += PenTrigObserver;

				if( penTrig01 != null ) {
					squishesAudioSourcePen = penTrig01.gameObject.AddComponent<AudioSource>();
					
					squishesLPFPen = penTrig01.gameObject.AddComponent<AudioLowPassFilter>();
					squishesLPFPen.cutoffFrequency = 22000.0f;
				}

			}

			if( squishesAudioSource != null )
			{
				// Setting the default parameters of the audio source
				squishesAudioSource.spatialBlend = 1f;
				squishesAudioSource.dopplerLevel = 0f;
				squishesAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
				squishesAudioSource.minDistance = 1f;
				squishesAudioSource.maxDistance = 1.5f;
				squishesAudioSource.reverbZoneMix = _SQSReverbMix;
			}
			
			if( squishesAudioSourcePen != null )
			{
				// Setting the default parameters of the audio source
				squishesAudioSourcePen.spatialBlend = 1f;
				squishesAudioSourcePen.dopplerLevel = 0f;
				squishesAudioSourcePen.rolloffMode = AudioRolloffMode.Logarithmic;
				squishesAudioSourcePen.minDistance = 1f;
				squishesAudioSourcePen.maxDistance = 1.5f;
				squishesAudioSourcePen.reverbZoneMix = _SQSReverbMix;
			}
		}
		
		protected void clearSquishesHandler() {
			// Disabling observers and removing components
			SexTriggerCollide tmp;
			if( myGender != null && myGender == DAZCharacterSelector.Gender.Female )
			{
				if (labTrig == null) return;
				if (labTrigSTC != null)
				{
					labTrigSTC.OnTrigger -= LabTrigObserver;
					Destroy( labTrigSTC );
				}
			}
			
			if ( myGender != null && ( myGender == DAZCharacterSelector.Gender.Male || isMyGenderAltFuta ) ) {
				if (penTrig01 == null && penTrig02 == null) return;

				penTrig01STC.OnCollide -= PenTrigObserver;
				penTrig02STC.OnCollide -= PenTrigObserver;
				// Not removing the components as they're needed by the arousal system contrary to female squishes based on labias
			}
			
			if( squishesAudioSource != null ) Destroy( squishesAudioSource );
			if( squishesAudioSourcePen != null ) Destroy( squishesAudioSourcePen );
			if( squishesLPF != null ) Destroy( squishesLPF );
			if( squishesLPFPen != null ) Destroy( squishesLPFPen );
			squishesAudioSource = null;
			squishesAudioSourcePen = null;
		}
		
		// **************************
		// Blowjob/Mouth
		// **************************
		protected void initBlowjobHandler() {
			// If it is disable or the audiosource already exists (handler initialized)
			if( enableBlowjob.val == false || mouthAudioSource != null ) return;
			
			// Initializing gameplay values
			_BJPaused = false; // will start paused based on the options of the user/creator
			_BJVolume = BJVolume.val;
			_BJMinDelay = BJMinDelay.val;
			_BJReverbMix = BJReverbMix.val;
			_BJMoanFallbackDelay = BJMoanFallbackDelay.val;
			
			// Blowjob/mouth (slurpy sounds)
			mouTrig = containingAtom.rigidbodies.First(rb => rb.name == "MouthTrigger");
			if( mouTrig != null ) {
				mouthAudioSource = mouTrig.gameObject.AddComponent<AudioSource>();
				// Setting the default parameters
				mouthAudioSource.spatialBlend = 1f;
				mouthAudioSource.dopplerLevel = 0f;
				mouthAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
				mouthAudioSource.minDistance = 1f;
				mouthAudioSource.maxDistance = 1.5f;
				mouthAudioSource.reverbZoneMix = _BJReverbMix;
				
				mouthLPF = mouTrig.gameObject.AddComponent<AudioLowPassFilter>();
				mouthLPF.cutoffFrequency = 22000.0f;
			}
			
			mouTrig.gameObject.AddComponent<SexTriggerCollide>().OnTrigger += MouTrigObserver;
		}
		
		protected void clearBlowjobHandler() {
			if( mouTrig == null ) return; 
			// Disabling observers and removing components
			SexTriggerCollide tmp;
			
			mouTrig.gameObject.GetComponent<SexTriggerCollide>().OnTrigger -= MouTrigObserver;
			tmp = mouTrig.gameObject.GetComponent<SexTriggerCollide>();
			if( tmp != null ) Destroy( tmp );
						
			if( mouthAudioSource != null ) Destroy( mouthAudioSource );
			if( mouthLPF != null ) Destroy( mouthLPF );
			mouthAudioSource = null;
		}
		
		// **************************
		// Callbacks and similar
		// **************************		
		protected void PSLEditAdvOptionsCallback( bool show ) {
			foreach( UIDynamic uidElem in PSLAdvOptionsElements ) {
				if( show == true ) {
					uidElem.GetComponent<LayoutElement>().transform.localScale = new Vector3(1,1,1);
					uidElem.GetComponent<LayoutElement>().ignoreLayout = false;
					PSLShowTriggers(true);
				} else {
					uidElem.GetComponent<LayoutElement>().transform.localScale = new Vector3(0,0,0);
					uidElem.GetComponent<LayoutElement>().ignoreLayout = true;
					PSLShowTriggers(false);
				}
			}
		}
		
		protected void SQSEditAdvOptionsCallback( bool show ) {
			foreach( UIDynamic uidElem in SQSAdvOptionsElements ) {
				if( show == true ) {
					uidElem.GetComponent<LayoutElement>().transform.localScale = new Vector3(1,1,1);
					uidElem.GetComponent<LayoutElement>().ignoreLayout = false;
				} else {
					uidElem.GetComponent<LayoutElement>().transform.localScale = new Vector3(0,0,0);
					uidElem.GetComponent<LayoutElement>().ignoreLayout = true;
				}
			}
		}
		
		protected void BJEditAdvOptionsCallback( bool show ) {
			foreach( UIDynamic uidElem in BJAdvOptionsElements ) {
				if( show == true ) {
					uidElem.GetComponent<LayoutElement>().transform.localScale = new Vector3(1,1,1);
					uidElem.GetComponent<LayoutElement>().ignoreLayout = false;
				} else {
					uidElem.GetComponent<LayoutElement>().transform.localScale = new Vector3(0,0,0);
					uidElem.GetComponent<LayoutElement>().ignoreLayout = true;
				}
			}
		}
		
		protected void switchArousalModeCallback(string selectedmode) {
			// Initializing if we're switching values
			// Breathing
			if( selectedmode == "Manual" ) {
				currentArousal = -1.0f; 
			} else {
				currentArousal = 0; 
			}
			// Resetting trigger count
			sexTriggerCount = 0;
			
		}
	    
		protected void maxTriggerCountCallback ( float triggerCount ) {
			if (VAMMValue_MaxTriggerCount != null)
			{
				VAMMValue_MaxTriggerCount.val = triggerCount;
			}
		}
		
		protected void arousalRateCallback( float arousalRate ) {}
		
		protected void VAMMVolumeCallback ( float volumeval ) {
			// Unity 的 AudioSource.volume 被钳制在 0-1，超出部分改由 PlayOneShot 的 volumeScale 承担
			float baseVol = Mathf.Min(1f, volumeval);
			_voiceVolumeScale = (baseVol > 0.0001f) ? (volumeval / baseVol) : 1f;
			headAudioSource.SetFloatParamValue("volume", baseVol);
		}
		
		protected void VAMMPitchCallback ( float pitchval ) {
			headAudioSource.SetFloatParamValue("pitch",pitchval);
		}
		
		protected void VAMMPauseCallback ( float pitchval ) {
			// Nothing for now
		}

		protected void VAMMReverbEnabledCallback( bool spatializeval ) {
			headAudioSource.SetBoolParamValue("spatialize", !spatializeval );
		}
		
		protected void VAMMAudioMinDistanceCallback( float minDist ) {
			headAudioSource.SetFloatParamValue("minDistance", minDist );
		}
		
		protected void VAMMAudioMaxDistanceCallback( float maxDist ) {
			headAudioSource.SetFloatParamValue("maxDistance", maxDist );
		}
		
		private void A_UpdateArousalValueCallback(string customText) {
			if( customText != null && customText != "" ) DoUpdateArousalValue( customText );
			A_UpdateArousalValue.valNoCallback = "";
		}
		
		protected void enablePelvicSlapCallback( bool enabled ) {
			if ( SuperController.singleton.isLoading || voiceInitialized == false ) return;
			
			if( enabled == true ) {
				initPelvicSlapHandler();
			} else {
				PSLEditAdvOptions.SetVal(false);
				clearPelvicSlapHandler();
			}
		}
		
		protected void PSLVolumeCallback( float volume ) {
			_PSLVolume = volume;
		}
		
		protected void PSLGainCallback( float gain ) {
			_PSLGain = gain;
		}
		
		protected void PSLReverbMixCallback( float mix ) {
			_PSLReverbMix = mix;
			if( pelvicSlapAudioSource != null ) {
				pelvicSlapAudioSource.reverbZoneMix = _PSLReverbMix;
			}
		}
		
		protected void PSLMinDelayCallback( float delay ) {
			_PSLMinDelay = delay;
		}
		
		protected void PSLMinIntensityCallback( float minint ) {
			_PSLMinIntensity = minint;
		}
		
		protected void PSLMaxIntensityCallback( float maxint ) {
			_PSLMaxIntensity = maxint;
		}
		
		protected void PSLTrigger01OffsetCallback( float offset ) {
			PSLTrigger01UpdateTransform();
		}
		
		protected void PSLTrigger02OffsetCallback( float offset ) {
			PSLTrigger02UpdateTransform();
		}
		
		protected void enableSquishesCallback( bool enabled ) {
			if ( SuperController.singleton.isLoading || voiceInitialized == false ) return;
			
			if( enabled == true ) {
				initSquishesHandler();
			} else {
				SQSEditAdvOptions.SetVal(false);
				clearSquishesHandler();
			}
		}
		
		protected void SQSVolumeCallback( float volume ) {
			_SQSVolume = volume;
		}
		
		protected void SQSReverbMixCallback( float mix ) {
			_SQSReverbMix = mix;
			if( squishesAudioSource != null ) {
				squishesAudioSource.reverbZoneMix = _SQSReverbMix;
			}
			if( squishesAudioSourcePen != null ) {
				squishesAudioSourcePen.reverbZoneMix = _SQSReverbMix;
			}
		}
		
		protected void SQSMinDelayCallback( float delay ) {
			_SQSMinDelay = delay;
		}
		
		protected void enableBlowjobCallback( bool enabled ) {
			if ( SuperController.singleton.isLoading || voiceInitialized == false ) return;
			
			if( enabled == true ) {
				initBlowjobHandler();
			} else {
				// BJEditAdvOptions.SetVal(false);
				clearBlowjobHandler();
			}
		}
		
		protected void BJVolumeCallback( float volume ) {
			_BJVolume = volume;
		}
		
		protected void BJReverbMixCallback( float mix ) {
			_BJReverbMix = mix;
			if( mouthAudioSource != null ) {
				mouthAudioSource.reverbZoneMix = _BJReverbMix;
			}
		}
		
		protected void BJMinDelayCallback( float delay ) {
			_BJMinDelay = delay;
		}
		
		protected void BJMoanFallbackDelayCallback( float delay ) {
			_BJMoanFallbackDelay = delay;
		}
		
		protected void VAMMBreathingEnabledCallback( bool state ) {}

		protected void randMoanTriggerOccurenceMinCallback(float min)
		{
			updateRandomizedMoanHelp();
		}
		
		protected void randMoanTriggerOccurenceMaxCallback(float min)
		{
			updateRandomizedMoanHelp();
		}
		
		protected void randMoanTriggerChanceCallback(float scale)
		{
			updateRandomizedMoanHelp();
		}
		
		protected void VAMMBreathingScaleCallback( float scale ) {
			resetMorphs();
		}
		
		protected void VAMMAutoJawCallback( bool state ) {
			JawControlAJ.SetBoolParamValue("driveXRotationFromAudioSource", state); // Setting the jaw control in "Misc Physics"
			AutoJawMouthMorphsJS.SetBoolParamValue("enabled", state); // Settings the jaw mouth control in "Auto behaviors"
		}
		
		protected void setOptimalJawParamsCallback() {
			if( voice != null ) {
				JawControlAJ.SetFloatParamValue("spring", float.Parse( voice.voiceConfig["config"]["settings"]["jawHoldSpring"].Value ) );
				JawControlAJ.SetFloatParamValue("damper", float.Parse( voice.voiceConfig["config"]["settings"]["jawHoldDamper"].Value ) );
				JawControlAJ.SetFloatParamValue("driveXRotationFromAudioSourceMultiplier", float.Parse( voice.voiceConfig["config"]["settings"]["audioDriveMultiplier"].Value ) );
				JawControlAJ.SetFloatParamValue("driveXRotationFromAudioSourceAdditionalAngle", float.Parse( voice.voiceConfig["config"]["settings"]["audioDriveExtraAngle"].Value ) );
				JawControlAJ.SetFloatParamValue("driveXRotationFromAudioSourceMaxAngle", float.Parse( voice.voiceConfig["config"]["settings"]["audioDriveMaxAngle"].Value ) );
			}
		}

		protected void onChangeGlobalOcclusionLPF()
		{
			if (mouthLPF != null) mouthLPF.cutoffFrequency = VAMM_GlobalOcclusion_lpf.val;
			if (squishesLPF != null) squishesLPF.cutoffFrequency = VAMM_GlobalOcclusion_lpf.val;
			if (squishesLPFPen != null) squishesLPFPen.cutoffFrequency = VAMM_GlobalOcclusion_lpf.val;
			if (pelvicSlapLPF != null) pelvicSlapLPF.cutoffFrequency = VAMM_GlobalOcclusion_lpf.val;
		}

		// **************************
		// INTERACTIVE MODE
		// **************************
		protected void DoUpdateArousalValue(string customText){
			// Parsing the value
			float arousalChangeVal = 0f;
			try
			{
				arousalChangeVal = float.Parse(customText);
				
				float normalizedArousal = (float)Math.Floor( getInteractiveArousal( sexTriggerCount + 1 ) );
				executeTriggers( normalizedArousal );
				
				sexTriggerCount += arousalChangeVal;
			}			
            catch(Exception e)
            {
                logDebug("VAMMoan : the value you have typed in cannot be converted to float. Allowed characters are : numbers, dot and minus sign. Example: -10.5");
				logDebug("VAMMoan : Update arousal exception :" + e);
				return;
            }
		}
		
		protected void DoTriggerArousalInteraction(){
			// Updating the last collision time ( it is not a collision, but it should act like it is )
			lastCollisionTime = vammTimer;
		}
		
		// **************************
		// COROUTINES
		// **************************
		// Inspired from ToumeiHitsuji's timeout process in slap stuff
		private IEnumerator pelvisSlapTimeout()
		{
			_PSLAllowed = false;
			yield return new WaitForSeconds(_PSLMinDelay);
			_PSLAllowed = true;
		}
		
		private IEnumerator squishesTimeout()
		{
			_SQSAllowed = false;
			yield return new WaitForSeconds(_SQSMinDelay);
			_SQSAllowed = true;
		}
		
		private IEnumerator squishesPenTimeout()
		{
			_SQSPAllowed = false;
			yield return new WaitForSeconds(_SQSMinDelay);
			_SQSPAllowed = true;
		}
		
		private IEnumerator blowjobTimeout()
		{
			_BJAllowed = false;
			yield return new WaitForSeconds(_BJMinDelay);
			_BJAllowed = true;
		}
			
		// **************************
		// Collision events
		// **************************
		protected void FemaleArousalObserver(object sender, TriggerEventArgs e)
        {
			if(e == null || e.evtType == null) return;
			
			if( ArousalMode.val != "Interactive" ) return;
			if (e.evtType == "Entered" && vammTimer >= nextTriggerAllowed )
			{
				float normalizedArousal = (float)Math.Floor( getInteractiveArousal( sexTriggerCount + 1 ) );
				executeTriggers( normalizedArousal );
				
				sexTriggerCount += 1.0f * arousalRate.val;
				
				// Updating the last collision time
				lastCollisionTime = vammTimer;
			}
        }
		
		protected void MaleArousalObserver(object sender, CollideEventArgs e)
        {
			if(e == null || e.evtType == null) return;
			
			if( ArousalMode.val != "Interactive" ) return;
			if (e.evtType == "CEntered" && vammTimer >= nextTriggerAllowed && vammTimer > lastCollisionTime + maleCollisionTimeout )
			{
				float normalizedArousal = (float)Math.Floor( getInteractiveArousal( sexTriggerCount + 1 ) );
				executeTriggers( normalizedArousal );
				
				sexTriggerCount += 1.0f * arousalRate.val;
				
				// Updating the last collision time
				lastCollisionTime = vammTimer;
			}
        }
		
		protected void LabTrigObserver(object sender, TriggerEventArgs e)
        {
			if(e == null || e.evtType == null) return;
			
			// Timeout
			if (_SQSAllowed == false || _SQSPaused == true ) return;
			
			if (e.evtType == "Entered" )
			{
				// Preventing any other sounds to be played
				StartCoroutine("squishesTimeout");
				
				AudioClip sqsclip = voice.GetTriggeredAudioSquish(1);
				if (sqsclip != null)
				{
					PlayAudioSquishes(sqsclip,0.85f,1.15f);
				}
			}
        }
		
		protected void PenTrigObserver(object sender, CollideEventArgs e)
        {
			if(e == null || e.evtType == null) return;
			
			// Timeout
			if (_SQSPAllowed == false || _SQSPaused == true ) return;
			
			if (e.evtType == "CEntered" )
			{
				// Preventing any other sounds to be played
				StartCoroutine("squishesPenTimeout");
				
				AudioClip sqspclip = voice.GetTriggeredAudioSquishPenis(1);
				if (sqspclip != null)
				{
					PlayAudioSquishesPen(sqspclip,0.85f,1.15f);
				}
			}
        }
		
		protected void MouTrigObserver(object sender, TriggerEventArgs e)
        {
			if(e == null || e.evtType == null) return;
			
			// Timeout
			if (_BJAllowed == false || _BJPaused == true ) return;
			
			if (e.evtType == "Entered" )
			{
				// Preventing any other sounds to be played
				StartCoroutine("blowjobTimeout");
				
				// Triggering the bj trigger "start" if we have reached the moment where the fallback occured
				if( vammTimer > _BJEndTimeLastSample + _BJMoanFallbackDelay ) {
					startBlowjobTrigger.Trigger();
				}
				
				AudioClip bjclip = voice.GetTriggeredAudioBlowjob(0f);
				if (bjclip != null)
				{
					PlayAudioBlowjob(bjclip,0.85f,1.15f);
				}
			}
        }
		
		// Handles the pelvic slappy slappy stuff :3
		protected void PslCollisionObserver(object sender, TriggerEventArgs e)
		{
			if(e == null || e.evtType == null) return;

			// Timeout
			if (_PSLAllowed == false || _PSLPaused == true ) return;

			// No rigidbody for velocity computation?
			if (e.collider.attachedRigidbody == null) return;
			
			// No self collision
			if( containingAtom == e.collider.gameObject.GetComponentInParent<Atom>() ) return;

			// Only on enter
			if (e.evtType == "Entered" )
			{
				// Preventing any other sounds to be played
				StartCoroutine("pelvisSlapTimeout");
				
				// Relative velocity of my rigidbody and the collider triggered
				float computedVelocity = Vector3.Magnitude ( e.collider.attachedRigidbody.velocity - pslRigidbody01.velocity );
				
				// If the velocity isn't enough, then we ignore this (to prevent the timeout)
				if( computedVelocity < 0.1 ) return;			
				_PSLLastVelocity = computedVelocity;
							
				float intensityValue = 1f;
				if( computedVelocity > 0.20f && computedVelocity < 0.35f ) {
					intensityValue = 2f;
				} else if( computedVelocity > 0.35f ) {
					intensityValue = 3f;
				}
				
				// Clamping the value of intensity based on user settings
				intensityValue = Mathf.Round( Mathf.Clamp( intensityValue, _PSLMinIntensity, _PSLMaxIntensity ) );
				
				// 速度 → 音量增益：0.35 m/s 为 1.0 倍基准，速度上限 0.8，斜率由 Slap sound gain 控制
				float velocityForGain = Mathf.Clamp( computedVelocity, 0.1f, 0.8f );
				float slapGain = 1f + 4f * _PSLGain * ( velocityForGain - 0.35f );
				
				AudioClip pslclip = voice.GetTriggeredAudioPelvicSlap(intensityValue);
				if (pslclip != null)
				{
					PlayAudioPelvicSlap(pslclip,0.85f,1.15f,slapGain);
				}
			}
        }
		
		// **************************
		// OVERRIDES FOR THE TRIGGERS
		// **************************	
		public override JSONClass GetJSON(bool includePhysical = true, bool includeAppearance = true, bool forceStore = false)
		{
			JSONClass jc = base.GetJSON(includePhysical, includeAppearance, forceStore);
			if (includePhysical || forceStore)
			{
				needsStore = true;
				foreach( EventTrigger et in allTriggers ) {
					jc[et.Name] = et.GetJSON(base.subScenePrefix);
				}
				
			}
			return jc;
		}

		public override void LateRestoreFromJSON(JSONClass jc, bool restorePhysical = true, bool restoreAppearance = true, bool setMissingToDefault = true)
		{
			base.LateRestoreFromJSON(jc, restorePhysical, restoreAppearance, setMissingToDefault);
			if (!base.physicalLocked && restorePhysical && !IsCustomPhysicalParamLocked("trigger"))
			{
				foreach( EventTrigger et in allTriggers ) {
					et.RestoreFromJSON(jc, base.subScenePrefix, base.mergeRestore, setMissingToDefault);
				}
			}
		}
		
		// **************************
		// MY TOOLS
		// **************************
		private static void logDebug( string debugText ) {
			SuperController.LogMessage( debugText );
		}
		
		private static void disableScrollOnText(UIDynamicTextField target) {
			// THIS is important, it's a "useless" temporary canvas group that will prevent the event from the mousewheel
			CanvasGroup tmpCG = target.UItext.transform.parent.transform.parent.transform.parent.gameObject.AddComponent<CanvasGroup>();
			tmpCG.blocksRaycasts = false;
			
			ScrollRect targetSR = target.UItext.transform.parent.transform.parent.transform.parent.GetComponent<ScrollRect>();
			if( targetSR != null ) {
				targetSR.horizontal = false;
				targetSR.vertical = false;
			}
		}
		
		private JSONStorableString createStaticDescriptionText( string DescTitle, string DescText, bool rightSide, int fieldHeight ) {
			JSONStorableString staticDescString = new JSONStorableString(DescTitle,DescText);
			UIDynamicTextField staticDescStringField = CreateTextField(staticDescString, rightSide);
			staticDescStringField.backgroundColor = new Color(1f, 1f, 1f, 0f);
			LayoutElement sdsfLayout = staticDescStringField.GetComponent<LayoutElement>();
			sdsfLayout.preferredHeight = sdsfLayout.minHeight = fieldHeight;
			staticDescStringField.height = fieldHeight;
			disableScrollOnText(staticDescStringField);

			return staticDescString;
		}
		
		// ***********************************************************
		// EXTERNAL TOOLS - Thank you great coders for your content!
		// ***********************************************************
		
		// *********** MacGruber_Utils.cs START *********************
		// Get directory path where the plugin is located. Based on Alazi's & VAMDeluxe's method.
		public static string GetPluginPath(MVRScript self)
		{
			string id = self.name.Substring(0, self.name.IndexOf('_'));
			string filename = self.manager.GetJSON()["plugins"][id].Value;
			return filename.Substring(0, filename.LastIndexOfAny(new char[] { '/', '\\' }));
		}
				
		// Get path prefix of the package that contains our plugin.
		public static string GetPackagePath(MVRScript self)
		{
			string id = self.name.Substring(0, self.name.IndexOf('_'));
			string filename = self.manager.GetJSON()["plugins"][id].Value;
			int idx = filename.IndexOf(":/");
			if (idx >= 0)
				return filename.Substring(0, idx+2);
			else
				return string.Empty;
		}
				
		// Check if our plugin is running from inside a package
		public static bool IsInPackage(MVRScript self)
		{
			string id = self.name.Substring(0, self.name.IndexOf('_'));
			string filename = self.manager.GetJSON()["plugins"][id].Value;
			return filename.IndexOf(":/") >= 0;
		}	
		
		// ===========================================================================================
		// TriggerHandler implementation for easier handling of custom triggers.
		// Essentially call this in your plugin init code:
		//     StartCoroutine(SimpleTriggerHandler.LoadAssets());
		//
		// Credit to AcidBubbles for figuring out how to do custom triggers.
		//
		// hazmhox note : Edited a bit to allow the use of transitions ( original code coming from LogicBricks 4 )
		public class SimpleTriggerHandler : TriggerHandler
		{
			public static bool Loaded { get; private set; }

			private static SimpleTriggerHandler myInstance;

			private RectTransform myTriggerActionsPrefab;
			private RectTransform myTriggerActionMiniPrefab;
			private RectTransform myTriggerActionDiscretePrefab;
			private RectTransform myTriggerActionTransitionPrefab;

			public static SimpleTriggerHandler Instance {
				get {
					if (myInstance == null)
						myInstance = new SimpleTriggerHandler();
					return myInstance;
				}
			}

			public static void LoadAssets()
			{
				SuperController.singleton.StartCoroutine(Instance.LoadAssetsInternal());
			}

			private IEnumerator LoadAssetsInternal()
			{
				foreach (var x in LoadAsset("z_ui2", "TriggerActionsPanel", p => myTriggerActionsPrefab = p))
					yield return x;
				foreach (var x in LoadAsset("z_ui2", "TriggerActionMiniPanel", p => myTriggerActionMiniPrefab = p))
					yield return x;
				foreach (var x in LoadAsset("z_ui2", "TriggerActionDiscretePanel", p => myTriggerActionDiscretePrefab = p))
					yield return x;
				foreach (var x in LoadAsset("z_ui2", "TriggerActionTransitionPanel", p => myTriggerActionTransitionPrefab = p))
					yield return x;

				Loaded = true;
			}

			private IEnumerable LoadAsset(string assetBundleName, string assetName, Action<RectTransform> assign)
			{
				AssetBundleLoadAssetOperation request = AssetBundleManager.LoadAssetAsync(assetBundleName, assetName, typeof(GameObject));
				if (request == null)
					throw new NullReferenceException($"Request for {assetName} in {assetBundleName} assetbundle failed: Null request.");
				yield return request;
				GameObject go = request.GetAsset<GameObject>();
				if (go == null)
					throw new NullReferenceException($"Request for {assetName} in {assetBundleName} assetbundle failed: Null GameObject.");
				RectTransform prefab = go.GetComponent<RectTransform>();
				if (prefab == null)
					throw new NullReferenceException($"Request for {assetName} in {assetBundleName} assetbundle failed: Null RectTansform.");
				assign(prefab);
			}


			void TriggerHandler.RemoveTrigger(Trigger t)
			{
				// nothing to do
			}

			void TriggerHandler.DuplicateTrigger(Trigger t)
			{
				throw new NotImplementedException();
			}

			RectTransform TriggerHandler.CreateTriggerActionsUI()
			{
				return UnityEngine.Object.Instantiate(myTriggerActionsPrefab);
			}

			RectTransform TriggerHandler.CreateTriggerActionMiniUI()
			{
				return UnityEngine.Object.Instantiate(myTriggerActionMiniPrefab);
			}

			RectTransform TriggerHandler.CreateTriggerActionDiscreteUI()
			{
				return UnityEngine.Object.Instantiate(myTriggerActionDiscretePrefab);
			}

			RectTransform TriggerHandler.CreateTriggerActionTransitionUI()
			{
				return UnityEngine.Object.Instantiate(myTriggerActionTransitionPrefab);
			}

			void TriggerHandler.RemoveTriggerActionUI(RectTransform rt)
			{
				UnityEngine.Object.Destroy(rt?.gameObject);
			}
		}

		// Wrapper for easier handling of custom triggers.
		public class EventTrigger : Trigger
		{
			public string Name {
				get { return name; }
				set { name = value; myNeedInit = true; }
			}

			public MVRScript Owner {
				get; private set;
			}

			private string name;
			private bool myNeedInit = true;

			public EventTrigger(MVRScript owner, string name)
			{
				Name = name;
				Owner = owner;
				handler = SimpleTriggerHandler.Instance;
			}

			public void OpenPanelActionStart()
			{
				if (!SimpleTriggerHandler.Loaded)
				{
					SuperController.LogError("EventTrigger: You need to call SimpleTriggerHandler.LoadAssets() before use.");
					return;
				}

				triggerActionsParent = Owner.UITransform;
				InitTriggerUI();
				OpenTriggerActionsPanel();
				if (myNeedInit)
				{
					triggerActionsPanel.Find("Panel/Header Text").GetComponent<Text>().text = Name;
					triggerActionsPanel.Find("Content/Tab1/Label").GetComponent<Text>().text = "Event Actions";
					triggerActionsPanel.Find("Content/Tab2").gameObject.SetActive(false);
					triggerActionsPanel.Find("Content/Tab3").gameObject.SetActive(false);
					myNeedInit = false;
				}
			}
			
			public void OpenPanelTransition()
			{
				if (!SimpleTriggerHandler.Loaded)
				{
					SuperController.LogError("EventTrigger: You need to call SimpleTriggerHandler.LoadAssets() before use.");
					return;
				}

				triggerActionsParent = Owner.UITransform;
				InitTriggerUI();
				OpenTriggerActionsPanel();
				
				//getRoot(triggerActionsParent,0);
				if (myNeedInit)
				{
					triggerActionsPanel.Find("Panel/Header Text").GetComponent<Text>().text = Name;
					triggerActionsPanel.Find("Content/Tab2/Label").GetComponent<Text>().text = "Breathing Actions";
					triggerActionsPanel.Find("Content/Tab1").gameObject.SetActive(false);
					triggerActionsPanel.Find("Content/Tab2").gameObject.SetActive(true);
					triggerActionsPanel.Find("Content/Tab2").GetComponent<Toggle>().isOn = true;					
					triggerActionsPanel.Find("Content/Tab3").gameObject.SetActive(false);
					myNeedInit = false;
				}
			}
	
			public void RestoreFromJSON(JSONClass jc, string subScenePrefix, bool isMerge, bool setMissingToDefault)
			{
				if (jc.HasKey(Name))
				{
					JSONClass tc = jc[Name].AsObject;
					if (tc != null)
						base.RestoreFromJSON(tc, subScenePrefix, isMerge);
				}
				else if (setMissingToDefault)
				{
					base.RestoreFromJSON(new JSONClass());
				}
			}

			public void Trigger()
			{
				active = true;
				active = false;
			}
			
			public void TriggerTransition(float val)
			{
				this.transitionInterpValue = val;
			}
		}
		// *********** MacGruber_Utils.cs END *********************
		
		// **************************
		// Time to cleanup !
		// **************************	
		void OnDisable() {
			disableCustomAttenuation();
		}
		
		void OnDestroy() {
			// All event triggers
			SuperController.singleton.onAtomUIDRenameHandlers -= OnAtomRename;
			foreach( EventTrigger et in allTriggers ) {
				et.Remove();
			}
			
			clearMainArousalHandler();
			clearPelvicSlapHandler();
			clearSquishesHandler();
			clearBlowjobHandler();
			
			// Sounds
			if (voices != null)
            {
                voices.UnloadAudio();
            }
		}
	}
}