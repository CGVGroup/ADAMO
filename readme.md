<h1 align="center">A.D.A.M.O. — Agent for language-Driven Actions with Multimodal Observations</h1>

<p align="center">
  <img src="readme_resources/teaser_image.png" width="85%">
</p>

<p align="center"><em>Figure 1 — System overview of A.D.A.M.O.</em><br>
A.D.A.M.O. integrates symbolic and visual perception into a unified Observation module, combining egocentric RGB frames with labeled bounding boxes, 3D reference points, and a synchronized symbolic state. These observations populate a Multi-Modal Short-Term Memory (MSTM) composed of fixed behavioral instructions, a dynamic symbolic state, and a rolling buffer of recent interactions. A pretrained VLM processes this multimodal context to produce explicit reasoning notes and tool-based actions (Walk, Look, Pick, Drop), closing the perceive–reason–act loop that drives the agent inside the 3D environment.
</p>

<h2>Abstract</h2>

Creating believable Virtual Humans requires a unified architecture that connects perception and action through language-mediated reasoning. Existing systems remain modular, linking dialogue, perception, and control via handcrafted pipelines that limit adaptability and grounded behavior in 3D environments. We introduce A.D.A.M.O. (Agent for language-Driven Actions with Multimodal Observations), a language-driven Virtual Human framework that leverages a pretrained VLM with tool-calling to integrate perception, reasoning, and control. A.D.A.M.O. maintains a dual visual–symbolic world model, enabling spatial reasoning and goal-directed actions directly from natural language prompts. To evaluate this capability, we introduce a novel benchmark structured around a Capability-Difficulty taxonomy that decomposes spatial tasks by procedural and linguistic complexity, providing an interpretable measure of embodied reasoning difficulty. Experiments across multiple task families and scenes show consistent task-level generalization largely reducing manual authoring, thus establishing a foundation for scalable, language-grounded agentic behavior.

<p align="center">
  <img src="readme_resources/adamo_infrastructure_architecture.png" width="30%">
</p>

<p align="center"><em>Figure 2 — Infrastructure architecture.</em></p>

A.D.A.M.O. implements the Thought–Act–Observation loop through four modular components:

**Runtime Engine (Unity).**  
Manages the 3D environment, rendering, physics, and agent embodiment.  
Collects observations and executes high-level actions via the Action Server.

**Cognitive Server (Python).**  
Runs the Thought–Act–Observation loop, maintains the multimodal short-term memory (MSTM), assembles prompts, and communicates with the VLM through LangGraph/LangChain.

**VLM Inference Server (Model Providers).**  
Provides access to cloud or local VLMs (GPT-4o-vision, Claude Sonnet-3.5, Ollama).  
Performs multimodal reasoning and selects the next tool.

**Action Server (Unity).**  
Translates tool calls (Walk, Look, Pick, Drop) into Unity actions and returns tool feedback with updated observations.

**Execution Flow.**  
1. Unity sends initial observations + task prompt to the Cognitive Server.  
2. The Cognitive Server queries the VLM for the next tool or termination.  
3. If a tool is selected, it is executed by the Action Server.  
4. Unity returns updated observations.  
5. Steps 2–4 repeat until completion, then the final answer is returned to Unity.


<h2>Repository Overview</h2>

The repository follows the system architecture shown in Figure 2 and is organized into three main folders:

