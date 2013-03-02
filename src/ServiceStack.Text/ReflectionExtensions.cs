//
// https://github.com/ServiceStack/ServiceStack.Text
// ServiceStack.Text: .NET C# POCO JSON, JSV and CSV Text Serializers.
//
// Authors:
//   Demis Bellot (demis.bellot@gmail.com)
//
// Copyright 2012 ServiceStack Ltd.
//
// Licensed under the same terms of ServiceStack: new BSD license.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using ServiceStack.Text.Support;
#if WINDOWS_PHONE
using System.Linq.Expressions;
#endif

namespace ServiceStack.Text
{
    public delegate EmptyCtorDelegate EmptyCtorFactoryDelegate(Type type);
    public delegate object EmptyCtorDelegate();

    public static class ReflectionExtensions
    {
        private static Dictionary<Type, object> DefaultValueTypes = new Dictionary<Type, object>();

        public static object GetDefaultValue(this Type type)
        {
            if (!type.IsValueType()) return null;

            object defaultValue;
            if (DefaultValueTypes.TryGetValue(type, out defaultValue)) return defaultValue;

            defaultValue = Activator.CreateInstance(type);

            Dictionary<Type, object> snapshot, newCache;
            do
            {
                snapshot = DefaultValueTypes;
                newCache = new Dictionary<Type, object>(DefaultValueTypes);
                newCache[type] = defaultValue;

            } while (!ReferenceEquals(
                Interlocked.CompareExchange(ref DefaultValueTypes, newCache, snapshot), snapshot));

            return defaultValue;
        }

        public static bool IsInstanceOf(this Type type, Type thisOrBaseType)
        {
            while (type != null)
            {
                if (type == thisOrBaseType)
                    return true;

                type = type.BaseType();
            }
            return false;
        }

        public static bool HasGenericType(this Type type)
        {
            while (type != null)
            {
                if (type.IsGenericType())
                    return true;

                type = type.BaseType();
            }
            return false;
        }

        public static Type GetGenericType(this Type type)
        {
            while (type != null)
            {
                if (type.IsGenericType())
                    return type;

                type = type.BaseType();
            }
            return null;
        }

        public static bool IsOrHasGenericInterfaceTypeOf(this Type type, Type genericTypeDefinition)
        {
            return (type.GetTypeWithGenericTypeDefinitionOf(genericTypeDefinition) != null)
                || (type == genericTypeDefinition);
        }

        public static Type GetTypeWithGenericTypeDefinitionOf(this Type type, Type genericTypeDefinition)
        {
            foreach (var t in type.GetTypeInterfaces())
            {
                if (t.IsGenericType() && t.GetGenericTypeDefinition() == genericTypeDefinition)
                {
                    return t;
                }
            }

            var genericType = type.GetGenericType();
            if (genericType != null && genericType.GetGenericTypeDefinition() == genericTypeDefinition)
            {
                return genericType;
            }

            return null;
        }

        public static Type GetTypeWithInterfaceOf(this Type type, Type interfaceType)
        {
            if (type == interfaceType) return interfaceType;

            foreach (var t in type.GetTypeInterfaces())
            {
                if (t == interfaceType)
                    return t;
            }

            return null;
        }

        public static bool HasInterface(this Type type, Type interfaceType)
        {
            foreach (var t in type.GetTypeInterfaces())
            {
                if (t == interfaceType)
                    return true;
            }
            return false;
        }

        public static bool AllHaveInterfacesOfType(
            this Type assignableFromType, params Type[] types)
        {
            foreach (var type in types)
            {
                if (assignableFromType.GetTypeWithInterfaceOf(type) == null) return false;
            }
            return true;
        }

        public static bool IsNumericType(this Type type)
        {
            if (!type.IsValueType()) return false;
            return type.IsIntegerType() || type.IsRealNumberType();
        }

        public static bool IsIntegerType(this Type type)
        {
            if (!type.IsValueType()) return false;

            var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
            return underlyingType == typeof(byte)
               || underlyingType == typeof(sbyte)
               || underlyingType == typeof(short)
               || underlyingType == typeof(ushort)
               || underlyingType == typeof(int)
               || underlyingType == typeof(uint)
               || underlyingType == typeof(long)
               || underlyingType == typeof(ulong);
        }

