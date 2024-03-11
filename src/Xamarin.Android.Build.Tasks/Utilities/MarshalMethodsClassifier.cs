using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Java.Interop.Tools.Cecil;
using Java.Interop.Tools.JavaCallableWrappers;
using Java.Interop.Tools.TypeNameMappings;

using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Utilities;
using Mono.Cecil;

namespace Xamarin.Android.Tasks
{
	sealed class MarshalMethodEntry
	{
		/// <summary>
		/// The "real" native callback, used if it doesn't contain any non-blittable types in its parameters
		/// or return type.
		/// </summary>
		MethodDefinition nativeCallbackReal;

		/// <summary>
		/// Used only when <see cref="NeedsBlittableWorkaround"/> is <c>true</c>. This wrapper is generated by
		/// <see cref="MarshalMethodsAssemblyRewriter" /> when rewriting assemblies, for methods which have either
		/// a non-blittable return type or a parameter of a non-blittable type.
		/// </summary>
		public MethodDefinition? NativeCallbackWrapper { get; set; }
		public TypeDefinition DeclaringType            { get; }
		public MethodDefinition? Connector             { get; }
		public MethodDefinition? RegisteredMethod      { get; }
		public MethodDefinition? ImplementedMethod     { get; }
		public FieldDefinition? CallbackField          { get; }
		public string JniTypeName                      { get; }
		public string JniMethodName                    { get; }
		public string JniMethodSignature               { get; }
		public bool NeedsBlittableWorkaround           { get; }

		public MethodDefinition NativeCallback         => NativeCallbackWrapper ?? nativeCallbackReal;
		public bool IsSpecial                          { get; }

		public MarshalMethodEntry (TypeDefinition declaringType, MethodDefinition nativeCallback, MethodDefinition connector, MethodDefinition
		                           registeredMethod, MethodDefinition implementedMethod, FieldDefinition callbackField, string jniTypeName,
		                           string jniName, string jniSignature, bool needsBlittableWorkaround)
		{
			DeclaringType = declaringType ?? throw new ArgumentNullException (nameof (declaringType));
			nativeCallbackReal = nativeCallback ?? throw new ArgumentNullException (nameof (nativeCallback));
			Connector = connector ?? throw new ArgumentNullException (nameof (connector));
			RegisteredMethod = registeredMethod ?? throw new ArgumentNullException (nameof (registeredMethod));
			ImplementedMethod = implementedMethod ?? throw new ArgumentNullException (nameof (implementedMethod));
			CallbackField = callbackField; // we don't require the callback field to exist
			JniTypeName = EnsureNonEmpty (jniTypeName, nameof (jniTypeName));
			JniMethodName = EnsureNonEmpty (jniName, nameof (jniName));
			JniMethodSignature = EnsureNonEmpty (jniSignature, nameof (jniSignature));
			NeedsBlittableWorkaround = needsBlittableWorkaround;
			IsSpecial = false;
		}

		public MarshalMethodEntry (TypeDefinition declaringType, MethodDefinition nativeCallback, string jniTypeName, string jniName, string jniSignature)
		{
			DeclaringType = declaringType ?? throw new ArgumentNullException (nameof (declaringType));
			nativeCallbackReal = nativeCallback ?? throw new ArgumentNullException (nameof (nativeCallback));
			JniTypeName = EnsureNonEmpty (jniTypeName, nameof (jniTypeName));
			JniMethodName = EnsureNonEmpty (jniName, nameof (jniName));
			JniMethodSignature = EnsureNonEmpty (jniSignature, nameof (jniSignature));
			IsSpecial = true;
		}

		public MarshalMethodEntry (MarshalMethodEntry other, MethodDefinition nativeCallback)
			: this (other.DeclaringType, nativeCallback, other.Connector, other.RegisteredMethod,
			        other.ImplementedMethod, other.CallbackField, other.JniTypeName, other.JniMethodName,
			        other.JniMethodSignature, other.NeedsBlittableWorkaround)
		{}

		string EnsureNonEmpty (string s, string argName)
		{
			if (String.IsNullOrEmpty (s)) {
				throw new ArgumentException ("must not be null or empty", argName);
			}

			return s;
		}
	}

	class MarshalMethodsClassifier : JavaCallableMethodClassifier
	{
		sealed class ConnectorInfo
		{
			public string MethodName                  { get; }
			public string TypeName                    { get; }
			public AssemblyNameReference AssemblyName { get; }

