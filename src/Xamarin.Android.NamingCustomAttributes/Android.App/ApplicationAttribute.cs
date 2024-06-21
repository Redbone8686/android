//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by 'manifest-attribute-codegen'.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

#nullable enable

using System;

namespace Android.App;

[Serializable]
[AttributeUsage (AttributeTargets.Assembly | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed partial class ApplicationAttribute : Attribute, Java.Interop.IJniNameProviderAttribute {
	public ApplicationAttribute ()
	{
	}

	public bool AllowBackup { get; set; }

	public bool AllowClearUserData { get; set; }

	public bool AllowTaskReparenting { get; set; }

	public Type? BackupAgent { get; set; }

	public bool BackupInForeground { get; set; }

	public string? Banner { get; set; }

	public bool Debuggable { get; set; }

	public string? Description { get; set; }

	public bool DirectBootAware { get; set; }

	public bool Enabled { get; set; }

	public bool ExtractNativeLibs { get; set; }

	public bool FullBackupContent { get; set; }

	public bool FullBackupOnly { get; set; }

	public bool HardwareAccelerated { get; set; }

	public bool HasCode { get; set; }

	public string? Icon { get; set; }

	public bool KillAfterRestore { get; set; }

	public string? Label { get; set; }

	public bool LargeHeap { get; set; }

	public string? Logo { get; set; }

	public Type? ManageSpaceActivity { get; set; }

	public string? Name { get; set; }

	public string? NetworkSecurityConfig { get; set; }

	public string? Permission { get; set; }

	public bool Persistent { get; set; }

	public string? Process { get; set; }

	public string? RequiredAccountType { get; set; }

	public bool ResizeableActivity { get; set; }

	public bool RestoreAnyVersion { get; set; }

	public string? RestrictedAccountType { get; set; }

	public string? RoundIcon { get; set; }

	public bool SupportsRtl { get; set; }

	public string? TaskAffinity { get; set; }

	public string? Theme { get; set; }

	public Android.Content.PM.UiOptions UiOptions { get; set; }

	public bool UsesCleartextTraffic { get; set; }

	public bool VMSafeMode { get; set; }

#if XABT_MANIFEST_EXTENSIONS
	static Xamarin.Android.Manifest.ManifestDocumentElement<ApplicationAttribute> mapping = new ("application");

	static ApplicationAttribute ()
	{
		mapping.Add (
			member: "AllowBackup",
			attributeName: "allowBackup",
			getter: self => self.AllowBackup,
			setter: (self, value) => self.AllowBackup = (bool) value
		);
		mapping.Add (
			member: "AllowClearUserData",
			attributeName: "allowClearUserData",
			getter: self => self.AllowClearUserData,
			setter: (self, value) => self.AllowClearUserData = (bool) value
		);
		mapping.Add (
			member: "AllowTaskReparenting",
			attributeName: "allowTaskReparenting",
			getter: self => self.AllowTaskReparenting,
			setter: (self, value) => self.AllowTaskReparenting = (bool) value
		);
		mapping.Add (
			member: "BackupInForeground",
			attributeName: "backupInForeground",
			getter: self => self.BackupInForeground,
			setter: (self, value) => self.BackupInForeground = (bool) value
		);
		mapping.Add (
			member: "Banner",
			attributeName: "banner",
			getter: self => self.Banner,
			setter: (self, value) => self.Banner = (string?) value
		);
		mapping.Add (
			member: "Debuggable",
			attributeName: "debuggable",
			getter: self => self.Debuggable,
			setter: (self, value) => self.Debuggable = (bool) value
		);
		mapping.Add (
			member: "Description",
			attributeName: "description",
			getter: self => self.Description,
			setter: (self, value) => self.Description = (string?) value
		);
		mapping.Add (
			member: "DirectBootAware",
			attributeName: "directBootAware",
			getter: self => self.DirectBootAware,
			setter: (self, value) => self.DirectBootAware = (bool) value
		);
		mapping.Add (
			member: "Enabled",
			attributeName: "enabled",
			getter: self => self.Enabled,
			setter: (self, value) => self.Enabled = (bool) value
		);
		mapping.Add (
			member: "ExtractNativeLibs",
			attributeName: "extractNativeLibs",
			getter: self => self.ExtractNativeLibs,
			setter: (self, value) => self.ExtractNativeLibs = (bool) value
		);
		mapping.Add (
			member: "FullBackupContent",
			attributeName: "fullBackupContent",
			getter: self => self.FullBackupContent,
			setter: (self, value) => self.FullBackupContent = (bool) value
		);
		mapping.Add (
			member: "FullBackupOnly",
			attributeName: "fullBackupOnly",
			getter: self => self.FullBackupOnly,
			setter: (self, value) => self.FullBackupOnly = (bool) value
		);
		mapping.Add (
			member: "HardwareAccelerated",
			attributeName: "hardwareAccelerated",
			getter: self => self.HardwareAccelerated,
			setter: (self, value) => self.HardwareAccelerated = (bool) value
		);
		mapping.Add (
			member: "HasCode",
			attributeName: "hasCode",
			getter: self => self.HasCode,
			setter: (self, value) => self.HasCode = (bool) value
		);
		mapping.Add (
			member: "Icon",
			attributeName: "icon",
			getter: self => self.Icon,
			setter: (self, value) => self.Icon = (string?) value
		);
		mapping.Add (
			member: "KillAfterRestore",
			attributeName: "killAfterRestore",
			getter: self => self.KillAfterRestore,
			setter: (self, value) => self.KillAfterRestore = (bool) value
		);
		mapping.Add (
			member: "Label",
			attributeName: "label",
			getter: self => self.Label,
			setter: (self, value) => self.Label = (string?) value
		);
		mapping.Add (
			member: "LargeHeap",
			attributeName: "largeHeap",
			getter: self => self.LargeHeap,
			setter: (self, value) => self.LargeHeap = (bool) value
		);
		mapping.Add (
			member: "Logo",
			attributeName: "logo",
			getter: self => self.Logo,
			setter: (self, value) => self.Logo = (string?) value
		);
		mapping.Add (
			member: "NetworkSecurityConfig",
			attributeName: "networkSecurityConfig",
			getter: self => self.NetworkSecurityConfig,
			setter: (self, value) => self.NetworkSecurityConfig = (string?) value
		);
		mapping.Add (
			member: "Permission",
			attributeName: "permission",
			getter: self => self.Permission,
			setter: (self, value) => self.Permission = (string?) value
		);
		mapping.Add (
			member: "Persistent",
			attributeName: "persistent",
			getter: self => self.Persistent,
			setter: (self, value) => self.Persistent = (bool) value
		);
		mapping.Add (
			member: "Process",
			attributeName: "process",
			getter: self => self.Process,
			setter: (self, value) => self.Process = (string?) value
		);
		mapping.Add (
			member: "RequiredAccountType",
			attributeName: "requiredAccountType",
			getter: self => self.RequiredAccountType,
			setter: (self, value) => self.RequiredAccountType = (string?) value
		);
		mapping.Add (
			member: "ResizeableActivity",
			attributeName: "resizeableActivity",
			getter: self => self.ResizeableActivity,
			setter: (self, value) => self.ResizeableActivity = (bool) value
		);
		mapping.Add (
			member: "RestoreAnyVersion",
			attributeName: "restoreAnyVersion",
			getter: self => self.RestoreAnyVersion,
			setter: (self, value) => self.RestoreAnyVersion = (bool) value
		);
		mapping.Add (
			member: "RestrictedAccountType",
			attributeName: "restrictedAccountType",
			getter: self => self.RestrictedAccountType,
			setter: (self, value) => self.RestrictedAccountType = (string?) value
		);
		mapping.Add (
			member: "RoundIcon",
			attributeName: "roundIcon",
			getter: self => self.RoundIcon,
			setter: (self, value) => self.RoundIcon = (string?) value
		);
		mapping.Add (
			member: "SupportsRtl",
			attributeName: "supportsRtl",
			getter: self => self.SupportsRtl,
			setter: (self, value) => self.SupportsRtl = (bool) value
		);
		mapping.Add (
			member: "TaskAffinity",
			attributeName: "taskAffinity",
			getter: self => self.TaskAffinity,
			setter: (self, value) => self.TaskAffinity = (string?) value
		);
		mapping.Add (
			member: "Theme",
			attributeName: "theme",
			getter: self => self.Theme,
			setter: (self, value) => self.Theme = (string?) value
		);
		mapping.Add (
			member: "UiOptions",
			attributeName: "uiOptions",
			getter: self => self.UiOptions,
			setter: (self, value) => self.UiOptions = (Android.Content.PM.UiOptions) value
		);
		mapping.Add (
			member: "UsesCleartextTraffic",
			attributeName: "usesCleartextTraffic",
			getter: self => self.UsesCleartextTraffic,
			setter: (self, value) => self.UsesCleartextTraffic = (bool) value
		);
		mapping.Add (
			member: "VMSafeMode",
			attributeName: "vmSafeMode",
			getter: self => self.VMSafeMode,
			setter: (self, value) => self.VMSafeMode = (bool) value
		);

		AddManualMapping ();
	}

	static partial void AddManualMapping ();
#endif // XABT_MANIFEST_EXTENSIONS
}
