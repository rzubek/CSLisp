using CSLisp.Error;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CSLisp.Core
{
    public static class TypeUtils
    {
        /// <summary>
        /// Maps from a fully qualified type name, to its corresponding .net type.
        /// This cache can be cleared as needed (eg. when reloading assemblies)
        /// </summary>
        public static Dictionary<string, Type> NameToTypeCache { get; private set; }

        /// <summary>
        /// Maps from a type, to a filtered list of serializable members of this type.
        /// This cache can be cleared as needed (eg. when reloading assemblies)
        /// </summary>
        public static Dictionary<Type, List<MemberInfo>> TypeToMemberCache { get; private set; }

        static TypeUtils () {
            NameToTypeCache = new Dictionary<string, Type>();
            TypeToMemberCache = new Dictionary<Type, List<MemberInfo>>();
        }


        //
        // support for mapping from names to types, and instantiation

        /// <summary>
        /// Returns type corresponding to the fully qualified name (including namespace),
        /// based on all currently loaded assemblies. If no such type was found, returns null.
        /// 
        /// Note that this is a caching operation; when new assemblies are loaded please clear type caches.
        /// </summary>
        public static Type GetType (string fullname) {
            if (string.IsNullOrEmpty(fullname)) { return null; }

            if (!NameToTypeCache.TryGetValue(fullname, out Type result)) {

                result = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => a.GetTypes())
                    .Where(t => t.FullName == fullname)
                    .FirstOrDefault();

                if (result != null) {
                    NameToTypeCache[fullname] = result;
                }
            }

            return result;
        }

        /// <summary>
        /// Makes an instance of a type based on fully qualified name (including namespace),
        /// and passing in the specified arguments. If this type is not found, returns null.
        /// If this type is found but invalid arguments were passed, or another reflection
        /// exception occurred, it throws an Interop exception.
        /// </summary>
        public static object Instantiate (string fullname, params object[] args) {
            var t = GetType(fullname);
            return t != null ? Instantiate(t, args) : null;
        }

        /// <summary>
        /// Makes an instance of a type based on fully qualified name (including namespace),
        /// and passing in the specified arguments. If invalid arguments were passed, or another reflection
        /// exception occurred, it throws an Interop exception.
        /// </summary>
        public static object Instantiate (Type t, params object[] args) {
            try {
                return Activator.CreateInstance(t, args);
            } catch (Exception e) {
                throw new InteropError("Failed to instantiate type: " + t.FullName, e);
            }
        }


        //
        // support for accessing members of a given type

        /// <summary>
        /// If the member is either a variable or a property, sets its value
        /// </summary>
        public static void SetValue (MemberInfo member, object obj, object value) {
            if (member is PropertyInfo prop) { prop.SetValue(obj, value, null); }
            if (member is FieldInfo field) { field.SetValue(obj, value); }
        }

        /// <summary>
        /// If the member is either a variable or a property, returns its value
        /// </summary>
        public static object GetValue (MemberInfo member, object obj) =>
            (member is PropertyInfo prop) ? prop.GetValue(obj, null) :
            (member is FieldInfo field) ? field.GetValue(obj) :
            null;

        /// <summary>
        /// Returns all members from the given object
        /// </summary>
        public static IEnumerable<MemberInfo> GetMembers (object obj) =>
            GetMembers(obj.GetType());

        /// <summary>
        /// Returns all members from a type descriptor, with a given member name
        /// </summary>
        public static IEnumerable<MemberInfo> GetMembers (Type t, string name) =>
            GetMembers(t).Where(m => m.Name == name);

        /// <summary>
        /// Returns all members from a type descriptor
        /// </summary>
        public static IEnumerable<MemberInfo> GetMembers (Type t) {
            var found = TypeToMemberCache.TryGetValue(t, out List<MemberInfo> result);
            if (!found) {
                result = TypeToMemberCache[t] =
                    t.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy)
                    .ToList();
            }
            return result;
        }

        /// <summary>
        /// Returns public methods on a type that match a specific name and signature
        /// </summary>
        public static IEnumerable<MethodInfo> GetMethodsBySig (Type type, string name, params Type[] args) =>
            GetMembers(type)
                .OfType<MethodInfo>()
                .Where(m => {
                    if (m.Name != name) { return false; }

                    var parameters = m.GetParameters();
                    int argCount = args?.Length ?? 0, methodCount = parameters?.Length ?? 0;

                    if (argCount != methodCount) { return false; }

                    for (int i = 0; i < argCount; i++) {
                        if (parameters[i].ParameterType != args[i]) { return false; }
                    }

                    return true;
                });

        /// <summary>
        /// Returns public methods on a type that match a specific name and signature
        /// </summary>
        public static IEnumerable<MethodInfo> GetMethodsByArgCount (Type type, string name, int argCount) =>
            GetMembers(type)
                .OfType<MethodInfo>()
                .Where(m => {
                    if (m.Name != name) { return false; }
                    if (m.GetParameters().Length != argCount) { return false; }

                    return true;
                });
    }
}