			public ConnectorInfo (string spec)
			{
				string[] connectorSpec = spec.Split (':');
				MethodName = connectorSpec[0];

				if (connectorSpec.Length < 2) {
					return;
				}

				string fullTypeName = connectorSpec[1];
				int comma = fullTypeName.IndexOf (',');
				TypeName = fullTypeName.Substring (0, comma);
				AssemblyName = AssemblyNameReference.Parse (fullTypeName.Substring (comma + 1).Trim ());
			}
		}

		interface IMethodSignatureMatcher
		{
			bool Matches (MethodDefinition method);
		}

		sealed class NativeCallbackSignature : IMethodSignatureMatcher
		{
			static readonly HashSet<string> verbatimTypes = new HashSet<string> (StringComparer.Ordinal) {
				"System.Boolean",
				"System.Byte",
				"System.Char",
				"System.Double",
				"System.Int16",
				"System.Int32",
				"System.Int64",
				"System.IntPtr",
				"System.SByte",
				"System.Single",
				"System.UInt16",
				"System.UInt32",
				"System.UInt64",
				"System.Void",
			};

			readonly List<string> paramTypes;
			readonly string returnType;
			readonly TaskLoggingHelper log;
			readonly TypeDefinitionCache cache;

			public NativeCallbackSignature (MethodDefinition target, TaskLoggingHelper log, TypeDefinitionCache cache)
			{
				this.log = log;
				this.cache = cache;
				returnType = MapType (target.ReturnType);
				paramTypes = new List<string> {
					"System.IntPtr", // jnienv
					"System.IntPtr", // native__this
				};

				foreach (ParameterDefinition pd in target.Parameters) {
					paramTypes.Add (MapType (pd.ParameterType));
				}
			}

			string MapType (TypeReference typeRef)
			{
				string? typeName = null;
				if (!typeRef.IsGenericParameter && !typeRef.IsArray) {
					TypeDefinition typeDef = cache.Resolve (typeRef);
					if (typeDef == null) {
						throw new InvalidOperationException ($"Unable to resolve type '{typeRef.FullName}'");
					}

					if (typeDef.IsEnum) {
						return GetEnumUnderlyingType (typeDef).FullName;
					}
				}

				if (String.IsNullOrEmpty (typeName)) {
					typeName = typeRef.FullName;
				}

				if (verbatimTypes.Contains (typeName)) {
					return typeName;
				}

				// Android.Graphics.Color is mapped to/from a native `int`
				if (String.Compare (typeName, "Android.Graphics.Color", StringComparison.Ordinal) == 0) {
					return "System.Int32";
				}

				return "System.IntPtr";
			}

			static TypeReference GetEnumUnderlyingType (TypeDefinition td)
			{
				var fields = td.Fields;

				for (int i = 0; i < fields.Count; i++) {
					var field = fields [i];
					if (!field.IsStatic)
						return field.FieldType;
				}

				throw new InvalidOperationException ($"Unable to determine underlying type of the '{td.FullName}' enum");
			}

			public bool Matches (MethodDefinition method)
			{
				if (method.Parameters.Count != paramTypes.Count || !method.IsStatic) {
					log.LogWarning ($"Method '{method.FullName}' doesn't match native callback signature (invalid parameter count or not static)");
					return false;
				}

				if (String.Compare (returnType, method.ReturnType.FullName, StringComparison.Ordinal) != 0) {
					log.LogWarning ($"Method '{method.FullName}' doesn't match native callback signature (invalid return type: expected '{returnType}', found '{method.ReturnType.FullName}')");
					return false;
				}

				for (int i = 0; i < method.Parameters.Count; i++) {
					ParameterDefinition pd = method.Parameters[i];
					string parameterTypeName;

					if (pd.ParameterType.IsArray) {
						parameterTypeName = $"{pd.ParameterType.FullName}[]";
					} else {
						parameterTypeName = pd.ParameterType.FullName;
					}

					if (String.Compare (parameterTypeName, paramTypes[i], StringComparison.Ordinal) != 0) {
						log.LogWarning ($"Method '{method.FullName}' doesn't match native callback signature, expected parameter type '{paramTypes[i]}' at position {i}, found '{parameterTypeName}'");
						return false;
					}
				}

				return true;
			}
		}