        public static bool IsRealNumberType(this Type type)
        {
            if (!type.IsValueType()) return false;

            var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
            return underlyingType == typeof(float)
               || underlyingType == typeof(double)
               || underlyingType == typeof(decimal);
        }

        public static Type GetTypeWithGenericInterfaceOf(this Type type, Type genericInterfaceType)
        {
            foreach (var t in type.GetTypeInterfaces())
            {
                if (t.IsGenericType() && t.GetGenericTypeDefinition() == genericInterfaceType) 
                    return t;
            }

            if (!type.IsGenericType()) return null;

            var genericType = type.GetGenericType();
            return genericType.GetGenericTypeDefinition() == genericInterfaceType
                    ? genericType
                    : null;
        }

        public static bool HasAnyTypeDefinitionsOf(this Type genericType, params Type[] theseGenericTypes)
        {
            if (!genericType.IsGenericType()) return false;

            var genericTypeDefinition = genericType.GenericTypeDefinition();

            foreach (var thisGenericType in theseGenericTypes)
            {
                if (genericTypeDefinition == thisGenericType)
                    return true;
            }

            return false;
        }

        public static Type[] GetGenericArgumentsIfBothHaveSameGenericDefinitionTypeAndArguments(
            this Type assignableFromType, Type typeA, Type typeB)
        {
            var typeAInterface = typeA.GetTypeWithGenericInterfaceOf(assignableFromType);
            if (typeAInterface == null) return null;

            var typeBInterface = typeB.GetTypeWithGenericInterfaceOf(assignableFromType);
            if (typeBInterface == null) return null;

            var typeAGenericArgs = typeAInterface.GetTypeGenericArguments();
            var typeBGenericArgs = typeBInterface.GetTypeGenericArguments();

            if (typeAGenericArgs.Length != typeBGenericArgs.Length) return null;

            for (var i = 0; i < typeBGenericArgs.Length; i++)
            {
                if (typeAGenericArgs[i] != typeBGenericArgs[i])
                {
                    return null;
                }
            }

            return typeAGenericArgs;
        }

        public static TypePair GetGenericArgumentsIfBothHaveConvertibleGenericDefinitionTypeAndArguments(
            this Type assignableFromType, Type typeA, Type typeB)
        {
            var typeAInterface = typeA.GetTypeWithGenericInterfaceOf(assignableFromType);
            if (typeAInterface == null) return null;

            var typeBInterface = typeB.GetTypeWithGenericInterfaceOf(assignableFromType);
            if (typeBInterface == null) return null;

            var typeAGenericArgs = typeAInterface.GetTypeGenericArguments();
            var typeBGenericArgs = typeBInterface.GetTypeGenericArguments();

            if (typeAGenericArgs.Length != typeBGenericArgs.Length) return null;

            for (var i = 0; i < typeBGenericArgs.Length; i++)
            {
                if (!AreAllStringOrValueTypes(typeAGenericArgs[i], typeBGenericArgs[i]))
                {
                    return null;
                }
            }

            return new TypePair(typeAGenericArgs, typeBGenericArgs);
        }

        public static bool AreAllStringOrValueTypes(params Type[] types)
        {
            foreach (var type in types)
            {
                if (!(type == typeof(string) || type.IsValueType())) return false;
            }
            return true;
        }

        static Dictionary<Type, EmptyCtorDelegate> ConstructorMethods = new Dictionary<Type, EmptyCtorDelegate>();
        public static EmptyCtorDelegate GetConstructorMethod(Type type)
        {
            EmptyCtorDelegate emptyCtorFn;
            if (ConstructorMethods.TryGetValue(type, out emptyCtorFn)) return emptyCtorFn;

            emptyCtorFn = GetConstructorMethodToCache(type);

            Dictionary<Type, EmptyCtorDelegate> snapshot, newCache;
            do
            {
                snapshot = ConstructorMethods;
                newCache = new Dictionary<Type, EmptyCtorDelegate>(ConstructorMethods);
                newCache[type] = emptyCtorFn;

            } while (!ReferenceEquals(
                Interlocked.CompareExchange(ref ConstructorMethods, newCache, snapshot), snapshot));

            return emptyCtorFn;
        }

