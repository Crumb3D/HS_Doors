# HS_Doors — game calls (from Source)

Not extracted from the game DLL. This is `HS_Doors/Source`. Unity / System / Harmony / our types skipped.

3.2 vs 3.3: `7DaysRef/HS_Doors_3.2_vs_3.3.md`. 3.4 uses the 3.3 calls.

## Inherits
- `Block`
- `BlockPowered`
- `ConsoleCmdAbstract`
- `IModApi`
- `ItemAction`
- `NetPackage`

## HarmonyPatch / typeof game types
- `Entity`
- `EntityAlive`
- `NetPackageManager`
- `PlayerMoveController`
- `string`
- `TextureFullArray`
- `XUiC_InteractionPrompt`

## Type.Method (static / nested)
- `Angle.ToString`
- `Block.GetBlockName`
- `Block.GetBlockValue`
- `Block.GetLocalizedBlockName`
- `BlockShapeNew.GetRotationStatic`
- `Cells.Clear`
- `Doors.Add`
- `Doors.AddRange`
- `Doors.Clear`
- `Doors.Contains`
- `Doors.Find`
- `Doors.FindIndex`
- `Excludes.Add`
- `Excludes.RemoveAt`
- `FieldType.GetGenericArguments`
- `GameIO.GetSaveGameDir`
- `GameIO.GetUserGameDataDir`
- `GameManager.ShowTooltip`
- `GameStartDone.RegisterHandler`
- `Instance.GetPowerItemByWorldPos`
- `Instance.Output`
- `ItemClass.GetForId`
- `Localization.Get`
- `Log.Error`
- `Log.Out`
- `Log.Warning`
- `MaxCrushDps.ToString`
- `MaxMovingCells.ToString`
- `MaxRevolveSpeed.ToString`
- `MaxSlideSpeed.ToString`
- `Open.ToString`
- `PlayerSpawnedInWorld.RegisterHandler`
- `Radius.ToString`
- `Text.IndexOf`
- `Versioning.TargetFrameworkAttribute`
- `WorldShuttingDown.RegisterHandler`
- `XUiC_InteractionPrompt.SetText`
- `XUiC_Radial.RadialContextHoldingSlotIndex`

## new
- `new BlockActivationCommand`
- `new BlockChangeInfo`
- `new BlockValue`
- `new DamageSource`
- `new Vector3i`

## override (must still exist on 3.3 base)
- `CanInteract`
- `Execute`
- `ExecuteAction`
- `GetActivationText`
- `GetBlockActivationCommands`
- `getCommands`
- `getDescription`
- `getHelp`
- `GetLength`
- `HasBlockActivationCommands`
- `HasRadial`
- `OnBlockActivated`
- `OnBlockEntityTransformAfterActivated`
- `ProcessPackage`
- `read`
- `SetupRadial`
- `write`

## By file (line)
### HSDoorsCapture.cs
- `104: new Vector3i`
- `116: Block.GetBlockName`
- `142: new Vector3i`
- `144: new BlockChangeInfo`
- `150: new BlockChangeInfo`
- `206: new Vector3i`
- `207: new Vector3i`
- `212: Block.GetBlockValue`
- `215: new BlockChangeInfo`
- `224: new Vector3i`
- `224: new Vector3i`
- `225: new Vector3i`
- `225: new Vector3i`
- `233: new Vector3i`
- `235: Block.GetBlockValue`
- `247: BlockShapeNew.GetRotationStatic`
- `254: BlockShapeNew.GetRotationStatic`
- `278: BlockShapeNew.GetRotationStatic`
- `308: new Vector3i`
- `311: new BlockValue`
- `312: new BlockChangeInfo`
- `329: new Vector3i`
- `333: new BlockChangeInfo`

### HSDoorsCommands.cs
- `4: inherits ConsoleCmdAbstract`
- `6: override getCommands`
- `11: override getDescription`
- `18: override getHelp`
- `33: override Execute`
- `61: Instance.Output`