		TypeDefinitionCache tdCache;
		XAAssemblyResolver resolver;
		Dictionary<string, IList<MarshalMethodEntry>> marshalMethods;
		HashSet<AssemblyDefinition> assemblies;
		TaskLoggingHelper log;
		HashSet<TypeDefinition> typesWithDynamicallyRegisteredMethods;
		ulong rejectedMethodCount = 0;
		ulong wrappedMethodCount = 0;
		StreamWriter ignoredMethodsLog;

		public IDictionary<string, IList<MarshalMethodEntry>> MarshalMethods => marshalMethods;
		public ICollection<AssemblyDefinition> Assemblies => assemblies;
		public ulong RejectedMethodCount => rejectedMethodCount;
		public ulong WrappedMethodCount => wrappedMethodCount;

		public MarshalMethodsClassifier (TypeDefinitionCache tdCache, XAAssemblyResolver res, TaskLoggingHelper log, string intermediateOutputDirectory)
		{
			this.log = log ?? throw new ArgumentNullException (nameof (log));
			this.tdCache = tdCache ?? throw new ArgumentNullException (nameof (tdCache));
			resolver = res ?? throw new ArgumentNullException (nameof (tdCache));
			marshalMethods = new Dictionary<string, IList<MarshalMethodEntry>> (StringComparer.Ordinal);
			assemblies = new HashSet<AssemblyDefinition> ();
			typesWithDynamicallyRegisteredMethods = new HashSet<TypeDefinition> ();

			var fs = File.Open (Path.Combine (intermediateOutputDirectory, "marshal-methods-ignored.txt"), FileMode.Create);
			ignoredMethodsLog = new StreamWriter (fs, Files.UTF8withoutBOM);
		}

		public void FlushAndCloseOutputs ()
		{
			ignoredMethodsLog.WriteLine ();
			ignoredMethodsLog.WriteLine ($"Marshal methods count: {MarshalMethods.Count}; Rejected methods count: {RejectedMethodCount}");
			ignoredMethodsLog.Flush ();
			ignoredMethodsLog.Close ();
		}

		public override bool ShouldBeDynamicallyRegistered (TypeDefinition topType, MethodDefinition registeredMethod, MethodDefinition implementedMethod, CustomAttribute? registerAttribute)
		{
			if (registeredMethod == null) {
				throw new ArgumentNullException (nameof (registeredMethod));
			}

			if (implementedMethod == null) {
				throw new ArgumentNullException (nameof (registeredMethod));
			}

			if (registerAttribute == null) {
				throw new ArgumentNullException (nameof (registerAttribute));
			}

			if (!IsDynamicallyRegistered (topType, registeredMethod, implementedMethod, registerAttribute)) {
				return false;
			}

			typesWithDynamicallyRegisteredMethods.Add (topType);
			return true;
		}

		public bool FoundDynamicallyRegisteredMethods (TypeDefinition type)
		{
			return typesWithDynamicallyRegisteredMethods.Contains (type);
		}