        static Dictionary<string, EmptyCtorDelegate> TypeNamesMap = new Dictionary<string, EmptyCtorDelegate>();
        public static EmptyCtorDelegate GetConstructorMethod(string typeName)
        {
            EmptyCtorDelegate emptyCtorFn;
            if (TypeNamesMap.TryGetValue(typeName, out emptyCtorFn)) return emptyCtorFn;

            var type = JsConfig.TypeFinder.Invoke(typeName);
            if (type == null) return null;
            emptyCtorFn = GetConstructorMethodToCache(type);

            Dictionary<string, EmptyCtorDelegate> snapshot, newCache;
            do
            {
                snapshot = TypeNamesMap;
                newCache = new Dictionary<string, EmptyCtorDelegate>(TypeNamesMap);
                newCache[typeName] = emptyCtorFn;

            } while (!ReferenceEquals(
                Interlocked.CompareExchange(ref TypeNamesMap, newCache, snapshot), snapshot));

            return emptyCtorFn;
        }

        public static EmptyCtorDelegate GetConstructorMethodToCache(Type type)
        {
            var emptyCtor = type.GetEmptyConstructor();
            if (emptyCtor != null)
            {

#if MONOTOUCH || c|| XBOX || NETFX_CORE
				return () => Activator.CreateInstance(type);
#elif WINDOWS_PHONE
                return Expression.Lambda<EmptyCtorDelegate>(Expression.New(type)).Compile();
#else
#if SILVERLIGHT
                var dm = new System.Reflection.Emit.DynamicMethod("MyCtor", type, Type.EmptyTypes);
#else
                var dm = new System.Reflection.Emit.DynamicMethod("MyCtor", type, Type.EmptyTypes, typeof(ReflectionExtensions).Module, true);
#endif
                var ilgen = dm.GetILGenerator();
                ilgen.Emit(System.Reflection.Emit.OpCodes.Nop);
                ilgen.Emit(System.Reflection.Emit.OpCodes.Newobj, emptyCtor);
                ilgen.Emit(System.Reflection.Emit.OpCodes.Ret);

                return (EmptyCtorDelegate)dm.CreateDelegate(typeof(EmptyCtorDelegate));
#endif
            }

#if (SILVERLIGHT && !WINDOWS_PHONE) || XBOX
            return () => Activator.CreateInstance(type);
#elif WINDOWS_PHONE
            return Expression.Lambda<EmptyCtorDelegate>(Expression.New(type)).Compile();
#else
            //Anonymous types don't have empty constructors
            return () => FormatterServices.GetUninitializedObject(type);
#endif
        }

        private static class TypeMeta<T>
        {
            public static readonly EmptyCtorDelegate EmptyCtorFn;
            static TypeMeta()
            {
                EmptyCtorFn = GetConstructorMethodToCache(typeof(T));
            }
        }

        public static object CreateInstance<T>()
        {
            return TypeMeta<T>.EmptyCtorFn();
        }

        public static object CreateInstance(this Type type)
        {
            var ctorFn = GetConstructorMethod(type);
            return ctorFn();
        }

        public static object CreateInstance(string typeName)
        {
            var ctorFn = GetConstructorMethod(typeName);
            return ctorFn();
        }

        public static PropertyInfo[] GetPublicProperties(this Type type)
        {
            if (type.IsInterface())
            {
                var propertyInfos = new List<PropertyInfo>();

                var considered = new List<Type>();
                var queue = new Queue<Type>();
                considered.Add(type);
                queue.Enqueue(type);

                while (queue.Count > 0)
                {
                    var subType = queue.Dequeue();
                    foreach (var subInterface in subType.GetTypeInterfaces())
                    {
                        if (considered.Contains(subInterface)) continue;

                        considered.Add(subInterface);
                        queue.Enqueue(subInterface);
                    }

                    var typeProperties = subType.GetTypesPublicProperties();

                    var newPropertyInfos = typeProperties
                        .Where(x => !propertyInfos.Contains(x));

                    propertyInfos.InsertRange(0, newPropertyInfos);
                }

                return propertyInfos.ToArray();
            }

            return type.GetTypesPublicProperties()
                .Where(t => t.GetIndexParameters().Length == 0) // ignore indexed properties
                .ToArray();
        }

        const string DataContract = "DataContractAttribute";
        const string DataMember = "DataMemberAttribute";
        const string IgnoreDataMember = "IgnoreDataMemberAttribute";

