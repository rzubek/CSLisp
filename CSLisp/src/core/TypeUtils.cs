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
        /// Maps from a type, to a filtered list of instance members of this type.
        /// This cache can be cleared as needed (eg. when reloading assemblies)
        /// </summary>
        public static Dictionary<Type, List<MemberInfo>> InstanceMemberCache { get; private set; }

        /// <summary>
        /// Maps from a type, to a filtered list of static members of this type.
        /// This cache can be cleared as needed (eg. when reloading assemblies)
        /// </summary>
        public static Dictionary<Type, List<MemberInfo>> StaticMemberCache { get; private set; }

        static TypeUtils () {
            NameToTypeCache = new Dictionary<string, Type>();
            InstanceMemberCache = new Dictionary<Type, List<MemberInfo>>();
            StaticMemberCache = new Dictionary<Type, List<MemberInfo>>();
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
        public static IEnumerable<MemberInfo> GetInstanceMembers (object obj) =>
            GetInstanceMembers(obj.GetType());

        /// <summary>
        /// Returns all members from a type descriptor, with a given member name
        /// </summary>
        public static IEnumerable<MemberInfo> GetInstanceMembers (Type t, string name) =>
            GetInstanceMembers(t).Where(m => m.Name == name);

        /// <summary>
        /// Returns all public instance members from a type descriptor
        /// </summary>
        public static IEnumerable<MemberInfo> GetInstanceMembers (Type t) {
            var found = InstanceMemberCache.TryGetValue(t, out List<MemberInfo> result);
            if (!found) {
                result = InstanceMemberCache[t] =
                    t.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy)
                    .ToList();
            }
            return result;
        }

        /// <summary>
        /// Returns all public static members from a type descriptor
        /// </summary>
        public static IEnumerable<MemberInfo> GetStaticMembers (Type t) {
            var found = StaticMemberCache.TryGetValue(t, out List<MemberInfo> result);
            if (!found) {
                result = StaticMemberCache[t] =
                    t.GetMembers(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy)
                    .ToList();
            }
            return result;
        }

        /// <summary>
        /// Returns public methods on a type that match a specific name and signature
        /// </summary>
        public static MethodBase GetMethodByArgs (Type type, string name, object[] varargs) {
            var varargTypes = varargs.Select(a => a?.GetType() ?? typeof(object)).ToArray();
            var methods = GetInstanceMembers(type)
                .OfType<MethodInfo>()
                .Where(m => m.Name == name)
                .ToArray();

            var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.CreateInstance;

            var result = Type.DefaultBinder.SelectMethod(flags, methods, varargTypes, null);

            return result;
        }

        /// <summary>
        /// Returns a public member field or property, suitable for setting or getting.
        /// In this iteration we don't support accessing non-public ones.
        /// <returns></returns>
        public static MemberInfo GetMemberFieldOrProp (Type type, string member) {
            var fields = GetInstanceMembers(type)
                .Where(m => m.Name == member)
                .Where(m => m is FieldInfo || m is PropertyInfo)
                .ToArray(); // there should be at most one

            var result = fields.Length > 0 ? fields[0] : null;
            return result;
        }
    }
}