		void AddTypeManagerSpecialCaseMethods ()
		{
			const string FullTypeName = "Java.Interop.TypeManager+JavaTypeManager, Mono.Android";

			AssemblyDefinition monoAndroid = resolver.Resolve ("Mono.Android");
			TypeDefinition? typeManager = monoAndroid?.MainModule.FindType ("Java.Interop.TypeManager");
			TypeDefinition? javaTypeManager = typeManager?.GetNestedType ("JavaTypeManager");

			if (javaTypeManager == null) {
				throw new InvalidOperationException ($"Internal error: unable to find the {FullTypeName} type in the Mono.Android assembly");
			}

			MethodDefinition? nActivate_mm = null;
			MethodDefinition? nActivate = null;

			foreach (MethodDefinition method in javaTypeManager.Methods) {
				if (nActivate_mm == null && IsMatchingMethod (method, "n_Activate_mm")) {
					if (method.GetCustomAttributes ("System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute").Any (cattr => cattr != null)) {
						nActivate_mm = method;
					} else {
						log.LogWarning ($"Method '{method.FullName}' isn't decorated with the UnmanagedCallersOnly attribute");
						continue;
					}
				}

				if (nActivate == null && IsMatchingMethod (method, "n_Activate")) {
					nActivate = method;
				}

				if (nActivate_mm != null && nActivate != null) {
					break;
				}
			}

			if (nActivate_mm == null) {
				ThrowMissingMethod ("nActivate_mm");
			}

			if (nActivate == null) {
				ThrowMissingMethod ("nActivate");
			}

			string? jniTypeName = null;
			foreach (CustomAttribute cattr in javaTypeManager.GetCustomAttributes ("Android.Runtime.RegisterAttribute")) {
				if (cattr.ConstructorArguments.Count != 1) {
					log.LogDebugMessage ($"[Register] attribute on type '{FullTypeName}' is expected to have 1 constructor argument, found {cattr.ConstructorArguments.Count}");
					continue;
				}

				jniTypeName = (string)cattr.ConstructorArguments[0].Value;
				if (!String.IsNullOrEmpty (jniTypeName)) {
					break;
				}
			}

			string? jniMethodName = null;
			string? jniSignature = null;
			foreach (CustomAttribute cattr in nActivate.GetCustomAttributes ("Android.Runtime.RegisterAttribute")) {
				if (cattr.ConstructorArguments.Count != 3) {
					log.LogDebugMessage ($"[Register] attribute on method '{nActivate.FullName}' is expected to have 3 constructor arguments, found {cattr.ConstructorArguments.Count}");
					continue;
				}

				jniMethodName = (string)cattr.ConstructorArguments[0].Value;
				jniSignature = (string)cattr.ConstructorArguments[1].Value;

				if (!String.IsNullOrEmpty (jniMethodName) && !String.IsNullOrEmpty (jniSignature)) {
					break;
				}
			}

			bool missingInfo = false;
			if (String.IsNullOrEmpty (jniTypeName)) {
				missingInfo = true;
				log.LogDebugMessage ($"Failed to obtain Java type name from the [Register] attribute on type '{FullTypeName}'");
			}

			if (String.IsNullOrEmpty (jniMethodName)) {
				missingInfo = true;
				log.LogDebugMessage ($"Failed to obtain Java method name from the [Register] attribute on method '{nActivate.FullName}'");
			}

			if (String.IsNullOrEmpty (jniSignature)) {
				missingInfo = true;
				log.LogDebugMessage ($"Failed to obtain Java method signature from the [Register] attribute on method '{nActivate.FullName}'");
			}

			if (missingInfo) {
				throw new InvalidOperationException ($"Missing information while constructing marshal method for the '{nActivate_mm.FullName}' method");
			}

			var entry = new MarshalMethodEntry (javaTypeManager, nActivate_mm, jniTypeName, jniMethodName, jniSignature);
			marshalMethods.Add (".:!SpEcIaL:Java.Interop.TypeManager+JavaTypeManager::n_Activate_mm", new List<MarshalMethodEntry> { entry });

			void ThrowMissingMethod (string name)
			{
				throw new InvalidOperationException ($"Internal error: unable to find the '{name}' method in the '{FullTypeName}' type");
			}

			bool IsMatchingMethod (MethodDefinition method, string name)
			{
				if (String.Compare (name, method.Name, StringComparison.Ordinal) != 0) {
					return false;
				}

				if (!method.IsStatic) {
					log.LogWarning ($"Method '{method.FullName}' is not static");
					return false;
				}

				if (!method.IsPrivate) {
					log.LogWarning ($"Method '{method.FullName}' is not private");
					return false;
				}

				return true;
			}
		}

		/// <summary>
		/// Adds MarshalMethodEntry for each method that won't be returned by the JavaInterop type scanner, mostly
		/// used for hand-written methods (e.g. Java.Interop.TypeManager+JavaTypeManager::n_Activate)
		/// </summary>
		public void AddSpecialCaseMethods ()
		{
			AddTypeManagerSpecialCaseMethods ();
		}

		bool IsDynamicallyRegistered (TypeDefinition topType, MethodDefinition registeredMethod, MethodDefinition implementedMethod, CustomAttribute registerAttribute)
		{
			if (registerAttribute.ConstructorArguments.Count != 3) {
				log.LogWarning ($"Method '{registeredMethod.FullName}' will be registered dynamically, not enough arguments to the [Register] attribute to generate marshal method.");
				return true;
			}

			var connector = new ConnectorInfo ((string)registerAttribute.ConstructorArguments[2].Value);

			if (IsStandardHandler (topType, connector, registeredMethod, implementedMethod, jniName: (string)registerAttribute.ConstructorArguments[0].Value, jniSignature: (string)registerAttribute.ConstructorArguments[1].Value)) {
				return false;
			}

			log.LogWarning ($"Method '{registeredMethod.FullName}' will be registered dynamically");
			rejectedMethodCount++;
			return true;
		}