        public static PropertyInfo[] GetSerializableProperties(this Type type)
        {
            var publicProperties = GetPublicProperties(type);
            var publicReadableProperties = publicProperties.Where(x => x.PropertyGetMethod() != null);

            if (type.IsDto())
            {
                return !Env.IsMono
                    ? publicReadableProperties.Where(attr => 
                        attr.IsDefined(typeof(DataMemberAttribute), false)).ToArray()
                    : publicReadableProperties.Where(attr => 
                        attr.GetCustomAttributes(false).Any(x => x.GetType().Name == DataMember)).ToArray();
            }

            // else return those properties that are not decorated with IgnoreDataMember
            return publicReadableProperties.Where(prop => !prop.GetCustomAttributes(false).Any(attr => attr.GetType().Name == IgnoreDataMember)).ToArray();
        }

        public static FieldInfo[] GetSerializableFields(this Type type)
        {
            if (type.IsDto()) {
                return new FieldInfo[0];
            }
            
            var publicFields = PlatformExtensions.GetPublicFields(type);

            // else return those properties that are not decorated with IgnoreDataMember
            return publicFields.Where(prop => !prop.GetCustomAttributes(false).Any(attr => attr.GetType().Name == IgnoreDataMember)).ToArray();
        }

        public static bool HasAttr<T>(this Type type) where T : Attribute
        {
            return type.GetTypeAttributes<T>();
        }

#if !SILVERLIGHT && !MONOTOUCH 
        static readonly Dictionary<Type, FastMember.TypeAccessor> typeAccessorMap 
            = new Dictionary<Type, FastMember.TypeAccessor>();
#endif

        public static DataContractAttribute GetDataContract(this Type type)
        {
            var dataContract = type.FirstAttribute<DataContractAttribute>();

#if !SILVERLIGHT && !MONOTOUCH && !XBOX
            if (dataContract == null && Env.IsMono)
                return type.GetWeakDataContract();
#endif
            return dataContract;
        }

        public static DataMemberAttribute GetDataMember(this PropertyInfo pi)
        {
            var dataMember = pi.GetCustomAttributes(typeof(DataMemberAttribute), false)
                .FirstOrDefault() as DataMemberAttribute;

#if !SILVERLIGHT && !MONOTOUCH && !XBOX
            if (dataMember == null && Env.IsMono)
                return pi.GetWeakDataMember();
#endif
            return dataMember;
        }

#if !SILVERLIGHT && !MONOTOUCH && !XBOX
        public static DataContractAttribute GetWeakDataContract(this Type type)
        {
            var attr = type.GetCustomAttributes(true).FirstOrDefault(x => x.GetType().Name == DataContract);
            if (attr != null)
            {
                var attrType = attr.GetType();

                FastMember.TypeAccessor accessor;
                lock (typeAccessorMap)
                {
                    if (!typeAccessorMap.TryGetValue(attrType, out accessor))
                        typeAccessorMap[attrType] = accessor = FastMember.TypeAccessor.Create(attr.GetType());
                }

                return new DataContractAttribute {
                    Name = (string)accessor[attr, "Name"],
                    Namespace = (string)accessor[attr, "Namespace"],
                };
            }
            return null;
        }

        public static DataMemberAttribute GetWeakDataMember(this PropertyInfo pi)
        {
            var attr = pi.GetCustomAttributes(true).FirstOrDefault(x => x.GetType().Name == DataMember);
            if (attr != null)
            {
                var attrType = attr.GetType();

                FastMember.TypeAccessor accessor;
                lock (typeAccessorMap)
                {
                    if (!typeAccessorMap.TryGetValue(attrType, out accessor))
                        typeAccessorMap[attrType] = accessor = FastMember.TypeAccessor.Create(attr.GetType());
                }

                var newAttr = new DataMemberAttribute {
                    Name = (string) accessor[attr, "Name"],
                    EmitDefaultValue = (bool)accessor[attr, "EmitDefaultValue"],
                    IsRequired = (bool)accessor[attr, "IsRequired"],
                };

                var order = (int)accessor[attr, "Order"];
                if (order >= 0)
                    newAttr.Order = order; //Throws Exception if set to -1

                return newAttr;
            }
            return null;
        }
#endif
    }

