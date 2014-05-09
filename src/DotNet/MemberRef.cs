/*
    Copyright (C) 2012-2014 de4dot@gmail.com

    Permission is hereby granted, free of charge, to any person obtaining
    a copy of this software and associated documentation files (the
    "Software"), to deal in the Software without restriction, including
    without limitation the rights to use, copy, modify, merge, publish,
    distribute, sublicense, and/or sell copies of the Software, and to
    permit persons to whom the Software is furnished to do so, subject to
    the following conditions:

    The above copyright notice and this permission notice shall be
    included in all copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
    EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
    MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
    IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
    CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
    TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
    SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

﻿using System;
using System.Collections.Generic;
using System.Threading;
using dnlib.Utils;
using dnlib.DotNet.MD;
using dnlib.Threading;

namespace dnlib.DotNet {
	/// <summary>
	/// A high-level representation of a row in the MemberRef table
	/// </summary>
	public abstract class MemberRef : IHasCustomAttribute, IMethodDefOrRef, ICustomAttributeType, IField {
		/// <summary>
		/// The row id in its table
		/// </summary>
		protected uint rid;

		/// <summary>
		/// The owner module
		/// </summary>
		protected ModuleDef module;

		/// <inheritdoc/>
		public MDToken MDToken {
			get { return new MDToken(Table.MemberRef, rid); }
		}

		/// <inheritdoc/>
		public uint Rid {
			get { return rid; }
			set { rid = value; }
		}

		/// <inheritdoc/>
		public int HasCustomAttributeTag {
			get { return 6; }
		}

		/// <inheritdoc/>
		public int MethodDefOrRefTag {
			get { return 1; }
		}

		/// <inheritdoc/>
		public int CustomAttributeTypeTag {
			get { return 3; }
		}

		/// <summary>
		/// From column MemberRef.Class
		/// </summary>
		public abstract IMemberRefParent Class { get; set; }

		/// <summary>
		/// From column MemberRef.Name
		/// </summary>
		public abstract UTF8String Name { get; set; }

		/// <summary>
		/// From column MemberRef.Signature
		/// </summary>
		public abstract CallingConventionSig Signature { get; set; }

		/// <summary>
		/// Gets all custom attributes
		/// </summary>
		public abstract CustomAttributeCollection CustomAttributes { get; }

		/// <inheritdoc/>
		public bool HasCustomAttributes {
			get { return CustomAttributes.Count > 0; }
		}

		/// <inheritdoc/>
		public ITypeDefOrRef DeclaringType {
			get {
				var owner = Class;

				var tdr = owner as ITypeDefOrRef;
				if (tdr != null)
					return tdr;

				var method = owner as MethodDef;
				if (method != null)
					return method.DeclaringType;

				var mr = owner as ModuleRef;
				if (mr != null) {
					var tr = GetGlobalTypeRef(mr);
					if (module != null)
						return module.UpdateRowId(tr);
					return tr;
				}

				return null;
			}
		}

		TypeRefUser GetGlobalTypeRef(ModuleRef mr) {
			if (module == null)
				return CreateDefaultGlobalTypeRef(mr);
			var globalType = module.GlobalType;
			if (globalType != null && new SigComparer().Equals(module, mr))
				return new TypeRefUser(module, globalType.Namespace, globalType.Name, mr);
			var asm = module.Assembly;
			if (asm == null)
				return CreateDefaultGlobalTypeRef(mr);
			var mod = asm.FindModule(mr.Name);
			if (mod == null)
				return CreateDefaultGlobalTypeRef(mr);
			globalType = mod.GlobalType;
			if (globalType == null)
				return CreateDefaultGlobalTypeRef(mr);
			return new TypeRefUser(module, globalType.Namespace, globalType.Name, mr);
		}

		TypeRefUser CreateDefaultGlobalTypeRef(ModuleRef mr) {
			return new TypeRefUser(module, string.Empty, "<Module>", mr);
		}

		bool IMemberRef.IsType {
			get { return false; }
		}

		bool IMemberRef.IsMethod {
			get { return IsMethodRef; }
		}

		bool IMemberRef.IsField {
			get { return IsFieldRef; }
		}

		bool IMemberRef.IsTypeSpec {
			get { return false; }
		}

		bool IMemberRef.IsTypeRef {
			get { return false; }
		}

		bool IMemberRef.IsTypeDef {
			get { return false; }
		}

		bool IMemberRef.IsMethodSpec {
			get { return false; }
		}

		bool IMemberRef.IsMethodDef {
			get { return false; }
		}

		bool IMemberRef.IsMemberRef {
			get { return true; }
		}

		bool IMemberRef.IsFieldDef {
			get { return false; }
		}

		bool IMemberRef.IsPropertyDef {
			get { return false; }
		}

		bool IMemberRef.IsEventDef {
			get { return false; }
		}

		bool IMemberRef.IsGenericParam {
			get { return false; }
		}

		/// <summary>
		/// <c>true</c> if this is a method reference (<see cref="MethodSig"/> != <c>null</c>)
		/// </summary>
		public bool IsMethodRef {
			get { return MethodSig != null; }
		}

		/// <summary>
		/// <c>true</c> if this is a field reference (<see cref="FieldSig"/> != <c>null</c>)
		/// </summary>
		public bool IsFieldRef {
			get { return FieldSig != null; }
		}

		/// <summary>
		/// Gets/sets the method sig
		/// </summary>
		public MethodSig MethodSig {
			get { return Signature as MethodSig; }
			set { Signature = value; }
		}

		/// <summary>
		/// Gets/sets the field sig
		/// </summary>
		public FieldSig FieldSig {
			get { return Signature as FieldSig; }
			set { Signature = value; }
		}

		/// <inheritdoc/>
		public ModuleDef Module {
			get { return module; }
		}

		/// <summary>
		/// <c>true</c> if the method has a hidden 'this' parameter
		/// </summary>
		public bool HasThis {
			get {
				var ms = MethodSig;
				return ms == null ? false : ms.HasThis;
			}
		}

		/// <summary>
		/// <c>true</c> if the method has an explicit 'this' parameter
		/// </summary>
		public bool ExplicitThis {
			get {
				var ms = MethodSig;
				return ms == null ? false : ms.ExplicitThis;
			}
		}

		/// <summary>
		/// Gets the calling convention
		/// </summary>
		public CallingConvention CallingConvention {
			get {
				var ms = MethodSig;
				return ms == null ? 0 : ms.CallingConvention & CallingConvention.Mask;
			}
		}

		/// <summary>
		/// Gets/sets the method return type
		/// </summary>
		public TypeSig ReturnType {
			get {
				var ms = MethodSig;
				return ms == null ? null : ms.RetType;
			}
			set {
				var ms = MethodSig;
				if (ms != null)
					ms.RetType = value;
			}
		}

		/// <inheritdoc/>
		bool IGenericParameterProvider.IsMethod {
			get { return true; }
		}

		/// <inheritdoc/>
		bool IGenericParameterProvider.IsType {
			get { return false; }
		}

		/// <inheritdoc/>
		int IGenericParameterProvider.NumberOfGenericParameters {
			get {
				var sig = MethodSig;
				return sig == null ? 0 : (int)sig.GenParamCount;
			}
		}

		/// <summary>
		/// Gets the full name
		/// </summary>
		public string FullName {
			get {
				var parent = Class;
				IList<TypeSig> typeGenArgs = null;
				if (parent is TypeSpec) {
					var sig = ((TypeSpec)parent).TypeSig as GenericInstSig;
					if (sig != null)
						typeGenArgs = sig.GenericArguments;
				}
				var methodSig = MethodSig;
				if (methodSig != null)
					return FullNameCreator.MethodFullName(GetDeclaringTypeFullName(parent), Name, methodSig, typeGenArgs, null);
				var fieldSig = FieldSig;
				if (fieldSig != null)
					return FullNameCreator.FieldFullName(GetDeclaringTypeFullName(parent), Name, fieldSig, typeGenArgs);
				return string.Empty;
			}
		}

		/// <summary>
		/// Get the declaring type's full name
		/// </summary>
		/// <returns>Full name or <c>null</c> if there's no declaring type</returns>
		public string GetDeclaringTypeFullName() {
			return GetDeclaringTypeFullName(Class);
		}

		string GetDeclaringTypeFullName(IMemberRefParent parent) {
			if (parent == null)
				return null;
			if (parent is ITypeDefOrRef)
				return ((ITypeDefOrRef)parent).FullName;
			if (parent is ModuleRef)
				return string.Format("[module:{0}]<Module>", ((ModuleRef)parent).ToString());
			if (parent is MethodDef) {
				var declaringType = ((MethodDef)parent).DeclaringType;
				return declaringType == null ? null : declaringType.FullName;
			}
			return null;	// Should never be reached
		}

		/// <summary>
		/// Resolves the method/field
		/// </summary>
		/// <returns>A <see cref="MethodDef"/> or a <see cref="FieldDef"/> instance or <c>null</c>
		/// if it couldn't be resolved.</returns>
		public IMemberForwarded Resolve() {
			if (module == null)
				return null;
			return module.Context.Resolver.Resolve(this);
		}

		/// <summary>
		/// Resolves the method/field
		/// </summary>
		/// <returns>A <see cref="MethodDef"/> or a <see cref="FieldDef"/> instance</returns>
		/// <exception cref="MemberRefResolveException">If the method/field couldn't be resolved</exception>
		public IMemberForwarded ResolveThrow() {
			var memberDef = Resolve();
			if (memberDef != null)
				return memberDef;
			throw new MemberRefResolveException(string.Format("Could not resolve method/field: {0}", this));
		}

		/// <summary>
		/// Resolves the field
		/// </summary>
		/// <returns>A <see cref="FieldDef"/> instance or <c>null</c> if it couldn't be resolved.</returns>
		public FieldDef ResolveField() {
			return Resolve() as FieldDef;
		}

		/// <summary>
		/// Resolves the field
		/// </summary>
		/// <returns>A <see cref="FieldDef"/> instance</returns>
		/// <exception cref="MemberRefResolveException">If the field couldn't be resolved</exception>
		public FieldDef ResolveFieldThrow() {
			var field = ResolveField();
			if (field != null)
				return field;
			throw new MemberRefResolveException(string.Format("Could not resolve field: {0}", this));
		}

		/// <summary>
		/// Resolves the method
		/// </summary>
		/// <returns>A <see cref="MethodDef"/> instance or <c>null</c> if it couldn't be resolved.</returns>
		public MethodDef ResolveMethod() {
			return Resolve() as MethodDef;
		}

		/// <summary>
		/// Resolves the method
		/// </summary>
		/// <returns>A <see cref="MethodDef"/> instance</returns>
		/// <exception cref="MemberRefResolveException">If the method couldn't be resolved</exception>
		public MethodDef ResolveMethodThrow() {
			var method = ResolveMethod();
			if (method != null)
				return method;
			throw new MemberRefResolveException(string.Format("Could not resolve method: {0}", this));
		}

		/// <inheritdoc/>
		public override string ToString() {
			return FullName;
		}
	}

	/// <summary>
	/// A MemberRef row created by the user and not present in the original .NET file
	/// </summary>
	public class MemberRefUser : MemberRef {
		IMemberRefParent @class;
		UTF8String name;
		CallingConventionSig signature;
		readonly CustomAttributeCollection customAttributeCollection = new CustomAttributeCollection();

		/// <inheritdoc/>
		public override IMemberRefParent Class {
			get { return @class; }
			set { @class = value; }
		}

		/// <inheritdoc/>
		public override UTF8String Name {
			get { return name; }
			set { name = value; }
		}

		/// <inheritdoc/>
		public override CallingConventionSig Signature {
			get { return signature; }
			set { signature = value; }
		}

		/// <inheritdoc/>
		public override CustomAttributeCollection CustomAttributes {
			get { return customAttributeCollection; }
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="module">Owner module</param>
		public MemberRefUser(ModuleDef module) {
			this.module = module;
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="module">Owner module</param>
		/// <param name="name">Name of ref</param>
		public MemberRefUser(ModuleDef module, UTF8String name) {
			this.module = module;
			this.name = name;
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="module">Owner module</param>
		/// <param name="name">Name of field ref</param>
		/// <param name="sig">Field sig</param>
		public MemberRefUser(ModuleDef module, UTF8String name, FieldSig sig)
			: this(module, name, sig, null) {
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="module">Owner module</param>
		/// <param name="name">Name of field ref</param>
		/// <param name="sig">Field sig</param>
		/// <param name="class">Owner of field</param>
		public MemberRefUser(ModuleDef module, UTF8String name, FieldSig sig, IMemberRefParent @class) {
			this.module = module;
			this.name = name;
			this.@class = @class;
			this.signature = sig;
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="module">Owner module</param>
		/// <param name="name">Name of method ref</param>
		/// <param name="sig">Method sig</param>
		public MemberRefUser(ModuleDef module, UTF8String name, MethodSig sig)
			: this(module, name, sig, null) {
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="module">Owner module</param>
		/// <param name="name">Name of method ref</param>
		/// <param name="sig">Method sig</param>
		/// <param name="class">Owner of method</param>
		public MemberRefUser(ModuleDef module, UTF8String name, MethodSig sig, IMemberRefParent @class) {
			this.module = module;
			this.name = name;
			this.@class = @class;
			this.signature = sig;
		}
	}

	/// <summary>
	/// Created from a row in the MemberRef table
	/// </summary>
	sealed class MemberRefMD : MemberRef, IMDTokenProviderMD, IContainsGenericParameter {
		/// <summary>The module where this instance is located</summary>
		readonly ModuleDefMD readerModule;
		/// <summary>The raw table row. It's <c>null</c> until <see cref="InitializeRawRow_NoLock"/> is called</summary>
		RawMemberRefRow rawRow;

		readonly GenericParamContext gpContext;
		readonly uint origRid;
		UserValue<IMemberRefParent> @class;
		UserValue<UTF8String> name;
		UserValue<CallingConventionSig> signature;
		CustomAttributeCollection customAttributeCollection;
#if THREAD_SAFE
		readonly Lock theLock = Lock.Create();
#endif

		/// <inheritdoc/>
		public uint OrigRid {
			get { return origRid; }
		}

		/// <inheritdoc/>
		public override IMemberRefParent Class {
			get { return @class.Value; }
			set { @class.Value = value; }
		}

		/// <inheritdoc/>
		public override UTF8String Name {
			get { return name.Value; }
			set { name.Value = value; }
		}

		/// <inheritdoc/>
		public override CallingConventionSig Signature {
			get { return signature.Value; }
			set { signature.Value = value; }
		}

		/// <inheritdoc/>
		public override CustomAttributeCollection CustomAttributes {
			get {
				if (customAttributeCollection == null) {
					var list = readerModule.MetaData.GetCustomAttributeRidList(Table.MemberRef, origRid);
					var tmp = new CustomAttributeCollection((int)list.Length, list, (list2, index) => readerModule.ReadCustomAttribute(((RidList)list2)[index]));
					Interlocked.CompareExchange(ref customAttributeCollection, tmp, null);
				}
				return customAttributeCollection;
			}
		}

		bool IContainsGenericParameter.ContainsGenericParameter {
			get { return TypeHelper.ContainsGenericParameter(this); }
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="readerModule">The module which contains this <c>MemberRef</c> row</param>
		/// <param name="rid">Row ID</param>
		/// <param name="gpContext">Generic parameter context</param>
		/// <exception cref="ArgumentNullException">If <paramref name="readerModule"/> is <c>null</c></exception>
		/// <exception cref="ArgumentException">If <paramref name="rid"/> is invalid</exception>
		public MemberRefMD(ModuleDefMD readerModule, uint rid, GenericParamContext gpContext) {
#if DEBUG
			if (readerModule == null)
				throw new ArgumentNullException("readerModule");
			if (readerModule.TablesStream.MemberRefTable.IsInvalidRID(rid))
				throw new BadImageFormatException(string.Format("MemberRef rid {0} does not exist", rid));
#endif
			this.origRid = rid;
			this.rid = rid;
			this.readerModule = readerModule;
			this.module = readerModule;
			this.gpContext = gpContext;
			Initialize();
		}

		void Initialize() {
			@class.ReadOriginalValue = () => {
				InitializeRawRow_NoLock();
				return readerModule.ResolveMemberRefParent(rawRow.Class, gpContext);
			};
			name.ReadOriginalValue = () => {
				InitializeRawRow_NoLock();
				return readerModule.StringsStream.ReadNoNull(rawRow.Name);
			};
			signature.ReadOriginalValue = () => {
				InitializeRawRow_NoLock();
				return readerModule.ReadSignature(rawRow.Signature, gpContext);
			};
#if THREAD_SAFE
			@class.Lock = theLock;
			name.Lock = theLock;
			signature.Lock = theLock;
#endif
		}

		void InitializeRawRow_NoLock() {
			if (rawRow != null)
				return;
			rawRow = readerModule.TablesStream.ReadMemberRefRow(origRid);
		}
	}
}