		void LogIgnored (TypeDefinition type, MethodDefinition method, string message, bool logWarning = true)
		{
			if (logWarning) {
				log.LogWarning (message);
			}

			ignoredMethodsLog.WriteLine ($"{type.FullName}\t{method.FullName}\t{message}");
		}

		bool IsStandardHandler (TypeDefinition topType, ConnectorInfo connector, MethodDefinition registeredMethod, MethodDefinition implementedMethod, string jniName, string jniSignature)
		{
			const string HandlerNameStart = "Get";
			const string HandlerNameEnd = "Handler";

			string connectorName = connector.MethodName;
			if (connectorName.Length < HandlerNameStart.Length + HandlerNameEnd.Length + 1 ||
			    !connectorName.StartsWith (HandlerNameStart, StringComparison.Ordinal) ||
			    !connectorName.EndsWith (HandlerNameEnd, StringComparison.Ordinal)) {
				log.LogWarning ($"\tConnector name '{connectorName}' must start with '{HandlerNameStart}', end with '{HandlerNameEnd}' and have at least one character between the two parts.");
				return false;
			}

			// TODO: if we can't find native callback and/or delegate field using `callbackNameCore`, fall back to `jniName` (which is the first argument to the `[Register]`
			// attribute). Or simply use `jniName` at once - needs testing.

			string callbackNameCore = connectorName.Substring (HandlerNameStart.Length, connectorName.Length - HandlerNameStart.Length - HandlerNameEnd.Length);
			string nativeCallbackName = $"n_{callbackNameCore}";
			string delegateFieldName = $"cb_{Char.ToLowerInvariant (callbackNameCore[0])}{callbackNameCore.Substring (1)}";

			TypeDefinition connectorDeclaringType = connector.AssemblyName == null ? registeredMethod.DeclaringType : FindType (resolver.Resolve (connector.AssemblyName), connector.TypeName);

			MethodDefinition connectorMethod = FindMethod (connectorDeclaringType, connectorName);
			if (connectorMethod == null) {
				LogIgnored (topType, registeredMethod, $"\tConnector method '{connectorName}' not found in type '{connectorDeclaringType.FullName}'");
				return false;
			}

			if (String.Compare ("System.Delegate", connectorMethod.ReturnType.FullName, StringComparison.Ordinal) != 0) {
				LogIgnored (topType, registeredMethod, $"\tConnector '{connectorName}' in type '{connectorDeclaringType.FullName}' has invalid return type, expected 'System.Delegate', found '{connectorMethod.ReturnType.FullName}'");
				return false;
			}

			var ncbs = new NativeCallbackSignature (registeredMethod, log, tdCache);
			MethodDefinition nativeCallbackMethod = FindMethod (connectorDeclaringType, nativeCallbackName, ncbs);
			if (nativeCallbackMethod == null) {
				LogIgnored (topType, registeredMethod, $"\tUnable to find native callback method '{nativeCallbackName}' in type '{connectorDeclaringType.FullName}', matching the '{registeredMethod.FullName}' signature (jniName: '{jniName}')");
				return false;
			}

			if (!EnsureIsValidUnmanagedCallersOnlyTarget (topType, registeredMethod, nativeCallbackMethod, out bool needsBlittableWorkaround)) {
				return false;
			}

			// In the standard handler "pattern", the native callback backing field is private, static and thus in the same type
			// as the native callback.
			FieldDefinition delegateField = FindField (nativeCallbackMethod.DeclaringType, delegateFieldName);
			if (delegateField != null) {
				if (String.Compare ("System.Delegate", delegateField.FieldType.FullName, StringComparison.Ordinal) != 0) {
					LogIgnored (topType, registeredMethod, $"\tdelegate field '{delegateFieldName}' in type '{nativeCallbackMethod.DeclaringType.FullName}' has invalid type, expected 'System.Delegate', found '{delegateField.FieldType.FullName}'");
					return false;
				}
			}

			// TODO: check where DeclaringType is lost between here and rewriter, for:
			//
			// Classifying:
			//         method: Java.Lang.Object Microsoft.Maui.Controls.Platform.Compatibility.ShellSearchViewAdapter::GetItem(System.Int32)
			//         registered method: Java.Lang.Object Android.Widget.BaseAdapter::GetItem(System.Int32))
			//         Attr: Android.Runtime.RegisterAttribute (parameter count: 3)
			//         Top type: Microsoft.Maui.Controls.Platform.Compatibility.ShellSearchViewAdapter
			//         Managed type: Android.Widget.BaseAdapter, Mono.Android
			//         connector: GetGetItem_IHandler (from spec: 'GetGetItem_IHandler')
			//         connector name: GetGetItem_IHandler
			//         native callback name: n_GetItem_I
			//         delegate field name: cb_getItem_I
			// ##G1: Microsoft.Maui.Controls.Platform.Compatibility.ShellSearchViewAdapter -> crc640ec207abc449b2ca/ShellSearchViewAdapter
			// ##G1: top type: Microsoft.Maui.Controls.Platform.Compatibility.ShellSearchViewAdapter -> crc640ec207abc449b2ca/ShellSearchViewAdapter
			// ##G1: connectorMethod: System.Delegate Android.Widget.BaseAdapter::GetGetItem_IHandler()
			// ##G1: delegateField: System.Delegate Android.Widget.BaseAdapter::cb_getItem_I
			//
			// And in the rewriter:
			//
			//         System.IntPtr Android.Widget.BaseAdapter::n_GetItem_I(System.IntPtr,System.IntPtr,System.Int32) (token: 0x5fe3)
			// Top type == 'Microsoft.Maui.Controls.Platform.Compatibility.ShellSearchViewAdapter'
			// 	NativeCallback == 'System.IntPtr Android.Widget.BaseAdapter::n_GetItem_I(System.IntPtr,System.IntPtr,System.Int32)'
			// 	Connector == 'System.Delegate GetGetItem_IHandler()'
			// 	method.NativeCallback.CustomAttributes == Mono.Collections.Generic.Collection`1[Mono.Cecil.CustomAttribute]
			// 	method.Connector.DeclaringType == 'null'
			// 	method.Connector.DeclaringType.Methods == 'null'
			// 	method.CallbackField == System.Delegate cb_getItem_I
			// 	method.CallbackField?.DeclaringType == 'null'
			// 	method.CallbackField?.DeclaringType.Fields == 'null'

			StoreMethod (
				new MarshalMethodEntry (
					topType,
					nativeCallbackMethod,
					connectorMethod,
					registeredMethod,
					implementedMethod,
					delegateField,
					JavaNativeTypeManager.ToJniName (topType, tdCache),
					jniName,
					jniSignature,
					needsBlittableWorkaround
				)
			);

			StoreAssembly (connectorMethod.Module.Assembly);
			StoreAssembly (nativeCallbackMethod.Module.Assembly);
			if (delegateField != null) {
				StoreAssembly (delegateField.Module.Assembly);
			}

			return true;
		}