- **adam_unity/** — the Unity project containing the Runtime Engine, Action Server, embodiment logic, experiment scenes, and the Unity build used for large-scale evaluation.  
- **adam_python/** — the Cognitive Server, including the reasoning loop, MSTM, tool interface, LLM manager, and all Python-side logic.  
- **adam_experiments/** — scripts and utilities for batch experiments, automatic result aggregation, and plotting.

In the root folder you will also find the `supplementary_material.pdf` containing detailed task definitions, solution-checker mappings, and additional documentation used in the paper.

<h3>Requirements</h3>

A.D.A.M.O. relies on a hybrid Unity–Python architecture.  
Below we list all required components for running the system.

<h4>Unity Runtime Engine</h4>

- **Unity Version:** 6000.2.2f1 

Three commercial Unity packages are required for locomotion (free), inverse kinematics (paid) and in game debug console (free):

- **Motion Matching for Unity v2.3.2** — locomotion system for smooth, data-driven agent movement  
- **Final IK v2.4** — inverse kinematics system for articulated agent posing and reaching  
- **In Game Debug Console v1.8.2** — in-game debug console for logging, inspection, and developer commands during runtime  

To correctly integrate these packages in the project:
1. Import the packages into the project from the package manager
2. Inside "Assets/Plugins/MotionMatching" folder reorganize scripts and sub-folders to follow the structure illustrated in "Assets/Plugins/MotionMatching/MotionMatching_folder_structure.txt"

<details>
<summary>MotionMatching_folder_structure.txt</summary>
<pre>
MotionMatching
|   
+---Code
|   |   README.md
|   |   
|   +---Integrations
|   |       Integration_README.txt
|   |       
|   \---MxM
|       |   MxM.asmdef     
|       |   
|       +---Data
|       |   |   
|       |   +---Animation
|       |   |   |   BlendClipData.cs
|       |   |   |   BlendSpace1DData.cs
|       |   |   |   BlendSpaceData.cs
|       |   |   |   ClipData.cs
|       |   |   |   CompositeData.cs
|       |   |   |   IComplexAnimData.cs
|       |   |   |   IdleSetData.cs
|       |   |   |   MotionCurveData.cs
|       |   |   |   SequenceData.cs
|       |   |   |   
|       |   |   \---Assets
|       |   |           IMxMAnim.cs
|       |   |           MxMAnimationClipComposite.cs
|       |   |           MxMAnimationIdleSet.cs
|       |   |           MxMBlendClip.cs
|       |   |           MxMBlendSpace.cs
|       |   |           
|       |   +---AnimSpeedModifier
|       |   |       MotionPreset.cs
|       |   |       MotionSection.cs
|       |   |       MotionTimingPresets.cs
|       |   |       SpeedModData.cs
|       |   |       
|       |   +---Core
|       |   |       CalibrationModule.cs
|       |   |       CompositeCategory.cs
|       |   |       Goal.cs
|       |   |       JointData.cs
|       |   |       MxMAnimData.cs
|       |   |       MxMCalibrationData.cs
|       |   |       MxMPreProcessData.cs
|       |   |       NativeAnimData.cs
|       |   |       PoseCluster.cs
|       |   |       PoseData.cs
|       |   |       PoseJoint.cs
|       |   |       Trajectory.cs
|       |   |       TrajectoryPoint.cs
|       |   |       WarpModule.cs
|       |   |       
|       |   +---Curves
|       |   |       MxMCurveTrack.cs
|       |   |       
|       |   +---Debug
|       |   |       MxMDebugData.cs
|       |   |       MxMDebugFrame.cs
|       |   |       PoseMask.cs
|       |   |       
|       |   +---Events
|       |   |       EventContact.cs
|       |   |       EventData.cs
|       |   |       EventFrameData.cs
|       |   |       MxMEventDefinition.cs
|       |   |       
|       |   +---Modules
|       |   |       AnimationModule.cs
|       |   |       AnimModuleDefaults.cs
|       |   |       EventNamingModule.cs
|       |   |       MotionMatchConfigModule.cs
|       |   |       TagNamingModule.cs
|       |   |       TrajectoryGeneratorModule.cs
|       |   |       
|       |   +---Tags
|       |   |       BoolTagTrack.cs
|       |   |       EventMarker.cs
|       |   |       FloatTagTrack.cs
|       |   |       FootStepTagTrack.cs
|       |   |       TagTrack.cs
|       |   |       TagTrackBase.cs
|       |   |       
|       |   \---Utility
|       |           FootstepTagTrackData.cs
|       |           GenericTagTrackData.cs
|       |           MxMInputProfile.cs
|       |           
|       +---Editor
|       |   |   DocumentationLinks.cs
|       |   |   MxM.Editor.asmdef
|       |   |   MxMAssetHandler.cs
|       |   |   
|       |   +---Data
|       |   |       MxMSettings.cs
|       |   |       MxMSettingsProvider.cs
|       |   |       
|       |   +---EditorWindows
|       |   |   |   MxMAnimationClipCompositeWindow.cs
|       |   |   |   MxMAnimationIdleSetWindow.cs
|       |   |   |   MxMAnimConfigWindow.cs
|       |   |   |   MxMBlendSpaceWindow.cs
|       |   |   |   MxMDebuggerWindow.cs
|       |   |   |   MxMTaggingWindow.cs
|       |   |   |   
|       |   |   \---Utility
|       |   |           AnimModuleSettingsWindow.cs
|       |   |           CompositeCategorySettingsWindow.cs
|       |   |           
|       |   +---Enumerations
|       |   |       EUtilityTagTrack.cs
|       |   |       
|       |   +---Inspectors
|       |   |       AnimationModuleInspector.cs
|       |   |       CalibrationModuleInspector.cs
|       |   |       ConfigurationModuleInspector.cs
|       |   |       EventNamingModuleInspector.cs
|       |   |       MotionTimingPresetInspector.cs
|       |   |       MxMAnimatorInspector.cs
|       |   |       MxMAnimDataInspector.cs
|       |   |       MxMBlendSpaceInspector.cs
|       |   |       MxMCompositeInspector.cs
|       |   |       MxMEventDefinitionInspector.cs
|       |   |       MxMIdleSetInspector.cs
|       |   |       MxMInputProfileInspector.cs
|       |   |       MxMPreProcessDataInspector.cs
|       |   |       MxMRootMotionApplicatorInspector.cs
|       |   |       TagNamingModuleInspector.cs
|       |   |       WarpModuleInspector.cs
|       |   |       
|       |   +---Preview
|       |   |       IPreviewable.cs
|       |   |       MxMPreviewScene.cs
|       |   |       
|       |   +---Resources
|       |   |       BlackProtoGrid.png
|       |   |       DebugArrow.mesh
|       |   |       DebugArrowMat.mat
|       |   |       GroundGrid.prefab
|       |   |       ProtoGridBlack.mat
|       |   |       ProtoGridBlack2.mat
|       |   |       
|       |   \---UTIL
|       |           EditorUtility.cs
|       |           
|       +---Enumerations
|       |       EAnimatorStates.cs
|       |       EBlendSpaceSmoothing.cs
|       |       EBlendSpaceType.cs
|       |       EBlendStatus.cs
|       |       EComplexAnimType.cs
|       |       EEventState.cs
|       |       EEventWarpType.cs
|       |       EFavourTagMode.cs
|       |       EFootStepTags.cs
|       |       EGenericTags.cs
|       |       EIdleState.cs
|       |       EJointVelocityCalculationMethod.cs
|       |       ELongitudinalErrorWarp.cs
|       |       EMotionModSmooth.cs
|       |       EMotionModType.cs
|       |       EMotionWarping.cs
|       |       EMxMAnimtype.cs
|       |       EMxMEventType.cs
|       |       EMxMRootMotion.cs
|       |       EPastTrajectoryMode.cs
|       |       EPoseMatchMethod.cs
|       |       EPostEventTrajectoryMode.cs
|       |       ETags.cs
|       |       ETrajectoryMoveMode.cs
|       |       ETransitionMethod.cs
|       |       EUserTags.cs
|       |       TagSelection.cs
|       |       
|       +---Processors
|       |       GlobalSpacePose.cs
|       |       MxMFootstepDetector.cs
|       |       MxMPreProcessor.cs
|       |       
|       \---Runtime
|           |   
|           +---Components
|           |   |   
|           |   +---Controller
|           |   |       GenericControllerWrapper.cs
|           |   |       UnityControllerWrapper.cs
|           |   |       
|           |   +---Decoupling
|           |   |       MxMAnimationDecoupler.cs
|           |   |       
|           |   +---Extensions
|           |   |       MxMAnimatorPlaybackSync.cs
|           |   |       MxMBlendSpaceLayers.cs
|           |   |       MxMEventLayers.cs
|           |   |       MxMTIPExtension.cs
|           |   |       
|           |   +---RootMotion
|           |   |       MxMDecoupleMotionApplicator.cs
|           |   |       MxMRootMotionApplicator.cs
|           |   |       
|           |   +---Spawner
|           |   |       RuntimeMxMConstructor.cs
|           |   |       
|           |   \---Trajectory
|           |           MxMTrajectoryGenerator.cs
|           |           MxMTrajectoryGeneratorBase.cs
|           |           MxMTrajectoryGenerator_AI.cs
|           |           MxMTrajectoryGenerator_BasicAI.cs
|           |           TrajectoryGeneratorJob.cs
|           |           
|           +---Debug
|           |       DrawArrow.cs
|           |       PlayableUtils.cs
|           |       
|           +---Experimental
|           |   |   
|           |   +---InertialBlending
|           |   |       InertialBlendModule.cs
|           |   |       InertializerJob.cs
|           |   |       TransformData.cs
|           |   |       
|           |   \---SearchManager
|           |           MxMSearchManager.cs
|           |           
|           +---FSM
|           |       FSM.cs
|           |       FSMState.cs
|           |       
|           +---Interfaces
|           |       ILongitudinalWarper.cs
|           |       IMxMExtension.cs
|           |       IMxMRootMotion.cs
|           |       IMxMTrajectory.cs
|           |       IMxMUnityRiggingIntegration.cs
|           |       
|           +---Jobs
|           |       BlendSpaceWeightJob.cs
|           |       JobData.cs
|           |       MinimaJobs.cs
|           |       PoseJobs.cs
|           |       PoseJobs_VelCost.cs
|           |       TrajectoryJobs.cs
|           |       
|           +---MxMAnimator
|           |       MxMAnimator.cs
|           |       MxMAnimator_AnimManagement.cs
|           |       MxMAnimator_BlendSpace.cs
|           |       MxMAnimator_Curves.cs
|           |       MxMAnimator_Debug.cs
|           |       MxMAnimator_Events.cs
|           |       MxMAnimator_Idle.cs
|           |       MxMAnimator_Jobs.cs
|           |       MxMAnimator_Layers.cs
|           |       MxMAnimator_States.cs
|           |       MxMAnimator_Tags.cs
|           |       MxMLayer.cs
|           |       MxMUtility.cs
|           |       
|           +---Playables
|           |       MotionMatchingPlayable.cs
|           |       MxMBlendSpaceState.cs
|           |       MxMPlayableState.cs
|           |       
|           \---Templates
|                   RootMotionApplicatorTemplate.cs
|                   TrajectoryGeneratorTemplate.cs
|                   
+---Demo
|   |   
|   +---Animations
|   |   |   LowerBody.mask
|   |   |   UpperBody.mask
|   |   |   
|   |   +---Events
|   |   |   |   JogJump_ToLeft_1.anim
|   |   |   |   JogJump_ToLeft_1_Mirror.anim
|   |   |   |   JogJump_ToLeft_2.anim
|   |   |   |   JogJump_ToLeft_2_Mirror.anim
|   |   |   |   RunJump_ToLeft_1.anim
|   |   |   |   RunJump_ToLeft_1_Mirror.anim
|   |   |   |   RunJump_ToLeft_3.anim
|   |   |   |   RunJump_ToLeft_3_Mirror.anim
|   |   |   |   RunJump_ToLeft_4.anim
|   |   |   |   RunJump_ToLeft_4_Mirror.anim
|   |   |   |   RunSlide_ToRight_1.anim
|   |   |   |   RunSlide_ToRight_2.anim
|   |   |   |   
|   |   |   +---VaultOff
|   |   |   |       Run_JumpDownHigh_Roll_Run.anim
|   |   |   |       Run_JumpDownHigh_Roll_Run_Mirror.anim
|   |   |   |       Run_JumpDownLow_Roll_Run.anim
|   |   |   |       Run_JumpDownLow_Roll_Run_Mirror.anim
|   |   |   |       _209_Run_JumpDownLow_Run.anim
|   |   |   |       _209_Run_JumpDownLow_Run_Mirror.anim
|   |   |   |       
|   |   |   \---VaultUp
|   |   |           RunJump_ToLeft_2.anim
|   |   |           RunJump_ToRight_2.anim
|   |   |           Run_JumpUpHigh_Run.anim
|   |   |           Run_JumpUpHigh_Run_Mirror.anim
|   |   |           Run_JumpUpMedium_2Hands_Run.anim
|   |   |           Run_JumpUpMedium_2Hands_Run_Mirror.anim
|   |   |           
|   |   +---Idle
|   |   |       Idle_Neutral_1.anim
|   |   |       
|   |   +---LayerTests
|   |   |       IdleGrab_FrontHigh.anim
|   |   |       IdleGrab_FrontHigh_Looped.anim
|   |   |       Idle_JumpDownMed_Idle.anim
|   |   |       
|   |   +---Locomotion
|   |   |   |   HalfSteps2Idle_PasingLongStepTOIdle.anim
|   |   |   |   HalfSteps2Idle_PasingLongStepTOIdle_Right.anim
|   |   |   |   Idle2Run135L.anim
|   |   |   |   Idle2Run135R.anim
|   |   |   |   Idle2Run180L.anim
|   |   |   |   Idle2Run180R.anim
|   |   |   |   Idle2Run45L.anim
|   |   |   |   Idle2Run45R.anim
|   |   |   |   Idle2Run90L.anim
|   |   |   |   Idle2Run90R.anim
|   |   |   |   Idle2walk_AllAngles.anim
|   |   |   |   Idle2walk_AllAngles_Right.anim
|   |   |   |   JogForwardTurnLeft_NtrlMedium.anim
|   |   |   |   JogForwardTurnRight_NtrlMedium.anim
|   |   |   |   PlantNTurn135_Run_L.anim
|   |   |   |   PlantNTurn135_Run_R.anim
|   |   |   |   PlantNTurn180_Run_L_2.anim
|   |   |   |   PlantNTurn180_Run_R_2.anim
|   |   |   |   PlantNTurn90_Run_L.anim
|   |   |   |   PlantNTurn90_Run_R.anim
|   |   |   |   RunForwardStart.anim
|   |   |   |   RunFwdStop.anim
|   |   |   |   Run_LedgeStop2_Idle.anim
|   |   |   |   Run_LedgeStop_Idle.anim
|   |   |   |   SmallStep.anim
|   |   |   |   WalkForwardStart.anim
|   |   |   |   WalkForward_NtrlFaceFwd.anim
|   |   |   |   
|   |   |   \---BlendSpace_Anims
|   |   |           RunArcLeft_Narrow.anim
|   |   |           RunArcLeft_Wide.anim
|   |   |           RunArcRight_Narrow.anim
|   |   |           RunArcRight_Wide.anim
|   |   |           RunForward_NtrlFaceFwd.anim
|   |   |           WalkArkLeft.anim
|   |   |           WalkArkLeft_Narrow.anim
|   |   |           WalkArkRight.anim
|   |   |           WalkArkRight_Narrow.anim
|   |   |           WalkFWD.anim
|   |   |           
|   |   \---Mocap
|   |           RunningMocapSet.fbx
|   |           SprintMocapSet.fbx
|   |           StrafeMocapSet.fbx
|   |           WalkingMocapSet.fbx
|   |           
|   +---Code
|   |   |   AIDestinationSetter.cs
|   |   |   ExampleDecoupleMovementControl.cs
|   |   |   ExampleDemoInput.cs
|   |   |   LocomotionSpeedRamp.cs
|   |   |   StressTestSpawner.cs
|   |   |   
|   |   +---UI
|   |   |       HelpUIControl.cs
|   |   |       
|   |   \---Vault System
|   |           EVaultContactOffsetMethod.cs
|   |           EVaultType.cs
|   |           VaultableProfile.cs
|   |           VaultDefinition.cs
|   |           VaultDetectionConfig.cs
|   |           VaultDetector.cs
|   |           
|   +---Data
|   |   |   
|   |   +---EventDefinitions
|   |   |       EventDef_Dance.asset
|   |   |       EventDef_VaultOff.asset
|   |   |       EventDef_VaultOff_High.asset
|   |   |       EventDef_VaultOff_Med.asset
|   |   |       EventDef_VaultOverLong.asset
|   |   |       EventDef_VaultOverShort.asset
|   |   |       EventDef_VaultUp.asset
|   |   |       EventDef_VaultUp_High.asset
|   |   |       EventDef_VaultUp_Med.asset
|   |   |       JumpEventDef.asset
|   |   |       SlideEventDef.asset
|   |   |       
|   |   +---InputProfiles
|   |   |   |   MxMInputProfile.asset
|   |   |   |   
|   |   |   +---Balanced
|   |   |   |       MocapInputProfile_Balanced.asset
|   |   |   |       MocapSprintInputProfile_Balanced.asset
|   |   |   |       MocapStrafeInputProfile_Balanced.asset
|   |   |   |       
|   |   |   +---HighQuality
|   |   |   |       MocapInputProfile_HQ.asset
|   |   |   |       MocapSprintInputProfile_HQ.asset
|   |   |   |       
|   |   |   \---HighResponsiveness
|   |   |           MocapInputProfile_Responsive.asset
|   |   |           MocapSprintInputProfile_Responsive.asset
|   |   |           
|   |   +---Legacy
|   |   |       MotionMatchConfigModule.asset
|   |   |       MxMAnimData.asset
|   |   |       MxMAnimDataAI.asset
|   |   |       MxMDemo_RunAnims.asset
|   |   |       MxMDemo_RunAnims_AI.asset
|   |   |       MxMDemo_WalkAnims_AI.asset
|   |   |       MxMPreProcessData.asset
|   |   |       MxMPreProcessDataAI.asset
|   |   |       Test.asset
|   |   |       
|   |   +---MotionMatching
|   |   |   |   MocapAnimData.asset
|   |   |   |   MocapAnimDataAI.asset
|   |   |   |   MocapPreProcessData.asset
|   |   |   |   MocapPreProcessData_AI.asset
|   |   |   |   
|   |   |   \---Modules
|   |   |       |   EventNamingModule.asset
|   |   |       |   GeneralWarpModule.asset
|   |   |       |   MocapMatchConfigModule.asset
|   |   |       |   TagNamingModule.asset
|   |   |       |   
|   |   |       +---AnimModules
|   |   |       |       MocapDemo_OtherAnims.asset
|   |   |       |       MocapDemo_RunAnims.asset
|   |   |       |       MocapDemo_RunAnims_AI.asset
|   |   |       |       MocapDemo_SprintAnims.asset
|   |   |       |       MocapDemo_StrafeAnims.asset
|   |   |       |       MocapDemo_WalkAnims.asset
|   |   |       |       MxMDemo_ParkourAnims.asset
|   |   |       |       
|   |   |       \---Calibrations
|   |   |               MxMCalibrationModule_Balanced.asset
|   |   |               MxMCalibrationModule_HighQuality.asset
|   |   |               MxMCalibrationModule_HighResponsiveness.asset
|   |   |               
|   |   \---VaultData
|   |           VaultDef_VaultOff.asset
|   |           VaultDef_VaultOff_High.asset
|   |           VaultDef_VaultOff_Med.asset
|   |           VaultDef_VaultOverLong.asset
|   |           VaultDef_VaultOverShort_FromStanding.asset
|   |           VaultDef_VaultUp.asset
|   |           VaultDef_VaultUp_High.asset
|   |           VaultDef_VaultUp_Med.asset
|   |           VaultDetectionConfig.asset
|   |           
|   +---Materials
|   |       AITarget.mat
|   |       AITarget2.mat
|   |       Ground.mat
|   |       Obstacle.mat
|   |       ProtoGridOrange.mat
|   |       Robot_Color.mat
|   |       Wall.mat
|   |       
|   +---Model
|   |       Robot Kyle.fbx
|   |       
|   +---Prefabs
|   |       CM_ThirdPerson.prefab
|   |       Main Camera.prefab
|   |       Robot Kyle.prefab
|   |       Robot Kyle_AI (WithRootMotion).prefab
|   |       Robot Kyle_AI.prefab
|   |       Robot Kyle_MOCAP_Balanced.prefab
|   |       Robot Kyle_MOCAP_HighResponsiveness.prefab
|   |       Robot Kyle_MOCAP_Quality.prefab
|   |       Robot Kyle_StressTest.prefab
|   |       RobotKyle (AlternativeHierarchy).prefab
|   |       RobotKyle_Decouple.prefab
|   |       
|   +---Scenes
|   |   |   MxMDemo.unity
|   |   |   MxMDemoSettings.lighting
|   |   |   MxMDemo_StressTest.unity
|   |   |   
|   |   \---MxMDemo
|   |           LightingData.asset
|   |           NavMesh.asset
|   |           ReflectionProbe-0.exr
|   |           
|   \---Textures
|           OrangeProtoGrid.png
|           Robot_Color.tga
|           Robot_Normal.tga
|           
\---Prefabs
        MxMSearchManager.prefab
</pre>

</details>

<h4>Python</h4>

- **Python Version:** 3.12.10  

We recommend using a dedicated virtual environment so that Unity can call a Python executable with the correct dependencies installed.

Create and activate a `.venv`, then install all requirements:

```bash
python -m venv .venv
# On Windows
.\.venv\Scripts\activate
# On macOS / Linux
source .venv/bin/activate

pip install -r requirements.txt
```

<h4>Vision-Language Models (VLM)</h4>

A.D.A.M.O. requires a VLM with native tool/function calling and multimodal input (image + text).
The system is designed to support multiple models and providers and can be easily extended to new backends through the LLM manager.

Model and provider management is handled by:

```text
adam_python/agentic_forge/llm_manager.py  (class: LlmManager)
```

LlmManager loads its configuration from:
```text
adam_python/agentic_forge/configs/llm_manager_config.yaml
```

This configuration file defines, for each provider and model:
1. Which provider to use (e.g., OpenAI, OpenRouter, Ollama)
2. The model identifiers and default parameters
3. Provider-specific options such as base URLs and timeouts
    Currently supported providers:
    - OpenAI
    - OpenRouter
    - Ollama (local server)

Provider API keys are configured via:
```text
adam_python/agentic_forge/configs/provider_apy_key.example.yaml (remove .example)
```
Edit this file and set the corresponding fields for OpenAI and OpenRouter

LlmManager loads its configuration from:

```text
adam_python/agentic_forge/configs/llm_manager_config.yaml
```
For local models served through Ollama, update the corresponding provider section in the same configuration file, setting the appropriate base_url for the Ollama server and specifying the desired model names.
The LlmManager uses these configuration files to instantiate and route calls to different models in a unified way, so adding or tweaking models and providers only requires editing the YAML configuration rather than changing the core code.

<h2>How to Use A.D.A.M.O.</h2>

A.D.A.M.O. can be used in **two different modes** depending on whether you have access to the Unity commercial packages required by the Runtime Engine.

### 1. Unity Editor Mode (requires commercial assets)
This mode uses the full Unity project (`adam_unity/`) and allows:
- interactive debugging
- visualization inside the 3D scene
- custom task configuration from the Editor

However, this mode **requires** the following paid (or free but with license agreement) Unity packages:
- Motion Matching for Unity (free)
- Final IK (paid)
- In-Game Debug Console (free) 

If you do not own these assets, the Editor mode **cannot be used**.

### 2. Build Mode (recommended for most users)
This mode uses the preconfigured Unity build and does **not** require any commercial packages.  
It is the recommended setup for:
- running the entire benchmark  
- reproducing all results from the paper  
- parallel execution on multi-core systems  
- headless evaluation without Unity Editor

All batch experiments in the paper were executed through the Build Mode using:

```text
adam_experiments/run_experiments.py
```
If you do not have the commercial Unity packages, skip the [Editor instructions section](#running-tasks-from-the-unity-editor)  and go directly to the [Build Mode section](#running-batch-experiments-from-the-build). 

## Running Tasks from the Unity Editor

A.D.A.M.O. includes an integrated experimentation scene for launching custom tasks directly from the Unity Editor.  
This workflow is useful for debugging, rapid prototyping, visualization, and small-scale experimentation.

<h3>1. Open the Experiment Scene</h3>

Inside the Unity project, open:

```text
adam_unity/Assets/Scenes/_Experiments.unity
```

This scene is intentionally minimal and contains a single manager object: <strong>Experiments_Prefab</strong>

which holds the BenchmarkManager component.
This component orchestrates experiment setup, execution, and communication with the Python Cognitive Server.

<p align="center"> <img src="readme_resources/benchmark_manager.png" width="30%"> </p> <p align="center"><em>Figure 3 — The Benchmark Manager component inside the _Experiments scene.</em></p> 

<h3>2. Benchmark Manager Overview</h3>

The Benchmark Manager automatically:

- starts the Python Cognitive Server
- sends configuration parameters (scene, task, model, repetitions, etc.)
- runs episodes in Unity
- logs and stores results under <code>BenchmarkData/{ExperimentName}</code>

Below we detail all configuration fields.


<h4>2.1 Configs Section</h3>

These settings control how runs are executed:

- <strong>Debug Mode</strong>
Enables step-by-step execution. After each repetition Unity pauses and displays a dialog asking whether to continue.
Useful for inspecting logs or intermediate agent states.

- <strong>Use Custom Run</strong>
When enabled, Unity executes a single run using the parameters in Current Run.
When disabled, runs are loaded from a CSV file (see CSV Relative Path).

- <strong>Run Python Server</strong>
Automatically launches the Cognitive Server on localhost:50000.
The server is implemented in FastAPI (adam_python/adam_agent_server.py).

- <strong>CSV Relative Path</strong>
Path to a CSV file describing a batch of runs.
Example file:
```text
adam_unity/Assets/BenchmarkData/run_static.csv
```
Disable Use Custom Run to let the CSV drive all experiment parameters.

- <strong>Experiment Name</strong>
Results will be saved under:
```text
adam_unity/Assets/BenchmarkData/{Experiment Name}/
```

- <strong>Time Multiplier</strong>
Speeds up Unity execution (animations, physics).
Useful for accelerating experiments while keeping logic consistent.

- <strong>Timeout Seconds</strong>
Maximum time Unity waits for a reply from the Cognitive Server before terminating the run.

<h4>2.2 Runs Data — Current Run</h3>

These fields define a single episode configuration and are used only if Use Custom Run is enabled.

- <strong>Scene</strong>
Select the environment:
S1 (tabletop) or S2 (living room).

- <strong>Task Id</strong>
Selects the task to execute.
Must correspond to a valid entry in the benchmark (see supplementary material).

- <strong>Task Prompt</strong>
Natural-language instruction given to the agent.

- <strong>Solution Checker</strong>
Chooses the deterministic checker for evaluating task completion.
Each task has a dedicated checker (see supplementary material for mapping).

- <strong>Graphical Resolution & Graphical Lighting</strong>
Deprecated (do not use).

- <strong>Model</strong>
Selects which VLM backend to use:
G4O (GPT-4o-vision), S35 (Claude Sonnet-3.5)
or internal codes mapped in llm_manager_config.yaml.

- <strong>Object Identifier</strong>
Labeling scheme for object tags:
    - SEM → semantic (ObjectName_InstanceID)
    - OPAQ → numeric opaque (InstanceID only)

- <strong>Coordinates Type</strong>
Deprecated (do not use).

- <strong>Repetitions</strong>
Number of independent repetitions for the selected run.

All remaining fields are runtime or debug fields used internally — do not edit them.

<h4>2.3. Launching the Run</h3>

1. Open the _Experiments scene

2. Configure Configs and Current Run

3. Press Play in Unity

4. Unity will:

    1. spawn the agent
    2. start the Cognitive Server
    3. run the task
    4. save logs and results under BenchmarkData/{ExperimentName}
    5. loop for the specified number of repetitions

For batch execution, disable Use Custom Run and set the CSV path to a valid experiment file.


## Running Batch Experiments from the Build

In addition to running single tasks from the Unity Editor, A.D.A.M.O. supports fully automated **batch experiments** using a Unity build and the `run_experiments.py` script.  
This mode is intended for large-scale evaluation, parallel execution, and automatic result aggregation.

<h3>0. Setup</h3>
Extract the .zip content from GitHub release in Build directory:

```text
adam_unity/Build/
```

<h3>1. Benchmark Data Folder and CSV Specification</h3>

Batch runs use a shared benchmark data directory:

```text
adam_unity/Build/ADAMO_Build_data/BenchmarkData
```
In this folder you must provide a master CSV:
```text
runs.csv (runs_static.csv provides an example of the table)
```

each row in runs.csv specifies a single episode configuration (scene, task, model, labeling scheme, repetitions, etc.), using the same fields used by the Unity BenchmarkManager (Current Run).
When run_experiments.py is executed, Unity will:

1. read the episode definitions from runs.csv

2. execute them through the build

3. write logs back into the same BenchmarkData directory, organized according to the parameters provided on the command line.

4. final aggregated results (metrics and plots) are computed automatically and stored under:
```text
adam_experiments/experiment/{exp-name}/
```
where {exp-name} is the experiment name you choose when invoking run_experiments.py.

<h3>2. AdamConfig (internal runtime configuration)</h3>

The Python side includes an AdamConfig model that controls how the Cognitive Server and Unity tool server communicate:

- <strong>agent_host / agent_port</strong>
HTTP endpoint of the Cognitive Server (FastAPI). This is the address Unity uses to send observations and receive actions.

- <strong>tool_host / tool_port</strong>
HTTP endpoint of the Unity Action Server, used by the Cognitive Server to call tools (Walk, Look, Pick, Drop).

- <strong>msg_window_size</strong>
Size of the rolling buffer in the MSTM (number of messages kept in context).
Note: a tool call and its corresponding tool response are treated as a single atomic message in this window.

These parameters are not exposed directly via the run_experiments.py CLI; they are set when Unity spawns the Cognitive Server.
If you need to change them, you must do so in the Python code by editing the defaults in AdamConfig (and ensuring Unity is configured accordingly), rather than through command-line arguments.

<h3>3. Running Batch Experiments with <code>run_experiments.py</code></h3>

The main entry point for batch execution is:
```text
./run_experiments.py
```

Internally, this script:

1. Reads the master runs.csv from the benchmark root directory.
2. Splits it into smaller batch CSVs (if needed) under a batch root directory.
3. Launches multiple Unity build processes in parallel, each consuming one batch CSV.
4. Collects logs and metrics from BenchmarkData.
5. Aggregates episode results, computes summary tables and plots, and writes them under adam_experiments/experiment/{exp-name}.

A typical invocation:
```text
python run_experiments.py \
  --parallelism 4 \
  --exp-name cd_benchmark_g4o_sem \
  --timescale 4
```

<h3>4. Command-line Arguments</h3>
The script exposes the following CLI parameters (defaults are defined inside run_experiments.py):

 - Path-related arguments (--exe, --python-exe, --csv, --benchmark-root, --batch-root, --exp-dir) are usually left at their defaults, which are aligned with the project layout

The most useful parameters to tune from the command line are:

 - <strong>--parallelism (-p)</strong>
Number of Unity processes to run in parallel.
Parallelism is applied over episodes, not over repetitions of the same episode.
 - <strong>--exp-name</strong>
Name of the experiment; determines the folder under adam_experiments/experiment/{exp-name} where final metrics and plots are stored.
 - <strong>--timescale</strong>
Time multiplier for Unity. Values > 1 speed up simulation (shorter wall-clock time), while 1 corresponds to realtime.
