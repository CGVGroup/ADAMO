# 🚀 Quick Setup Guide

## **Editor-Based NPC Setup**

### Step 1: Add the Setup Component
1. Select your humanoid NPC in the scene
2. Add the `NPCInteractionSetup` component to your NPC
3. Use the buttons in the inspector to configure everything offline in the editor

### Step 2: Configure the System
1. **Validate Character Setup** - Check if your character is properly configured
2. **Setup Complete System** - Configure everything at once (recommended)
3. **Or use individual setup buttons:**
   - Setup Rigging - Creates Animation Rigging components
   - Setup Components - Adds required components
   - Create Interaction Points - Creates IK targets and rest positions
   - Setup Example Usage - Adds testing component

### Step 3: Create Test Objects
1. Create some GameObjects in your scene
2. Add `SimpleInteractable` component to each object
3. Assign them to the `testObjects` array in `ExampleUsage`

### Step 4: Test the System
Press these keys to test different interactions:

**Touch Interactions:**
- `T` - Touch with right hand
- `Y` - Touch with left hand

**Grab Interactions:**
- `G` - Grab with left hand  
- `H` - Grab with right hand

**Use Interactions:**
- `U` - Use with right hand
- `I` - Use with left hand

**Push Interactions:**
- `P` - Push with right hand
- `O` - Push with left hand

**Other:**
- `F` - Foot interaction
- `ESC` - Stop current interaction
- `Ctrl+H` - Show help

## **Editor Features**

### **Validation Buttons:**
- **Validate Character Setup** - Checks if your character has proper Animator setup
- **Check Required Components** - Lists missing components

### **Setup Buttons:**
- **Setup Complete System** - One-click setup for everything
- **Setup Rigging** - Creates Animation Rigging components only
- **Setup Components** - Adds required components only
- **Create Interaction Points** - Creates IK targets and rest positions only
- **Setup Example Usage** - Adds testing component only

### **Cleanup Buttons:**
- **Clean Up All** - Removes all created objects
- **Clean Up Rigging** - Removes rigging components only
- **Clean Up Components** - Removes interaction components only
- **Clean Up Interaction Points** - Removes interaction points only

## **Manual Setup (if needed)**

If you prefer manual setup:

1. **Add Components to NPC:**
   - `AnimationRiggingController`
   - `InteractionExecutor`
   - `ExampleUsage`

2. **Configure Animation Rigging:**
   - Add `RigBuilder` component
   - Create Rigs for arms, legs, head, spine
   - Set up IK constraints

3. **Create Interaction Points:**
   - Create targets and rest positions for each effector
   - Assign them in the inspector

## **Troubleshooting**

### Common Issues:
- **"No Animator component found"** - Add Animator component to your NPC
- **"Animator is not humanoid"** - Set Animator Controller Type to Humanoid
- **"No rigs found"** - Run the setup again or check Animation Rigging package

### Validation:
Right-click on the `NPCInteractionSetup` component and select "Validate Setup" to check if everything is configured correctly.

## **Advanced Configuration**

The `NPCInteractionSetup` component has several settings you can adjust:

- **Auto Setup On Start** - Automatically setup when the game starts
- **Create Interaction Points** - Automatically create IK targets and rest positions
- **Setup Rigging** - Create Animation Rigging components
- **Setup Components** - Add required components to the NPC
- **Rigging Settings** - Adjust blend speed, move speed, etc.
- **Interaction Points** - Customize rest position offsets

## **Using the Simple Function**

Once setup is complete, you can use the simple function:

```csharp
// Get the interaction executor
InteractionExecutor executor = GetComponent<InteractionExecutor>();

// Simple interaction
executor.InteractWith(targetObject, EffectorType.RightHand);
```

That's it! Your NPC is now ready for interactions. 🎉 