### HSDoorsConfig.cs
- `62: new Vector3i`
- `65: new Vector3i`
- `68: new Vector3i`
- `110: Excludes.RemoveAt`
- `114: Excludes.Add`
- `165: GameIO.GetSaveGameDir`
- `236: Doors.Add`
- `260: Doors.Add`
- `274: Doors.AddRange`
- `282: Doors.Clear`
- `284: Doors.Add`
- `332: Doors.FindIndex`
- `334: Doors.Contains`
- `334: Doors.Add`
- `359: Doors.Find`
- `373: Doors.Add`
- `426: Radius.ToString`
- `461: new BlockValue`
- `468: Block.GetLocalizedBlockName`
- `472: Block.GetBlockName`

### HSDoorsController.cs
- `76: Open.ToString`
- `77: Angle.ToString`
- `94: Localization.Get`

### HSDoorsDebug.cs
- `11: Log.Out`
- `16: Log.Out`
- `21: Log.Warning`
- `26: Log.Error`

### HSDoorsMod.cs
- `6: inherits IModApi`
- `18: GameIO.GetUserGameDataDir`
- `29: GameStartDone.RegisterHandler`
- `30: WorldShuttingDown.RegisterHandler`
- `31: PlayerSpawnedInWorld.RegisterHandler`

### HSDoorsNet.cs
- `52: FieldType.GetGenericArguments`
- `168: GameManager.ShowTooltip`
- `186: inherits NetPackage`
- `214: override read`
- `224: new Vector3i`
- `227: override write`
- `243: override ProcessPackage`
- `285: override GetLength`
- `291: HarmonyPatch NetPackageManager`

### HSDoorsPower.cs
- `34: Instance.GetPowerItemByWorldPos`

### HSDoorsPush.cs
- `87: new DamageSource`

### HSDoorsSettings.cs
- `46: GameIO.GetSaveGameDir`
- `127: MaxMovingCells.ToString`
- `128: MaxSlideSpeed.ToString`
- `128: MaxRevolveSpeed.ToString`
- `129: MaxCrushDps.ToString`

### HSDoorsSetup.cs
- `11: GameManager.ShowTooltip`
- `12: Instance.Output`
- `224: Radius.ToString`
- `278: Cells.Clear`

### HSDoorsVisual.cs
- `47: ItemClass.GetForId`

### HSGameVersion.cs
- `20: Log.Error`
- `25: Log.Error`
- `31: Log.Out`

### ItemActionHSDoorsTool.cs
- `6: inherits ItemAction`
- `38: new Vector3i`
- `42: new Vector3i`
- `99: override HasRadial`
- `104: override ExecuteAction`
- `110: GameManager.ShowTooltip`
- `110: Localization.Get`
- `118: override CanInteract`
- `120: Localization.Get`
- `123: override SetupRadial`
- `132: Localization.Get`
- `133: Localization.Get`
- `134: Localization.Get`
- `135: Localization.Get`
- `136: Localization.Get`
- `137: Localization.Get`
- `138: Localization.Get`
- `139: Localization.Get`
- `143: XUiC_Radial.RadialContextHoldingSlotIndex`

### Blocks\BlockHSDoorsPanel.cs
- `3: inherits BlockPowered`
- `5: override HasBlockActivationCommands`
- `10: override GetBlockActivationCommands`
- `27: new BlockActivationCommand`
- `28: new BlockActivationCommand`
- `29: new BlockActivationCommand`
- `30: new BlockActivationCommand`
- `31: new BlockActivationCommand`
- `32: new BlockActivationCommand`
- `33: new BlockActivationCommand`
- `43: override GetActivationText`
- `49: Localization.Get`
- `51: Localization.Get`
- `54: Localization.Get`
- `55: Localization.Get`
- `64: override OnBlockActivated`
- `69: override OnBlockActivated`
- `83: GameManager.ShowTooltip`

### Blocks\BlockHSDoorsPart.cs
- `9: inherits Block`
- `11: override OnBlockEntityTransformAfterActivated`

### obj\Release\.NETFramework,Version=v4.8.AssemblyAttributes.cs
- `4: Versioning.TargetFrameworkAttribute`

### Patches\HSDoorsToolPatch.cs
- `4: HarmonyPatch PlayerMoveController`
- `23: XUiC_InteractionPrompt.SetText`
- `23: Localization.Get`
- `53: XUiC_InteractionPrompt.SetText`
- `62: Localization.Get`
- `79: HarmonyPatch XUiC_InteractionPrompt`
- `86: Localization.Get`
- `88: Text.IndexOf`
- `94: XUiC_InteractionPrompt.SetText`