		bool EnsureIsValidUnmanagedCallersOnlyTarget (TypeDefinition topType, MethodDefinition registeredMethod, MethodDefinition nativeCallbackMethod, out bool needsBlittableWorkaround)
		{
			needsBlittableWorkaround = false;

			// Requirements: https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.unmanagedcallersonlyattribute?view=net-6.0#remarks
			if (!nativeCallbackMethod.IsStatic) {
				return LogReasonWhyAndReturnFailure ($"is not static");
			}

			if (nativeCallbackMethod.HasGenericParameters) {
				return LogReasonWhyAndReturnFailure ($"has generic parameters");
			}

			TypeReference type;
			bool needsWrapper = false;
			if (String.Compare ("System.Void", nativeCallbackMethod.ReturnType.FullName, StringComparison.Ordinal) != 0) {
				type = GetRealType (nativeCallbackMethod.ReturnType);
				if (!IsAcceptable (type)) {
					needsBlittableWorkaround = true;
					WarnWhy ($"has a non-blittable return type '{type.FullName}'");
					needsWrapper = true;
				}
			}

			if (nativeCallbackMethod.DeclaringType.HasGenericParameters) {
				return LogReasonWhyAndReturnFailure ($"is declared in a type with generic parameters");
			}

			if (!nativeCallbackMethod.HasParameters) {
				return UpdateWrappedCountAndReturn (true);
			}

			foreach (ParameterDefinition pdef in nativeCallbackMethod.Parameters) {
				type = GetRealType (pdef.ParameterType);

				if (!IsAcceptable (type)) {
					needsBlittableWorkaround = true;
					WarnWhy ($"has a parameter ({pdef.Name}) of non-blittable type '{type.FullName}'");
					needsWrapper = true;
				}
			}

			return UpdateWrappedCountAndReturn (true);

			bool UpdateWrappedCountAndReturn (bool retval)
			{
				if (needsWrapper) {
					wrappedMethodCount++;
				}

				return retval;
			}

			bool IsAcceptable (TypeReference type)
			{
				if (type.IsArray) {
					var array = new ArrayType (type);
					if (array.Rank > 1) {
						return false;
					}
				}

				return type.IsBlittable ();
			}

			TypeReference GetRealType (TypeReference type)
			{
				if (type.IsArray) {
					return type.GetElementType ();
				}

				return type;
			}

			bool LogReasonWhyAndReturnFailure (string why)
			{
				LogIgnored (topType, registeredMethod, $"Method '{nativeCallbackMethod.FullName}' {why}. It cannot be used with the `[UnmanagedCallersOnly]` attribute");
				return false;
			}

			void WarnWhy (string why)
			{
				// TODO: change to LogWarning once the generator can output code which requires no non-blittable wrappers
				log.LogDebugMessage ($"Method '{nativeCallbackMethod.FullName}' {why}. A workaround is required, this may make the application slower");
			}
		}

