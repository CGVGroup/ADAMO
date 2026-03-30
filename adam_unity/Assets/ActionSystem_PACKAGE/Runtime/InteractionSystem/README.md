# Modular Animation Rigging Interaction System

A deeply modular interaction system that leverages Unity's Animation Rigging to provide simple commands for NPC interactions.

## 🎯 Core Features

- **Simple Function**: `InteractWith(target, effector)` - Command NPCs to interact with objects
- **Modular Design**: Easy to read, modify, and extend
- **Animation Rigging**: Leverages Unity's Animation Rigging for realistic IK
- **Multiple Interaction Types**: Touch, Grab, Use, Push interactions
- **REACH and REST**: Basic functionality with effector movement from place to place

## 📁 File Structure

```
Scripts/InteractionSystem/
├── IInteractable.cs              # Base interfaces for interactable objects
├── InteractionTypes.cs            # Core types, enums, and command structure
├── AnimationRiggingController.cs  # Manages Animation Rigging components
├── InteractionExecutor.cs         # Main interaction system (SIMPLE FUNCTION)
├── InteractionBehaviors.cs        # Modular interaction behaviors
├── SimpleInteractable.cs          # Example interactable object
├── ExampleUsage.cs               # Usage examples and testing
└── README.md                     # This file
```

## 🚀 Quick Start

### 1. Setup Animation Rigging

First, ensure your character has Animation Rigging components set up:

1. Add `RigBuilder` component to your character
2. Create Rigs for each effector (arms, legs, head, spine)
3. Set up IK constraints for each rig
4. Create IK targets and rest positions

### 2. Add Components to Character

Add these components to your character GameObject:

```csharp
// Required components
- AnimationRiggingController
- InteractionExecutor
```

### 3. Configure AnimationRiggingController

Assign the rigging components in the inspector:
- RigBuilder
- Rigs (rightArmRig, leftArmRig, etc.)
- IK Targets (rightHandTarget, leftHandTarget, etc.)
- Rest Positions (rightHandRest, leftHandRest, etc.)

### 4. Create Interactable Objects

Add `SimpleInteractable` component to objects you want to interact with:

```csharp
// On any GameObject you want to interact with
- SimpleInteractable
```

### 5. Use the Simple Function

```csharp
// Get the interaction executor
InteractionExecutor executor = GetComponent<InteractionExecutor>();

// Simple interaction - touch with right hand
executor.InteractWith(targetObject, EffectorType.RightHand);

// Custom interaction with settings
var command = new InteractionCommand(target, InteractionType.Grab, EffectorType.LeftHand)
{
    reachDuration = 1.5f,
    holdDuration = 0.8f,
    returnDuration = 1.2f,
    useLookAt = true,
    useSpineIK = true
};
executor.ExecuteInteraction(command);
```

## 🎮 Usage Examples

### Basic Touch Interaction
```csharp
// Simple touch with right hand
executor.InteractWith(myObject, EffectorType.RightHand);
```

### Custom Grab Interaction
```csharp
var command = new InteractionCommand(target, InteractionType.Grab, EffectorType.LeftHand)
{
    reachDuration = 1.5f,
    holdDuration = 0.8f,
    returnDuration = 1.2f
};
executor.ExecuteInteraction(command);
```

### Stop Current Interaction
```csharp
executor.StopInteraction();
```

### Check Interaction Status
```csharp
if (executor.IsInteracting)
{
    Debug.Log("Currently interacting");
}
```

## 🔧 Modular Components

### InteractionCommand
Represents a complete interaction with all settings:
- Target object
- Interaction type (Touch, Grab, Use, Push)
- Effector (RightHand, LeftHand, RightFoot, LeftFoot)
- Timing (reach, hold, return durations)
- Animation curves
- Advanced settings (lookAt, spineIK)

### AnimationRiggingController
Manages Animation Rigging components:
- Controls IK targets
- Handles rig weight blending
- Manages effector movement
- Provides look-at functionality

### InteractionBehaviors
Modular behaviors for different interaction types:
- `TouchBehavior`: Simple touch interaction
- `GrabBehavior`: Pick up and carry objects
- `UseBehavior`: Use/activate objects
- `PushBehavior`: Push objects with physics

## 🎨 Customization

### Creating Custom Interaction Behaviors