    public static class PlatformExtensions //Because WinRT is a POS
    {
        public static bool IsInterface(this Type type)
        {
#if NETFX_CORE
            return type.GetTypeInfo().IsInterface);
#else
            return type.IsInterface;
#endif
        }

        public static bool IsValueType(this Type type)
        {
#if NETFX_CORE
            return type.GetTypeInfo().IsValueType);
#else
            return type.IsValueType;
#endif
        }

        internal static bool IsGenericType(this Type type)
        {
#if NETFX_CORE
            return type.GetTypeInfo().IsGenericType;
#else
            return type.IsGenericType;
#endif
        }

        public static Type BaseType(this Type type)
        {
#if NETFX_CORE
            return type.GetTypeInfo().BaseType;
#else
            return type.BaseType;
#endif
        }

        public static Type GenericTypeDefinition(this Type type)
        {
#if NETFX_CORE
            return type.GetTypeInfo().GetGenericTypeDefinition();
#else
            return type.GetGenericTypeDefinition();
#endif
        }

        public static Type[] GetTypeInterfaces(this Type type)
        {
#if NETFX_CORE
            return type.GetTypeInfo().ImplementedInterfaces;
#else
            return type.GetInterfaces();
#endif
        }

        public static Type[] GetTypeGenericArguments(this Type type)
        {
#if NETFX_CORE
            return type.GenericTypeArguments;
#else
            return type.GetGenericArguments();
#endif
        }

        public static ConstructorInfo GetEmptyConstructor(this Type type)
        {
#if NETFX_CORE
            return type.GetTypeInfo().DeclaredConstructors.FirstOrDefault(c => c.GetParameters().Count() == 0);
#else
            return type.GetConstructor(Type.EmptyTypes);
#endif
        }

        internal static PropertyInfo[] GetTypesPublicProperties(this Type subType)
        {
#if NETFX_CORE 
            return subType.GetRuntimeProperties();
#else
            return subType.GetProperties(
                BindingFlags.FlattenHierarchy | 
                BindingFlags.Public | 
                BindingFlags.Instance);
#endif
        }

        public static FieldInfo[] GetPublicFields(this Type type)
        {
            if (type.IsInterface())
            {
                return new FieldInfo[0];
            }

#if NETFX_CORE
            return type.GetRuntimeFields().Where(p => p.IsPublic && !p.IsStatic).ToArray();
#else
            return type.GetFields(BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Instance)
            .ToArray();
#endif
        }

        public static bool GetTypeAttributes<T>(this Type type) where T : Attribute
        {
#if NETFX_CORE
            return type.GetTypeInfo().GetCustomAttributes(true).Any(x => x.GetType() == typeof(T));
#else
            return type.GetCustomAttributes(true).Any(x => x.GetType() == typeof(T));
#endif
        }

        const string DataContract = "DataContractAttribute";
        public static bool IsDto(this Type type)
        {
#if NETFX_CORE
            return type.GetTypeInfo().IsDefined(typeof(DataContractAttribute), false);
#else
            return !Env.IsMono
                   ? type.IsDefined(typeof(DataContractAttribute), false)
                   : type.GetCustomAttributes(true).Any(x => x.GetType().Name == DataContract);
#endif
        }

        public static MethodInfo PropertyGetMethod(this PropertyInfo pi, bool nonPublic = false)
        {
#if NETFX_CORE
            return pi.GetMethod;
#else
            return pi.GetGetMethod(false);
#endif
        }

        public static TAttr FirstAttribute<TAttr>(this Type type, bool inherit = true) where TAttr : Attribute
        {
#if NETFX_CORE
            return type.GetTypeInfo().GetCustomAttributes(typeof(TAttr), true)
                .FirstOrDefault() as TAttr;
#else
            return type.GetCustomAttributes(typeof(TAttr), true)
                   .FirstOrDefault() as TAttr;
#endif
        }

        public static MethodInfo GetPublicStaticMethod(this Type type, string methodName, Type[] types = null)
        {
#if NETFX_CORE
            return type.GetRuntimeMethod(parseMethod, types);
#else
            return types == null
                ? type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)
                : type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, types, null);