		TypeDefinition FindType (AssemblyDefinition asm, string typeName)
		{
			foreach (ModuleDefinition md in asm.Modules) {
				foreach (TypeDefinition td in md.Types) {
					TypeDefinition match = GetMatchingType (td);
					if (match != null) {
						return match;
					}
				}
			}

			return null;

			TypeDefinition GetMatchingType (TypeDefinition def)
			{
				if (String.Compare (def.FullName, typeName, StringComparison.Ordinal) == 0) {
					return def;
				}

				if (!def.HasNestedTypes) {
					return null;
				}

				TypeDefinition ret;
				foreach (TypeDefinition nested in def.NestedTypes) {
					ret = GetMatchingType (nested);
					if (ret != null) {
						return ret;
					}
				}

				return null;
			}
		}

		MethodDefinition FindMethod (TypeDefinition type, string methodName, IMethodSignatureMatcher signatureMatcher = null)
		{
			foreach (MethodDefinition method in type.Methods) {
				if (!method.IsManaged || method.IsConstructor) {
					continue;
				}

				if (String.Compare (methodName, method.Name, StringComparison.Ordinal) != 0) {
					continue;
				}

				if (signatureMatcher == null || signatureMatcher.Matches (method)) {
					return method;
				}
			}

			if (type.BaseType == null) {
				return null;
			}

			return FindMethod (tdCache.Resolve (type.BaseType), methodName, signatureMatcher);
		}

		FieldDefinition FindField (TypeDefinition type, string fieldName, bool lookForInherited = false)
		{
			foreach (FieldDefinition field in type.Fields) {
				if (String.Compare (field.Name, fieldName, StringComparison.Ordinal) == 0) {
					return field;
				}
			}

			if (!lookForInherited || type.BaseType == null) {
				return null;
			}

			return FindField (tdCache.Resolve (type.BaseType), fieldName, lookForInherited);
		}

		public string GetStoreMethodKey (MarshalMethodEntry methodEntry)
		{
			MethodDefinition registeredMethod = methodEntry.RegisteredMethod;
			string typeName = registeredMethod.DeclaringType.FullName.Replace ('/', '+');
			return $"{typeName}, {registeredMethod.DeclaringType.GetPartialAssemblyName (tdCache)}\t{registeredMethod.Name}";
		}

		void StoreMethod (MarshalMethodEntry entry)
		{
			string key = GetStoreMethodKey (entry);

			// Several classes can override the same method, we need to generate the marshal method only once, at the same time
			// keeping track of overloads
			if (!marshalMethods.TryGetValue (key, out IList<MarshalMethodEntry> list) || list == null) {
				list = new List<MarshalMethodEntry> ();
				marshalMethods.Add (key, list);
			}

			string registeredName = $"{entry.DeclaringType.FullName}::{entry.ImplementedMethod.Name}";
			if (list.Count == 0 || !list.Any (me => String.Compare (registeredName, me.ImplementedMethod.FullName, StringComparison.Ordinal) == 0)) {
				list.Add (entry);
			}
		}

		void StoreAssembly (AssemblyDefinition asm)
		{
			if (assemblies.Contains (asm)) {
				return;
			}
			assemblies.Add (asm);
		}
	}
}