```csharp
public class CustomBehavior : InteractionBehavior
{
    public override IEnumerator ExecuteBehavior(InteractionCommand command)
    {
        // Your custom interaction logic here
        yield return StartCoroutine(riggingController.MoveEffectorToTarget(
            command.effector,
            command.target.InteractionPoint,
            command.reachDuration,
            command.reachCurve
        ));
        
        // Custom behavior
        yield return new WaitForSeconds(1.0f);
        
        // Return to rest
        yield return StartCoroutine(riggingController.ReturnEffectorToRest(
            command.effector,
            command.returnDuration,
            command.returnCurve
        ));
    }
}
```

### Creating Custom Interactable Objects

```csharp
public class CustomInteractable : MonoBehaviour, IInteractable
{
    public Transform InteractionPoint => transform;
    public string InteractionName => "Custom Object";
    public bool CanInteract => true;
    
    // Custom interaction logic
    public void OnInteractionStart()
    {
        // Your custom logic
    }
}
```

## 🎯 Key Features

### Simple Function
The main function you requested:
```csharp
executor.InteractWith(target, effector);
```

### REACH and REST Functionality
- **REACH**: Effector moves from rest position to target
- **REST**: Effector stays at target position for specified duration
- **RETURN**: Effector moves back to rest position

### Modular Design
- Each component has a single responsibility
- Easy to modify individual parts
- Extensible for new interaction types
- Clear separation of concerns

### Animation Rigging Integration
- Leverages Unity's Animation Rigging
- Realistic IK movement
- Smooth weight blending
- Look-at functionality

## 🔍 Debugging

### Enable Debug Logging
```csharp
// In ExampleUsage.cs
[SerializeField] private bool enableDebugLogging = true;
```

### Check Interaction State
```csharp
Debug.Log($"Current State: {executor.CurrentState}");
Debug.Log($"Is Interacting: {executor.IsInteracting}");
```

### Visual Debugging
- Interaction points are drawn as green spheres in Scene view
- Lines show connection between object and interaction point

## 📋 Requirements

- Unity 2021.3 or later
- Animation Rigging package
- Humanoid character with proper bone setup
- IK constraints configured for each effector

## 🚀 Getting Started Checklist

1. ✅ Install Animation Rigging package
2. ✅ Set up character with RigBuilder and IK constraints
3. ✅ Add AnimationRiggingController to character
4. ✅ Add InteractionExecutor to character
5. ✅ Configure rigging components in inspector
6. ✅ Create interactable objects with SimpleInteractable
7. ✅ Test with ExampleUsage script
8. ✅ Use `InteractWith(target, effector)` function

## 🎮 Input Testing

The ExampleUsage script includes keyboard shortcuts:
- **T**: Touch with right hand
- **G**: Grab with left hand
- **U**: Use with right hand
- **P**: Push with right hand
- **Escape**: Stop current interaction

## 🔧 Troubleshooting

### Common Issues

1. **Rig not moving**: Check rig weight and IK target assignments
2. **Effector not reaching target**: Verify interaction point position
3. **Animation not smooth**: Adjust blend speed and animation curves
4. **Look-at not working**: Ensure head rig and look-at target are assigned

### Debug Steps

1. Check console for error messages
2. Verify all required components are assigned
3. Test with ExampleUsage script
4. Use Scene view to visualize interaction points
5. Check Animation Rigging setup

## 📚 Advanced Usage

### Event System
```csharp
executor.OnInteractionStarted += (command) => Debug.Log("Started");
executor.OnInteractionCompleted += (command) => Debug.Log("Completed");
executor.OnInteractionFailed += (command) => Debug.Log("Failed");
```

### Custom Animation Curves
```csharp
var command = new InteractionCommand(target, InteractionType.Touch, EffectorType.RightHand)
{
    reachCurve = AnimationCurve.EaseInOut(0, 0, 1, 1),
    returnCurve = AnimationCurve.EaseInOut(0, 0, 1, 1)
};
```

### Multiple Effectors
```csharp
// Use different effectors for different interactions
executor.InteractWith(target1, EffectorType.RightHand);
executor.InteractWith(target2, EffectorType.LeftHand);
executor.InteractWith(target3, EffectorType.RightFoot);
```

This modular system provides the simple function you requested while being deeply modular and easily extensible for your specific needs. 