#endif
        }

        public static MethodInfo GetMethod(this Type type, string methodName, Type[] types = null)
        {
#if NETFX_CORE
            return type.GetRuntimeMethod(parseMethod, types);
#else
            return types == null
                ? type.GetMethod(methodName)
                : type.GetMethod(methodName, types);
#endif
        }

        public static FieldInfo GetPublicStaticField(this Type type, string fieldName)
        {
#if NETFX_CORE
            return type.GetRuntimeField(methodName);
#else
            return type.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
#endif
        }

        public static Delegate MakeDelegate(this MethodInfo mi, Type delegateType, bool throwOnBindFailure=true)
        {
#if NETFX_CORE
            return mi.CreateDelegate(delegateType);
#else
            return Delegate.CreateDelegate(delegateType, mi, throwOnBindFailure);
#endif
        }

        public static Type[] GenericTypeArguments(this Type type)
        {
#if NETFX_CORE
            return type.GenericTypeArguments;
#else
            return type.GetGenericArguments();
#endif
        }

        public static ConstructorInfo[] DeclaredConstructors(this Type type)
        {
#if NETFX_CORE
            return type.GetTypeInfo().DeclaredConstructors;
#else
            return type.GetConstructors();
#endif
        }

        public static bool AssignableFrom(this Type type, Type fromType)
        {
#if NETFX_CORE
            return type.GetTypeInfo().IsAssignableFrom(fromType.GetTypeInfo();
#else
            return type.IsAssignableFrom(fromType);
#endif
        }
        
        public static bool IsStandardClass(this Type type)
        {
#if NETFX_CORE
            var typeInfo = type.GetTypeInfo();
            return typeInfo.IsClass && !typeInfo.IsAbstract && !typeInfo.IsInterface;
#else
            return type.IsClass && !type.IsAbstract && !type.IsInterface;
#endif
        }

        public static bool IsAbstract(this Type type)
        {
#if NETFX_CORE
            return type.GetTypeInfo.IsAbstract;
#else
            return type.IsAbstract;
#endif
        }

        public static PropertyInfo GetPropertyInfo(this Type type, string propertyName)
        {
#if NETFX_CORE
            return type.GetRuntimeProperty(propertyName);
#else
            return type.GetProperty(propertyName);
#endif
        }

        public static FieldInfo GetFieldInfo(this Type type, string fieldName)
        {
#if NETFX_CORE
            return type.GetRuntimeField(fieldName);
#else
            return type.GetField(fieldName);
#endif
        }

        public static FieldInfo[] GetWritableFields(this Type type)
        {
#if NETFX_CORE
            return type.GetRuntimeFields().Where(p => !p.IsPublic && !p.IsStatic);
#else
            return type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.SetField);
#endif
        }

        public static MethodInfo SetMethod(this PropertyInfo pi, bool nonPublic = true)
        {
#if NETFX_CORE
            return pi.SetMethod;
#else
            return pi.GetSetMethod(nonPublic);
#endif
        }

        public static MethodInfo GetMethod(this PropertyInfo pi, bool nonPublic = true)
        {
#if NETFX_CORE
            return pi.GetMethod;
#else
            return pi.GetGetMethod(nonPublic);
#endif
        }

        public static bool InstanceOfType(this Type type, object instance)
        {
#if NETFX_CORE
            return type.IsInstanceOf(instance.GetType());
#else
            return type.IsInstanceOfType(instance);
#endif
        }
        
        public static bool IsClass(this Type type)
        {
#if NETFX_CORE
            return type.GetTypeInfo().IsClass;
#else
            return type.IsClass;
#endif
        }

        public static bool IsEnum(this Type type)
        {
#if NETFX_CORE
            return type.GetTypeInfo().IsEnum;
#else
            return type.IsEnum;
#endif
        }

        public static bool IsUnderlyingEnum(this Type type)
        {
#if NETFX_CORE
            return type.GetTypeInfo().IsEnum;
#else
            return type.IsEnum || type.UnderlyingSystemType.IsEnum;
#endif
        }

        public static MethodInfo[] GetMethodInfos(this Type type)
        {
#if NETFX_CORE
            return type.GetTypeInfo().GetMethods();
#else
            return type.GetMethods();
#endif
        }

        public static PropertyInfo[] GetPropertyInfos(this Type type)
        {
#if NETFX_CORE
            return type.GetRuntimeProperties();
#else
            return type.GetProperties();
#endif
        }
    }

}